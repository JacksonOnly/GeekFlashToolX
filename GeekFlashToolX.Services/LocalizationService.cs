using System.Globalization;
using System.Text.Json;
using GeekFlashToolX.Core.Models;
using GeekFlashToolX.Core.Services;
using GeekFlashToolX.Services.Serialization;

namespace GeekFlashToolX.Services;

public sealed class LocalizationService : ILocalizationService
{
    private const string FallbackLanguage = "en-US";
    private readonly string _languageDirectory;
    private Dictionary<string, string> _fallback = new(StringComparer.Ordinal);
    private Dictionary<string, string> _strings = new(StringComparer.Ordinal);

    public LocalizationService(string? languageDirectory = null)
    {
        _languageDirectory = languageDirectory ?? Path.Combine(AppContext.BaseDirectory, "Langs");
    }

    public event EventHandler? CultureChanged;

    public string CurrentLanguageCode { get; private set; } = FallbackLanguage;

    public IReadOnlyList<LanguageOption> AvailableLanguages { get; private set; } = [];

    public string this[string key] => String(key);

    public string String(string key) => _strings.TryGetValue(key, out var value)
        ? value
        : _fallback.TryGetValue(key, out value) ? value : key;

    public string FormatString(string key, params object?[] arguments) =>
        string.Format(CultureInfo.GetCultureInfo(CurrentLanguageCode), String(key), arguments);

    public async Task InitializeAsync(string? preferredLanguageCode, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_languageDirectory);
        AvailableLanguages = DiscoverLanguages();
        _fallback = await LoadDictionaryAsync(FallbackLanguage, cancellationToken).ConfigureAwait(false);

        var requested = string.IsNullOrWhiteSpace(preferredLanguageCode)
            ? CultureInfo.CurrentUICulture.Name
            : preferredLanguageCode;
        var matched = MatchLanguage(requested);
        await SetLanguageCoreAsync(matched, cancellationToken).ConfigureAwait(false);
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetLanguageAsync(string languageCode, CancellationToken cancellationToken = default)
    {
        var matched = MatchLanguage(languageCode);
        if (string.Equals(CurrentLanguageCode, matched, StringComparison.OrdinalIgnoreCase) && _strings.Count > 0)
        {
            return;
        }

        await SetLanguageCoreAsync(matched, cancellationToken).ConfigureAwait(false);
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task SetLanguageCoreAsync(string languageCode, CancellationToken cancellationToken)
    {
        _strings = await LoadDictionaryAsync(languageCode, cancellationToken).ConfigureAwait(false);
        CurrentLanguageCode = languageCode;
        var culture = CultureInfo.GetCultureInfo(languageCode);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private IReadOnlyList<LanguageOption> DiscoverLanguages()
    {
        var languages = Directory.EnumerateFiles(_languageDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(static code => !string.IsNullOrWhiteSpace(code))
            .Select(static code =>
            {
                try
                {
                    var culture = CultureInfo.GetCultureInfo(code!);
                    return new LanguageOption(culture.Name, culture.NativeName);
                }
                catch (CultureNotFoundException)
                {
                    return null;
                }
            })
            .Where(static item => item is not null)
            .Cast<LanguageOption>()
            .OrderBy(static item => item.NativeName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return languages.Count > 0 ? languages : [new LanguageOption(FallbackLanguage, "English (United States)")];
    }

    private string MatchLanguage(string requested)
    {
        var exact = AvailableLanguages.FirstOrDefault(item =>
            string.Equals(item.Code, requested, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact.Code;
        }

        var language = requested.Split('-', '_')[0];
        return AvailableLanguages.FirstOrDefault(item =>
                   item.Code.StartsWith(language + "-", StringComparison.OrdinalIgnoreCase))?.Code
               ?? AvailableLanguages.FirstOrDefault(item => item.Code.Equals(FallbackLanguage, StringComparison.OrdinalIgnoreCase))?.Code
               ?? AvailableLanguages[0].Code;
    }

    private async Task<Dictionary<string, string>> LoadDictionaryAsync(string languageCode, CancellationToken cancellationToken)
    {
        var path = Path.Combine(_languageDirectory, languageCode + ".json");
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        await using var stream = File.OpenRead(path);
        var values = await JsonSerializer.DeserializeAsync(stream, AppJsonContext.Default.DictionaryStringString, cancellationToken)
            .ConfigureAwait(false);
        return values is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(values, StringComparer.Ordinal);
    }
}
