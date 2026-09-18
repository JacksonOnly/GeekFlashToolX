using ReactiveUI;

namespace GeekFlashToolX.ViewModels;

public sealed class SplashViewModel : ReactiveObject
{
    private double _progress;
    private string _status = "Initializing workspace";

    public double Progress
    {
        get => _progress;
        set => this.RaiseAndSetIfChanged(ref _progress, value);
    }

    public string Status
    {
        get => _status;
        set => this.RaiseAndSetIfChanged(ref _status, value);
    }
}