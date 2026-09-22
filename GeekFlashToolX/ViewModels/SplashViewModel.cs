using CommunityToolkit.Mvvm.ComponentModel;

namespace GeekFlashToolX.ViewModels;

public sealed partial class SplashViewModel : ObservableObject
{
    private double _progress;
    [ObservableProperty] private string _status = "正在启动…";

    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, Math.Max(_progress, Math.Clamp(value, 0, 100)));
    }

    public void SetStage(string status, double progress)
    {
        Status = status;
        Progress = progress;
    }
}
