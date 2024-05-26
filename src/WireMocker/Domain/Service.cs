using FluentValidation;

namespace Tirax.Application.WireMocker.Domain;

public sealed record Service(Guid Id, string Name)
{
    public ProxySetting? Proxy { get; init; }
    public Dictionary<Guid, Endpoint> Endpoints { get; init; } = new();

    public static readonly AbstractValidator<Service> Validator = new ValidatorType();
    sealed class ValidatorType : AbstractValidator<Service>
    {
        public ValidatorType() {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.Name).NotEmpty();
        }
    }
}
