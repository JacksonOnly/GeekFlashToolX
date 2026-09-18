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
    private readonly HomeViewModel _home;
    private readonly OtherViewModel _other;
    private readonly SettingsViewModel _settings;
    private readonly IPageTransition _animatedPageTransition = new CrossFade(TimeSpan.FromMilliseconds(220));
    private readonly IPageTransition _instantPageTransition = new InstantPageTransition();
    private ViewModelBase _currentPage;
    private bool _isSidebarExpanded = true;

    public MainViewModel(
        ILocalizationService localization,
        IAppSettingsService settingsService,
        HomeViewModel home,
        OtherViewModel other,
        SettingsViewModel settings) : base(localization)
    {
        _settingsService = settingsService;
        _home = home;
        _other = other;
        _settings = settings;
        _currentPage = home;

        NavigateHomeCommand = ReactiveCommand.Create(() => NavigateTo(_home));
        NavigateOtherCommand = ReactiveCommand.Create(() => NavigateTo(_other));
        NavigateSettingsCommand = ReactiveCommand.Create(() => NavigateTo(_settings));
        ToggleSidebarCommand = ReactiveCommand.Create(() => IsSidebarExpanded = !IsSidebarExpanded);

        settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    public string AppName => Text("App.Name");
    public string AppTagline => Text("App.Tagline");
    public string HomeLabel => Text("Nav.Home");
    public string OtherLabel => Text("Nav.Other");
    public string SettingsLabel => Text("Nav.Settings");
    public string ReadyLabel => Text("Footer.Ready");
    public string AppVersion => $"v{Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0"}";
    public string ToggleSidebarLabel => Text(IsSidebarExpanded ? "Window.CollapseSidebar" : "Window.ExpandSidebar");
    public string MinimizeLabel => Text("Window.Minimize");
    public string MaximizeLabel => Text("Window.MaximizeRestore");
    public string CloseLabel => Text("Window.Close");
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
        private set
        {
            this.RaiseAndSetIfChanged(ref _currentPage, value);
            this.RaisePropertyChanged(nameof(IsHomeActive));
            this.RaisePropertyChanged(nameof(IsOtherActive));
            this.RaisePropertyChanged(nameof(IsSettingsActive));
        }
    }

    public bool IsHomeActive => ReferenceEquals(CurrentPage, _home);
    public bool IsOtherActive => ReferenceEquals(CurrentPage, _other);
    public bool IsSettingsActive => ReferenceEquals(CurrentPage, _settings);
    public bool AnimationsDisabled => !_settingsService.Current.AnimationsEnabled;

    public IPageTransition PageTransition => _settingsService.Current.AnimationsEnabled
        ? _animatedPageTransition
        : _instantPageTransition;

    public ICommand NavigateHomeCommand { get; }
    public ICommand NavigateOtherCommand { get; }
    public ICommand NavigateSettingsCommand { get; }
    public ICommand ToggleSidebarCommand { get; }

    private void NavigateTo(ViewModelBase page)
    {
        CurrentPage = page;
        if (IsCompact) IsSidebarExpanded = false;
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