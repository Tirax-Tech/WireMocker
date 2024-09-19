using System.Reactive;
using System.Reactive.Linq;
using LanguageExt.Common;
using ReactiveUI;
using RZ.Foundation.Types;
using Unit = LanguageExt.Unit;

// ReSharper disable once CheckNamespace
namespace RZ.Foundation.Reactive;

public static class ObservableExtensions
{
    public static IObservable<T> Bind<T>(this IObservable<T> stream, MutableRef<T> storage) =>
        stream.Do(v => storage.Value = v);
}