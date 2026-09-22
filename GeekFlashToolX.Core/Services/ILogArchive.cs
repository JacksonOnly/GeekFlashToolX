using GeekFlashToolX.Core.Models;
using Serilog.Events;

namespace GeekFlashToolX.Core.Services;

/// <summary>Provides operation log files and the searchable log archive.</summary>
public interface ILogArchive : IDisposable
{
    string LogDirectory { get; }
    string? LastError { get; }
    IOperationLog BeginOperation(string title, string operation, string? device = null);
    Task<IReadOnlyList<WorkLogInfo>> GetLogsAsync(LogQuery? query = null, CancellationToken cancellationToken = default);
    Task<WorkLogInfo> GetInfoAsync(WorkLogInfo log, CancellationToken cancellationToken = default);
    Task<string> ReadTailAsync(string filePath, CancellationToken cancellationToken = default);
}

public interface IOperationLog : IDisposable
{
    string Id { get; }
    void Write(LogEventLevel level, string message, Exception? exception = null);
    void Complete(WorkLogStatus status = WorkLogStatus.Succeeded);
}
