using System.ComponentModel;
using RZ.Foundation.Types;
// ReSharper disable MemberCanBeProtected.Global

namespace Tirax.Application.WireMocker.Components;

public abstract class AppReactiveComponent<T> : ReactiveComponentBase<T> where T : class, INotifyPropertyChanged
{
    public IDisposable Bind<TValue>(IObservable<TValue> stream, MutableRef<TValue> target) =>
        stream.Subscribe(v => {
            target.Value = v;
            StateHasChanged();
        });
}