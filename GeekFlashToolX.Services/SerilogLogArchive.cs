using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using GeekFlashToolX.Core.Models;
using GeekFlashToolX.Core.Services;
using GeekFlashToolX.Services.Serialization;
using Serilog;
using Serilog.Events;

namespace GeekFlashToolX.Services;

/// <summary>Indexes operation logs for the UI. Serilog owns every log file writer.</summary>
public sealed class SerilogLogArchive : ILogArchive
{
    private readonly ConcurrentDictionary<string, OperationLog> _operations = new();
    private readonly ILogger _logger;
    private readonly object _gate = new();
    private bool _disposed;

    public SerilogLogArchive(string logDirectory, ILogger logger)
    {
        LogDirectory = Path.GetFullPath(logDirectory);
        Directory.CreateDirectory(LogDirectory);
        _logger = logger;
    }

    public string LogDirectory { get; }
    public string? LastError { get; private set; }

    public IOperationLog BeginOperation(string title, string operation, string? device = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var started = DateTimeOffset.Now;
            var id = $"{started:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}";
            var info = new WorkLogInfo
            {
                Id = id, Title = title, Operation = operation, Device = device,
                StartedAt = started, Status = WorkLogStatus.Running,
                FilePath = Path.Combine(LogDirectory, id + ".log"),
            };
            var result = new OperationLog(this, info, _logger);
            _operations[id] = result;
            return result;
        }
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
                        RecordError(ex);
                    }
                    info ??= new WorkLogInfo
                    {
                        Id = Path.GetFileNameWithoutExtension(path), Title = Path.GetFileNameWithoutExtension(path),
                        Operation = "Application", StartedAt = file.CreationTimeUtc,
                        EndedAt = file.LastWriteTimeUtc, Status = WorkLogStatus.Succeeded,
                        Kind = LogRecordKind.Application,
                    };
                    info = info with { FilePath = path, SizeBytes = file.Length };
                    if (info.Status == WorkLogStatus.Running && !_operations.ContainsKey(info.Id))
                        info = info with { Status = WorkLogStatus.Interrupted };
                    if (query?.Status is { } status && info.Status != status) continue;
                    if (query?.Kind is { } kind && info.Kind != kind) continue;
                    if (query?.From is { } from && info.StartedAt < from) continue;
                    if (query?.To is { } to && info.StartedAt >= to) continue;
                    if (!string.IsNullOrWhiteSpace(query?.Search) &&
                        !$"{info.Title} {info.Operation} {info.Device} {Path.GetFileName(path)}"
                            .Contains(query.Search.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                    result.Add(info);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { RecordError(ex); }
            }
            return result.OrderByDescending(log => log.StartedAt).ToArray();
        }, cancellationToken);

    public async Task<string> ReadTailAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ValidateLogPath(filePath);
        await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
        const int maxBytes = 256 * 1024;
        var length = stream.Length;
        var buffer = new byte[(int)Math.Min(length, maxBytes)];
        if (length > maxBytes) stream.Seek(length - maxBytes, SeekOrigin.Begin);
        var count = 0;
        while (count < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(count), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            count += read;
        }
        var start = 0;
        if (length > maxBytes)
            while (start < count && (buffer[start] & 0xC0) == 0x80) start++;
        return Encoding.UTF8.GetString(buffer, start, count - start);
    }

    public async Task<WorkLogInfo> GetInfoAsync(WorkLogInfo log, CancellationToken cancellationToken = default)
    {
        var path = ValidateLogPath(log.FilePath);
        var metadataPath = path + ".meta.json";
        if (!File.Exists(metadataPath)) return log;
        var json = await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false);
        var info = JsonSerializer.Deserialize(json, AppJsonContext.Default.WorkLogInfo);
        if (info is null) return log;
        var file = new FileInfo(path);
        if (info.Status == WorkLogStatus.Running && !_operations.ContainsKey(info.Id))
            info = info with { Status = WorkLogStatus.Interrupted };
        return info with { FilePath = path, SizeBytes = file.Length };
    }

    private string ValidateLogPath(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var relative = Path.GetRelativePath(LogDirectory, fullPath);
        if (Path.IsPathRooted(relative) || relative == ".." ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Log path must be inside the log directory.", nameof(filePath));
        return fullPath;
    }

    private void RecordError(Exception exception)
    {
        LastError = exception.Message;
        _logger.Warning(exception, "Could not read the log archive");
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var operation in _operations.Values) operation.Complete(WorkLogStatus.Interrupted);
        }
    }

    private sealed class OperationLog : IOperationLog
    {
        private readonly SerilogLogArchive _owner;
        private readonly ILogger _global;
        private readonly Serilog.Core.Logger _file;
        private readonly object _gate = new();
        private WorkLogInfo _info;
        private bool _completed;

        public OperationLog(SerilogLogArchive owner, WorkLogInfo info, ILogger global)
        {
            _owner = owner;
            _info = info;
            _global = global.ForContext("OperationId", info.Id).ForContext("Operation", info.Operation);
            _file = new LoggerConfiguration().MinimumLevel.Verbose()
                .WriteTo.File(info.FilePath, outputTemplate: "{Timestamp:O} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
            try { SaveMetadata(); }
            catch { _file.Dispose(); throw; }
            Write(LogEventLevel.Information, $"Started: {info.Title} | {info.Operation} | {info.Device}");
        }

        public string Id => _info.Id;

        public void Write(LogEventLevel level, string message, Exception? exception = null)
        {
            lock (_gate)
            {
                if (_completed) return;
                _global.Write(level, exception, "{Message}", message);
                _file.Write(level, exception, "{Message}", message);
            }
        }

        public void Complete(WorkLogStatus status = WorkLogStatus.Succeeded)
        {
            if (status == WorkLogStatus.Running) throw new ArgumentException("Completion requires a terminal status.", nameof(status));
            lock (_gate)
            {
                if (_completed) return;
                Write(status == WorkLogStatus.Failed ? LogEventLevel.Error : LogEventLevel.Information,
                    $"Completed: {status}");
                _completed = true;
                _info = _info with { Status = status, EndedAt = DateTimeOffset.Now };
                _file.Dispose();
                try { SaveMetadata(); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _owner.RecordError(ex); }
                _owner._operations.TryRemove(Id, out _);
            }
        }

        private void SaveMetadata()
        {
            var path = _info.FilePath + ".meta.json";
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(_info, AppJsonContext.Default.WorkLogInfo));
            File.Move(path + ".tmp", path, true);
        }

        public void Dispose() => Complete(WorkLogStatus.Interrupted);
    }
}
