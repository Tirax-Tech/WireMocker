using FluentValidation;
using FluentValidation.Results;

namespace Tirax.Application.WireMocker.Helpers;

public static class Validator
{
    public delegate Seq<ValidationFailure> Func<in T>(T data);
    public delegate void                   ConfigureValidator<T>(IRuleBuilderInitial<Wrapper<T>, T> builder);
    public readonly record struct Wrapper<T>(T Value);

    public static Func<T> Create<T>(ConfigureValidator<T> configure) where T : class
    {
        var validator = new GenericValidator<T>(configure);
        return x => validator.Validate(new Wrapper<T>(x)).Errors.ToSeq();
    }

    sealed class GenericValidator<T> : AbstractValidator<Wrapper<T>>
    {
        public GenericValidator(ConfigureValidator<T> configure){
            configure(RuleFor(x => x.Value));
        }
    }
}