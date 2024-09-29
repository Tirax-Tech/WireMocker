using System.Reactive.Concurrency;
using System.Reactive.Linq;
using ReactiveUI;
using RZ.Foundation;
using Tirax.Application.WireMocker.Services;
using WireMock.Server;

namespace Tirax.Application.WireMocker.Components.Features.MockData;

public sealed class XPortViewModel : ViewModel
{
    readonly IWireMockServer mockServer;
    readonly ObservableAsPropertyHelper<bool> hasMappings;
    readonly ObservableAsPropertyHelper<bool> canLoad;

    string mappings = string.Empty;
    int mappingCount;

    public XPortViewModel(IScheduler scheduler, IDataStore dataStore, ShellViewModel shell, IWireMockServer mockServer) {
        this.mockServer = mockServer;
        hasMappings = this.WhenAnyValue(vm => vm.Mappings)
                          .Select(m => !string.IsNullOrWhiteSpace(m))
                          .ToProperty(this, vm => vm.HasMappings);

        ReloadMappingCount();

        LoadMappings = ReactiveCommand.CreateFromObservable(
                () => ObservableFrom.Func(() => {
                    mockServer.LoadMappings(Mappings);
                    ReloadMappingCount();
                    return unit;
                }).CatchToOutcome(),
                this.WhenAnyValue(vm => vm.HasMappings),
                scheduler
            );

        LoadMappings.Subscribe(outcome => {
            shell.Notify(outcome.IfFail(out var error, out _)
                             ? (Severity.Error, error.Message)
                             : (Severity.Info, "Mappings loaded successfully"));
        });

        canLoad = this.WhenAnyValue(x => x.HasMappings)
            .CombineLatest(LoadMappings.IsExecuting, (hasMappings, isExecuting) => !isExecuting && hasMappings)
                      .ToProperty(this, x => x.CanLoad);

        LoadData = ReactiveCommand.CreateFromTask<Stream, Outcome<Unit>>(async content => {
            var result = await TryCatch(() => dataStore.LoadFromSnapshot(content));
            shell.Notify(result.IfSuccess(out _, out var error)
                             ? (Severity.Success, "Loaded")
                             : (Severity.Error, error.ToString()));
            return result;
        }, outputScheduler: scheduler);
        SaveData = ReactiveCommand.Create(dataStore.SnapshotData, outputScheduler: scheduler);
    }

    public bool CanLoad => canLoad.Value;

    public bool HasMappings => hasMappings.Value;

    public string Mappings
    {
        get => mappings;
        set => this.RaiseAndSetIfChanged(ref mappings, value);
    }

    public int MappingCount
    {
        get => mappingCount;
        set => this.RaiseAndSetIfChanged(ref mappingCount, value);
    }

    public ReactiveCommand<Stream, Outcome<Unit>> LoadData { get; }

    public ReactiveCommand<RUnit, Outcome<Unit>> LoadMappings { get; }

    public ReactiveCommand<RUnit, Stream> SaveData { get; }

    void ReloadMappingCount() {
        MappingCount = mockServer.Mappings.Count(map => !map.IsAdminInterface);
    }
}