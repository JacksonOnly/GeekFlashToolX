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
        progress?.Report(0);
        await LoadSettingsAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(0.3);
        await ApplyAppearanceAsync();
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(0.5);
        await InitializeLocalizationAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(0.85);
        await SaveNormalizedLanguageAsync(cancellationToken).ConfigureAwait(false);
        progress?.Report(1);
    }

    private Task LoadSettingsAsync(CancellationToken cancellationToken) =>
        settingsService.LoadAsync(cancellationToken);

    private async Task ApplyAppearanceAsync() =>
        await Dispatcher.UIThread.InvokeAsync(() => appearanceService.Apply(settingsService.Current));

    private Task InitializeLocalizationAsync(CancellationToken cancellationToken) =>
        localizationService.InitializeAsync(settingsService.Current.LanguageCode, cancellationToken);

    private async Task SaveNormalizedLanguageAsync(CancellationToken cancellationToken)
    {
        var languageCode = localizationService.CurrentLanguageCode;
        if (settingsService.Current.LanguageCode == languageCode) return;
        settingsService.Current.LanguageCode = languageCode;
        await settingsService.SaveAsync(cancellationToken).ConfigureAwait(false);
    }
}
