using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using GeekFlashToolX.ViewModels;

namespace GeekFlashToolX.Views;

public partial class FlashView : UserControl
{
    public FlashView()
    {
        InitializeComponent();
    }

    private async void OnCloseTab(object? sender, RoutedEventArgs args)
    {
        args.Handled = true;
        if (DataContext is FlashViewModel flash && sender is Button { DataContext: FlashTabItemViewModel tab })
            await flash.CloseTabAsync(tab);
    }
}
