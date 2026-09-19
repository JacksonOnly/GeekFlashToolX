using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GeekFlashToolX.ViewModels;

namespace GeekFlashToolX.Views;

public partial class LogsView : UserControl
{
    private bool _previewOpen;

    public LogsView()
    {
        InitializeComponent();
        WorkLogGrid.AddHandler(DoubleTappedEvent, OnLogDoubleTapped, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private async void OnLogDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Resolve the clicked row rather than the selection, including after sorting.
        if (_previewOpen || DataContext is not LogsViewModel model ||
            e.Source is not Control control || !WorkLogGrid.TryGetRowModel<LogRow>(control, out var row) ||
            row is null || TopLevel.GetTopLevel(this) is not Window owner) return;

        e.Handled = true;
        _previewOpen = true;
        try
        {
            using var preview = model.CreatePreview(row);
            var dialog = new LogPreviewWindow { DataContext = preview };
            await dialog.ShowDialog(owner);
        }
        finally { _previewOpen = false; }
    }
}
