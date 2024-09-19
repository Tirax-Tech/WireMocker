using System.Collections.Specialized;
using System.Reactive.Linq;
using WireMock;
using WireMock.Logging;
using WireMock.Server;

namespace Tirax.Application.WireMocker.Services;

public class MockServer(WireMockServer server) : IMockServer
{
    public ILogEntry[] AllLogEntries => server.LogEntries.ToArray();

    public IObservable<ILogEntry[]> LogEntries { get; } =
        Observable
           .FromEventPattern<NotifyCollectionChangedEventHandler,
                NotifyCollectionChangedEventArgs>(add => server.LogEntriesChanged += add,
                                                  remove => server.LogEntriesChanged -= remove)
           .Select(args => args.EventArgs.NewItems!.Cast<ILogEntry>().ToArray());

    public IMapping[] Mappings => server.Mappings.Where(map => !map.IsAdminInterface).ToArray();

    public void LoadMappings(string mappings) {
        server.ResetMappings();
        server.WithMapping(mappings);
    }
}

public interface IMockServer
{
    ILogEntry[] AllLogEntries { get; }

    IObservable<ILogEntry[]> LogEntries { get; }

    IMapping[] Mappings { get; }

    void LoadMappings(string mappings);
}
