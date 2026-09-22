using Avalonia;

using GeekFlashToolX.Diagnostics;
using GeekFlashToolX.Services;
using Serilog;
using Serilog.Events;

namespace GeekFlashToolX;

internal sealed class Program
{
    // Initialize logging before Avalonia so startup failures are captured.
    [STAThread]
    public static void Main(string[] args)
    {
        using var crashReporter = CrashReporter.Install(LogPaths.Directory);
        try
        {
            var logDirectory = LogPaths.Directory;
            Directory.CreateDirectory(logDirectory);
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Enrich.FromLogContext()
                .WriteTo.File(Path.Combine(logDirectory, "application-.log"),
                    rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true,
                    fileSizeLimitBytes: 20 * 1024 * 1024, retainedFileCountLimit: 30,
                    shared: true, outputTemplate: "{Timestamp:O} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                .WriteTo.File(Path.Combine(logDirectory, "crash-.log"),
                    restrictedToMinimumLevel: LogEventLevel.Fatal,
                    rollingInterval: RollingInterval.Day, retainedFileCountLimit: 10,
                    shared: true, outputTemplate: "{Timestamp:O} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                .CreateLogger();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception exception)
        {
            crashReporter.Report(exception, "Application entry point");
            throw;
        }
        finally { Log.CloseAndFlush(); }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
