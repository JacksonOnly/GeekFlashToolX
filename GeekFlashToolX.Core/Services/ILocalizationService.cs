using GeekFlashToolX.Core.Models;

namespace GeekFlashToolX.Core.Services;

public interface ILocalizationService
{
    event EventHandler? CultureChanged;

    string CurrentLanguageCode { get; }

    IReadOnlyList<LanguageOption> AvailableLanguages { get; }

    string this[string key] { get; }

    /// <summary>Returns the translation, the English fallback, or the key if neither exists.</summary>
    string String(string key);

    /// <summary>Formats a translated composite format using the selected language's culture.</summary>
    string FormatString(string key, params object?[] arguments);

    Task InitializeAsync(string? preferredLanguageCode, CancellationToken cancellationToken = default);

    Task SetLanguageAsync(string languageCode, CancellationToken cancellationToken = default);
}
