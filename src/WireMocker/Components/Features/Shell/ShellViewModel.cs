using ReactiveUI;

namespace Tirax.Application.WireMocker.Components.Features.Shell;

public sealed class ShellViewModel : ViewModel
{
    bool isDrawerOpen = true;

    public bool IsDrawerOpen
    {
        get => isDrawerOpen;
        set => this.RaiseAndSetIfChanged(ref isDrawerOpen, value);
    }
}