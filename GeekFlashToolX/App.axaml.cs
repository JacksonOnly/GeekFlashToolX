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
        var dialog = new DialogService(() => desktop.MainWindow);

        Locator.CurrentMutable.RegisterConstant<IAppSettingsService>(settings);
        Locator.CurrentMutable.RegisterConstant<ILocalizationService>(localization);
        Locator.CurrentMutable.RegisterConstant<IAppearanceService>(appearance);
        Locator.CurrentMutable.RegisterConstant<IDialogService>(dialog);
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
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var localization = Resolve<ILocalizationService>();
                var settings = Resolve<IAppSettingsService>();
                var appearance = Resolve<IAppearanceService>();
                var dialog = Resolve<IDialogService>();
                var home = new HomeViewModel(localization, dialog);
                var other = new OtherViewModel(localization);
                var settingsPage = new SettingsViewModel(settings, localization, appearance);
                var main = new MainViewModel(localization, settings, home, other, settingsPage);
                var window = new MainWindow { DataContext = main };
                desktop.MainWindow = window;
                window.Show();
                splash.Close();
            });
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            splashViewModel.Status = exception.Message;
        }
    }

    private static T Resolve<T>() where T : class
    {
        return Locator.Current.GetService<T>() ??
               throw new InvalidOperationException($"Service {typeof(T).Name} is not registered.");
    }
}