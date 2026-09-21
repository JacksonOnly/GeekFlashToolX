using Avalonia.Controls;
using Avalonia.Interactivity;

namespace GeekFlashToolX.Views;

public partial class ProtocolSelectionWindow : Window
{
    public ProtocolSelectionWindow() => InitializeComponent();

    private void OnCancel(object? sender, RoutedEventArgs args) => Close(null);

    private void OnConfirm(object? sender, RoutedEventArgs args) =>
        Close((ProtocolComboBox.SelectedItem as ComboBoxItem)?.Content as string);
}
