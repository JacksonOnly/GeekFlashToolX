using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GeekFlashToolX.ViewModels;

namespace GeekFlashToolX.Views;

public partial class LogsView : UserControl
{
    public LogsView()
    {
        InitializeComponent();
        WorkLogGrid.AddHandler(DoubleTappedEvent, OnLogDoubleTapped, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void OnLogDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Resolve the clicked row rather than the selection, including after sorting.
        if (e.Source is not Control control || !WorkLogGrid.TryGetRowModel<LogRow>(control, out var row) || row is null)
            return;

        if (DataContext is not LogsViewModel model) return;
        e.Handled = true;
        model.PreviewRowCommand.Execute(row);
    }
}
