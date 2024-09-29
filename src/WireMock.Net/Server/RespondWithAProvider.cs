// Copyright © WireMock.Net and mock4net by Alexandre Victoor

// This source file is based on mock4net by Alexandre Victoor which is licensed under the Apache 2.0 License.
// For more details see 'mock4net/LICENSE.txt' and 'mock4net/readme.md' in this project root.

// Modified by Ruxo Zheng, 2024.
using System;
using System.Collections.Generic;
using System.Net;
using Stef.Validation;
using WireMock.Matchers.Request;
using WireMock.Models;
using WireMock.ResponseBuilders;
using WireMock.ResponseProviders;
using WireMock.Settings;
using WireMock.Types;
using WireMock.Util;

namespace WireMock.Server;

/// <summary>
/// The respond with a provider.
/// </summary>
internal class RespondWithAProvider : IRespondWithAProvider
{
    readonly RegistrationCallback registrationCallback;
    readonly IRequestMatcher requestMatcher;
    readonly WireMockServerSettings settings;
    readonly TimeProvider clock;
    readonly bool saveToFile;

    int priority;
    string? title;
    string? description;
    string? path;
    string? executionConditionState;
    string? nextState;
    string? scenario;
    int timesInSameState = 1;
    bool? useWebhookFireAndForget;
    double? probability;
    IdOrText? protoDefinition;
    // ReSharper disable once NotAccessedField.Local
    GraphQLSchemaDetails? graphQLSchemaDetails;

    public Guid Guid { get; private set; }

    public IWebhook[]? Webhooks { get; private set; }

    public ITimeSettings? TimeSettings { get; private set; }

    public object? Data { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RespondWithAProvider"/> class.
    /// </summary>
    /// <param name="registrationCallback">The registration callback.</param>
    /// <param name="requestMatcher">The request matcher.</param>
    /// <param name="settings">The WireMockServerSettings.</param>
    /// <param name="guidUtils">GuidUtils to make unit testing possible.</param>
    /// <param name="clock">DateTimeUtils to make unit testing possible.</param>
    /// <param name="saveToFile">Optional boolean to indicate if this mapping should be saved as static mapping file.</param>
    public RespondWithAProvider(
        RegistrationCallback registrationCallback,
        IRequestMatcher requestMatcher,
        WireMockServerSettings settings,
        IGuidUtils guidUtils,
        TimeProvider clock,
        bool saveToFile = false
    )
    {
        this.registrationCallback = Guard.NotNull(registrationCallback);
        this.requestMatcher = Guard.NotNull(requestMatcher);
        this.settings = Guard.NotNull(settings);
        this.clock = Guard.NotNull(clock);
        this.saveToFile = saveToFile;

        Guid = guidUtils.NewGuid();
    }

    /// <inheritdoc />
    public void RespondWith(IResponseProvider provider)
    {
        var mapping = new Mapping
        (
            Guid,
            clock.GetUtcNow(),
            title,
            description,
            path,
            settings,
            requestMatcher,
            provider,
            priority,
            scenario,
            executionConditionState,
            nextState,
            timesInSameState,
            Webhooks,
            useWebhookFireAndForget,
            TimeSettings,
            Data
        );

        if (probability != null)
            mapping.WithProbability(probability.Value);

        if (protoDefinition != null)
            mapping.WithProtoDefinition(protoDefinition.Value);

        registrationCallback(mapping, saveToFile);
    }

    /// <inheritdoc />
    public void ThenRespondWith(Action<IResponseBuilder> action)
    {
        var responseBuilder = Response.Create();

        action(responseBuilder);

        RespondWith(responseBuilder);
    }

    /// <inheritdoc />
    public void ThenRespondWithOK()
        => RespondWith(Response.Create());

    /// <inheritdoc />
    public void ThenRespondWithStatusCode(int code)
        => RespondWith(Response.Create().WithStatusCode(code));

    /// <inheritdoc />
    public void ThenRespondWithStatusCode(string code)
        => RespondWith(Response.Create().WithStatusCode(code));

    /// <inheritdoc />
    public void ThenRespondWithStatusCode(HttpStatusCode code)
        => RespondWith(Response.Create().WithStatusCode(code));

    /// <inheritdoc />
    public IRespondWithAProvider WithData(object data)
    {
        Data = data;
        return this;
    }

    /// <inheritdoc />
    public IRespondWithAProvider WithGuid(string guid)
        => WithGuid(Guid.Parse(guid));

    /// <inheritdoc />
    public IRespondWithAProvider WithGuid(Guid guid)
    {
        Guid = guid;
        return this;
    }

    /// <inheritdoc />
    public IRespondWithAProvider DefineGuid(Guid guid)
        => WithGuid(guid);

    /// <inheritdoc />
    public IRespondWithAProvider DefineGuid(string guid)
        => WithGuid(guid);

    /// <inheritdoc />
    public IRespondWithAProvider WithTitle(string title)
    {
        this.title = title;

        return this;
    }

    /// <inheritdoc />
    public IRespondWithAProvider WithDescription(string description)
    {
        this.description = description;
        return this;
    }

    /// <inheritdoc />
    public IRespondWithAProvider WithPath(string path)
    {
        this.path = path;
        return this;
    }

    /// <inheritdoc />
    public IRespondWithAProvider AtPriority(int priority)
    {
        this.priority = priority;
        return this;
    }

    /// <inheritdoc />
    public IRespondWithAProvider InScenario(string scenario)
    {
        this.scenario = Guard.NotNullOrWhiteSpace(scenario);
        return this;
    }

    /// <inheritdoc />
    public IRespondWithAProvider InScenario(int scenario)
        => InScenario(scenario.ToString());

    /// <inheritdoc />
    public IRespondWithAProvider WhenStateIs(string state)
    {
        if (string.IsNullOrEmpty(scenario))
            throw new NotSupportedException("Unable to set state condition when no scenario is defined.");

        executionConditionState = state;

        return this;
    }

    /// <inheritdoc />
    public IRespondWithAProvider WhenStateIs(int state)
        => WhenStateIs(state.ToString());

    /// <inheritdoc />
    public IRespondWithAProvider WillSetStateTo(string state, int? times = 1)
    {
        if (string.IsNullOrEmpty(scenario))
            throw new NotSupportedException("Unable to set next state when no scenario is defined.");

        nextState = state;
        timesInSameState = times ?? 1;

        return this;
    }

    /// <inheritdoc />
    public IRespondWithAProvider WillSetStateTo(int state, int? times = 1)
        => WillSetStateTo(state.ToString(), times);

    /// <inheritdoc />
    public IRespondWithAProvider WithTimeSettings(ITimeSettings timeSettings)
    {
        TimeSettings = Guard.NotNull(timeSettings);

        return this;
    }

    /// <inheritdoc />
    public IRespondWithAProvider WithWebhook(params IWebhook[] webhooks)
    {
        Guard.HasNoNulls(webhooks);

        Webhooks = webhooks;
        return this;
    }

    /// <inheritdoc />
    public IRespondWithAProvider WithWebhook(
        string url,
        string method = "post",
        IDictionary<string, WireMockList<string>>? headers = null,
        string? body = null,
        bool useTransformer = true,
        TransformerType transformerType = TransformerType.Handlebars)
    {
        Guard.NotNull(url);
        Guard.NotNull(method);

        Webhooks = [InitWebhook(url, method, headers, useTransformer, transformerType)];

        if (body != null)
            Webhooks[0].Request.BodyData = new BodyData
            {
                BodyAsString = body,
                BodyType = BodyType.String,
                ContentType = ContentTypes.Text
            };

        return this;
    }

    /// <inheritdoc />
    public IRespondWithAProvider WithWebhook(
        string url,
        string method = "post",
        IDictionary<string, WireMockList<string>>? headers = null,
        object? body = null,
        bool useTransformer = true,
        TransformerType transformerType = TransformerType.Handlebars)
    {
        Guard.NotNull(url);
        Guard.NotNull(method);

        Webhooks = [InitWebhook(url, method, headers, useTransformer, transformerType)];

        if (body != null)
            Webhooks[0].Request.BodyData = new BodyData
            {
                BodyAsJson = body,
                BodyType = BodyType.Json,
                ContentType = ContentTypes.Json
            };

        return this;
    }

    public IRespondWithAProvider WithWebhookFireAndForget(bool useWebhooksFireAndForget)
    {
        useWebhookFireAndForget = useWebhooksFireAndForget;
        return this;
    }

    public IRespondWithAProvider WithProbability(double probability)
    {
        this.probability = Guard.Condition(probability, p => p is >= 0 and <= 1.0);
        return this;
    }

    /// <inheritdoc />
    public IRespondWithAProvider WithProtoDefinition(string protoDefinitionOrId)
    {
        Guard.NotNullOrWhiteSpace(protoDefinitionOrId);

        this.protoDefinition = settings.ProtoDefinitions?.TryGetValue(protoDefinitionOrId, out var protoDefinition) == true
                                   ? new(protoDefinitionOrId, protoDefinition)
                                   : new(null, protoDefinitionOrId);

        return this;
    }

    /// <inheritdoc />
    public IRespondWithAProvider WithGraphQLSchema(string graphQlSchemaOrId, IDictionary<string, Type>? customScalars = null)
    {
        Guard.NotNullOrWhiteSpace(graphQlSchemaOrId);

        if (settings.GraphQLSchemas?.TryGetValue(graphQlSchemaOrId, out graphQLSchemaDetails) != true)
        {
            graphQLSchemaDetails = new GraphQLSchemaDetails
            {
                SchemaAsString = graphQlSchemaOrId,
                CustomScalars = customScalars
            };
        }

        return this;
    }

    static IWebhook InitWebhook(
        string url,
        string method,
        IDictionary<string, WireMockList<string>>? headers,
        bool useTransformer,
        TransformerType transformerType
    )
        => new Webhook {
            Request = new WebhookRequest {
                Url = url,
                Method = method,
                Headers = headers,
                UseTransformer = useTransformer,
                TransformerType = transformerType
            }
        };
}