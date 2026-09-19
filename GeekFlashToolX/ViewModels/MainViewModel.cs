using System.ComponentModel;
using System.Reflection;
using System.Windows.Input;
using Avalonia;
using Avalonia.Animation;
using GeekFlashToolX.Core.Services;
using ReactiveUI;

namespace GeekFlashToolX.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IAppSettingsService _settingsService;
    private readonly NavigationRegistry _navigation;
    private readonly SettingsViewModel _settings;
    private bool _disposed;
    private readonly IPageTransition _animatedPageTransition = new CrossFade(TimeSpan.FromMilliseconds(220));
    private readonly IPageTransition _instantPageTransition = new InstantPageTransition();
    private ViewModelBase _currentPage;
    private bool _isSidebarExpanded = true;

    public MainViewModel(
        ILocalizationService localization,
        IAppSettingsService settingsService,
        NavigationRegistry navigation) : base(localization)
    {
        _settingsService = settingsService;
        _navigation = navigation;
        _settings = navigation.Page<SettingsViewModel>();
        _currentPage = navigation.Initial.Page;
        NavigateToCommand = ReactiveCommand.Create<PageKey>(NavigateTo);
        ToggleSidebarCommand = ReactiveCommand.Create(() => IsSidebarExpanded = !IsSidebarExpanded);

        _settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    public string AppVersion => $"v{Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0"}";
    public string ToggleSidebarLabel => String(IsSidebarExpanded ? "Window.CollapseSidebar" : "Window.ExpandSidebar");
    public bool IsCompact { get; set; }

    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded;
        set
        {
            this.RaiseAndSetIfChanged(ref _isSidebarExpanded, value);
            this.RaisePropertyChanged(nameof(ToggleSidebarLabel));
        }
    }

    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        private set => this.RaiseAndSetIfChanged(ref _currentPage, value);
    }

    public IReadOnlyList<NavigationItem> NavigationItems => _navigation.Items;
    public IReadOnlyList<NavigationItem> PrimaryNavigationItems => _navigation.PrimaryItems;
    public IReadOnlyList<NavigationItem> FooterNavigationItems => _navigation.FooterItems;
    public bool AnimationsDisabled => !_settingsService.Current.AnimationsEnabled;

    public IPageTransition PageTransition => _settingsService.Current.AnimationsEnabled
        ? _animatedPageTransition
        : _instantPageTransition;

    public ICommand NavigateToCommand { get; }
    public ICommand ToggleSidebarCommand { get; }

    private void NavigateTo(PageKey key)
    {
        if (_disposed || !_navigation.TryGet(key, out var destination)) return;
        if (!ReferenceEquals(CurrentPage, destination.Page))
        {
            CurrentPage = destination.Page;
            _navigation.Select(destination);
        }
        if (IsCompact) IsSidebarExpanded = false;
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _settings.PropertyChanged -= OnSettingsPropertyChanged;
        (NavigateToCommand as IDisposable)?.Dispose();
        (ToggleSidebarCommand as IDisposable)?.Dispose();
        _navigation.Dispose();
        base.Dispose();
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SettingsViewModel.AnimationsEnabled))
        {
            this.RaisePropertyChanged(nameof(PageTransition));
            this.RaisePropertyChanged(nameof(AnimationsDisabled));
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
