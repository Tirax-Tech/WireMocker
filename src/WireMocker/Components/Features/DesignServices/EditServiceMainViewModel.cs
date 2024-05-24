using System.Collections.ObjectModel;
using System.Reactive.Linq;
using ReactiveUI;
using Endpoint = Tirax.Application.WireMocker.Domain.Endpoint;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices;

public sealed class EditServiceMainViewModel : ViewModel, IHasDetailPanel
{
    readonly string name;
    readonly ObservableAsPropertyHelper<bool> showAddEndpoint;

    string proxy = string.Empty;
    EditServiceDetailViewModel? detail;

    public EditServiceMainViewModel(string name) {
        this.name = name;
        detail = new EditServiceDetailViewModel();

        showAddEndpoint = Endpoints.WhenAnyValue(x => x.Count).Select(c => c > 0).ToProperty(this, x => x.ShowAddEndpoint);
        AddEndpoint = ReactiveCommand.Create<Unit, Unit>(_ => {
            return unit;
        });
    }

    public ViewModel? DetailPanel => detail;

    public string ServiceName => name;

    public string Proxy
    {
        get => proxy;
        set => this.RaiseAndSetIfChanged(ref proxy, value);
    }

    public bool ShowAddEndpoint => showAddEndpoint.Value;

    public ObservableCollection<Endpoint> Endpoints { get; } = new();

    public ReactiveCommand<Unit, Unit> AddEndpoint { get; }
}