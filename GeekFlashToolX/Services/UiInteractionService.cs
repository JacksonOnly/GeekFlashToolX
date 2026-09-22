using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using GeekFlashToolX.ViewModels;
using GeekFlashToolX.Views;

namespace GeekFlashToolX.Services;

public sealed class UiInteractionService(Func<Window?> mainWindowProvider) : IUiInteractionService
{
    private Window? _previewWindow;

    public async Task<string?> SelectProtocolAsync()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (mainWindowProvider() is not { IsVisible: true } owner) return null;
        var dialog = new ProtocolSelectionWindow();
        dialog.DataContext = new ProtocolSelectionViewModel(result => dialog.Close(result));
        return await dialog.ShowDialog<string?>(owner);
    }

    public async Task<string?> PickFirehoseLoaderAsync()
    {
        Dispatcher.UIThread.VerifyAccess();
        var storage = mainWindowProvider()?.StorageProvider;
        if (storage is null) return null;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 Firehose Loader",
            AllowMultiple = false,
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task ShowLogPreviewAsync(LogPreviewViewModel preview)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_previewWindow is not null || mainWindowProvider() is not { IsVisible: true } owner) return;
        var dialog = new LogPreviewWindow { DataContext = preview };
        _previewWindow = dialog;
        dialog.Opened += (_, _) => preview.Start();
        try { await dialog.ShowDialog(owner); }
        finally
        {
            _previewWindow = null;
        }
    }

    public async Task CopyTextAsync(string text)
    {
        Dispatcher.UIThread.VerifyAccess();
        var clipboard = (_previewWindow ?? mainWindowProvider())?.Clipboard;
        if (clipboard is not null) await clipboard.SetTextAsync(text);
    }

    public void ClosePreview() => _previewWindow?.Close();

    public void MinimizeMainWindow()
    {
        if (mainWindowProvider() is { } window) window.WindowState = WindowState.Minimized;
    }

    public void ToggleMainWindowMaximize()
    {
        if (mainWindowProvider() is not { } window) return;
        window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    public void CloseMainWindow() => mainWindowProvider()?.Close();
}
