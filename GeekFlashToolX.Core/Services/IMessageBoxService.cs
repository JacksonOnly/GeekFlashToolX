namespace GeekFlashToolX.Core.Services;

public sealed record MessageBoxRequest(
    string Title,
    string Message,
    string PrimaryButtonText = "OK",
    string? SecondaryButtonText = null,
    string? CheckBoxText = null);

public sealed record MessageBoxResult(bool PrimaryButtonClicked, bool IsChecked = false);

/// <summary>Displays a modal, owner-bound message box. Call on the UI thread.</summary>
public interface IMessageBoxService
{
    Task<MessageBoxResult> ShowAsync(
        MessageBoxRequest request,
        CancellationToken cancellationToken = default);
}
