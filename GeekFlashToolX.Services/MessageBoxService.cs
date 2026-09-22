using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using GeekFlashToolX.Core.Services;

namespace GeekFlashToolX.Services;

public sealed class MessageBoxService(Func<Window?> ownerProvider) : IMessageBoxService
{
    public async Task<MessageBoxResult> ShowAsync(
        MessageBoxRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Dispatcher.UIThread.VerifyAccess();

        var owner = ownerProvider();
        if (owner is null || !owner.IsVisible) return new(false);

        var primary = CreateButton(request.PrimaryButtonText, "MessageBoxPrimaryButton");
        primary.Classes.Add("primary");
        var secondary = request.SecondaryButtonText is null
            ? null
            : CreateButton(request.SecondaryButtonText, "MessageBoxSecondaryButton");
        var checkBox = request.CheckBoxText is null
            ? null
            : new CheckBox { Content = request.CheckBoxText, Name = "MessageBoxCheckBox" };
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        if (secondary is not null) actions.Children.Add(secondary);
        actions.Children.Add(primary);

        var content = new Grid
        {
            Margin = new Thickness(24),
            RowDefinitions = new RowDefinitions("*,Auto,Auto"),
            RowSpacing = 16,
        };
        content.Children.Add(new ScrollViewer
        {
            MaxHeight = 420,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = new TextBlock { Text = request.Message, TextWrapping = TextWrapping.Wrap },
        });
        if (checkBox is not null)
        {
            Grid.SetRow(checkBox, 1);
            content.Children.Add(checkBox);
        }
        Grid.SetRow(actions, 2);
        content.Children.Add(actions);

        var dialog = new Window
        {
            Title = request.Title,
            Width = 560,
            MinWidth = 360,
            MaxHeight = 620,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = content,
        };
        var primaryClicked = false;
        primary.Command = new RelayCommand(() =>
        {
            primaryClicked = true;
            dialog.Close();
        });
        if (secondary is not null) secondary.Command = new RelayCommand(dialog.Close);
        using var registration = cancellationToken.Register(() => Dispatcher.UIThread.Post(dialog.Close));
        await dialog.ShowDialog(owner);
        cancellationToken.ThrowIfCancellationRequested();
        return new(primaryClicked, checkBox?.IsChecked == true);
    }

    private static Button CreateButton(string content, string name) => new()
    {
        Content = content,
        Name = name,
        MinWidth = 96,
        HorizontalContentAlignment = HorizontalAlignment.Center,
    };
}
