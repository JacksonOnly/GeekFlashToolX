using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace GeekFlashToolX.Views;

public partial class LogPreviewWindow : Window
{
    public LogPreviewWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            Close();
        }, RoutingStrategies.Tunnel);
    }
}
