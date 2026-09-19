using GeekFlashToolX.Core.Services;
using IconPacks.Avalonia.Codicons;

namespace GeekFlashToolX.ViewModels;

/// <summary>Owns the window-scoped page instances and their navigation metadata.</summary>
public sealed class NavigationRegistry : IDisposable
{
    private readonly IReadOnlyDictionary<PageKey, NavigationItem> _byKey;
    private bool _disposed;

    internal NavigationRegistry(ILocalizationService localization, IReadOnlyList<NavigationRegistration> registrations, PageKey initialKey)
    {
        if (registrations.Count == 0) throw new ArgumentException("At least one page must be registered.", nameof(registrations));
        var duplicate = registrations.GroupBy(item => item.Key).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null) throw new ArgumentException($"Duplicate page key: {duplicate.Key}.", nameof(registrations));

        Items = registrations.Select(item => new NavigationItem(
            localization, item.Key, item.TitleKey, item.Icon, item.Page, item.Placement)).ToArray();
        _byKey = Items.ToDictionary(item => item.Key);
        Initial = Get(initialKey);
        Initial.IsSelected = true;
        PrimaryItems = Items.Where(item => item.Placement == NavigationPlacement.Primary).ToArray();
        FooterItems = Items.Where(item => item.Placement == NavigationPlacement.Footer).ToArray();
    }

    public IReadOnlyList<NavigationItem> Items { get; }
    public IReadOnlyList<NavigationItem> PrimaryItems { get; }
    public IReadOnlyList<NavigationItem> FooterItems { get; }
    public NavigationItem Initial { get; }

    public bool TryGet(PageKey key, out NavigationItem item) => _byKey.TryGetValue(key, out item!);
    public NavigationItem Get(PageKey key) => _byKey.TryGetValue(key, out var item)
        ? item
        : throw new KeyNotFoundException($"Page key is not registered: {key}.");

    public TPage Page<TPage>() where TPage : ViewModelBase =>
        Items.Select(item => item.Page).OfType<TPage>().Single();

    public void Select(NavigationItem selected)
    {
        foreach (var item in Items) item.IsSelected = ReferenceEquals(item, selected);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var item in Items) item.Dispose();
        foreach (var page in Items.Select(item => item.Page).Distinct()) page.Dispose();
    }
}

public sealed class NavigationRegistryBuilder(ILocalizationService localization)
{
    private readonly List<NavigationRegistration> _registrations = [];
    private PageKey _initialKey = PageKey.Home;

    public NavigationRegistryBuilder Add(
        PageKey key, string titleKey, PackIconCodiconsKind icon, ViewModelBase page,
        NavigationPlacement placement = NavigationPlacement.Primary)
    {
        _registrations.Add(new(key, titleKey, icon, page, placement));
        return this;
    }

    public NavigationRegistryBuilder StartAt(PageKey key)
    {
        _initialKey = key;
        return this;
    }

    public NavigationRegistry Build() => new(localization, _registrations.AsReadOnly(), _initialKey);
}
