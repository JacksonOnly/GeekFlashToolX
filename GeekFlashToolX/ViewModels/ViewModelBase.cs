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

    protected string Text(string key)
    {
        return _localization?[key] ?? key;
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