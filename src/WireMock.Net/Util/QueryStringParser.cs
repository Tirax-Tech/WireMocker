// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using WireMock.Types;

namespace WireMock.Util;

/// <summary>
/// Based on https://stackoverflow.com/questions/659887/get-url-parameters-from-a-string-in-net
/// </summary>
static class QueryStringParser
{
    static readonly Dictionary<string, WireMockList<string>> Empty = new();

    public static IDictionary<string, string>? TryParse(string queryString, bool caseIgnore)
    {
        var parts = queryString.Split(["&"], StringSplitOptions.RemoveEmptyEntries)
                               .Select(parameter => parameter.Split('='))
                               .Distinct();

        var nameValueCollection = caseIgnore ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>();
        foreach (var part in parts)
            if (part.Length == 2)
                nameValueCollection.Add(part[0], WebUtility.UrlDecode(part[1]));

        return nameValueCollection;
    }

    public static IDictionary<string, WireMockList<string>> Parse(string? queryString, QueryParameterMultipleValueSupport? support = null)
    {
        if (string.IsNullOrEmpty(queryString))
        {
            return Empty;
        }

        var queryParameterMultipleValueSupport = support ?? QueryParameterMultipleValueSupport.All;

        string[] JoinParts(string[] parts)
            => parts.Length > 1
                   ? queryParameterMultipleValueSupport.HasFlag(QueryParameterMultipleValueSupport.Comma)
                         ? parts[1].Split([","], StringSplitOptions.RemoveEmptyEntries)
                         : // Support "?key=1,2"
                         [parts[1]]
                   : [];

        var splitOn = new List<string>();
        if (queryParameterMultipleValueSupport.HasFlag(QueryParameterMultipleValueSupport.Ampersand))
            splitOn.Add("&"); // Support "?key=value&key=anotherValue"
        if (queryParameterMultipleValueSupport.HasFlag(QueryParameterMultipleValueSupport.SemiColon))
            splitOn.Add(";"); // Support "?key=value;key=anotherValue"

        return queryString.TrimStart('?')
                          .Split(splitOn.ToArray(), StringSplitOptions.RemoveEmptyEntries)
                          .Select(parameter => parameter.Split(['='], 2, StringSplitOptions.RemoveEmptyEntries))
                          .GroupBy(parts => parts[0], JoinParts)
                          .ToDictionary(grouping => grouping.Key,
                                        grouping => new WireMockList<string>(from x in grouping
                                                                             from s in x
                                                                             select WebUtility.UrlDecode(s)));
    }
}