using Avalonia.Controls;
using GeekFlashToolX.ViewModels;
using GeekFlashToolX.Views;
using Avalonia.Interactivity;

namespace GeekFlashToolX.Views.Page;

public partial class FlashOperationPage : UserControl
{
    public FlashOperationPage() => InitializeComponent();

    private async void OnDeviceDropDownOpened(object? sender, EventArgs args)
    {
        if (DataContext is FlashViewModel model) await model.RefreshDevicesAsync();
    }

    private async void OnConnectDevice(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not FlashViewModel model) return;
        if (!model.CanConnectSelectedDevice) return;
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var protocol = await new ProtocolSelectionWindow().ShowDialog<string?>(owner);
        if (protocol is not null) model.OpenSelectedDevice(protocol);
    }
}
