using System.ComponentModel;
using System.Reflection;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using GeekFlashToolX.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GeekFlashToolX.Services;

namespace GeekFlashToolX.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
{
    private readonly IAppSettingsService _settingsService;
    private readonly NavigationRegistry _navigation;
    private readonly SettingsViewModel _settings;
    private readonly IUiInteractionService? _ui;
    private bool _disposed;
    private readonly IPageTransition _animatedPageTransition = new CrossFade(TimeSpan.FromMilliseconds(220));
    private readonly IPageTransition _instantPageTransition = new InstantPageTransition();
    private Control _currentPage;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleSidebarLabel))]
    private bool _isSidebarExpanded = true;

    public MainViewModel(
        ILocalizationService localization,
        IAppSettingsService settingsService,
        NavigationRegistry navigation,
        IUiInteractionService? ui = null) : base(localization)
    {
        _settingsService = settingsService;
        _navigation = navigation;
        _settings = navigation.Page<SettingsViewModel>();
        _ui = ui;
        _currentPage = navigation.Initial.View!;

        _settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    public string AppVersion => $"v{Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0"}";
    public string ToggleSidebarLabel => String(IsSidebarExpanded ? "Window.CollapseSidebar" : "Window.ExpandSidebar");
    public bool IsCompact { get; set; }

    public Control CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public IReadOnlyList<NavigationItem> NavigationItems => _navigation.AllItems;
    public IReadOnlyList<NavigationItem> PrimaryNavigationItems => _navigation.PrimaryItems;
    public IReadOnlyList<NavigationItem> FooterNavigationItems => _navigation.FooterItems;
    public bool AnimationsDisabled => !_settingsService.Current.AnimationsEnabled;

    public IPageTransition PageTransition => _settingsService.Current.AnimationsEnabled
        ? _animatedPageTransition
        : _instantPageTransition;

    [RelayCommand]
    private void NavigateTo(NavigationItem? item)
    {
        if (_disposed || item is null) return;
        if (item.HasChildren)
        {
            item.IsExpanded = !item.IsExpanded;
            if (item.IsExpanded) IsSidebarExpanded = true;
            return;
        }

        if (item.View is null) return;
        if (!ReferenceEquals(CurrentPage, item.View)) CurrentPage = item.View;
        _navigation.Select(item);
        if (IsCompact) IsSidebarExpanded = false;
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarExpanded = !IsSidebarExpanded;

    [RelayCommand]
    private void Minimize() => _ui?.MinimizeMainWindow();

    [RelayCommand]
    private void Maximize() => _ui?.ToggleMainWindowMaximize();

    [RelayCommand]
    private void CloseWindow() => _ui?.CloseMainWindow();

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _settings.PropertyChanged -= OnSettingsPropertyChanged;
        _navigation.Dispose();
        base.Dispose();
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SettingsViewModel.AnimationsEnabled))
        {
            OnPropertyChanged(nameof(PageTransition));
            OnPropertyChanged(nameof(AnimationsDisabled));
        }
    }

    private sealed class InstantPageTransition : IPageTransition
    {
        public Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
        {
            if (from is not null)
            {
                from.Opacity = 1;
                from.IsVisible = false;
            }

            if (to is not null)
            {
                to.Opacity = 1;
                to.IsVisible = true;
            }

            return Task.CompletedTask;
        }
    }
}
