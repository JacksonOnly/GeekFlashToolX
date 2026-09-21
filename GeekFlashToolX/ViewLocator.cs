using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.VisualTree;

namespace GeekFlashToolX;

public sealed class ViewLocator : IDataTemplate
{
    private static readonly ConcurrentDictionary<Type, ViewRegistration> Factories = new();
    private static readonly ConditionalWeakTable<object, ViewCache> Views = new();

    public static ViewLocator Instance { get; } = new();

    public void RegisterView<TViewModel, TView>() where TViewModel : class where TView : Control, new() =>
        RegisterView(typeof(TViewModel), typeof(TView), static () => new TView());

    public void EnsureView<TViewModel, TView>() where TViewModel : class where TView : Control, new() =>
        EnsureView<TView>(typeof(TViewModel));

    public void EnsureView<TView>(Type viewModelType) where TView : Control, new()
    {
        ArgumentNullException.ThrowIfNull(viewModelType);
        var registration = Factories.GetOrAdd(viewModelType,
            static _ => new ViewRegistration(typeof(TView), static () => new TView()));
        if (registration.ViewType != typeof(TView))
            throw new InvalidOperationException($"{viewModelType.FullName} is already mapped to {registration.ViewType.FullName}.");
    }

    public void RegisterView<TViewModel>(Func<Control> factory) where TViewModel : class
    {
        RegisterView(typeof(TViewModel), typeof(Control), factory);
    }

    public void RegisterView(Type viewModelType, Func<Control> factory)
    {
        RegisterView(viewModelType, typeof(Control), factory);
    }

    private static void RegisterView(Type viewModelType, Type viewType, Func<Control> factory)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);
        ArgumentNullException.ThrowIfNull(factory);
        if (!Factories.TryAdd(viewModelType, new ViewRegistration(viewType, factory)))
            throw new InvalidOperationException($"A view is already registered for {viewModelType.FullName}.");
    }

    public bool Match(object? data) => data is not null && Factories.ContainsKey(data.GetType());

    public Control? Build(object? data)
    {
        if (data is null) return null;
        var cache = GetCache(data);
        if (cache.Primary.GetVisualParent() is null) return cache.Primary;
        // A transition may still display the persistent view in its old presenter.
        cache.Overlaps.RemoveAll(static weakView => !weakView.TryGetTarget(out _));
        var overlap = CreateView(data);
        cache.Overlaps.Add(new WeakReference<Control>(overlap));
        return overlap;
    }

    /// <summary>The View stays alive while its ViewModel is alive. Both can be collected together.</summary>
    public Control GetView(object viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        return GetCache(viewModel).Primary;
    }

    private static ViewCache GetCache(object viewModel)
    {
        if (!Factories.TryGetValue(viewModel.GetType(), out var registration))
            throw new InvalidOperationException($"No view is registered for {viewModel.GetType().FullName}.");
        return Views.GetValue(viewModel, model =>
            new ViewCache(CreateView(model, registration)));
    }

    private static Control CreateView(object viewModel)
    {
        if (!Factories.TryGetValue(viewModel.GetType(), out var registration))
            throw new InvalidOperationException($"No view is registered for {viewModel.GetType().FullName}.");
        return CreateView(viewModel, registration);
    }

    private static Control CreateView(object viewModel, ViewRegistration registration)
    {
        var view = registration.Factory();
        view.DataContext = viewModel;
        return view;
    }

    /// <summary>Release a page removed from navigation or a dynamic tab.</summary>
    public void Release(object viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        if (Views.TryGetValue(viewModel, out var cache))
        {
            cache.Primary.DataContext = null;
            foreach (var weakView in cache.Overlaps)
                if (weakView.TryGetTarget(out var overlap)) overlap.DataContext = null;
        }
        Views.Remove(viewModel);
    }

    private sealed record ViewRegistration(Type ViewType, Func<Control> Factory);

    private sealed class ViewCache(Control primary)
    {
        public Control Primary { get; } = primary;
        public List<WeakReference<Control>> Overlaps { get; } = [];
    }
}
