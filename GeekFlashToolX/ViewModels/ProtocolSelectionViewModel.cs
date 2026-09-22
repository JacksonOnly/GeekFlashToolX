using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GeekFlashToolX.ViewModels;

public sealed partial class ProtocolSelectionViewModel(Action<string?> complete) : ObservableObject
{
    public IReadOnlyList<string> Protocols { get; } = ["Qualcomm"];

    [ObservableProperty] private string? _selectedProtocol = "Qualcomm";

    [RelayCommand]
    private void Confirm() => complete(SelectedProtocol);

    [RelayCommand]
    private void Cancel() => complete(null);
}
