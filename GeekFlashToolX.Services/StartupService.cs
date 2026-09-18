using GeekFlashToolX.Core.Services;
using Avalonia.Threading;

namespace GeekFlashToolX.Services;

public sealed class StartupService(
    IAppSettingsService settingsService,
    ILocalizationService localizationService,
    IAppearanceService appearanceService) : IStartupService
{
    public async Task InitializeAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(0.15);
        await settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        progress?.Report(0.5);
        // Apply the persisted theme while the splash screen is still visible.
        await Dispatcher.UIThread.InvokeAsync(() => appearanceService.Apply(settingsService.Current));
        await localizationService.InitializeAsync(settingsService.Current.LanguageCode, cancellationToken).ConfigureAwait(false);
        settingsService.Current.LanguageCode = localizationService.CurrentLanguageCode;
        progress?.Report(0.85);
        await settingsService.SaveAsync(cancellationToken).ConfigureAwait(false);
        progress?.Report(1);
    }
}
