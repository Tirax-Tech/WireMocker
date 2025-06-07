using System.Reactive.Disposables;
using System.Reactive.Linq;
using JetBrains.Annotations;
using ReactiveUI;
using WireMock;
using WireMock.Logging;
using WireMock.Server;

namespace Tirax.Application.WireMocker.Components.Features.Dashboard;

[UsedImplicitly]
partial class Dashboard
{
    public Dashboard() {
        this.WhenActivated(_ => { });
    }
}

public sealed class DashboardViewModel : ActivatableViewModel
{
    readonly IWireMockServer mockServer;
    const int MaxLogEntries = 1000;

    readonly Dictionary<Guid, HttpTransactionViewModel> httpEntryLookup = new();

    public DashboardViewModel(IWireMockServer mockServer) {
        this.mockServer = mockServer;
        InitLogEntries(mockServer.LogEntries.ToSeq());

        ClearLogEntries = ReactiveCommand.Create(() => {
            this.RaisePropertyChanging(nameof(HttpEntries));
            HttpEntries.Clear();
            httpEntryLookup.Clear();
            mockServer.ResetLogEntries();
            this.RaisePropertyChanged(nameof(HttpEntries));
        });
    }

    public LinkedList<HttpTransactionViewModel> HttpEntries { get; } = new();

    public ReactiveCommand<RUnit, RUnit> ClearLogEntries { get; }

    void AddLogEntry(HttpTransactionViewModel entry)
    {
        this.RaisePropertyChanging(nameof(HttpEntries));
        HttpEntries.AddFirst(entry);
        while (HttpEntries.Count > MaxLogEntries)
            HttpEntries.RemoveLast();
        this.RaisePropertyChanged(nameof(HttpEntries));
    }

    static RequestPanelViewModel ToRequestVm(IRequestMessage req)
        => new(req.Method,
               req.Path,
               req.DateTime,
               req.Query.Map(h => (h.Key, (IReadOnlyList<string>)h.Value)).ToArray(),
               req.Headers.Map(h => (h.Key, (IReadOnlyList<string>)h.Value)).ToArray(),
               req.BodyData);

    static ResponsePanelViewModel ToResponseVm(IResponseMessage res, bool isMatched)
        => new(res.StatusCode,
               res.Headers.Map(h => (h.Key, (IReadOnlyList<string>)h.Value)).ToArray(),
               res.BodyData,
               res.Timestamp,
               res.ElapsedTime,
                isMatched);

    static bool IsMatched(ILogEntry log)
        => log.RequestMatchResult is not null || log.PartialMatchResult is not null;

    static HttpTransactionViewModel ToHttpTransaction(ILogEntry log) {
        var request = ToRequestVm(log.RequestMessage);
        var response = ToResponseVm(log.ResponseMessage, IsMatched(log));
        return new HttpTransactionViewModel(log.Guid, request, response);
    }

    void InitLogEntries(in Seq<ILogEntry> entries)
    {
        this.RaisePropertyChanging(nameof(HttpEntries));
        entries.OrderByDescending(i => i.RequestMessage.DateTime)
               .Take(MaxLogEntries)
               .Iter(log => HttpEntries.AddLast(ToHttpTransaction(log)));
        this.RaisePropertyChanged(nameof(HttpEntries));
    }

    protected override void OnActivated(CompositeDisposable disposables) {
        mockServer.HttpEvents.Where(ev => !ev.IsAdmin).Subscribe(ev => {
            switch (ev){
                case HttpEvents.Request req: {
                    var entry = new HttpTransactionViewModel(req.Id, ToRequestVm(req.Message));
                    httpEntryLookup[req.Id] = entry;
                    AddLogEntry(entry);
                    break;
                }
                case HttpEvents.Response res:
                {
                    httpEntryLookup[res.Id].ResponseVm = ToResponseVm(res.Log.ResponseMessage, IsMatched(res.Log));
                    httpEntryLookup.Remove(res.Id);
                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(ev));
            }
        }).DisposeWith(disposables);
    }
}