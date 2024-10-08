// Copyright © WireMock.Net

using System;
using System.Collections.Generic;
using System.Linq;
using Stef.Validation;
using WireMock.Types;

namespace WireMock.Matchers.Request;

/// <summary>
/// The request body GraphQL matcher.
/// </summary>
public class RequestMessageGraphQLMatcher : IRequestMatcher
{
    /// <summary>
    /// The matchers.
    /// </summary>
    public IMatcher[]? Matchers { get; }

    /// <summary>
    /// The <see cref="MatchOperator"/>
    /// </summary>
    public MatchOperator MatchOperator { get; } = MatchOperator.Or;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestMessageGraphQLMatcher"/> class.
    /// </summary>
    /// <param name="matchBehaviour">The match behaviour.</param>
    /// <param name="schema">The schema.</param>
    /// <param name="customScalars">A dictionary defining the custom scalars used in this schema. [optional]</param>
    public RequestMessageGraphQLMatcher(MatchBehaviour matchBehaviour, string schema, IDictionary<string, Type>? customScalars = null) :
        this(CreateMatcherArray(matchBehaviour, schema, customScalars))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestMessageGraphQLMatcher"/> class.
    /// </summary>
    /// <param name="matchBehaviour">The match behaviour.</param>
    /// <param name="schema">The schema.</param>
    /// <param name="customScalars">A dictionary defining the custom scalars used in this schema. [optional]</param>
    public RequestMessageGraphQLMatcher(MatchBehaviour matchBehaviour, GraphQL.Types.ISchema schema, IDictionary<string, Type>? customScalars = null) :
        this(CreateMatcherArray(matchBehaviour, new AnyOfTypes.AnyOf<string, WireMock.Models.StringPattern, GraphQL.Types.ISchema>(schema), customScalars))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestMessageGraphQLMatcher"/> class.
    /// </summary>
    /// <param name="matchers">The matchers.</param>
    public RequestMessageGraphQLMatcher(params IMatcher[] matchers)
    {
        Matchers = Guard.NotNull(matchers);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestMessageGraphQLMatcher"/> class.
    /// </summary>
    /// <param name="matchers">The matchers.</param>
    /// <param name="matchOperator">The <see cref="MatchOperator"/> to use.</param>
    public RequestMessageGraphQLMatcher(MatchOperator matchOperator, params IMatcher[] matchers)
    {
        Matchers = Guard.NotNull(matchers);
        MatchOperator = matchOperator;
    }

    /// <inheritdoc />
    public double GetMatchingScore(IRequestMessage requestMessage, IRequestMatchResult requestMatchResult)
    {
        var results = CalculateMatchResults(requestMessage);
        var (score, exception) = MatchResult.From(results, MatchOperator).Expand();

        return requestMatchResult.AddScore(GetType(), score, exception);
    }

    static MatchResult CalculateMatchResult(IRequestMessage requestMessage, IMatcher matcher)
        // In case the matcher is a IStringMatcher and the body is a Json or a String, use the BodyAsString to match on.
        => matcher is IStringMatcher stringMatcher &&
           requestMessage.BodyData?.BodyType is BodyType.Json or BodyType.String or BodyType.FormUrlEncoded
               ? stringMatcher.IsMatch(requestMessage.BodyData.BodyAsString)
               : default;

    IReadOnlyList<MatchResult> CalculateMatchResults(IRequestMessage requestMessage)
        => Matchers == null ? [new MatchResult()] : Matchers.Select(matcher => CalculateMatchResult(requestMessage, matcher)).ToArray();

    static IMatcher[] CreateMatcherArray(
        MatchBehaviour matchBehaviour,
        AnyOfTypes.AnyOf<string, WireMock.Models.StringPattern, GraphQL.Types.ISchema> schema,
        IDictionary<string, Type>? customScalars)
        => new[] { new GraphQLMatcher(schema, customScalars, matchBehaviour) }.Cast<IMatcher>().ToArray();
}