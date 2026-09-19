using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using GeekFlashToolX.ViewModels;

namespace GeekFlashToolX.Views;

public partial class LogPreviewWindow : Window
{
    public LogPreviewWindow()
    {
        InitializeComponent();
        Opened += (_, _) => (DataContext as LogPreviewViewModel)?.Start();
        Closed += (_, _) => (DataContext as LogPreviewViewModel)?.Dispose();
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            Close();
        }, RoutingStrategies.Tunnel);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private async void OnCopyPathClick(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null && DataContext is LogPreviewViewModel model)
            await clipboard.SetTextAsync(model.FilePath);
    }

    private async void OnOpenFileClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not LogPreviewViewModel model) return;
        try { await model.OpenFileAsync(); }
        catch (Exception exception) { model.SetError(exception.Message); }
    }
}
