using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GeekFlashToolX.ViewModels;

namespace GeekFlashToolX.Views;

public partial class MainWindow : Window
{
    private bool _isCompact;
    private bool _expandedBeforeCompact = true;

    public MainWindow()
    {
        InitializeComponent();
        SizeChanged += (_, args) => UpdateResponsiveLayout(args.NewSize.Width);
        DataContextChanged += (_, _) => UpdateResponsiveLayout(Bounds.Width > 0 ? Bounds.Width : Width);
    }

    private void UpdateResponsiveLayout(double width)
    {
        var compact = width < 1000;
        Classes.Set("compact", compact);
        NavigationShell.DisplayMode =
            compact ? SplitViewDisplayMode.CompactOverlay : SplitViewDisplayMode.CompactInline;
        if (DataContext is not MainViewModel model) return;
        model.IsCompact = compact;
        if (compact != _isCompact)
        {
            if (compact)
            {
                _expandedBeforeCompact = model.IsSidebarExpanded;
                model.IsSidebarExpanded = false;
            }
            else
            {
                model.IsSidebarExpanded = _expandedBeforeCompact;
            }

            _isCompact = compact;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty)
            Classes.Set("maximized", WindowState == WindowState.Maximized);
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.ClickCount == 2) ToggleMaximize();
        else BeginMoveDrag(e);
        e.Handled = true;
    }

    private void OnMinimize(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximize(object? sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void OnClose(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
}