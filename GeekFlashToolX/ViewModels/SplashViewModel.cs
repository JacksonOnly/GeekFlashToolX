using ReactiveUI;

namespace GeekFlashToolX.ViewModels;

public sealed class SplashViewModel : ReactiveObject
{
    private double _progress;
    private string _status = "正在启动…";

    public double Progress
    {
        get => _progress;
        set => this.RaiseAndSetIfChanged(ref _progress, Math.Max(_progress, Math.Clamp(value, 0, 100)));
    }

    public string Status
    {
        get => _status;
        set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    public void SetStage(string status, double progress)
    {
        Status = status;
        Progress = progress;
    }
}
