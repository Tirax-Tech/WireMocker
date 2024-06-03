using System.Reactive;
using System.Reactive.Linq;
using LanguageExt.Common;

// ReSharper disable once CheckNamespace
namespace RZ.Foundation.Observable;

public class ErrorInfoException : ApplicationException
{
    public ErrorInfoException(int errorCode, string errorMessage) : base(errorMessage) =>
        ErrorCode = errorCode;

    public ErrorInfoException(int errorCode, string errorMessage, Exception innerException) : base(errorMessage, innerException) =>
        ErrorCode = errorCode;

    public int ErrorCode { get; }
}

public static class ObservableExtensions
{
    public static IObservable<Error> GetErrorStream<T>(this IObservable<T> stream) =>
        from r in stream.Materialize()
        where r.Kind == NotificationKind.OnError
        let e = r.Exception!
        select e is ErrorInfoException ei
                   ? Error.New(ei.ErrorCode, ei.Message)
                   : Error.New(StandardErrors.UnexpectedCode, e.ToString());
}