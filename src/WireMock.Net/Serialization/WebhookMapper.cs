// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System;
using System.Collections.Generic;
using System.Linq;
using WireMock.Admin.Mappings;
using WireMock.Http;
using WireMock.Models;
using WireMock.Types;
using WireMock.Util;

namespace WireMock.Serialization;

static class WebhookMapper
{
    public static IWebhook Map(WebhookModel model) {
        var webhook = new Webhook {
            Request = new WebhookRequest {
                Url = model.Request.Url,
                Method = model.Request.Method,
                Delay = model.Request.Delay,
                MinimumRandomDelay = model.Request.MinimumRandomDelay,
                MaximumRandomDelay = model.Request.MaximumRandomDelay,
                Headers = model.Request.Headers?.ToDictionary(x => x.Key, x => new WireMockList<string>(x.Value)) ??
                          new Dictionary<string, WireMockList<string>>()
            }
        };

        if (model.Request.UseTransformer == true){
            webhook.Request.UseTransformer = true;

            if (!Enum.TryParse<TransformerType>(model.Request.TransformerType, out var transformerType))
                transformerType = TransformerType.Handlebars;
            webhook.Request.TransformerType = transformerType;

            if (!Enum.TryParse<ReplaceNodeOptions>(model.Request.TransformerReplaceNodeOptions, out var option))
                option = ReplaceNodeOptions.EvaluateAndTryToConvert;
            webhook.Request.TransformerReplaceNodeOptions = option;
        }

        IEnumerable<string>? contentTypeHeader = null;
        if (webhook.Request.Headers != null &&
            webhook.Request.Headers.Any(header => string.Equals(header.Key, HttpKnownHeaderNames.ContentType,
                                                                StringComparison.OrdinalIgnoreCase)))
            contentTypeHeader = webhook.Request.Headers
                                       .First(header => string.Equals(header.Key, HttpKnownHeaderNames.ContentType,
                                                                      StringComparison.OrdinalIgnoreCase)).Value;

        if (model.Request.Body != null)
            webhook.Request.BodyData = new BodyData {
                BodyAsString = model.Request.Body,
                BodyType = contentTypeHeader?.FirstOrDefault().BindValue(v => BodyParser.DetectBodyTypeFromContentType(v, encoding: null)) ?? BodyType.String,
                ContentType = ContentTypes.Text
            };
        else if (model.Request.BodyAsJson != null)
            webhook.Request.BodyData = new BodyData {
                BodyAsJson = model.Request.BodyAsJson,
                BodyType = contentTypeHeader?.FirstOrDefault().BindValue(v => BodyParser.DetectBodyTypeFromContentType(v, encoding: null)) ?? BodyType.Json,
                ContentType = ContentTypes.Json
            };

        return webhook;
    }

    public static WebhookModel Map(IWebhook webhook) {
        var model = new WebhookModel {
            Request = new WebhookRequestModel {
                Url = webhook.Request.Url,
                Method = webhook.Request.Method,
                Headers = webhook.Request.Headers?.ToDictionary(x => x.Key, x => x.Value.ToString()),
                UseTransformer = webhook.Request.UseTransformer,
                TransformerType = webhook.Request.UseTransformer == true ? webhook.Request.TransformerType.ToString() : null,
                TransformerReplaceNodeOptions = webhook.Request.TransformerReplaceNodeOptions.ToString(),
                Delay = webhook.Request.Delay,
                MinimumRandomDelay = webhook.Request.MinimumRandomDelay,
                MaximumRandomDelay = webhook.Request.MaximumRandomDelay,
            }
        };

        switch (webhook.Request.BodyData?.BodyType){
            case BodyType.String:
            case BodyType.FormUrlEncoded:
                model.Request.Body = webhook.Request.BodyData.BodyAsString;
                break;

            case BodyType.Json:
                model.Request.BodyAsJson = webhook.Request.BodyData.BodyAsJson;
                break;
        }

        return model;
    }
}