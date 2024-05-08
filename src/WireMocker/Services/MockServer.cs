using System.Collections.Specialized;
using System.Reactive.Linq;
using WireMock;
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

    public Seq<IMapping> Mappings => server.Mappings.Where(map => !map.IsAdminInterface).ToSeq();

    public OutcomeT<Synchronous, Unit> LoadMappings(string mappings) {
        server.ResetMappings();
        return TryCatch(() => server.WithMapping(mappings)).Map(_ => unit).As();
    }
}

public interface IMockServer
{
    Seq<ILogEntry> AllLogEntries { get; }

    IObservable<Seq<ILogEntry>> LogEntries { get; }

    Seq<IMapping> Mappings { get; }

    OutcomeT<Synchronous, Unit> LoadMappings(string mappings);
}
