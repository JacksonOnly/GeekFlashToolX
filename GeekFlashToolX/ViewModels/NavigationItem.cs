using Avalonia.Controls;
using GeekFlashToolX.Core.Services;
using IconPacks.Avalonia.Codicons;
using CommunityToolkit.Mvvm.ComponentModel;

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
public sealed partial class NavigationItem : ViewModelBase
{
    private bool _isSelected;
    private bool _isActive;
    [ObservableProperty] private bool _isExpanded;

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

    public bool IsSelected { get => _isSelected; internal set => SetProperty(ref _isSelected, value); }
    public bool IsActive { get => _isActive; internal set => SetProperty(ref _isActive, value); }

}
