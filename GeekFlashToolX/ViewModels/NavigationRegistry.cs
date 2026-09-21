using Avalonia.Controls;
using GeekFlashToolX.Core.Services;
using IconPacks.Avalonia.Codicons;

namespace GeekFlashToolX.ViewModels;

/// <summary>Owns the window-scoped page instances and hierarchical navigation metadata.</summary>
public sealed class NavigationRegistry : IDisposable
{
    private readonly IReadOnlyList<NavigationItem> _allItems;
    private bool _disposed;

    internal NavigationRegistry(ILocalizationService localization, IReadOnlyList<NavigationRegistration> registrations)
    {
        if (registrations.Count == 0)
            throw new ArgumentException("At least one page must be registered.", nameof(registrations));

        Items = registrations.Select(item => CreateItem(localization, item)).ToArray();
        _allItems = Flatten(Items).ToArray();
        Initial = _allItems.FirstOrDefault(item => item.CanNavigate)
            ?? throw new ArgumentException("At least one navigable page must be registered.", nameof(registrations));
        PrimaryItems = Items.Where(item => item.Placement == NavigationPlacement.Primary).ToArray();
        FooterItems = Items.Where(item => item.Placement == NavigationPlacement.Footer).ToArray();
        Select(Initial);
    }

    public IReadOnlyList<NavigationItem> Items { get; }
    public IReadOnlyList<NavigationItem> AllItems => _allItems;
    public IReadOnlyList<NavigationItem> PrimaryItems { get; }
    public IReadOnlyList<NavigationItem> FooterItems { get; }
    public NavigationItem Initial { get; }

    public TPage Page<TPage>() where TPage : ViewModelBase =>
        _allItems.Select(item => item.ViewModel).OfType<TPage>().Single();

    public void Select(NavigationItem selected)
    {
        if (!_allItems.Contains(selected) || !selected.CanNavigate) return;
        foreach (var item in Items) UpdateSelection(item, selected);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var page in _allItems.Select(item => item.ViewModel).OfType<ViewModelBase>().Distinct())
            ViewLocator.Instance.Release(page);
        foreach (var item in _allItems) item.Dispose();
        foreach (var page in _allItems.Select(item => item.ViewModel).OfType<ViewModelBase>().Distinct())
            page.Dispose();
    }

    private static NavigationItem CreateItem(ILocalizationService localization, NavigationRegistration registration)
    {
        var children = registration.Children.Select(child => CreateItem(localization, child)).ToArray();
        return new NavigationItem(localization, registration, children);
    }

    private static IEnumerable<NavigationItem> Flatten(IEnumerable<NavigationItem> items)
    {
        foreach (var item in items)
        {
            yield return item;
            foreach (var child in Flatten(item.Children)) yield return child;
        }
    }

    private static bool UpdateSelection(NavigationItem item, NavigationItem selected)
    {
        item.IsSelected = ReferenceEquals(item, selected);
        var hasSelectedChild = item.Children.Any(child => UpdateSelection(child, selected));
        item.IsActive = item.IsSelected || hasSelectedChild;
        if (hasSelectedChild) item.IsExpanded = true;
        return item.IsActive;
    }
}

public sealed class NavigationRegistryBuilder(ILocalizationService localization)
{
    private readonly List<NavigationRegistration> _registrations = [];

    public NavigationRegistryBuilder Add<TView>(
        string titleKey,
        PackIconCodiconsKind icon,
        ViewModelBase viewModel,
        NavigationPlacement placement = NavigationPlacement.Primary)
        where TView : Control, new()
    {
        _registrations.Add(Page<TView>(titleKey, icon, viewModel, placement));
        return this;
    }

    public NavigationRegistryBuilder AddGroup(
        string titleKey,
        PackIconCodiconsKind icon,
        Action<NavigationGroupBuilder> configure,
        bool isExpanded = false,
        NavigationPlacement placement = NavigationPlacement.Primary)
    {
        var group = new NavigationGroupBuilder();
        configure(group);
        _registrations.Add(new(titleKey, icon, null, group.Build(), placement, isExpanded));
        return this;
    }

    public NavigationRegistry Build() => new(localization, _registrations.AsReadOnly());

    internal static NavigationRegistration Page<TView>(
        string titleKey,
        PackIconCodiconsKind icon,
        ViewModelBase viewModel,
        NavigationPlacement placement)
        where TView : Control, new() =>
        RegisterPage<TView>(titleKey, icon, viewModel, placement);

    private static NavigationRegistration RegisterPage<TView>(
        string titleKey, PackIconCodiconsKind icon, ViewModelBase viewModel, NavigationPlacement placement)
        where TView : Control, new()
    {
        var type = viewModel.GetType();
        ViewLocator.Instance.EnsureView<TView>(type);
        return new(titleKey, icon, viewModel, [], placement, false);
    }
}

public sealed class NavigationGroupBuilder
{
    private readonly List<NavigationRegistration> _registrations = [];

    public NavigationGroupBuilder Add<TView>(string titleKey, PackIconCodiconsKind icon, ViewModelBase viewModel)
        where TView : Control, new()
    {
        _registrations.Add(NavigationRegistryBuilder.Page<TView>(
            titleKey, icon, viewModel, NavigationPlacement.Primary));
        return this;
    }

    public NavigationGroupBuilder AddGroup(
        string titleKey,
        PackIconCodiconsKind icon,
        Action<NavigationGroupBuilder> configure,
        bool isExpanded = false)
    {
        var group = new NavigationGroupBuilder();
        configure(group);
        _registrations.Add(new(
            titleKey, icon, null, group.Build(), NavigationPlacement.Primary, isExpanded));
        return this;
    }

    internal IReadOnlyList<NavigationRegistration> Build()
    {
        if (_registrations.Count == 0)
            throw new InvalidOperationException("A navigation group must contain at least one item.");
        return _registrations.AsReadOnly();
    }
}
