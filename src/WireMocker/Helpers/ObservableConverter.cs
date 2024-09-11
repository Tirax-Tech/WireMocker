using System.Reactive.Linq;

namespace Tirax.Application.WireMocker.Helpers;

public static class ObservableConverter
{
    public static IObservable<T> From<T>(Func<CancellationToken, OutcomeT<Asynchronous, T>> func) =>
        Observable.Create<T>(async (observer, token) => {
            var result = await func(token).RunIO();
            if (result.IfSuccess(out var v, out var e)){
                observer.OnNext(v);
                observer.OnCompleted();
            }
            else
                observer.OnError(e.ToErrorException());
        });

    public static IObservable<T> From<T>(Func<CancellationToken, OutcomeT<Asynchronous, IAsyncEnumerable<T>>> func) =>
        Observable.Create<T>(async (observer, token) => {
            var result = await func(token).RunIO();
            if (result.IfSuccess(out var enumerable, out var e))
                try{
                    await foreach (var v in enumerable.WithCancellation(token))
                        observer.OnNext(v);
                    observer.OnCompleted();
                }
                catch (Exception ex){
                    observer.OnError(ex);
                }
            else
                observer.OnError(e.ToErrorException());
        });

    public static IObservable<T> Shared<T>(this IObservable<T> coldObservable) =>
        coldObservable.Publish().RefCount();
}