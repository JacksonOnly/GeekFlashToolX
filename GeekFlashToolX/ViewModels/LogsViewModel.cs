using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Controls;
using GeekFlashToolX.Core.Models;
using GeekFlashToolX.Core.Services;
using ReactiveUI;

namespace GeekFlashToolX.ViewModels;

public sealed record LogFilterOption(WorkLogStatus? Value, string Name);
public sealed record LogPeriodOption(int Days, string Name);
public sealed record LogRow(WorkLogInfo Info, string StatusText)
{
    public string Title => Info.Title;
    public string Device => Info.Device ?? "—";
    public string Operation => Info.Operation;
    public string Started => Info.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string Ended => Info.EndedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "—";
    public string Size => $"{Info.SizeBytes / 1024d:N1} KB";
}

public sealed class LogsViewModel : ViewModelBase
{
    private readonly ILogService _logs;
    private readonly ILocalizationService _localization;
    private readonly IExternalLauncher _launcher;
    private readonly ObservableCollection<LogRow> _rows = [];
    private string _search = "";
    private string _error = "";
    private bool _busy;
    private bool _disposed;
    private LogFilterOption? _selectedStatus;
    private LogPeriodOption? _selectedPeriod;
    private FlatTreeDataGridSource<LogRow> _source;

    public LogsViewModel(ILocalizationService localization, ILogService logs, IExternalLauncher launcher) : base(localization)
    {
        _logs = logs;
        _localization = localization;
        _launcher = launcher;
        RebuildOptions();
        _source = CreateSource();
        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
        OpenFolderCommand = ReactiveCommand.CreateFromTask(OpenFolderAsync);
    }

    public string CountLabel => FormatString("Logs.Count", _rows.Count);
    public string LogDirectory => _logs.LogDirectory;
    public bool IsEmpty => _rows.Count == 0;
    public bool HasError => !string.IsNullOrEmpty(Error);
    public string Error { get => _error; private set { this.RaiseAndSetIfChanged(ref _error, value); this.RaisePropertyChanged(nameof(HasError)); } }
    public bool IsBusy { get => _busy; private set => this.RaiseAndSetIfChanged(ref _busy, value); }
    public FlatTreeDataGridSource<LogRow> Source { get => _source; private set => this.RaiseAndSetIfChanged(ref _source, value); }
    public ObservableCollection<LogFilterOption> StatusOptions { get; } = [];
    public ObservableCollection<LogPeriodOption> PeriodOptions { get; } = [];
    public ICommand RefreshCommand { get; }
    public ICommand OpenFolderCommand { get; }

    public string Search
    {
        get => _search;
        set { this.RaiseAndSetIfChanged(ref _search, value); _ = RefreshAsync(); }
    }
    public LogFilterOption? SelectedStatus
    {
        get => _selectedStatus;
        set { this.RaiseAndSetIfChanged(ref _selectedStatus, value); _ = RefreshAsync(); }
    }
    public LogPeriodOption? SelectedPeriod
    {
        get => _selectedPeriod;
        set { this.RaiseAndSetIfChanged(ref _selectedPeriod, value); _ = RefreshAsync(); }
    }

    private FlatTreeDataGridSource<LogRow> CreateSource()
    {
        var source = new FlatTreeDataGridSource<LogRow>(_rows)
            .WithTextColumn(String("Logs.TitleColumn"), x => x.Title, c => c.Width = new GridLength(220))
            .WithTextColumn(String("Logs.Device"), x => x.Device, c => c.Width = new GridLength(150))
            .WithTextColumn(String("Logs.Operation"), x => x.Operation, c => c.Width = new GridLength(130))
            .WithTextColumn(String("Logs.Status"), x => x.StatusText, c => c.Width = new GridLength(100))
            .WithTextColumn(String("Logs.Started"), x => x.Started, c => c.Width = new GridLength(175))
            .WithTextColumn(String("Logs.Ended"), x => x.Ended, c => c.Width = new GridLength(175))
            .WithTextColumn(String("Logs.Size"), x => x.Size, c => c.Width = new GridLength(90));
        source.RowSelection!.SingleSelect = true;
        return source;
    }
    
    public async Task RefreshAsync()
    {
        if (_busy || _disposed) return;
        IsBusy = true;
        try
        {
            LogQuery query;
            IReadOnlyList<WorkLogInfo> logs;
            // If filters change while scanning, show only the newest filter result.
            do
            {
                query = CurrentQuery();
                logs = await _logs.GetLogsAsync(query);
            } while (!_disposed && query != CurrentQuery());
            if (_disposed) return;
            var selected = Source.RowSelection?.SelectedItem?.Info.Id;
            var rows = logs.Select(log => new LogRow(log, String($"LogStatus.{log.Status}"))).ToArray();
            if (!_rows.SequenceEqual(rows))
            {
                _rows.Clear();
                foreach (var row in rows) _rows.Add(row);
                var index = selected is null ? -1 : _rows.ToList().FindIndex(row => row.Info.Id == selected);
                if (_rows.Count > 0) Source.RowSelection!.SelectedIndex = Math.Max(0, index);
            }
            Error = _logs.LastError ?? "";
            this.RaisePropertyChanged(nameof(IsEmpty));
            this.RaisePropertyChanged(nameof(CountLabel));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Error = String("Logs.ReadError") + " " + ex.Message;
        }
        finally { IsBusy = false; }
    }

    private LogQuery CurrentQuery() => new(Search, SelectedStatus?.Value,
        SelectedPeriod is { Days: > 0 } period ? DateTimeOffset.Now.Date.AddDays(1 - period.Days) : null);

    public LogPreviewViewModel CreatePreview(LogRow row) => new(_localization, _logs, _launcher, row.Info);

    private async Task OpenFolderAsync()
    {
        try { await _launcher.OpenFolderAsync(LogDirectory); }
        catch (Exception ex) { Error = ex.Message; }
    }

    private void RebuildOptions()
    {
        var status = _selectedStatus?.Value;
        var days = _selectedPeriod?.Days ?? 0;
        StatusOptions.Clear();
        StatusOptions.Add(new(null, String("Logs.AllStatus")));
        foreach (var value in Enum.GetValues<WorkLogStatus>()) StatusOptions.Add(new(value, String($"LogStatus.{value}")));
        PeriodOptions.Clear();
        foreach (var value in new[] { 0, 1, 7, 30 }) PeriodOptions.Add(new(value, String($"Logs.Period{value}")));
        _selectedStatus = StatusOptions.First(option => option.Value == status);
        _selectedPeriod = PeriodOptions.First(option => option.Days == days);
    }

    protected override void OnLanguageChanged()
    {
        RebuildOptions();
        var previous = Source;
        Source = CreateSource();
        previous.Dispose();
        base.OnLanguageChanged();
        _ = RefreshAsync();
    }

    public override void Dispose()
    {
        _disposed = true;
        Source.Dispose();
        base.Dispose();
    }
}
