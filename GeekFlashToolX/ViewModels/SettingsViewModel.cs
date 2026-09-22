using System.Collections.ObjectModel;
using GeekFlashToolX.Core.Models;
using GeekFlashToolX.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GeekFlashToolX.Services;

namespace GeekFlashToolX.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly IAppSettingsService _settingsService;
    private readonly ILocalizationService _localization;
    private readonly IAppearanceService _appearanceService;
    private ThemeOption? _selectedTheme;
    private LanguageOption? _selectedLanguage;
    private AccentOption? _selectedAccent;
    [ObservableProperty] private bool _animationsEnabled;
    [ObservableProperty] private bool _autoCheckUpdates;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CheckUpdatesKey))]
    private bool _isCheckingUpdates;
    private readonly IUpdateCoordinator _updateCoordinator;
    private readonly IUiInteractionService? _ui;

    public SettingsViewModel(
        IAppSettingsService settingsService,
        ILocalizationService localization,
        IAppearanceService appearanceService,
        IUpdateCoordinator updateCoordinator,
        IUiInteractionService? ui = null) : base(localization)
    {
        _settingsService = settingsService;
        _localization = localization;
        _appearanceService = appearanceService;
        _updateCoordinator = updateCoordinator;
        _ui = ui;
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
        _autoCheckUpdates = settingsService.Current.AutoCheckUpdates;
    }

    public string SettingsPath => _settingsService.SettingsFilePath;
    [RelayCommand]
    private Task CopySettingsPathAsync() => _ui?.CopyTextAsync(SettingsPath) ?? Task.CompletedTask;
    public string CheckUpdatesKey => IsCheckingUpdates ? "Update.Checking" : "Update.CheckTitle";
    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        IsCheckingUpdates = true;
        try { await _updateCoordinator.CheckAndNotifyAsync(); }
        finally { IsCheckingUpdates = false; }
    }

    partial void OnAutoCheckUpdatesChanged(bool value)
    {
        _settingsService.Current.AutoCheckUpdates = value;
        _ = SaveSafelyAsync();
    }

    partial void OnAnimationsEnabledChanging(bool value) => _settingsService.Current.AnimationsEnabled = value;
    partial void OnAnimationsEnabledChanged(bool value) => _ = SaveSafelyAsync();

    public ObservableCollection<ThemeOption> ThemeOptions { get; }
    public ObservableCollection<LanguageOption> Languages { get; }
    public ObservableCollection<AccentOption> AccentOptions { get; }

    public ThemeOption? SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (Equals(_selectedTheme, value) || value is null) return;
            SetProperty(ref _selectedTheme, value);
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
            SetProperty(ref _selectedLanguage, value);
            _ = ChangeLanguageAsync(value.Code);
        }
    }

    public AccentOption? SelectedAccent
    {
        get => _selectedAccent;
        set
        {
            if (Equals(_selectedAccent, value) || value is null) return;
            SetProperty(ref _selectedAccent, value);
            _settingsService.Current.AccentColor = value.Hex;
            ApplyAndSave();
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
        AccentOptions.Add(new AccentOption("#2F81F7", String("Accent.Blue")));
        AccentOptions.Add(new AccentOption("#06B6D4", String("Accent.Cyan")));
        AccentOptions.Add(new AccentOption("#8B5CF6", String("Accent.Violet")));
        _selectedAccent = AccentOptions.FirstOrDefault(item => item.Hex == selectedHex) ?? AccentOptions[0];
        OnPropertyChanged(nameof(SelectedAccent));
    }

    private void RebuildThemeOptions()
    {
        var selectedValue = _selectedTheme?.Value ?? _settingsService.Current.Theme;
        ThemeOptions.Clear();
        ThemeOptions.Add(new ThemeOption(ThemeMode.System, String("Theme.System")));
        ThemeOptions.Add(new ThemeOption(ThemeMode.Light, String("Theme.Light")));
        ThemeOptions.Add(new ThemeOption(ThemeMode.Dark, String("Theme.Dark")));
        _selectedTheme = ThemeOptions.First(item => item.Value == selectedValue);
        OnPropertyChanged(nameof(SelectedTheme));
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
