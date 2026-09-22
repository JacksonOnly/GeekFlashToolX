using GeekFlashToolX.Core.Models;
using GeekFlashToolX.Core.Services;
using Serilog;

namespace GeekFlashToolX.Services;

/// <summary>Call on the UI thread after the main window is shown. Repeated checks never overlap dialogs.</summary>
public sealed class UpdateCoordinator(IUpdateService updates, IAppSettingsService settings, IMessageBoxService messageBoxes,
    IExternalLauncher launcher, ILocalizationService localization,
    INotifier notifier) : IUpdateCoordinator
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task CheckAndNotifyAsync(bool automatic = false, CancellationToken cancellationToken = default)
    {
        if (automatic && !settings.Current.AutoCheckUpdates) return;
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            if (!automatic) notifier.Show(localization.String("Update.CheckTitle"), localization.String("Update.AlreadyChecking"));
            return;
        }
        IDisposable? checking = null;
        try
        {
            if (!automatic)
                checking = notifier.Show(localization.String("Update.CheckTitle"), localization.String("Update.Checking"),
                    expiration: TimeSpan.Zero);
            var result = await updates.CheckAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            checking?.Dispose();
            checking = null;
            if (!automatic)
            {
                var kind = result.Status switch
                {
                    UpdateCheckStatus.Available or UpdateCheckStatus.UpToDate => NotificationKind.Success,
                    UpdateCheckStatus.Failed => NotificationKind.Error,
                    _ => NotificationKind.Information,
                };
                notifier.Show(localization.String("Update.CheckTitle"), localization.String($"Update.{result.Status}"), kind);
            }
            if (result is { Status: UpdateCheckStatus.Available, Manifest: { } manifest, ReleaseUri: { } release })
            {
                var decision = await messageBoxes.ShowAsync(new MessageBoxRequest(
                    localization.String("Update.Available"),
                    $"v{manifest.Version}\n\n{manifest.Changelog}",
                    localization.String("Update.View"),
                    localization.String("Update.Close"),
                    localization.String("Update.Ignore")), cancellationToken);
                if (decision.IsChecked)
                    await updates.IgnoreVersionAsync(manifest.Version, cancellationToken);
                if (decision.PrimaryButtonClicked) await launcher.OpenUriAsync(release);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Log.Error(ex, "Update notification or user action failed.");
            if (!automatic && !cancellationToken.IsCancellationRequested)
                notifier.Show(localization.String("Update.CheckTitle"), localization.String("Update.Failed"), NotificationKind.Error);
        }
        finally { checking?.Dispose(); _gate.Release(); }
    }
}
