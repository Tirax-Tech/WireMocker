// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using WireMock.Types;
using WireMock.Util;

namespace WireMock.Matchers.Helpers;

internal static class BodyDataMatchScoreCalculator
{
    public static MatchResult CalculateMatchScore(IBodyData? requestMessage, IMatcher matcher)
    {
        if (requestMessage == null)
            return default;

        if (matcher is NotNullOrEmptyMatcher notNullOrEmptyMatcher)
            switch (requestMessage.BodyType)
            {
                case BodyType.Json:
                case BodyType.String:
                case BodyType.FormUrlEncoded:
                    return notNullOrEmptyMatcher.IsMatch(requestMessage.BodyAsString);

                case BodyType.Bytes:
                    return notNullOrEmptyMatcher.IsMatch(requestMessage.BodyAsBytes);

                default:
                    return default;
            }

        if (matcher is ExactObjectMatcher exactObjectMatcher)
        {
            // If the body is a byte array, try to match.
            var detectedBodyType = requestMessage.BodyType;
            if (detectedBodyType is BodyType.Bytes or BodyType.String or BodyType.FormUrlEncoded)
                return exactObjectMatcher.IsMatch(requestMessage.BodyAsBytes);
        }

        // Check if the matcher is a IObjectMatcher
        if (matcher is IObjectMatcher objectMatcher)
        {
            // If the body is a JSON object, try to match.
            if (requestMessage.BodyType == BodyType.Json)
                return objectMatcher.IsMatch(requestMessage.BodyAsJson);

            // If the body is a byte array, try to match.
            if (requestMessage.BodyType == BodyType.Bytes)
                return objectMatcher.IsMatch(requestMessage.BodyAsBytes);
        }

        // In case the matcher is a IStringMatcher and If  body is a Json or a String, use the BodyAsString to match on.
        if (matcher is IStringMatcher stringMatcher && requestMessage.BodyType is BodyType.Json or BodyType.String or BodyType.FormUrlEncoded)
            return stringMatcher.IsMatch(requestMessage.BodyAsString);

        return default;
    }
}