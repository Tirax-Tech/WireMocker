using Tirax.Application.WireMocker.Components;

namespace Tirax.Application.WireMocker.Services;

public interface IViewModelFactory
{
    T Create<T>(params object[] args) where T : ViewModel;
}

sealed class ViewModelFactory(IServiceProvider serviceProvider) : IViewModelFactory
{
    public T Create<T>(params object[] args) where T : ViewModel =>
        ActivatorUtilities.CreateInstance<T>(serviceProvider, args);
}