using FluentValidation;

namespace Tirax.Application.WireMocker.Helpers;

public static class ValidationExtensions
{
    public static Func<object, string, Task<IEnumerable<string>>> ValueValidator<T>(this AbstractValidator<T> validator) =>
        async (model, propertyName) => {
            var result = await validator.ValidateAsync(
                             ValidationContext<T>.CreateWithOptions((T)model, x => x.IncludeProperties(propertyName)));
            return result.IsValid ? System.Array.Empty<string>() : result.Errors.Select(e => e.ErrorMessage);
        };
}