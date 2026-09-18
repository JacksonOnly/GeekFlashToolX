using System.Windows.Input;
using GeekFlashToolX.Core.Services;
using ReactiveUI;

namespace GeekFlashToolX.ViewModels;

public sealed class HomeViewModel : ViewModelBase
{
    private readonly IDialogService _dialogService;

    public HomeViewModel(ILocalizationService localization, IDialogService dialogService) : base(localization)
    {
        _dialogService = dialogService;
        OpenDialogCommand = ReactiveCommand.CreateFromTask(OpenDialogAsync);
    }

    public string Eyebrow => Text("Home.Eyebrow");
    public string Title => Text("Home.Title");
    public string Subtitle => Text("Home.Subtitle");
    public string Action => Text("Home.Action");
    public string DeviceTitle => Text("Home.DeviceTitle");
    public string DeviceValue => Text("Home.DeviceValue");
    public string EngineTitle => Text("Home.EngineTitle");
    public string EngineValue => Text("Home.EngineValue");
    public string SafetyTitle => Text("Home.SafetyTitle");
    public string SafetyValue => Text("Home.SafetyValue");
    public string WorkspaceTitle => Text("Home.WorkspaceTitle");
    public string WorkspaceBody => Text("Home.WorkspaceBody");

    public ICommand OpenDialogCommand { get; }

    private Task OpenDialogAsync()
    {
        return _dialogService.ShowMessageAsync(Text("Dialog.Title"), Text("Dialog.Message"));
    }
}