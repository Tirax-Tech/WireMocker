using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using MudBlazor;
using ReactiveUI;
using RZ.Foundation;
using Tirax.Application.WireMocker.Services;

namespace Tirax.Application.WireMocker.Components.Features.MockData;

public sealed class XPortViewModel : ViewModelDisposable
{
    readonly ObservableAsPropertyHelper<bool> hasMappings;
    readonly Subject<(Severity Severity, string Message)> notifications = new();

    string mappings = string.Empty;

    public XPortViewModel(IMockServer mockServer) {
        hasMappings = this.WhenAnyValue(vm => vm.Mappings)
                          .Select(m => !string.IsNullOrWhiteSpace(m))
                          .ToProperty(this, vm => vm.HasMappings)
                          .DisposeWith(Disposables);

        LoadMappings = ReactiveCommand.Create<Unit, Outcome<Unit>>(
            _ => mockServer.LoadMappings(Mappings).RunIO(),
            this.WhenAnyValue(vm => vm.HasMappings),
            new SynchronizationContextScheduler(SynchronizationContext.Current!)
            );

        LoadMappings.Subscribe(outcome => {
            notifications.OnNext(outcome.IfFail(out var error, out _)
                                     ? (Severity.Error, error.Message)
                                     : (Severity.Info, "Mappings loaded successfully"));
        }).DisposeWith(Disposables);
    }

    public bool HasMappings => hasMappings.Value;

    public string Mappings
    {
        get => mappings;
        set => this.RaiseAndSetIfChanged(ref mappings, value);
    }

    public IObservable<(Severity Severity, string Message)> Notifications => notifications;

    #region Commands

    public ReactiveCommand<Unit, Outcome<Unit>> LoadMappings { get; }

    #endregion
}