using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using Microsoft.Win32.SafeHandles;
using Serilog;

namespace GeekFlashToolX.Diagnostics;

/// <summary>Records unexpected exceptions and requests a process dump before termination.</summary>
internal sealed class CrashReporter : IDisposable
{
    private readonly string _dumpDirectory;
    private Exception? _lastReported;
    private int _dumpAttempted;
    private bool _dispatcherAttached;

    private CrashReporter(string logDirectory)
    {
        _dumpDirectory = Path.Combine(logDirectory, "Dumps");
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public static CrashReporter? Current { get; private set; }

    public static CrashReporter Install(string logDirectory)
    {
        var reporter = new CrashReporter(logDirectory);
        Current = reporter;
        return reporter;
    }

    public void AttachDispatcher()
    {
        if (_dispatcherAttached) return;
        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
        _dispatcherAttached = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args) =>
        Report(args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString()), "AppDomain");

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs args)
    {
        Report(args.Exception, "Avalonia UI thread");
        // A fatal UI exception should still terminate the process after the dump attempt.
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        try { Log.Error(args.Exception, "Unobserved task exception"); }
        catch { Console.Error.WriteLine(args.Exception); }
        // This event does not represent a process crash; no dump is requested here.
    }

    public void Report(Exception exception, string source)
    {
        if (ReferenceEquals(Interlocked.Exchange(ref _lastReported, exception), exception)) return;
        try { Log.Fatal(exception, "Unhandled exception from {Source}", source); }
        catch { Console.Error.WriteLine(exception); }
        if (Interlocked.Exchange(ref _dumpAttempted, 1) != 0) return;

        try
        {
            // DbgHelp is not safe for concurrent calls. Run it off the failing thread.
            var worker = new Thread(WriteDump) { IsBackground = true, Name = "Crash dump writer" };
            worker.Start();
            if (!worker.Join(TimeSpan.FromSeconds(60)))
                Log.Error("Crash dump writer timed out after 60 seconds");
        }
        catch (Exception dumpError)
        {
            try { Log.Error(dumpError, "Could not start crash dump writer"); }
            catch { Console.Error.WriteLine(dumpError); }
        }
    }

    private void WriteDump()
    {
        string path;
        try
        {
            path = CreateDump(_dumpDirectory);
        }
        catch (Exception primaryError)
        {
            try { path = CreateDump(Path.Combine(Path.GetTempPath(), "GeekFlashTool-X", "Dumps")); }
            catch (Exception fallbackError)
            {
                var error = new AggregateException("Crash dump creation failed in both locations.", primaryError, fallbackError);
                try { Log.Error(error, "Crash dump creation failed"); }
                catch { Console.Error.WriteLine(error); }
                return;
            }
        }
        try { Log.Information("Crash dump saved to {DumpPath}", path); }
        catch { Console.Error.WriteLine($"Crash dump saved to {path}"); }
    }

    private static string CreateDump(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory,
            $"crash-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}-{Environment.ProcessId}-{Guid.NewGuid():N}.dmp");
        MiniDumpWriter.Write(path);
        return path;
    }

    public void Dispose()
    {
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        if (_dispatcherAttached) Dispatcher.UIThread.UnhandledException -= OnDispatcherUnhandledException;
        if (ReferenceEquals(Current, this)) Current = null;
    }
}

internal static partial class MiniDumpWriter
{
    // MiniDumpWithFullMemory | MiniDumpWithThreadInfo
    private const uint DumpType = 0x00000002 | 0x00001000;

    public static void Write(string path)
    {
        var created = false;
        try
        {
            using var process = Process.GetCurrentProcess();
            using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            created = true;
            if (!MiniDumpWriteDump(process.Handle, (uint)process.Id, file.SafeFileHandle, DumpType,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
        }
        catch
        {
            if (created)
            {
                try { File.Delete(path); }
                catch { /* Keep the original dump error. */ }
            }
            throw;
        }
    }

    [LibraryImport("dbghelp.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool MiniDumpWriteDump(IntPtr process, uint processId, SafeFileHandle file,
        uint dumpType, IntPtr exceptionParam, IntPtr userStreamParam, IntPtr callbackParam);
}
