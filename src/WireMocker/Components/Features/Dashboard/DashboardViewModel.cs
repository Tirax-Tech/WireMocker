using ReactiveUI;
using WireMock.Logging;

namespace Tirax.Application.WireMocker.Components.Features.Dashboard;

public sealed class DashboardViewModel : ViewModel
{
    public LinkedList<ILogEntry> LogEntries { get; } = new();

    const int MaxLogEntries = 1000;

    public Unit AddLogEntry(Seq<ILogEntry> entry)
    {
        this.RaisePropertyChanging(nameof(LogEntries));
        entry.Iter(log => LogEntries.AddFirst(log));
        while (LogEntries.Count > MaxLogEntries)
            LogEntries.RemoveLast();
        this.RaisePropertyChanged(nameof(LogEntries));
        return unit;
    }

    public Unit InitLogEntries(in Seq<ILogEntry> entries)
    {
        this.RaisePropertyChanging(nameof(LogEntries));
        entries.Take(MaxLogEntries)
               .OrderByDescending(i => i.RequestMessage.DateTime)
               .Iter(log => LogEntries.AddLast(log));
        this.RaisePropertyChanged(nameof(LogEntries));
        return unit;
    }
}