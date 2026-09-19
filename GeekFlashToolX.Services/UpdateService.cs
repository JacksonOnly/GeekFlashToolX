using System.Net;
using System.Net.Http;
using System.Text.Json;
using GeekFlashToolX.Core.Models;
using GeekFlashToolX.Core.Services;
using GeekFlashToolX.Services.Serialization;
using NuGet.Versioning;

namespace GeekFlashToolX.Services;

public sealed class UpdateService(HttpClient httpClient, IAppSettingsService settings,
    ILocalizationService localization, ILogService logs, string currentVersion) : IUpdateService
{
    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var operation = logs.BeginOperation(localization.String("Update.CheckTitle"), "CheckUpdate");
        try
        {
            var repo = new Uri(localization.String("GIT_REPO_URL").TrimEnd('/') + "/", UriKind.Absolute);
            if (repo.Scheme != Uri.UriSchemeHttps) throw new InvalidDataException("Repository URL must use HTTPS.");
            var manifestUri = new Uri(repo, "releases/latest/download/latest.json");
            operation.Write(WorkLogLevel.Information, $"GET {manifestUri}");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            using var response = await httpClient.GetAsync(manifestUri, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                operation.Write(WorkLogLevel.Information, "404: latest.json not published; skipping update check.");
                operation.Complete();
                return new(UpdateCheckStatus.NotFound);
            }
            response.EnsureSuccessStatusCode();
            // Buffering is bounded even when Content-Length is absent or incorrect.
            const int limit = 1024 * 1024;
            await response.Content.LoadIntoBufferAsync(limit, timeout.Token).ConfigureAwait(false);
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            var manifest = await JsonSerializer.DeserializeAsync(stream, AppJsonContext.Default.UpdateManifest, timeout.Token).ConfigureAwait(false);
            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Changelog) ||
                !TryVersion(manifest.Version, out var latest) || !TryVersion(currentVersion, out var installed))
                throw new InvalidDataException("Invalid update manifest: version and changelog are required.");

            var status = VersionComparer.VersionRelease.Compare(latest, installed) <= 0
                ? UpdateCheckStatus.UpToDate
                : TryVersion(settings.Current.IgnoredUpdateVersion, out var ignored) && VersionComparer.VersionRelease.Compare(latest, ignored) <= 0
                    ? UpdateCheckStatus.Ignored : UpdateCheckStatus.Available;
            var release = new Uri(repo, string.IsNullOrWhiteSpace(manifest.ReleaseTag)
                ? "releases" : "releases/tag/" + Uri.EscapeDataString(manifest.ReleaseTag));
            operation.Write(WorkLogLevel.Information, $"Update {manifest.Version}: {status}; installed {currentVersion}.");
            operation.Complete();
            return new(status, manifest, release);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            operation.Complete(WorkLogStatus.Cancelled);
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or JsonException or OperationCanceledException or UriFormatException or InvalidOperationException)
        {
            operation.Write(WorkLogLevel.Warning, "Update check failed; application continues.", ex);
            operation.Complete(WorkLogStatus.Failed);
            return new(UpdateCheckStatus.Failed);
        }
    }

    public async Task IgnoreVersionAsync(string version, CancellationToken cancellationToken = default)
    {
        if (!TryVersion(version, out var parsed)) throw new ArgumentException("Invalid version.", nameof(version));
        var previous = settings.Current.IgnoredUpdateVersion;
        settings.Current.IgnoredUpdateVersion = parsed!.ToNormalizedString();
        try { await settings.SaveAsync(cancellationToken).ConfigureAwait(false); }
        catch { settings.Current.IgnoredUpdateVersion = previous; throw; }
        logs.Write(WorkLogLevel.Information, $"Ignored update version {version}.");
    }

    private static bool TryVersion(string? text, out NuGetVersion? version) =>
        NuGetVersion.TryParse(text?.Trim().TrimStart('v', 'V'), out version);
}
