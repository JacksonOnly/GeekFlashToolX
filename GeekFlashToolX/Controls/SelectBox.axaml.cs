using System.Collections;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace GeekFlashToolX.Controls;

/// <summary>Reusable searchable selection control for arbitrary item types.</summary>
public partial class SelectBox : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsProperty =
        AvaloniaProperty.Register<SelectBox, IEnumerable?>(nameof(Items));

    public static readonly StyledProperty<string> SearchTextProperty =
        AvaloniaProperty.Register<SelectBox, string>(nameof(SearchText), "", defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<SelectBox, object?>(nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<SelectBox, IDataTemplate?>(nameof(ItemTemplate));

    public static readonly StyledProperty<string> DisplayMemberPathProperty =
        AvaloniaProperty.Register<SelectBox, string>(nameof(DisplayMemberPath), "");

    public static readonly StyledProperty<string> PlaceholderTextProperty =
        AvaloniaProperty.Register<SelectBox, string>(nameof(PlaceholderText), "点击选择项目，或输入关键字筛选");

    private bool _suppressPopupReopen;

    public SelectBox()
    {
        InitializeComponent();
        DropDown.PlacementTarget = SearchTextBox;
        SearchTextBox.SizeChanged += (_, _) => MatchDropDownWidth();
        ItemList.ItemsSource = Items;
        SearchTextBox.Text = SearchText;
    }

    public IEnumerable? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    public string SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }
    public string DisplayMemberPath { get => GetValue(DisplayMemberPathProperty); set => SetValue(DisplayMemberPathProperty, value); }
    public string PlaceholderText { get => GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }
    public event EventHandler? DropDownOpened;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsProperty && ItemList is not null)
            ItemList.ItemsSource = Items;
        else if (change.Property == SearchTextProperty && SearchTextBox is not null && SearchTextBox.Text != SearchText)
            SearchTextBox.Text = SearchText;
    }

    private void OnSearchFocus(object? sender, RoutedEventArgs args)
    {
        if (_suppressPopupReopen) return;
        ResetSelectionForNewSearch();
        OpenDropDown();
    }

    private void OnSearchPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (_suppressPopupReopen) return;
        ResetSelectionForNewSearch();
        OpenDropDown();
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs args)
    {
        SetCurrentValue(SearchTextProperty, SearchTextBox.Text ?? "");
        if (!_suppressPopupReopen && SearchTextBox.IsFocused) OpenDropDown();
    }

    private void SelectItem(object? item)
    {
        if (item is null) return;

        _suppressPopupReopen = true;
        try
        {
            SetCurrentValue(SearchTextProperty, GetDisplayText(item));
            SetCurrentValue(SelectedItemProperty, item);
            DropDown.IsOpen = false;
            TopLevel.GetTopLevel(this)?.FocusManager?.Focus(null);
        }
        finally
        {
            Dispatcher.UIThread.Post(() => _suppressPopupReopen = false, DispatcherPriority.Input);
        }
    }

    private void OnItemClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is Button { DataContext: { } item }) SelectItem(item);
    }

    private void ResetSelectionForNewSearch()
    {
        if (SelectedItem is not { } selected ||
            !string.Equals(SearchTextBox.Text, GetDisplayText(selected), StringComparison.Ordinal)) return;
        SetCurrentValue(SelectedItemProperty, null);
        SetCurrentValue(SearchTextProperty, "");
    }

    private string GetDisplayText(object item)
    {
        if (string.IsNullOrWhiteSpace(DisplayMemberPath)) return item.ToString() ?? "";
        var property = item.GetType().GetProperty(DisplayMemberPath,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        return property?.GetValue(item)?.ToString() ?? item.ToString() ?? "";
    }

    private void OpenDropDown()
    {
        MatchDropDownWidth();
        if (DropDown.IsOpen) return;
        DropDown.IsOpen = true;
        DropDownOpened?.Invoke(this, EventArgs.Empty);
    }

    private void MatchDropDownWidth() => DropDownSurface.Width = SearchTextBox.Bounds.Width;
}
