﻿using System.Reactive.Disposables;
using ReactiveUI;
using RZ.Foundation.Blazor.MVVM;
using WireMock;
using WireMock.Logging;
using WireMock.Server;

namespace Tirax.Application.WireMocker.Components.Features.Dashboard;

public sealed class DashboardViewModel : ActivatableViewModel
{
    readonly IWireMockServer mockServer;
    const int MaxLogEntries = 1000;

    public DashboardViewModel(IWireMockServer mockServer) {
        this.mockServer = mockServer;
        InitLogEntries(mockServer.LogEntries.ToSeq());

        ClearLogEntries = ReactiveCommand.Create(() => {
            this.RaisePropertyChanging(nameof(HttpEntries));
            HttpEntries.Clear();
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

    static HttpTransactionViewModel ToHttpTransaction(ILogEntry log) {
        var request = ToRequestVm(log.RequestMessage);
        var response = new ResponsePanelViewModel();
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
        mockServer.HttpEvents.Subscribe(ev => {
            switch (ev){
                case HttpEvents.Request req:
                    AddLogEntry(new(req.Id, ToRequestVm(req.Message)));
                    break;
            }
        }).DisposeWith(disposables);
    }
}