using GeekFlashToolX.Core.Models;

namespace GeekFlashToolX.Core.Services;

public interface ILogService : IDisposable
{
    string LogDirectory { get; }
    string? LastError { get; }
    IOperationLog BeginOperation(string title, string operation, string? device = null);
    void Write(WorkLogLevel level, string message, Exception? exception = null, string? operationId = null);
    Task<IReadOnlyList<WorkLogInfo>> GetLogsAsync(LogQuery? query = null, CancellationToken cancellationToken = default);
    /// <summary>Returns a bounded tail of the file, including when it is still being written.</summary>
    Task<string> ReadTailAsync(string filePath, CancellationToken cancellationToken = default);
}

public interface IOperationLog : IDisposable
{
    string Id { get; }
    void Write(WorkLogLevel level, string message, Exception? exception = null);
    /// <summary>Call explicitly on success, failure or cancellation. Disposal alone means interrupted.</summary>
    void Complete(WorkLogStatus status = WorkLogStatus.Succeeded);
}
