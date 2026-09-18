using Avalonia.Controls;
using Avalonia.Media;

namespace GeekFlashToolX.Views;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            LogoImage.Opacity = 1;
            LogoImage.RenderTransform = new ScaleTransform(1, 1);
        };
    }
}