// Copyright © WireMock.Net

// Copied from https://github.com/Handlebars-Net/Handlebars.Net.Helpers/blob/master/src/Handlebars.Net.Helpers.DynamicLinq

// Modified by Ruxo Zheng, 2024.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Reflection;
using Newtonsoft.Json.Linq;
using JTokenResolvers = System.Collections.Generic.Dictionary<Newtonsoft.Json.Linq.JTokenType, System.Func<Newtonsoft.Json.Linq.JToken, WireMock.Json.DynamicJsonClassOptions?, object?>>;

namespace WireMock.Json;

internal static class JObjectExtensions
{
    static readonly JTokenResolvers Resolvers = new()
    {
        { JTokenType.Array, ConvertJTokenArray },
        { JTokenType.Boolean, (jToken, _) => jToken.Value<bool>() },
        { JTokenType.Bytes, (jToken, _) => jToken.Value<byte[]>() },
        { JTokenType.Date, (jToken, _) => jToken.Value<DateTime>() },
        { JTokenType.Float, ConvertJTokenFloat },
        { JTokenType.Guid, (jToken, _) => jToken.Value<Guid>() },
        { JTokenType.Integer, ConvertJTokenInteger },
        { JTokenType.None, (_, _) => null },
        { JTokenType.Null, (_, _) => null },
        { JTokenType.Object, ConvertJObject },
        { JTokenType.Property, ConvertJTokenProperty },
        { JTokenType.String, (jToken, _) => jToken.Value<string>() },
        { JTokenType.TimeSpan, (jToken, _) => jToken.Value<TimeSpan>() },
        { JTokenType.Undefined, (_, _) => null },
        { JTokenType.Uri, (o, _) => o.Value<Uri>() },
    };

    internal static DynamicClass? ToDynamicJsonClass(this JObject? src, DynamicJsonClassOptions? options = null)
    {
        if (src == null)
            return null;

        var dynamicPropertyWithValues = new List<DynamicPropertyWithValue>();

        foreach (var prop in src.Properties())
        {
            var value = Resolvers[prop.Type](prop.Value, options);
            if (value != null)
                dynamicPropertyWithValues.Add(new DynamicPropertyWithValue(prop.Name, value));
        }

        return CreateInstance(dynamicPropertyWithValues);
    }

    internal static IEnumerable ToDynamicClassArray(this JArray? src, DynamicJsonClassOptions? options = null)
        => src == null ? EmptyArray<object?>.Value : ConvertJTokenArray(src, options);

    static object? ConvertJObject(JToken arg, DynamicJsonClassOptions? options = null)
        => arg is JObject asJObject ? asJObject.ToDynamicJsonClass(options) : GetResolverFor(arg)(arg, options);

    static object PassThrough(JToken arg, DynamicJsonClassOptions? options)
        => arg;

    static Func<JToken, DynamicJsonClassOptions?, object?> GetResolverFor(JToken arg)
        => Resolvers.TryGetValue(arg.Type, out var result) ? result : PassThrough;

    static object ConvertJTokenFloat(JToken arg, DynamicJsonClassOptions? options = null)
    {
        if (arg.Type != JTokenType.Float)
            throw new InvalidOperationException($"Unable to convert {nameof(JToken)} of type: {arg.Type} to double or float.");

        if (options?.FloatConvertBehavior == FloatBehavior.UseFloat)
            try
            {
                return arg.Value<float>();
            }
            catch
            {
                return arg.Value<double>();
            }

        if (options?.FloatConvertBehavior == FloatBehavior.UseDecimal)
            try
            {
                return arg.Value<decimal>();
            }
            catch
            {
                return arg.Value<double>();
            }
        return arg.Value<double>();
    }

    static object ConvertJTokenInteger(JToken arg, DynamicJsonClassOptions? options = null)
    {
        if (arg.Type != JTokenType.Integer)
            throw new InvalidOperationException($"Unable to convert {nameof(JToken)} of type: {arg.Type} to long or int.");

        var longValue = arg.Value<long>();

        return options is not null && options.IntegerConvertBehavior != IntegerBehavior.UseInt ? longValue :
               longValue is >= int.MinValue and <= int.MaxValue                                ? Convert.ToInt32(longValue) : longValue;
    }

    static object? ConvertJTokenProperty(JToken arg, DynamicJsonClassOptions? options = null)
    {
        var resolver = GetResolverFor(arg);
        if (resolver is null)
            throw new InvalidOperationException($"Unable to handle {nameof(JToken)} of type: {arg.Type}.");

        return resolver(arg, options);
    }

    static IEnumerable ConvertJTokenArray(JToken arg, DynamicJsonClassOptions? options = null)
    {
        if (arg is not JArray array)
            throw new InvalidOperationException($"Unable to convert {nameof(JToken)} of type: {arg.Type} to {nameof(JArray)}.");

        var result = array.Select(i => ConvertJObject(i)).ToArray();
        var distinctType = FindSameTypeOf(result);
        return distinctType == null ? result : ConvertToTypedArray(result, distinctType);
    }

    static Type? FindSameTypeOf(IEnumerable<object?> src)
    {
        var types = src.Select(o => o?.GetType()).Distinct().OfType<Type>().ToArray();
        return types.Length == 1 ? types[0] : null;
    }

    static IEnumerable ConvertToTypedArray(IEnumerable<object?> src, Type newType)
    {
        var method = ConvertToTypedArrayGenericMethod.MakeGenericMethod(newType);
        return (IEnumerable)method.Invoke(null, [src])!;
    }

    static readonly MethodInfo ConvertToTypedArrayGenericMethod = typeof(JObjectExtensions).GetMethod(nameof(ConvertToTypedArrayGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;

    static T[] ConvertToTypedArrayGeneric<T>(IEnumerable<object> src)
    {
        return src.Cast<T>().ToArray();
    }

    public static DynamicClass CreateInstance(IList<DynamicPropertyWithValue> dynamicPropertiesWithValue, bool createParameterCtor = true)
    {
        var type = DynamicClassFactory.CreateType(dynamicPropertiesWithValue.Cast<DynamicProperty>().ToArray(), createParameterCtor);
        var dynamicClass = (DynamicClass)Activator.CreateInstance(type)!;
        foreach (var dynamicPropertyWithValue in dynamicPropertiesWithValue.Where(p => p.Value != null))
            dynamicClass.SetDynamicPropertyValue(dynamicPropertyWithValue.Name, dynamicPropertyWithValue.Value!);

        return dynamicClass;
    }
}