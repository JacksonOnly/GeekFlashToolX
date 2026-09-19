using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Metadata;
using Avalonia.Threading;
using GeekFlashToolX.Core.Services;
using Splat;

[assembly: XmlnsDefinition("https://github.com/avaloniaui", "GeekFlashToolX.Localization")]

namespace GeekFlashToolX.Localization;

/// <summary>Creates a live, one-way localization binding: <c>{I18 Home.Title}</c>.</summary>
public sealed class I18Extension(string key)
{
    public BindingBase ProvideValue()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var localization = Locator.Current.GetService<ILocalizationService>();
        if (localization is null && !Design.IsDesignMode)
            throw new InvalidOperationException("Register ILocalizationService before loading localized views.");

        // Subscribe per binding, so Avalonia releases the handler when the binding is disposed.
        // No reflection or DataContext dependency: this also works inside item templates.
        return new TranslationObservable(localization, key).ToBinding();
    }

    private sealed class TranslationObservable(ILocalizationService? localization, string key) : IObservable<string>
    {
        public IDisposable Subscribe(IObserver<string> observer) => new Subscription(localization, key, observer);
    }

    private sealed class Subscription : IDisposable
    {
        private readonly ILocalizationService? _localization;
        private readonly string _key;
        private IObserver<string>? _observer;

        public Subscription(ILocalizationService? localization, string key, IObserver<string> observer)
        {
            _localization = localization;
            _key = key;
            _observer = observer;
            if (_localization is not null) _localization.CultureChanged += OnCultureChanged;
            OnCultureChanged(null, EventArgs.Empty);
        }

        private void OnCultureChanged(object? sender, EventArgs args)
        {
            if (Dispatcher.UIThread.CheckAccess()) Publish();
            else Dispatcher.UIThread.Post(Publish);
        }

        private void Publish() => Volatile.Read(ref _observer)?.OnNext(_localization?.String(_key) ?? _key);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _observer, null) is null) return;
            if (_localization is not null) _localization.CultureChanged -= OnCultureChanged;
        }
    }
}
