using FluentValidation;
using Map = LanguageExt.Map;

namespace Tirax.Application.WireMocker.Domain;

public sealed record Service(Guid Id, string Name)
{
    public ProxySetting? Proxy { get; init; }
    public Map<Guid, Endpoint> Endpoints { get; init; } = Map.empty<Guid, Endpoint>();

    public static readonly AbstractValidator<Service> Validator = new ValidatorType();
    sealed class ValidatorType : AbstractValidator<Service>
    {
        public ValidatorType() {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.Name).NotEmpty();
        }
    }
}
