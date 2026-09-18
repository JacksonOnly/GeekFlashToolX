using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using GeekFlashToolX.Core.Models;
using GeekFlashToolX.Core.Services;

namespace GeekFlashToolX.Services;

public sealed class AppearanceService : IAppearanceService
{
    public void Apply(AppSettings settings)
    {
        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        application.RequestedThemeVariant = settings.Theme switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };

        if (Color.TryParse(settings.AccentColor, out var accent))
        {
            var buttonAccent = EnsureWhiteTextContrast(accent);
            application.Resources["AccentBrush"] = new SolidColorBrush(accent);
            application.Resources["AccentMutedBrush"] = new SolidColorBrush(accent, 0.14);
            application.Resources["AccentButtonBrush"] = new SolidColorBrush(buttonAccent);
            application.Resources["TextOnAccentBrush"] = new SolidColorBrush(Colors.White);
            application.Resources["AccentHoverBrush"] = new SolidColorBrush(Blend(buttonAccent, Colors.Black, 0.10));
            application.Resources["AccentPressedBrush"] = new SolidColorBrush(Blend(buttonAccent, Colors.Black, 0.20));
            SetAccentText(application, ThemeVariant.Light, Blend(accent, Colors.Black, 0.36));
            SetAccentText(application, ThemeVariant.Dark, Blend(accent, Colors.White, 0.48));
            application.Resources["SystemAccentColor"] = accent;
            application.Resources["SystemAccentColorLight1"] = Blend(accent, Colors.White, 0.16);
            application.Resources["SystemAccentColorLight2"] = Blend(accent, Colors.White, 0.30);
            application.Resources["SystemAccentColorLight3"] = Blend(accent, Colors.White, 0.46);
            application.Resources["SystemAccentColorDark1"] = Blend(accent, Colors.Black, 0.14);
            application.Resources["SystemAccentColorDark2"] = Blend(accent, Colors.Black, 0.28);
            application.Resources["SystemAccentColorDark3"] = Blend(accent, Colors.Black, 0.42);
        }
    }

    private static Color EnsureWhiteTextContrast(Color accent)
    {
        // Keep the chosen hue while giving white labels at least 4.5:1 contrast.
        var opaque = Color.FromRgb(accent.R, accent.G, accent.B);
        for (var step = 0; step <= 100; step++)
        {
            var candidate = Blend(opaque, Colors.Black, step / 100d);
            if (1.05 / (RelativeLuminance(candidate) + 0.05) >= 4.5) return candidate;
        }
        return Colors.Black;
    }

    private static void SetAccentText(Application application, ThemeVariant theme, Color color)
    {
        if (!application.Resources.ThemeDictionaries.TryGetValue(theme, out var resources))
        {
            resources = new Avalonia.Controls.ResourceDictionary();
            application.Resources.ThemeDictionaries[theme] = resources;
        }
        ((Avalonia.Controls.ResourceDictionary)resources)["AccentTextBrush"] = new SolidColorBrush(color);
    }

    private static double RelativeLuminance(Color color)
    {
        static double Linear(byte channel)
        {
            var value = channel / 255d;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
    }

    private static Color Blend(Color source, Color target, double amount)
    {
        static byte Channel(byte source, byte target, double amount) =>
            (byte)Math.Round(source + ((target - source) * amount));

        return Color.FromArgb(
            source.A,
            Channel(source.R, target.R, amount),
            Channel(source.G, target.G, amount),
            Channel(source.B, target.B, amount));
    }
}
