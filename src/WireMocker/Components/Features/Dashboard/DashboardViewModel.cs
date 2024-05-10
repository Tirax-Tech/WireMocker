using System.Reactive.Disposables;
using ReactiveUI;
using Tirax.Application.WireMocker.Services;
using WireMock.Logging;

namespace Tirax.Application.WireMocker.Components.Features.Dashboard;

public sealed class DashboardViewModel : ViewModel, IDisposable
{
    const int MaxLogEntries = 1000;

    readonly CompositeDisposable disposable = new();

    public DashboardViewModel(IMockServer mockServer) {
        InitLogEntries(mockServer.AllLogEntries);

        ClearLogEntries = ReactiveCommand.Create<Unit, Unit>(_ => {
            this.RaisePropertyChanging(nameof(LogEntries));
            LogEntries.Clear();
            this.RaisePropertyChanged(nameof(LogEntries));
            return unit;
        }).DisposeWith(disposable);

        mockServer.LogEntries.Subscribe(AddLogEntry).DisposeWith(disposable);
    }

    public LinkedList<ILogEntry> LogEntries { get; } = new();

    public ReactiveCommand<Unit, Unit> ClearLogEntries { get; }

    void AddLogEntry(Seq<ILogEntry> entry)
    {
        this.RaisePropertyChanging(nameof(LogEntries));
        entry.Iter(log => LogEntries.AddFirst(log));
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
}