// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System;
using System.Text;
using System.Threading.Tasks;
using JsonConverter.Abstractions;
using Stef.Validation;
using WireMock.Exceptions;
using WireMock.Models;
using WireMock.Types;
using WireMock.Util;

namespace WireMock.ResponseBuilders;

public partial class Response
{
    /// <inheritdoc />
    public IResponseBuilder WithBody(Func<IRequestMessage, string> bodyFactory, string? destination = BodyDestinationFormat.SameAsSource,
                                     Encoding? encoding = null)
        => WithCallbackInternal(true, req => new ResponseMessage {
            Timestamp = clock.GetUtcNow(),
            BodyData = new BodyData {
                BodyType = BodyType.String,
                ContentType = ContentTypes.Text,

                BodyAsString = bodyFactory(req),
                Encoding = encoding ?? Encoding.UTF8,
                IsFuncUsed = "Func<IRequestMessage, string>"
            }
        });

    /// <inheritdoc />
    public IResponseBuilder WithBody(Func<IRequestMessage, Task<string>> bodyFactory,
                                     string? destination = BodyDestinationFormat.SameAsSource, Encoding? encoding = null)
        => WithCallbackInternal(true, async req => new ResponseMessage {
            Timestamp = clock.GetUtcNow(),
            BodyData = new BodyData {
                BodyType = BodyType.String,
                ContentType = ContentTypes.Text,

                BodyAsString = await bodyFactory(req),
                Encoding = encoding ?? Encoding.UTF8,
                IsFuncUsed = "Func<IRequestMessage, Task<string>>"
            }
        });

    /// <inheritdoc />
    public IResponseBuilder WithBody(byte[] body, string? destination = BodyDestinationFormat.SameAsSource, Encoding? encoding = null)
    {
        ResponseMessage.BodyDestination = destination;
        ResponseMessage.BodyData = destination == BodyDestinationFormat.String
            ? new BodyData
            {
                BodyType = BodyType.String, ContentType = ContentTypes.Text,
                BodyAsString = encoding?.GetString(body) ?? Encoding.UTF8.GetString(body),
                Encoding = encoding ?? Encoding.UTF8
            }
            : new BodyData
            {
                BodyType = BodyType.Bytes, ContentType = ContentTypes.OctetStream,
                BodyAsBytes = body
            };
        return this;
    }

    /// <inheritdoc />
    public IResponseBuilder WithBodyFromFile(string filename, bool cache = true)
    {
        ResponseMessage.BodyData = new BodyData
        {
            BodyType = cache && !UseTransformer ? BodyType.Bytes : BodyType.File,
            ContentType = ContentTypes.OctetStream,
            BodyAsFileIsCached = cache,
            BodyAsFile = filename
        };
        return this;
    }

    /// <inheritdoc />
    public IResponseBuilder WithBody(string body, string? destination = BodyDestinationFormat.SameAsSource, Encoding? encoding = null) {
        encoding ??= Encoding.UTF8;

        ResponseMessage.BodyDestination = destination;
        ResponseMessage.BodyData =
            destination switch {
                BodyDestinationFormat.Bytes => new BodyData {
                    BodyType = BodyType.Bytes,
                    ContentType = ContentTypes.OctetStream,
                    BodyAsBytes = encoding.GetBytes(body)
                },
                BodyDestinationFormat.Json => new BodyData {
                    BodyType = BodyType.Json,
                    ContentType = ContentTypes.Json,
                    BodyAsJson = JsonUtils.DeserializeObject(body),
                    Encoding = encoding
                },

                _ => new BodyData {
                    BodyType = BodyType.String,
                    ContentType = ContentTypes.Text,
                    BodyAsString = body,
                    Encoding = encoding
                }
            };
        return this;
    }

    /// <inheritdoc />
    public IResponseBuilder WithBodyAsJson(object body, Encoding? encoding = null, bool? indented = null)
    {
        ResponseMessage.BodyDestination = null;
        ResponseMessage.BodyData = new BodyData
        {
            Encoding = encoding,
            BodyType = BodyType.Json,
            ContentType = ContentTypes.Json,
            BodyAsJson = body,
            BodyAsJsonIndented = indented
        };

        return this;
    }

    /// <inheritdoc />
    public IResponseBuilder WithBodyAsJson(object body, bool indented)
        => WithBodyAsJson(body, null, indented);

    /// <inheritdoc />
    public IResponseBuilder WithBodyAsJson(Func<IRequestMessage, object> bodyFactory, Encoding? encoding = null)
        => WithCallbackInternal(true, req => new ResponseMessage
        {
            Timestamp = clock.GetUtcNow(),
            BodyData = new BodyData
            {
                Encoding = encoding ?? Encoding.UTF8,
                BodyType = BodyType.Json,
                ContentType = ContentTypes.Json,
                BodyAsJson = bodyFactory(req),
                IsFuncUsed = "Func<IRequestMessage, object>"
            }
        });

    /// <inheritdoc />
    public IResponseBuilder WithBodyAsJson(Func<IRequestMessage, Task<object>> bodyFactory, Encoding? encoding = null)
        => WithCallbackInternal(true, async req => new ResponseMessage {
            Timestamp = clock.GetUtcNow(),
            BodyData = new BodyData {
                Encoding = encoding ?? Encoding.UTF8,
                BodyType = BodyType.Json,
                ContentType = ContentTypes.Json,
                BodyAsJson = await bodyFactory(req).ConfigureAwait(false),
                IsFuncUsed = "Func<IRequestMessage, Task<object>>"
            }
        });

    /// <inheritdoc />
    public IResponseBuilder WithBody(object body, IJsonConverter jsonConverter, JsonConverterOptions? options = null)
        => WithBody(body, null, jsonConverter, options);

    /// <inheritdoc />
    public IResponseBuilder WithBody(object body, Encoding? encoding, IJsonConverter jsonConverter, JsonConverterOptions? options = null)
    {
        ResponseMessage.BodyDestination = null;
        ResponseMessage.BodyData = new BodyData
        {
            Encoding = encoding,
            BodyType = BodyType.String,
            ContentType = ContentTypes.Text,
            BodyAsString = jsonConverter.Serialize(body, options)
        };

        return this;
    }

    /// <inheritdoc />
    public IResponseBuilder WithBodyAsProtoBuf(
        string protoDefinition,
        string messageType,
        object value,
        IJsonConverter? jsonConverter = null,
        JsonConverterOptions? options = null
    )
    {
        Guard.NotNullOrWhiteSpace(protoDefinition);
        Guard.NotNullOrWhiteSpace(messageType);
        Guard.NotNull(value);

        ResponseMessage.BodyDestination = null;
        ResponseMessage.BodyData = new BodyData
        {
            BodyType = BodyType.ProtoBuf,
            ContentType = ContentTypes.Json,
            BodyAsJson = value,
            ProtoDefinition = () => new (null, protoDefinition),
            ProtoBufMessageType = messageType
        };
        return this;
    }

    /// <inheritdoc />
    public IResponseBuilder WithBodyAsProtoBuf(
        string messageType,
        object value,
        IJsonConverter? jsonConverter = null,
        JsonConverterOptions? options = null
    )
    {
        ResponseMessage.BodyDestination = null;
        ResponseMessage.BodyData = new BodyData
        {
            BodyType = BodyType.ProtoBuf,
            ContentType = ContentTypes.Json,
            BodyAsJson = value,
            ProtoDefinition = () => Mapping.ProtoDefinition ?? throw new WireMockException("ProtoDefinition cannot be resolved. You probably forgot to call .WithProtoDefinition(...) on the mapping."),
            ProtoBufMessageType = messageType
        };
        return this;
    }
}