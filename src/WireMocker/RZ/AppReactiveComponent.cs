using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;

// ReSharper disable once CheckNamespace
namespace RZ.Foundation.ReactiveUi;

public abstract class AppReactiveComponent<T> : ReactiveComponentBase<T> where T : class, INotifyPropertyChanged
{
    protected void TrackChanges(CompositeDisposable disposables, params IObservable<Unit>[] observables) =>
        Observable.Merge(observables).Subscribe(_ => StateHasChanged()).DisposeWith(disposables);
}