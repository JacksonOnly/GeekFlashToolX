using Avalonia.Controls;

namespace GeekFlashToolX.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
        SizeChanged += (_, args) => Classes.Set("narrow", args.NewSize.Width < 920);
        AttachedToVisualTree += (_, _) => Classes.Set("narrow", Bounds.Width < 920);
    }
}
