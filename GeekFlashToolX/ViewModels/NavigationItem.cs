using Avalonia.Controls;
using GeekFlashToolX.Core.Services;
using IconPacks.Avalonia.Codicons;
using ReactiveUI;

namespace GeekFlashToolX.ViewModels;

public enum NavigationPlacement { Primary, Footer }

internal sealed record NavigationRegistration(
    string TitleKey,
    PackIconCodiconsKind Icon,
    ViewModelBase? ViewModel,
    IReadOnlyList<NavigationRegistration> Children,
    NavigationPlacement Placement,
    bool IsExpanded);

/// <summary>A page or expandable group displayed in the navigation sidebar.</summary>
public sealed class NavigationItem : ViewModelBase
{
    private bool _isSelected;
    private bool _isActive;
    private bool _isExpanded;

    internal NavigationItem(
        ILocalizationService localization,
        NavigationRegistration registration,
        IReadOnlyList<NavigationItem> children) : base(localization)
    {
        TitleKey = registration.TitleKey;
        Icon = registration.Icon;
        ViewModel = registration.ViewModel;
        Placement = registration.Placement;
        Children = children;
        _isExpanded = registration.IsExpanded;

        if (ViewModel is not null) View = ViewLocator.Instance.GetView(ViewModel);
    }

    public string TitleKey { get; }
    public PackIconCodiconsKind Icon { get; }
    public ViewModelBase? ViewModel { get; }
    public Control? View { get; }
    public NavigationPlacement Placement { get; }
    public IReadOnlyList<NavigationItem> Children { get; }
    public bool HasChildren => Children.Count > 0;
    public bool CanNavigate => View is not null;

    public bool IsSelected
    {
        get => _isSelected;
        internal set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    public bool IsActive
    {
        get => _isActive;
        internal set => this.RaiseAndSetIfChanged(ref _isActive, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }
}
