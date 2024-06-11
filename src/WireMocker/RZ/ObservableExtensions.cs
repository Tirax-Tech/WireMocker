using System.Reactive;
using System.Reactive.Linq;
using LanguageExt.Common;
using ReactiveUI;
using RZ.Foundation.Types;
using Unit = LanguageExt.Unit;

// ReSharper disable once CheckNamespace
namespace RZ.Foundation.Reactive;

public class ErrorInfoException : ApplicationException
{
    public ErrorInfoException(int code, string errorMessage) : base(errorMessage) =>
        Code = code;

    public ErrorInfoException(int code, string errorMessage, Exception innerException) : base(errorMessage, innerException) =>
        Code = code;

    public int Code { get; }
}

public static class ObservableExtensions
{
    public static IObservable<T> Bind<T>(this IObservable<T> stream, MutableRef<T> storage) =>
        stream.Do(v => storage.Value = v);

    public static IObservable<Error> GetErrorStream<T>(this IObservable<T> stream) =>
        from r in stream.Materialize()
        where r.Kind == NotificationKind.OnError
        let e = r.Exception!
        select e is ErrorInfoException ei
                   ? Error.New(ei.Code, ei.Message)
                   : Error.New(StandardErrors.UnexpectedCode, e.ToString());

    public static IObservable<Unit> Ignore<_>(this IObservable<_> stream) =>
        stream.Select(_ => unit);

    /// <summary>
    /// Get the execution handler for a reactive command
    /// </summary>
    public static Func<Task<T>> OnExecute<T>(this ReactiveCommand<Unit, T> command) => async () =>
        await command.Execute();

    public static Func<Task<TOut>> OnExecute<TIn,TOut>(this ReactiveCommand<TIn, TOut> command, Func<TIn> value) => async () =>
        await command.Execute(value());
}