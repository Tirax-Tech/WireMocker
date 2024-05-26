using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using MudBlazor;
using ReactiveUI;
using RZ.Foundation;
using Tirax.Application.WireMocker.Services;

namespace Tirax.Application.WireMocker.Components.Features.MockData;

public sealed class XPortViewModel : ViewModel
{
    readonly ObservableAsPropertyHelper<bool> hasMappings;
    readonly Subject<(Severity Severity, string Message)> notifications = new();

    string mappings = string.Empty;

    public XPortViewModel(IScheduler scheduler, IDataStore dataStore, IMockServer mockServer) {
        hasMappings = this.WhenAnyValue(vm => vm.Mappings)
                          .Select(m => !string.IsNullOrWhiteSpace(m))
                          .ToProperty(this, vm => vm.HasMappings);

        LoadMappings = ReactiveCommand.Create<Unit, Outcome<Unit>>(
            _ => mockServer.LoadMappings(Mappings).RunIO(),
            this.WhenAnyValue(vm => vm.HasMappings),
            scheduler
            );

        LoadMappings.Subscribe(outcome => {
            notifications.OnNext(outcome.IfFail(out var error, out _)
                                     ? (Severity.Error, error.Message)
                                     : (Severity.Info, "Mappings loaded successfully"));
        });

        LoadData = ReactiveCommand.Create<Stream, Outcome<Unit>>(dataStore.LoadFromSnapshot, outputScheduler: scheduler);
        SaveData = ReactiveCommand.Create<Unit, Stream>(dataStore.SnapshotData, outputScheduler: scheduler);
    }

    public bool HasMappings => hasMappings.Value;

    public string Mappings
    {
        get => mappings;
        set => this.RaiseAndSetIfChanged(ref mappings, value);
    }

    public IObservable<(Severity Severity, string Message)> Notifications => notifications;

    public ReactiveCommand<Stream, Outcome<Unit>> LoadData { get; }

    public ReactiveCommand<Unit, Outcome<Unit>> LoadMappings { get; }

    public ReactiveCommand<Unit, Stream> SaveData { get; }
}