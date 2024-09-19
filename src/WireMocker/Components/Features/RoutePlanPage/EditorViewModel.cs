﻿using System.Collections.ObjectModel;
using System.Diagnostics;
using FluentValidation;
using Tirax.Application.WireMocker.Domain;
using Tirax.Application.WireMocker.RZ;
using Tirax.Application.WireMocker.Services;

namespace Tirax.Application.WireMocker.Components.Features.RoutePlanPage;

public sealed class EditorViewModel : ViewModel
{
    public EditorViewModel(IChaotic chaotic, IDataStore store, Option<RoutePlan> plan) {
        Services = new(store.GetServices());
    }

    public ObservableCollection<Service> Services { get; }

    public static readonly ValidatorType Validator = new();

    public sealed class ValidatorType : AbstractValidator<EditorViewModel>
    {
        public ValidatorType(){}
    }
}