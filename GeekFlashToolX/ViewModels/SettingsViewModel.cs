using System.Collections.ObjectModel;
using GeekFlashToolX.Core.Models;
using GeekFlashToolX.Core.Services;
using ReactiveUI;

namespace GeekFlashToolX.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly IAppSettingsService _settingsService;
    private readonly ILocalizationService _localization;
    private readonly IAppearanceService _appearanceService;
    private ThemeOption? _selectedTheme;
    private LanguageOption? _selectedLanguage;
    private AccentOption? _selectedAccent;
    private bool _animationsEnabled;

    public SettingsViewModel(
        IAppSettingsService settingsService,
        ILocalizationService localization,
        IAppearanceService appearanceService) : base(localization)
    {
        _settingsService = settingsService;
        _localization = localization;
        _appearanceService = appearanceService;
        ThemeOptions = new ObservableCollection<ThemeOption>();
        Languages = new ObservableCollection<LanguageOption>(localization.AvailableLanguages);
        AccentOptions = new ObservableCollection<AccentOption>();
        RebuildThemeOptions();
        RebuildAccentOptions();

        _selectedTheme = ThemeOptions.First(item => item.Value == settingsService.Current.Theme);
        _selectedLanguage = Languages.FirstOrDefault(item => item.Code == localization.CurrentLanguageCode) ??
                            Languages.FirstOrDefault();
        _selectedAccent = AccentOptions.FirstOrDefault(item => item.Hex == settingsService.Current.AccentColor) ??
                          AccentOptions[0];
        _animationsEnabled = settingsService.Current.AnimationsEnabled;
    }

    public string Eyebrow => Text("Settings.Eyebrow");
    public string Title => Text("Settings.Title");
    public string Subtitle => Text("Settings.Subtitle");
    public string AppearanceTitle => Text("Settings.AppearanceTitle");
    public string AppearanceBody => Text("Settings.AppearanceBody");
    public string ThemeLabel => Text("Settings.Theme");
    public string LanguageLabel => Text("Settings.Language");
    public string AccentLabel => Text("Settings.Accent");
    public string AnimationTitle => Text("Settings.AnimationTitle");
    public string AnimationBody => Text("Settings.AnimationBody");
    public string SettingsPathLabel => Text("Settings.Path");
    public string CopySettingsPathLabel => Text("Settings.CopyPath");
    public string SettingsPath => _settingsService.SettingsFilePath;

    public ObservableCollection<ThemeOption> ThemeOptions { get; }
    public ObservableCollection<LanguageOption> Languages { get; }
    public ObservableCollection<AccentOption> AccentOptions { get; }

    public ThemeOption? SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (Equals(_selectedTheme, value) || value is null) return;
            this.RaiseAndSetIfChanged(ref _selectedTheme, value);
            _settingsService.Current.Theme = value.Value;
            ApplyAndSave();
        }
    }

    public LanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (Equals(_selectedLanguage, value) || value is null) return;
            this.RaiseAndSetIfChanged(ref _selectedLanguage, value);
            _ = ChangeLanguageAsync(value.Code);
        }
    }

    public AccentOption? SelectedAccent
    {
        get => _selectedAccent;
        set
        {
            if (Equals(_selectedAccent, value) || value is null) return;
            this.RaiseAndSetIfChanged(ref _selectedAccent, value);
            _settingsService.Current.AccentColor = value.Hex;
            ApplyAndSave();
        }
    }

    public bool AnimationsEnabled
    {
        get => _animationsEnabled;
        set
        {
            if (_animationsEnabled == value) return;
            _settingsService.Current.AnimationsEnabled = value;
            this.RaiseAndSetIfChanged(ref _animationsEnabled, value);
            _ = SaveSafelyAsync();
        }
    }

    protected override void OnLanguageChanged()
    {
        RebuildThemeOptions();
        RebuildAccentOptions();
        base.OnLanguageChanged();
    }

    private async Task ChangeLanguageAsync(string code)
    {
        await _localization.SetLanguageAsync(code);
        _settingsService.Current.LanguageCode = _localization.CurrentLanguageCode;
        await SaveSafelyAsync();
    }

    private void RebuildAccentOptions()
    {
        var selectedHex = _selectedAccent?.Hex ?? _settingsService.Current.AccentColor;
        AccentOptions.Clear();
        AccentOptions.Add(new AccentOption("#2F81F7", Text("Accent.Blue")));
        AccentOptions.Add(new AccentOption("#06B6D4", Text("Accent.Cyan")));
        AccentOptions.Add(new AccentOption("#8B5CF6", Text("Accent.Violet")));
        _selectedAccent = AccentOptions.FirstOrDefault(item => item.Hex == selectedHex) ?? AccentOptions[0];
        this.RaisePropertyChanged(nameof(SelectedAccent));
    }

    private void RebuildThemeOptions()
    {
        var selectedValue = _selectedTheme?.Value ?? _settingsService.Current.Theme;
        ThemeOptions.Clear();
        ThemeOptions.Add(new ThemeOption(ThemeMode.System, Text("Theme.System")));
        ThemeOptions.Add(new ThemeOption(ThemeMode.Light, Text("Theme.Light")));
        ThemeOptions.Add(new ThemeOption(ThemeMode.Dark, Text("Theme.Dark")));
        _selectedTheme = ThemeOptions.First(item => item.Value == selectedValue);
        this.RaisePropertyChanged(nameof(SelectedTheme));
    }

    private void ApplyAndSave()
    {
        _appearanceService.Apply(_settingsService.Current);
        _ = SaveSafelyAsync();
    }

    private async Task SaveSafelyAsync()
    {
        try
        {
            await _settingsService.SaveAsync();
        }
        catch (IOException)
        {
            // A later setting change retries the atomic save; the UI remains responsive.
        }
    }
}