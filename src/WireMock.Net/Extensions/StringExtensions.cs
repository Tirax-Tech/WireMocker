namespace WireMock.Extensions;

public static class StringExtensions
{
    public static string? NotEmpty(this string? value) => string.IsNullOrEmpty(value) ? null : value;
}