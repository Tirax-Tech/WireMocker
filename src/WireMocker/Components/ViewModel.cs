using System.Reactive.Disposables;
using ReactiveUI;

namespace Tirax.Application.WireMocker.Components;

public abstract class ViewModel : ReactiveObject;

public abstract class ViewModelDisposable : ViewModel, IDisposable
{
    protected CompositeDisposable Disposables { get; } = new();

    public void Dispose() {
        Disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}