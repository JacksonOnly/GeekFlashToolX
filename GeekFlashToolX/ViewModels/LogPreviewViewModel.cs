using Avalonia.Threading;
using Avalonia.Media;
using GeekFlashToolX.Core.Models;
using GeekFlashToolX.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GeekFlashToolX.Services;

namespace GeekFlashToolX.ViewModels;

public sealed partial class LogPreviewViewModel : ViewModelBase
{
    private readonly ILogArchive _logs;
    private readonly IExternalLauncher _launcher;
    private readonly IUiInteractionService? _ui;
    private readonly DispatcherTimer _timer;
    private readonly CancellationTokenSource _lifetime = new();
    private WorkLogInfo _log;
    [ObservableProperty] private string _content;
    private bool _reading;
    private bool _disposed;

    public LogPreviewViewModel(ILocalizationService localization, ILogArchive logs, IExternalLauncher launcher,
        WorkLogInfo log, IUiInteractionService? ui = null) : base(localization)
    {
        _logs = logs;
        _launcher = launcher;
        _ui = ui;
        _log = log;
        _content = String("Logs.Loading");
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += OnTick;
    }

    public WorkLogInfo Log
    {
        get => _log;
        private set
        {
            if (!SetProperty(ref _log, value)) return;
            OnPropertyChanged(nameof(StatusKey));
            OnPropertyChanged(nameof(StatusBrush));
            OnPropertyChanged(nameof(IsRunning));
        }
    }
    public string Title => Log.Title;
    public string Operation => Log.Operation;
    public string Device => Log.Device ?? "—";
    public string StatusKey => Log.Kind == LogRecordKind.Application ? "Logs.ApplicationLogs" : $"LogStatus.{Log.Status}";
    public bool IsRunning => Log.Status == WorkLogStatus.Running;
    public IBrush StatusBrush => Log.Kind == LogRecordKind.Application ? Brushes.Gray : Log.Status switch
    {
        WorkLogStatus.Succeeded => Brushes.SeaGreen,
        WorkLogStatus.Failed => Brushes.IndianRed,
        WorkLogStatus.Running => Brushes.DodgerBlue,
        _ => Brushes.Gray,
    };
    public string FilePath => Log.FilePath;
    public Task OpenFileAsync() => _launcher.OpenFileAsync(FilePath);
    public void SetError(string message) => Content = String("Logs.ReadError") + " " + message;

    [RelayCommand]
    private Task CopyContentAsync() => _ui?.CopyTextAsync(Content) ?? Task.CompletedTask;

    [RelayCommand]
    private Task CopyPathAsync() => _ui?.CopyTextAsync(FilePath) ?? Task.CompletedTask;

    [RelayCommand]
    private async Task OpenFileFromPreviewAsync()
    {
        try { await OpenFileAsync(); }
        catch (Exception exception) { SetError(exception.Message); }
    }

    [RelayCommand]
    private void ClosePreview() => _ui?.ClosePreview();

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
            var latest = await _logs.GetInfoAsync(Log, _lifetime.Token);
            var text = await _logs.ReadTailAsync(FilePath, _lifetime.Token);
            if (!_disposed) { Log = latest; Content = text; }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
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
