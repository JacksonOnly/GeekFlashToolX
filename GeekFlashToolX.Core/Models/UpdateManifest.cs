namespace GeekFlashToolX.Core.Models;

public sealed record UpdateManifest
{
    public string Version { get; init; } = string.Empty;
    public string Changelog { get; init; } = string.Empty;
    public string? ReleaseTag { get; init; }
}

public enum UpdateCheckStatus { Available, UpToDate, Ignored, NotFound, Failed }
public sealed record UpdateCheckResult(UpdateCheckStatus Status, UpdateManifest? Manifest = null, Uri? ReleaseUri = null);
