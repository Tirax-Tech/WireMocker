using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using LanguageExt.Common;
using MudBlazor;
using ReactiveUI;
using RZ.Foundation;
using RZ.Foundation.Extensions;
using Tirax.Application.WireMocker.Components.Features.Shell;
using Tirax.Application.WireMocker.Domain;
using Tirax.Application.WireMocker.Services;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices;

public sealed class SearchPanelViewModel : ViewModel
{
    readonly IDataStore dataStore;
    readonly ReadOnlyObservableCollection<Service> services;
    readonly ObservableAsPropertyHelper<bool> canNew;
    string serviceSearchText = string.Empty;

    public SearchPanelViewModel(ShellViewModel shell, IScheduler scheduler, IViewModelFactory vmFactory, IDataStore dataStore) {
        this.dataStore = dataStore;
        var normalized = this.WhenAnyValue(x => x.ServiceSearchText)
                             .Select(x => x.Trim())
                             .Select(s => string.IsNullOrWhiteSpace(s) ? null : s);

        canNew = normalized.Select(s => s is not null).ToProperty(this, x => x.CanNew);

        var serviceChanges = new Subject<IChangeSet<Service>>();
        var errorStream = new Subject<Error>();

        NewService = ReactiveCommand.Create<string, Unit>(
            title => {
                var vm = vmFactory.Create<AddServiceViewModel>(scheduler, title);
                shell.PushModal(vm);
                vm.Save.Where(r => r.IsFail).Select(r => r.UnwrapError()).Subscribe(errorStream.OnNext);
                vm.Save
                  .Where(r => r.IsSuccess)
                  .Do(_ => ServiceSearchText = string.Empty)
                  .Select(r => r.Unwrap()).ToObservableChangeSet()
                  .Subscribe(serviceChanges.OnNext);
                return unit;
            }, outputScheduler: scheduler);

        var serviceSource = Observable.FromAsync(async () => await LoadAllServices().RunIO());

        errorStream.Merge(serviceSource.Where(r => r.IsFail)
                                       .Select(r => r.UnwrapError()))
                   .Subscribe(e => shell.Notify((Severity.Error, e.ToString())));

        var initSource = serviceSource.Where(r => r.IsSuccess)
                                      .SelectMany(r => r.Unwrap())
                                      .ToObservableChangeSet();

        serviceChanges.Merge(initSource)
                      .Do(_ => this.RaisePropertyChanging(nameof(Services)))
                      .Bind(out services)
                      .Subscribe(_ => this.RaisePropertyChanged(nameof(Services)));
    }

    public string ServiceSearchText
    {
        get => serviceSearchText;
        set => this.RaiseAndSetIfChanged(ref serviceSearchText, value);
    }

    public bool CanNew => canNew.Value;

    public ReadOnlyObservableCollection<Service> Services => services;

    public ReactiveCommand<string, Unit> NewService { get; }

    OutcomeT<Asynchronous, Seq<Service>> LoadAllServices() =>
        from itor in dataStore.GetServices()
        from data in TryCatch(() => itor.ToArrayAsync())
        select data.ToSeq();
}