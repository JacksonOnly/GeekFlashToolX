using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using GeekFlashToolX.Core.Models;
using GeekFlashToolX.Core.Services;
using GeekFlashToolX.Services.Serialization;

namespace GeekFlashToolX.Services;

/// <summary>Flushes each line; metadata is atomically replaced and never used as a file path.</summary>
public sealed class FileLogService : ILogService
{
    private readonly ConcurrentDictionary<string, OperationLog> _operations = new();
    private readonly object _lifetimeGate = new();
    private readonly OperationLog _application;
    private bool _disposed;

    public FileLogService(string? logDirectory = null)
    {
        LogDirectory = Path.GetFullPath(logDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GeekFlashTool-X", "Logs"));
        _application = CreateOperation("Application", "Application", null);
    }

    public string LogDirectory { get; }
    public string? LastError { get; private set; }

    public IOperationLog BeginOperation(string title, string operation, string? device = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return CreateOperation(title, operation, device);
        }
    }

    private OperationLog CreateOperation(string title, string operation, string? device)
    {
        var started = DateTimeOffset.Now;
        var id = $"{started:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}";
        var log = new OperationLog(this, new WorkLogInfo
        {
            Id = id, Title = title, Operation = operation, Device = device,
            StartedAt = started, Status = WorkLogStatus.Running,
            FilePath = Path.Combine(LogDirectory, id + ".log"),
        });
        _operations[id] = log;
        return log;
    }

    public void Write(WorkLogLevel level, string message, Exception? exception = null, string? operationId = null)
    {
        var target = operationId is not null && _operations.TryGetValue(operationId, out var operation)
            ? operation : _application;
        target.Write(level, message, exception);
    }

    public Task<IReadOnlyList<WorkLogInfo>> GetLogsAsync(LogQuery? query = null, CancellationToken cancellationToken = default)
        => Task.Run<IReadOnlyList<WorkLogInfo>>(() =>
        {
            if (!Directory.Exists(LogDirectory)) return [];
            var result = new List<WorkLogInfo>();
            foreach (var path in Directory.EnumerateFiles(LogDirectory, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Path.GetExtension(path).ToLowerInvariant() is not (".log" or ".txt" or ".jsonl")) continue;
                try
                {
                    var file = new FileInfo(path);
                    WorkLogInfo? info = null;
                    try
                    {
                        if (File.Exists(path + ".meta.json"))
                            info = JsonSerializer.Deserialize(File.ReadAllText(path + ".meta.json"), AppJsonContext.Default.WorkLogInfo);
                    }
                    catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
                    {
                        // A missing/damaged sidecar must not hide an otherwise readable work log.
                    }
                    info ??= new WorkLogInfo
                    {
                        Id = Path.GetFileNameWithoutExtension(path), Title = Path.GetFileNameWithoutExtension(path),
                        Operation = "Imported", StartedAt = file.CreationTimeUtc,
                        EndedAt = file.LastWriteTimeUtc, Status = WorkLogStatus.Interrupted,
                    };
                    info = info with { FilePath = path, SizeBytes = file.Length };
                    // A running record from a previous process is interrupted, not successful.
                    if (info.Status == WorkLogStatus.Running && !_operations.ContainsKey(info.Id) && !IsBeingWritten(path))
                        info = info with { Status = WorkLogStatus.Interrupted };
                    if (query?.Status is { } status && info.Status != status) continue;
                    if (query?.From is { } from && info.StartedAt < from) continue;
                    if (query?.To is { } to && info.StartedAt > to) continue;
                    if (!string.IsNullOrWhiteSpace(query?.Search) &&
                        !$"{info.Title} {info.Operation} {info.Device} {Path.GetFileName(path)}".Contains(query.Search.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                    result.Add(info);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { RecordError(ex); }
            }
            return result.OrderByDescending(log => log.StartedAt).ToArray();
        }, cancellationToken);

    private static bool IsBeingWritten(string path)
    {
        try { using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None); return false; }
        catch (IOException) { return true; }
    }

    public async Task<string> ReadTailAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(filePath);
        var relative = Path.GetRelativePath(LogDirectory, fullPath);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Log path must be inside the log directory.", nameof(filePath));
        await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            4096, FileOptions.Asynchronous);
        const int maxBytes = 256 * 1024;
        var length = stream.Length;
        var buffer = new byte[(int)Math.Min(length, maxBytes)];
        var truncated = length > maxBytes;
        if (truncated) stream.Seek(length - maxBytes, SeekOrigin.Begin);
        var count = 0;
        while (count < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(count), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            count += read;
        }
        // Skip only a partial UTF-8 code point; a single long line is still useful output.
        var start = 0;
        if (truncated)
            while (start < count && (buffer[start] & 0xC0) == 0x80) start++;
        return Encoding.UTF8.GetString(buffer, start, count - start);
    }

    private void RecordError(Exception exception)
    {
        LastError = exception.Message;
        System.Diagnostics.Trace.WriteLine(exception);
    }

    public void Dispose()
    {
        lock (_lifetimeGate)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var operation in _operations.Values)
                operation.Complete(ReferenceEquals(operation, _application) ? WorkLogStatus.Succeeded : WorkLogStatus.Interrupted);
        }
    }

    private sealed class OperationLog : IOperationLog
    {
        private readonly FileLogService _owner;
        private readonly object _gate = new();
        private WorkLogInfo _info;
        private StreamWriter? _writer;
        private bool _completed;

        public OperationLog(FileLogService owner, WorkLogInfo info)
        {
            _owner = owner;
            _info = info;
            TryIo(() =>
            {
                Directory.CreateDirectory(owner.LogDirectory);
                _writer = new StreamWriter(new FileStream(info.FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read),
                    new UTF8Encoding(false)) { AutoFlush = true };
                SaveMetadata();
            });
            Write(WorkLogLevel.Information, $"Started: {info.Title} | {info.Operation} | {info.Device}");
        }

        public string Id => _info.Id;

        public void Write(WorkLogLevel level, string message, Exception? exception = null)
        {
            lock (_gate)
            {
                if (_completed) return;
                TryIo(() =>
                {
                    _writer?.WriteLine($"{DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture)} [{level}] {message}");
                    if (exception is not null) _writer?.WriteLine(exception);
                });
            }
        }

        public void Complete(WorkLogStatus status = WorkLogStatus.Succeeded)
        {
            if (status == WorkLogStatus.Running) throw new ArgumentException("Completion requires a terminal status.", nameof(status));
            lock (_gate)
            {
                if (_completed) return;
                Write(status == WorkLogStatus.Failed ? WorkLogLevel.Error : WorkLogLevel.Information, $"Completed: {status}");
                _completed = true;
                _info = _info with { Status = status, EndedAt = DateTimeOffset.Now };
                TryIo(() => _writer?.Dispose());
                TryIo(SaveMetadata);
                _owner._operations.TryRemove(Id, out _);
            }
        }

        private void SaveMetadata()
        {
            var path = _info.FilePath + ".meta.json";
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(_info, AppJsonContext.Default.WorkLogInfo));
            File.Move(path + ".tmp", path, true);
        }

        private void TryIo(Action action)
        {
            try { action(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _owner.RecordError(ex); }
        }

        public void Dispose() => Complete(WorkLogStatus.Interrupted);
    }
}
