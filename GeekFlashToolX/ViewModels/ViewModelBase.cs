using Avalonia.Threading;
using GeekFlashToolX.Core.Services;
using ReactiveUI;

namespace GeekFlashToolX.ViewModels;

public abstract class ViewModelBase : ReactiveObject, IDisposable
{
    private readonly ILocalizationService? _localization;

    protected ViewModelBase(ILocalizationService? localization = null)
    {
        _localization = localization;
        if (_localization is not null) _localization.CultureChanged += OnCultureChanged;
    }

    protected string String(string key)
    {
        return _localization?.String(key) ?? key;
    }

    protected string FormatString(string key, params object?[] arguments)
    {
        return _localization?.FormatString(key, arguments) ?? key;
    }

    protected virtual void OnLanguageChanged()
    {
        this.RaisePropertyChanged(string.Empty);
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        // Localization loads asynchronously; bound collections must change on the UI thread.
        if (Dispatcher.UIThread.CheckAccess()) OnLanguageChanged();
        else Dispatcher.UIThread.Post(OnLanguageChanged);
    }

    public virtual void Dispose()
    {
        if (_localization is not null) _localization.CultureChanged -= OnCultureChanged;
    }
}