using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using MudBlazor;
using ReactiveUI;
using RZ.Foundation.Types;
using Tirax.Application.WireMocker.Components.Features.Shell;
using Tirax.Application.WireMocker.Domain;
using Tirax.Application.WireMocker.Services;
using Unit = LanguageExt.Unit;

namespace Tirax.Application.WireMocker.Components.Features.DesignServices;

public sealed record SearchPanelViewData
{
    public required ReactiveCommand<Service, Service> UpdateService { get; init; }
}

public sealed class SearchPanelViewModel : ViewModel
{
    readonly ReadOnlyObservableCollection<SearchViewItemViewModel> services;
    readonly ObservableAsPropertyHelper<bool> canNew;
    string serviceSearchText = string.Empty;

    public SearchPanelViewModel(ShellViewModel shell, IScheduler scheduler, IViewModelFactory vmFactory, IDataStore dataStore) {
        var normalized = this.WhenAnyValue(x => x.ServiceSearchText)
                             .Select(x => x.Trim())
                             .Select(s => string.IsNullOrWhiteSpace(s) ? null : s);

        canNew = normalized.Select(s => s is not null).ToProperty(this, x => x.CanNew);

        var serviceChanges = new Subject<IChangeSet<SearchViewItemViewModel>>();
        var errorStream = new Subject<ErrorInfo>();

        var viewData = new SearchPanelViewData {
            UpdateService = ReactiveCommand.CreateFromObservable<Service, Service>(service => {
                var save = dataStore.Save(service);
                save.Subscribe(_ => shell.Notify((Severity.Success, "Service saved.")));
                return save;
            })
        };

        NewService = ReactiveCommand.Create<string, Unit>(
            title => {
                var vm = vmFactory.Create<AddServiceViewModel>(scheduler, viewData, title);
                shell.PushModal(vm);
                vm.Save.Subscribe(_ => ServiceSearchText = string.Empty);
                return unit;
            }, outputScheduler: scheduler);

        var serviceSource = ObservableFrom.Generator(dataStore.GetServices).Shared();
        var updatedSource = viewData.UpdateService
                                    .Select(CreateView)
                                    .Select(vm => {
                                         var existed = services!.TryFirst(v => v.Service.Id == vm.Service.Id);
                                         return existed.IfSome(out var old)
                                                    ? ReplaceOf(old, vm)
                                                    : ChangeOf(ListChangeReason.Add, vm);
                                     });

        errorStream.Merge(serviceSource.GetErrorStream())
                   .Merge(viewData.UpdateService.GetErrorStream())
                   .Subscribe(e => shell.Notify((Severity.Error, e.ToString())));

        var initSource = serviceSource.Select(CreateView).ToObservableChangeSet();

        serviceChanges.Merge(initSource)
                      .Merge(updatedSource)
                      .Do(_ => this.RaisePropertyChanging(nameof(Services)))
                      .Bind(out services)
                      .Subscribe(_ => this.RaisePropertyChanged(nameof(Services)));
        return;

        SearchViewItemViewModel CreateView(Service service) {
            var vm = vmFactory.Create<SearchViewItemViewModel>(viewData, service);
            vm.Delete
              .Where(result => result.IsSuccess)
              .Subscribe(_ => serviceChanges.OnNext(ChangeOf(ListChangeReason.Remove, vm)));
            return vm;
        }
    }

    public string ServiceSearchText
    {
        get => serviceSearchText;
        set => this.RaiseAndSetIfChanged(ref serviceSearchText, value);
    }

    public bool CanNew => canNew.Value;

    public ReadOnlyObservableCollection<SearchViewItemViewModel> Services => services;

    public ReactiveCommand<string, Unit> NewService { get; }

    static ChangeSet<T> ChangeOf<T>(ListChangeReason reason, params T[] items) where T : notnull =>
        new(items.Map(i => new DynamicData.Change<T>(reason, i)));

    static ChangeSet<T> ReplaceOf<T>(T old, T @new) where T : notnull =>
        new([new DynamicData.Change<T>(ListChangeReason.Replace, @new, old)]);
}