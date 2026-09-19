namespace GeekFlashToolX.Core.Models;

public enum WorkLogLevel { Trace, Debug, Information, Warning, Error, Critical }
public enum WorkLogStatus { Running, Succeeded, Failed, Cancelled, Interrupted }

/// <summary>One application run or device operation, independently archived on disk.</summary>
public sealed record WorkLogInfo
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty;
    public string? Device { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? EndedAt { get; init; }
    public WorkLogStatus Status { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
}

public sealed record LogQuery(string? Search = null, WorkLogStatus? Status = null,
    DateTimeOffset? From = null, DateTimeOffset? To = null);
