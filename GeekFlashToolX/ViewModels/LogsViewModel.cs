using System.Collections.ObjectModel;
using Avalonia.Controls;
using GeekFlashToolX.Core.Models;
using GeekFlashToolX.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GeekFlashToolX.Services;

namespace GeekFlashToolX.ViewModels;

public sealed record LogFilterOption(WorkLogStatus? Value)
{
    public string NameKey => Value is { } status ? $"LogStatus.{status}" : "Logs.AllStatus";
}
public sealed record LogPeriodOption(int Days)
{
    public string NameKey => $"Logs.Period{Days}";
}
public sealed record LogSourceOption(LogRecordKind Kind)
{
    public string NameKey => Kind == LogRecordKind.Work ? "Logs.WorkHistory" : "Logs.ApplicationLogs";
}
public sealed record LogRow(WorkLogInfo Info, string StatusText)
{
    public string Title => Info.Title;
    public string Device => Info.Device ?? "—";
    public string Operation => Info.Operation;
    public string Started => Info.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string Ended => Info.EndedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "—";
    public string Size => $"{Info.SizeBytes / 1024d:N1} KB";
}

public sealed partial class LogsViewModel : ViewModelBase
{
    private readonly ILogArchive _logs;
    private readonly ILocalizationService _localization;
    private readonly IExternalLauncher _launcher;
    private readonly IUiInteractionService? _ui;
    private readonly ObservableCollection<LogRow> _rows = [];
    [ObservableProperty] private string _search = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _error = "";
    [ObservableProperty] private bool _isBusy;
    private bool _disposed;
    [ObservableProperty] private LogFilterOption? _selectedStatus;
    [ObservableProperty] private LogPeriodOption? _selectedPeriod;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWorkHistory))]
    private LogSourceOption? _selectedSource;
    [ObservableProperty] private DateTime? _startDate;
    [ObservableProperty] private DateTime? _endDate;
    private FlatTreeDataGridSource<LogRow> _source;

    public LogsViewModel(ILocalizationService localization, ILogArchive logs, IExternalLauncher launcher,
        IUiInteractionService? ui = null) : base(localization)
    {
        _logs = logs;
        _localization = localization;
        _launcher = launcher;
        _ui = ui;
        SourceOptions.Add(new(LogRecordKind.Work));
        SourceOptions.Add(new(LogRecordKind.Application));
        _selectedSource = SourceOptions[0];
        RebuildOptions();
        _source = CreateSource();
    }

    public int Count => _rows.Count;
    public string LogDirectory => _logs.LogDirectory;
    public bool IsEmpty => _rows.Count == 0;
    public bool HasError => !string.IsNullOrEmpty(Error);
    public bool IsWorkHistory => SelectedSource?.Kind == LogRecordKind.Work;
    public FlatTreeDataGridSource<LogRow> Source { get => _source; private set => SetProperty(ref _source, value); }
    public ObservableCollection<LogFilterOption> StatusOptions { get; } = [];
    public ObservableCollection<LogPeriodOption> PeriodOptions { get; } = [];
    public ObservableCollection<LogSourceOption> SourceOptions { get; } = [];
    partial void OnSearchChanged(string value) => _ = RefreshAsync();
    partial void OnSelectedStatusChanged(LogFilterOption? value) => _ = RefreshAsync();
    partial void OnSelectedPeriodChanged(LogPeriodOption? value)
    {
        if (value is { Days: > 0 }) { StartDate = null; EndDate = null; }
        _ = RefreshAsync();
    }
    partial void OnSelectedSourceChanged(LogSourceOption? value) => _ = RefreshAsync();
    partial void OnStartDateChanged(DateTime? value)
    {
        if (value is not null) SelectedPeriod = PeriodOptions[0];
        _ = RefreshAsync();
    }
    partial void OnEndDateChanged(DateTime? value)
    {
        if (value is not null) SelectedPeriod = PeriodOptions[0];
        _ = RefreshAsync();
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
    
    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsBusy || _disposed) return;
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
            var rows = logs.Select(log => new LogRow(log,
                String(log.Kind == LogRecordKind.Application ? "Logs.ApplicationLogs" : $"LogStatus.{log.Status}"))).ToArray();
            if (!_rows.SequenceEqual(rows))
            {
                _rows.Clear();
                foreach (var row in rows) _rows.Add(row);
                var index = selected is null ? -1 : _rows.ToList().FindIndex(row => row.Info.Id == selected);
                if (_rows.Count > 0) Source.RowSelection!.SelectedIndex = Math.Max(0, index);
            }
            Error = _logs.LastError ?? "";
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(Count));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Error = String("Logs.ReadError") + " " + ex.Message;
        }
        finally { IsBusy = false; }
    }

    private LogQuery CurrentQuery()
    {
        DateTimeOffset? from = StartDate is { } start ? new DateTimeOffset(start.Date) : null;
        DateTimeOffset? to = EndDate is { } end ? new DateTimeOffset(end.Date.AddDays(1)) : null;
        if (from is null && to is null && SelectedPeriod is { Days: > 0 } period)
            from = DateTimeOffset.Now.Date.AddDays(1 - period.Days);
        return new(Search, IsWorkHistory ? SelectedStatus?.Value : null,
            from, to, SelectedSource?.Kind);
    }

    [RelayCommand]
    private void ClearFilters()
    {
        Search = string.Empty;
        SelectedStatus = StatusOptions[0];
        SelectedPeriod = PeriodOptions[0];
        StartDate = null;
        EndDate = null;
    }

    [RelayCommand]
    private Task PreviewSelectedAsync() => PreviewRowAsync(Source.RowSelection?.SelectedItem);

    [RelayCommand]
    private async Task PreviewRowAsync(LogRow? row)
    {
        if (row is null || _ui is null || _disposed) return;
        using var preview = new LogPreviewViewModel(_localization, _logs, _launcher, row.Info, _ui);
        await _ui.ShowLogPreviewAsync(preview);
    }

    [RelayCommand]
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
        StatusOptions.Add(new(null));
        foreach (var value in Enum.GetValues<WorkLogStatus>()) StatusOptions.Add(new(value));
        PeriodOptions.Clear();
        foreach (var value in new[] { 0, 1, 7, 30 }) PeriodOptions.Add(new(value));
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
