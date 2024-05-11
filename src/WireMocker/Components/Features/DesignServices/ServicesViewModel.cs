using System.Reactive.Linq;
using ReactiveUI;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices;

public sealed class ServicesViewModel : ViewModel
{
    string serviceSearchText = string.Empty;

    public ServicesViewModel() {
        var normalized = this.WhenAnyValue(x => x.ServiceSearchText)
                             .Select(x => x.Trim())
                             .Select(s => string.IsNullOrWhiteSpace(s) ? null : s);

        var canNew = normalized.Select(s => s is not null);

        NewService = ReactiveCommand.CreateFromObservable<Unit, Unit>(
            _ => normalized.Take(1).Select(s => s is null? unit : NewServiceImpl(s)),
            canNew);
    }

    public string ServiceSearchText
    {
        get => serviceSearchText;
        set => this.RaiseAndSetIfChanged(ref serviceSearchText, value);
    }

    public ReactiveCommand<Unit,Unit> NewService { get; }

    Unit NewServiceImpl(string serviceName) {
        Console.WriteLine("Clicked! {0}", serviceName);
        return unit;
    }
}