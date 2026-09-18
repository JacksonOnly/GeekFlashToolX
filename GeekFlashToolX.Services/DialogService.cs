using Avalonia.Controls;
using Avalonia.Layout;
using GeekFlashToolX.Core.Services;

namespace GeekFlashToolX.Services;

public sealed class DialogService(Func<Window?> ownerProvider) : IDialogService
{
    public async Task ShowMessageAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var closeButton = new Button
        {
            Content = "OK",
            MinWidth = 88,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(24),
                Spacing = 20,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    closeButton,
                },
            },
        };
        closeButton.Click += (_, _) => dialog.Close();

        var owner = ownerProvider();
        if (owner is not null)
        {
            await dialog.ShowDialog(owner);
        }
        else
        {
            dialog.Show();
        }
    }
}
