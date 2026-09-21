using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using GeekFlashToolX.ViewModels;

namespace GeekFlashToolX.Views.Page;

public partial class QcomDeviceConnectionView : UserControl
{
    public QcomDeviceConnectionView() => InitializeComponent();

    private async void OnBrowseLoader(object? sender, RoutedEventArgs args)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 Firehose Loader",
            AllowMultiple = false
        });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path && DataContext is QcomDeviceConnectionViewModel model)
            model.LoaderPath = path;
    }
}
