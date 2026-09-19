using GeekFlashToolX.Core.Services;
using IconPacks.Avalonia.Codicons;
using ReactiveUI;

namespace GeekFlashToolX.ViewModels;

public enum PageKey { Home, Logs, Settings }

public enum NavigationPlacement { Primary, Footer }

public sealed record NavigationRegistration(
    PageKey Key,
    string TitleKey,
    PackIconCodiconsKind Icon,
    ViewModelBase Page,
    NavigationPlacement Placement = NavigationPlacement.Primary);

/// <summary>A sidebar entry and its single, window-lifetime page instance.</summary>
public sealed class NavigationItem(
    ILocalizationService localization,
    PageKey key,
    string titleKey,
    PackIconCodiconsKind icon,
    ViewModelBase page,
    NavigationPlacement placement) : ViewModelBase(localization)
{
    private bool _isSelected;

    public PageKey Key { get; } = key;
    public string TitleKey { get; } = titleKey;
    public string Title => String(TitleKey);
    public PackIconCodiconsKind Icon { get; } = icon;
    public ViewModelBase Page { get; } = page;
    public NavigationPlacement Placement { get; } = placement;
    public bool IsSelected
    {
        get => _isSelected;
        internal set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }
}
