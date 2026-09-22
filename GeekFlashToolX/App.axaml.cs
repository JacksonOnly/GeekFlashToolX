using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using GeekFlashToolX.Core.Services;
using GeekFlashToolX.Services;
using GeekFlashToolX.ViewModels;
using GeekFlashToolX.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Reflection;
using IconPacks.Avalonia.Codicons;
using GeekFlashToolX.Views.Page;
using GeekFlashCore.UsbWatcher;

using GeekFlashToolX.Diagnostics;
using Serilog;

namespace GeekFlashToolX;

public partial class App : Application
{
    internal static IServiceProvider? Services { get; private set; }
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        CrashReporter.Current?.AttachDispatcher();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = new AppSettingsService();
            try
            {
                settings.LoadAsync().GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                Log.Warning(exception, "Initial appearance settings could not be loaded.");
            }

            var appearance = new AppearanceService();
            appearance.Apply(settings.Current);
            var splashViewModel = new SplashViewModel();
            var splash = new SplashWindow { DataContext = splashViewModel };
            desktop.MainWindow = splash;
            splash.Show();
            _ = CompleteStartupAsync(desktop, splash, splashViewModel, settings, appearance);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void RegisterServices(
        IClassicDesktopStyleApplicationLifetime desktop,
        AppSettingsService settings,
        AppearanceService appearance)
    {
        var services = new ServiceCollection();
        var localization = new LocalizationService();
        var logDirectory = LogPaths.Directory;
        var logs = new SerilogLogArchive(logDirectory, Log.Logger);
        var messageBoxes = new MessageBoxService(() => desktop.MainWindow);
        var ui = new UiInteractionService(() => desktop.MainWindow);
        var notifier = new Notifier(() => desktop.MainWindow);
        var launcher = new ExternalLauncher();
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("GeekFlashToolX/1.0");
        var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? "1.0.0";
        var updates = new UpdateService(http, settings, localization, logs, version);
        var updateCoordinator = new UpdateCoordinator(updates, settings, messageBoxes, launcher, localization, notifier);
        services.AddSingleton<INotifier>(notifier);
        services.AddSingleton<IMessageBoxService>(messageBoxes);
        services.AddSingleton<IUiInteractionService>(ui);
        services.AddSingleton<ILogArchive>(logs);
        services.AddSingleton<IExternalLauncher>(launcher);
        services.AddSingleton<IUpdateService>(updates);
        services.AddSingleton<IUpdateCoordinator>(updateCoordinator);
        desktop.Exit += (_, _) =>
        {
            logs.Dispose();
            http.Dispose();
            (Services as IDisposable)?.Dispose();
            Services = null;
        };

        services.AddSingleton<IAppSettingsService>(settings);
        services.AddSingleton<ILocalizationService>(localization);
        services.AddSingleton<IAppearanceService>(appearance);
        services.AddSingleton<IStartupService>(new StartupService(settings, localization, appearance));
        Services = services.BuildServiceProvider();
    }

    private static async Task CompleteStartupAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        SplashWindow splash,
        SplashViewModel splashViewModel,
        AppSettingsService settings,
        AppearanceService appearance)
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
            RegisterServices(desktop, settings, appearance);
            splashViewModel.SetStage("正在注册页面…", 6);
            RegisterViews();

            splashViewModel.SetStage("正在加载设置与语言…", 10);
            await InitializeConfigurationAsync(splashViewModel, startupLifetime.Token);
            startupLifetime.Token.ThrowIfCancellationRequested();

            var localization = Resolve<ILocalizationService>();
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
            main = new MainViewModel(localization, settings, navigation, Resolve<IUiInteractionService>());
            var window = CreateMainWindow(main, out var lifetime);
            desktop.MainWindow = window;
            window.Show();
            mainWindowShown = true;
            main = null;
            navigation = null;
            splashViewModel.SetStage(localization.String("Splash.Ready"), 100);
            await YieldForSplashAsync();
            splash.Close();
            Log.Information("Application startup completed.");
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
                Log.Error(exception, "Post-startup action failed.");
                return;
            }
            main?.Dispose();
            if (main is null) navigation?.Dispose();
            desktop.MainWindow = splash;
            Console.Error.WriteLine(exception);
            Log.Error(exception, "Application startup failed.");
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
            flash = new FlashViewModel(localization, UsbDeviceMonitorFactory.Create(), UsbEnumeratorFactory.Create(),
                ui: Resolve<IUiInteractionService>());
            logs = new LogsViewModel(localization, Resolve<ILogArchive>(), Resolve<IExternalLauncher>(),
                Resolve<IUiInteractionService>());
            settingsPage = new SettingsViewModel(settings, localization, appearance, Resolve<IUpdateCoordinator>(),
                Resolve<IUiInteractionService>());
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
            Log.Warning(exception, "USB monitoring could not start.");
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
        return Services?.GetRequiredService<T>() ??
               throw new InvalidOperationException("Application services are not initialized.");
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
