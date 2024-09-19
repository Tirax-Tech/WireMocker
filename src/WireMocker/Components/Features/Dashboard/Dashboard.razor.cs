using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using RZ.Foundation.Blazor.MVVM;
using WireMock.Logging;
using WireMock.Server;

namespace Tirax.Application.WireMocker.Components.Features.Dashboard;

public sealed class DashboardViewModel : ActivatableViewModel
{
    readonly IWireMockServer mockServer;
    const int MaxLogEntries = 1000;

    readonly CompositeDisposable disposable = new();

    public DashboardViewModel(IWireMockServer mockServer) {
        this.mockServer = mockServer;
        InitLogEntries(mockServer.LogEntries.ToSeq());

        ClearLogEntries = ReactiveCommand.Create(() => {
            this.RaisePropertyChanging(nameof(LogEntries));
            LogEntries.Clear();
            this.RaisePropertyChanged(nameof(LogEntries));
        }).DisposeWith(disposable);
    }

    public LinkedList<ILogEntry> LogEntries { get; } = new();

    public ReactiveCommand<RUnit, RUnit> ClearLogEntries { get; }

    void AddLogEntry(ILogEntry entry)
    {
        this.RaisePropertyChanging(nameof(LogEntries));
        LogEntries.AddFirst(entry);
        while (LogEntries.Count > MaxLogEntries)
            LogEntries.RemoveLast();
        this.RaisePropertyChanged(nameof(LogEntries));
    }

    void InitLogEntries(in Seq<ILogEntry> entries)
    {
        this.RaisePropertyChanging(nameof(LogEntries));
        entries.Take(MaxLogEntries)
               .OrderByDescending(i => i.RequestMessage.DateTime)
               .Iter(log => LogEntries.AddLast(log));
        this.RaisePropertyChanged(nameof(LogEntries));
    }

    public void Dispose() {
        disposable.Dispose();
    }

    protected override void OnActivated(CompositeDisposable disposables) {
        mockServer.HttpEvents
                  .Where(ev => ev is HttpEvents.Response)
                  .Cast<HttpEvents.Response>()
                  .Select(response => response.Log)
                  .Subscribe(AddLogEntry)
                  .DisposeWith(disposable);
    }
}