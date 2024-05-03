using System.Collections.Specialized;
using System.Reactive.Linq;
using WireMock.Logging;
using WireMock.Server;

namespace Tirax.Application.WireMocker.Services;

public class MockServer(WireMockServer server) : IMockServer
{
    public Seq<ILogEntry> AllLogEntries => server.LogEntries.ToSeq();

    public IObservable<Seq<ILogEntry>> LogEntries { get; } =
        Observable
           .FromEventPattern<NotifyCollectionChangedEventHandler,
                NotifyCollectionChangedEventArgs>(add => server.LogEntriesChanged += add,
                                                  remove => server.LogEntriesChanged -= remove)
           .Select(args => args.EventArgs.NewItems!.Cast<ILogEntry>().ToArray().ToSeq());
}

public interface IMockServer
{
    Seq<ILogEntry> AllLogEntries { get; }

    IObservable<Seq<ILogEntry>> LogEntries { get; }
}
