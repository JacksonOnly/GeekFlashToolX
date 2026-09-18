using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using GeekFlashToolX.ViewModels;

namespace GeekFlashToolX.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private async void OnCopySettingsPath(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(viewModel.SettingsPath);
    }
}