using Avalonia.Controls;

namespace GeekFlashToolX.Views.Page;

public partial class FlashOperationPage : UserControl
{
    public FlashOperationPage() => InitializeComponent();

    private void OnDeviceDropDownOpened(object? sender, EventArgs args)
    {
        if (DataContext is ViewModels.FlashViewModel flash)
            _ = flash.RefreshDevicesAsync();
    }
}
