using System.Reactive.Concurrency;

namespace Tirax.Application.WireMocker.Components.Layout;

public sealed class AppMainLayoutViewModel : MainLayoutViewModel
{
    public AppMainLayoutViewModel(ILogger<AppMainLayoutViewModel> logger, TimeProvider clock, IScheduler scheduler)
        : base(logger, clock, scheduler) {
        IsDarkMode = true;
    }
}