using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components;
using Tirax.Application.WireMocker.Components;

namespace Tirax.Application.WireMocker.Services;

public abstract record ViewState
{
    public sealed record Empty : ViewState
    {
        public static readonly ViewState Instance = new Empty();
    }

    public sealed record InvalidModel : ViewState
    {
        public static readonly ViewState Instance = new InvalidModel();
    }

    public sealed record ViewTypeNotFound(string ViewTypeName) : ViewState;

    public sealed record View(RenderFragment Content) : ViewState;

    internal sealed record ResolvedViewType(Type ViewType) : ViewState;
}

public interface IViewLocator
{
    ViewState Locate(ViewModel vm);
}

public class ViewLocator : IViewLocator
{
    readonly ConcurrentDictionary<Type, ViewState> viewLookup = new();

    public ViewState Locate(ViewModel vm) {
        var modelType = vm.GetType();
        var state = viewLookup.GetOrAdd(modelType, GetViewState);

        return state is ViewState.ResolvedViewType s? new ViewState.View(ForType(s.ViewType)) : state;

        RenderFragment ForType(Type vType) => builder => {
            builder.OpenComponent(0, vType);
            builder.AddAttribute(1, "ViewModel", vm);
            builder.CloseComponent();
        };
    }

    static ViewState GetViewState(Type modelType) {
        if (!modelType.Name.EndsWith("ViewModel"))
            return ViewState.InvalidModel.Instance;

        var viewTypeName = modelType.Name[..^9]; // 9 = "ViewModel".Length

        // get view type from the same namespace of the model
        var viewTypeFullName = $"{modelType.Namespace}.{viewTypeName}";
        var viewType = modelType.Assembly.GetType(viewTypeFullName);

        if (viewType is null)
            return new ViewState.ViewTypeNotFound(viewTypeFullName);

        return new ViewState.ResolvedViewType(viewType);
    }
}