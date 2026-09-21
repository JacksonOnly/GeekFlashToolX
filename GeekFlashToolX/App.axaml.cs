using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using GeekFlashToolX.Core.Services;
using GeekFlashToolX.Services;
using GeekFlashToolX.ViewModels;
using GeekFlashToolX.Views;
using Splat;
using System.Net.Http;
using System.Reflection;
using GeekFlashToolX.Core.Models;
using IconPacks.Avalonia.Codicons;
using GeekFlashToolX.Views.Page;
using GeekFlashCore.UsbWatcher;

namespace GeekFlashToolX;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var splashViewModel = new SplashViewModel();
            var splash = new SplashWindow { DataContext = splashViewModel };
            desktop.MainWindow = splash;
            splash.Show();
            _ = CompleteStartupAsync(desktop, splash, splashViewModel);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void RegisterServices(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var settings = new AppSettingsService();
        var localization = new LocalizationService();
        var appearance = new AppearanceService();
        var logs = new FileLogService();
        Serilog.Log.Logger = CoreLogAdapter.CreateApplicationLogger(logs);
        var messageBoxes = new MessageBoxService(() => desktop.MainWindow);
        var notifier = new Notifier(() => desktop.MainWindow);
        var launcher = new ExternalLauncher();
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("GeekFlashToolX/1.0");
        var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? "1.0.0";
        var updates = new UpdateService(http, settings, localization, logs, version);
        var updateCoordinator = new UpdateCoordinator(updates, settings, messageBoxes, launcher, localization, logs, notifier);
        Locator.CurrentMutable.RegisterConstant<INotifier>(notifier);
        Locator.CurrentMutable.RegisterConstant<IMessageBoxService>(messageBoxes);
        Locator.CurrentMutable.RegisterConstant<ILogService>(logs);
        Locator.CurrentMutable.RegisterConstant<IExternalLauncher>(launcher);
        Locator.CurrentMutable.RegisterConstant<IUpdateService>(updates);
        Locator.CurrentMutable.RegisterConstant<IUpdateCoordinator>(updateCoordinator);
        desktop.Exit += (_, _) =>
        {
            Serilog.Log.CloseAndFlush();
            logs.Dispose();
            http.Dispose();
        };

        Locator.CurrentMutable.RegisterConstant<IAppSettingsService>(settings);
        Locator.CurrentMutable.RegisterConstant<ILocalizationService>(localization);
        Locator.CurrentMutable.RegisterConstant<IAppearanceService>(appearance);
        Locator.CurrentMutable.RegisterConstant<IStartupService>(new StartupService(settings, localization,
            appearance));
    }

    private static async Task CompleteStartupAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        SplashWindow splash,
        SplashViewModel splashViewModel)
    {
        NavigationRegistry? navigation = null;
        MainViewModel? main = null;
        var mainWindowShown = false;
        using var startupLifetime = new CancellationTokenSource();
        void OnSplashClosed(object? sender, EventArgs args) => startupLifetime.Cancel();
        splash.Closed += OnSplashClosed;
        try
        {
            await YieldForSplashAsync();
            startupLifetime.Token.ThrowIfCancellationRequested();
            splashViewModel.SetStage("正在注册服务…", 2);
            RegisterServices(desktop);
            splashViewModel.SetStage("正在注册页面…", 6);
            RegisterViews();

            splashViewModel.SetStage("正在加载设置与语言…", 10);
            await InitializeConfigurationAsync(splashViewModel, startupLifetime.Token);
            startupLifetime.Token.ThrowIfCancellationRequested();

            var localization = Resolve<ILocalizationService>();
            var settings = Resolve<IAppSettingsService>();
            splashViewModel.SetStage(localization.String("Splash.CreatingViewModels"), 55);
            await YieldForSplashAsync();
            startupLifetime.Token.ThrowIfCancellationRequested();
            var pages = CreatePageViewModels(localization, settings, Resolve<IAppearanceService>());
            splashViewModel.SetStage(localization.String("Splash.CreatingPages"), 64);
            await YieldForSplashAsync();
            if (startupLifetime.IsCancellationRequested)
            {
                pages.Dispose();
                startupLifetime.Token.ThrowIfCancellationRequested();
            }
            navigation = BuildNavigation(localization, pages);

            splashViewModel.SetStage(localization.String("Splash.LoadingLogs"), 72);
            await PreloadContentAsync(navigation);
            startupLifetime.Token.ThrowIfCancellationRequested();

            splashViewModel.SetStage(localization.String("Splash.MonitoringDevices"), 86);
            await YieldForSplashAsync();
            startupLifetime.Token.ThrowIfCancellationRequested();
            StartDeviceMonitoring(navigation.Page<FlashViewModel>());
            startupLifetime.Token.ThrowIfCancellationRequested();

            splashViewModel.SetStage(localization.String("Splash.OpeningWindow"), 94);
            main = new MainViewModel(localization, settings, navigation);
            var window = CreateMainWindow(main, out var lifetime);
            desktop.MainWindow = window;
            window.Show();
            mainWindowShown = true;
            main = null;
            navigation = null;
            splashViewModel.SetStage(localization.String("Splash.Ready"), 100);
            await YieldForSplashAsync();
            splash.Close();
            Resolve<ILogService>().Write(WorkLogLevel.Information, "Application startup completed.");
            _ = Resolve<IUpdateCoordinator>().CheckAndNotifyAsync(automatic: true, lifetime.Token);
        }
        catch (OperationCanceledException) when (startupLifetime.IsCancellationRequested)
        {
            main?.Dispose();
            if (main is null) navigation?.Dispose();
        }
        catch (Exception exception)
        {
            if (mainWindowShown)
            {
                Console.Error.WriteLine(exception);
                Locator.Current.GetService<ILogService>()?.Write(
                    WorkLogLevel.Error, "Post-startup action failed.", exception);
                return;
            }
            main?.Dispose();
            if (main is null) navigation?.Dispose();
            desktop.MainWindow = splash;
            Console.Error.WriteLine(exception);
            Locator.Current.GetService<ILogService>()?.Write(WorkLogLevel.Error, "Application startup failed.", exception);
            splashViewModel.Status = exception.Message;
        }
        finally
        {
            splash.Closed -= OnSplashClosed;
        }
    }

    private static void RegisterViews()
    {
        var views = ViewLocator.Instance;
        views.EnsureView<HomeViewModel, HomeView>();
        views.EnsureView<FlashViewModel, FlashView>();
        views.EnsureView<QcomDeviceConnectionViewModel, QcomDeviceConnectionView>();
        views.EnsureView<LogsViewModel, LogsView>();
        views.EnsureView<SettingsViewModel, SettingsView>();
    }

    private static Task InitializeConfigurationAsync(SplashViewModel splash, CancellationToken cancellationToken)
    {
        var progress = new Progress<double>(value => splash.Progress = 10 + value * 45);
        return Resolve<IStartupService>().InitializeAsync(progress, cancellationToken);
    }

    private static async Task YieldForSplashAsync() =>
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);

    private static PageViewModels CreatePageViewModels(
        ILocalizationService localization,
        IAppSettingsService settings,
        IAppearanceService appearance)
    {
        HomeViewModel? home = null;
        FlashViewModel? flash = null;
        LogsViewModel? logs = null;
        SettingsViewModel? settingsPage = null;
        try
        {
            home = new HomeViewModel(localization, Resolve<IExternalLauncher>());
            flash = new FlashViewModel(localization, UsbDeviceMonitorFactory.Create(), UsbEnumeratorFactory.Create());
            logs = new LogsViewModel(localization, Resolve<ILogService>(), Resolve<IExternalLauncher>());
            settingsPage = new SettingsViewModel(settings, localization, appearance, Resolve<IUpdateCoordinator>());
            return new PageViewModels(home, flash, logs, settingsPage);
        }
        catch
        {
            home?.Dispose();
            flash?.Dispose();
            logs?.Dispose();
            settingsPage?.Dispose();
            throw;
        }
    }

    private static NavigationRegistry BuildNavigation(ILocalizationService localization, PageViewModels pages)
    {
        try
        {
            return new NavigationRegistryBuilder(localization)
                .Add<HomeView>("Nav.Home", PackIconCodiconsKind.Home, pages.Home)
                .Add<FlashView>("Nav.Flash", PackIconCodiconsKind.Rocket, pages.Flash)
                .Add<LogsView>("Logs.Title", PackIconCodiconsKind.Output, pages.Logs)
                .Add<SettingsView>(
                    "Nav.Settings", PackIconCodiconsKind.SettingsGear, pages.Settings, NavigationPlacement.Footer)
                .Build();
        }
        catch
        {
            pages.Dispose();
            throw;
        }
    }

    private static Task PreloadContentAsync(NavigationRegistry navigation) =>
        navigation.Page<LogsViewModel>().RefreshAsync();

    private static void StartDeviceMonitoring(FlashViewModel flash)
    {
        try { flash.StartMonitoring(); }
        catch (Exception exception)
        {
            Resolve<ILogService>().Write(WorkLogLevel.Warning, "USB monitoring could not start.", exception);
        }
    }

    private static MainWindow CreateMainWindow(MainViewModel main, out CancellationTokenSource lifetime)
    {
        var window = new MainWindow { DataContext = main };
        lifetime = new CancellationTokenSource();
        var windowLifetime = lifetime;
        window.Closed += (_, _) =>
        {
            windowLifetime.Cancel();
            main.Dispose();
            windowLifetime.Dispose();
        };
        return window;
    }

    private static T Resolve<T>() where T : class
    {
        return Locator.Current.GetService<T>() ??
               throw new InvalidOperationException($"Service {typeof(T).Name} is not registered.");
    }

    private sealed record PageViewModels(
        HomeViewModel Home,
        FlashViewModel Flash,
        LogsViewModel Logs,
        SettingsViewModel Settings) : IDisposable
    {
        public void Dispose()
        {
            Home.Dispose();
            Flash.Dispose();
            Logs.Dispose();
            Settings.Dispose();
        }
    }
}
