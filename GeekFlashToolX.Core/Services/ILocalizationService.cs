using GeekFlashToolX.Core.Models;

namespace GeekFlashToolX.Core.Services;

public interface ILocalizationService
{
    event EventHandler? CultureChanged;

    string CurrentLanguageCode { get; }

    IReadOnlyList<LanguageOption> AvailableLanguages { get; }

    string this[string key] { get; }

    Task InitializeAsync(string? preferredLanguageCode, CancellationToken cancellationToken = default);

    Task SetLanguageAsync(string languageCode, CancellationToken cancellationToken = default);
}
