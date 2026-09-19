using Avalonia.Threading;
using GeekFlashToolX.Core.Models;
using GeekFlashToolX.Core.Services;
using ReactiveUI;

namespace GeekFlashToolX.ViewModels;

public sealed class LogPreviewViewModel : ViewModelBase
{
    private readonly ILogService _logs;
    private readonly IExternalLauncher _launcher;
    private readonly DispatcherTimer _timer;
    private readonly CancellationTokenSource _lifetime = new();
    private string _content;
    private bool _reading;
    private bool _disposed;

    public LogPreviewViewModel(ILocalizationService localization, ILogService logs, IExternalLauncher launcher, WorkLogInfo log) : base(localization)
    {
        _logs = logs;
        _launcher = launcher;
        Log = log;
        _content = String("Logs.Loading");
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += OnTick;
    }

    public WorkLogInfo Log { get; }
    public string Title => Log.Title;
    public string FilePath => Log.FilePath;
    public string Content { get => _content; private set => this.RaiseAndSetIfChanged(ref _content, value); }
    public Task OpenFileAsync() => _launcher.OpenFileAsync(FilePath);
    public void SetError(string message) => Content = String("Logs.ReadError") + " " + message;

    public void Start()
    {
        if (_disposed) return;
        _timer.Start();
        _ = RefreshAsync();
    }

    private async void OnTick(object? sender, EventArgs args) => await RefreshAsync();

    public async Task RefreshAsync()
    {
        if (_disposed || _reading) return;
        _reading = true;
        try
        {
            var text = await _logs.ReadTailAsync(FilePath, _lifetime.Token);
            if (!_disposed) Content = text;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!_disposed) Content = String("Logs.ReadError") + " " + ex.Message;
        }
        finally { _reading = false; }
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _lifetime.Cancel();
        _lifetime.Dispose();
        base.Dispose();
    }
}
