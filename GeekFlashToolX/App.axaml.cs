using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System.Diagnostics;
using GeekFlashToolX.Core.Services;
using GeekFlashToolX.Services;
using GeekFlashToolX.ViewModels;
using GeekFlashToolX.Views;
using Splat;
using System.Net.Http;
using System.Reflection;
using GeekFlashToolX.Core.Models;
using IconPacks.Avalonia.Codicons;

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
            RegisterServices(desktop);
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
        var splashStarted = Stopwatch.StartNew();
        try
        {
            var progress = new Progress<double>(value => splashViewModel.Progress = value * 100);
            await Resolve<IStartupService>().InitializeAsync(progress);
            var remainingSplashTime = TimeSpan.FromSeconds(2) - splashStarted.Elapsed;
            if (remainingSplashTime > TimeSpan.Zero)
                await Task.Delay(remainingSplashTime);
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var localization = Resolve<ILocalizationService>();
                var settings = Resolve<IAppSettingsService>();
                var appearance = Resolve<IAppearanceService>();
                
                
                var home = new HomeViewModel(localization, Resolve<IExternalLauncher>());
                var settingsPage = new SettingsViewModel(settings, localization, appearance, Resolve<IUpdateCoordinator>());
                var logsPage = new LogsViewModel(localization, Resolve<ILogService>(), Resolve<IExternalLauncher>());
                await logsPage.RefreshAsync();
                var navigation = new NavigationRegistryBuilder(localization)
                    .Add(PageKey.Home, "Nav.Home", PackIconCodiconsKind.Home, home)
                    .Add(PageKey.Logs, "Logs.Title", PackIconCodiconsKind.Output, logsPage)
                    .Add(PageKey.Settings, "Nav.Settings", PackIconCodiconsKind.SettingsGear, settingsPage,
                        NavigationPlacement.Footer)
                    .StartAt(PageKey.Home)
                    .Build();
                
                
                var main = new MainViewModel(localization, settings, navigation);
                var window = new MainWindow { DataContext = main };
                var lifetime = new CancellationTokenSource();
                window.Closed += (_, _) => { lifetime.Cancel(); main.Dispose(); };
                desktop.MainWindow = window;
                window.Show();
                splash.Close();
                Resolve<ILogService>().Write(WorkLogLevel.Information, "Application startup completed.");
                _ = Resolve<IUpdateCoordinator>().CheckAndNotifyAsync(automatic: true, lifetime.Token);
            });
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            Resolve<ILogService>().Write(WorkLogLevel.Error, "Application startup failed.", exception);
            splashViewModel.Status = exception.Message;
        }
    }

    private static T Resolve<T>() where T : class
    {
        return Locator.Current.GetService<T>() ??
               throw new InvalidOperationException($"Service {typeof(T).Name} is not registered.");
    }
}
