// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using WireMock.Types;
using WireMock.Util;

namespace WireMock.Server;

public partial class WireMockServer
{
    static readonly Encoding[] FileBodyIsString = [Encoding.UTF8, Encoding.ASCII];

    #region Files/{filename}

    ResponseMessage FilePost(IRequestMessage requestMessage)
    {
        if (requestMessage.BodyAsBytes is null)
            return CreateResponse(HttpStatusCode.BadRequest, "Body is null");

        var filename = GetFileNameFromRequestMessage(requestMessage);

        var mappingFolder = settings.FileSystemHandler.GetMappingFolder();
        if (!settings.FileSystemHandler.FolderExists(mappingFolder))
            settings.FileSystemHandler.CreateFolder(mappingFolder);

        settings.FileSystemHandler.WriteFile(filename, requestMessage.BodyAsBytes);

        return CreateResponse(HttpStatusCode.OK, "File created");
    }

    ResponseMessage FilePut(IRequestMessage requestMessage)
    {
        if (requestMessage.BodyAsBytes is null)
            return CreateResponse(HttpStatusCode.BadRequest, "Body is null");

        var filename = GetFileNameFromRequestMessage(requestMessage);

        if (!settings.FileSystemHandler.FileExists(filename))
        {
            settings.Logger.Info("The file '{0}' does not exist, updating file will be skipped.", filename);
            return CreateResponse(HttpStatusCode.NotFound, "File is not found");
        }

        settings.FileSystemHandler.WriteFile(filename, requestMessage.BodyAsBytes);

        return CreateResponse(HttpStatusCode.OK, "File updated");
    }

    ResponseMessage FileGet(IRequestMessage requestMessage)
    {
        var filename = GetFileNameFromRequestMessage(requestMessage);

        if (!settings.FileSystemHandler.FileExists(filename))
        {
            settings.Logger.Info("The file '{0}' does not exist.", filename);
            return CreateResponse(HttpStatusCode.NotFound, "File is not found");
        }

        var bytes = settings.FileSystemHandler.ReadFile(filename);
        var response = new ResponseMessage
        {
            Timestamp = clock.GetUtcNow(),
            StatusCode = HttpStatusCode.OK,
            BodyData = new BodyData
            {
                BodyAsBytes = bytes,
                DetectedBodyType = BodyType.Bytes,
                DetectedBodyTypeFromContentType = BodyType.None
            }
        };

        if (BytesEncodingUtils.TryGetEncoding(bytes, out var encoding) && FileBodyIsString.Select(x => x.Equals(encoding)).Any())
        {
            response.BodyData.DetectedBodyType = BodyType.String;
            response.BodyData.BodyAsString = encoding.GetString(bytes);
        }

        return response;
    }

    /// <summary>
    /// Checks if file exists.
    /// Note: Response is returned with no body as a head request doesn't accept a body, only the status code.
    /// </summary>
    /// <param name="requestMessage">The request message.</param>
    ResponseMessage FileHead(IRequestMessage requestMessage)
    {
        var filename = GetFileNameFromRequestMessage(requestMessage);

        if (!settings.FileSystemHandler.FileExists(filename))
        {
            settings.Logger.Info("The file '{0}' does not exist.", filename);
            return CreateResponse(HttpStatusCode.NotFound);
        }

        return CreateResponse(HttpStatusCode.NoContent);
    }

    ResponseMessage FileDelete(IRequestMessage requestMessage)
    {
        var filename = GetFileNameFromRequestMessage(requestMessage);

        if (!settings.FileSystemHandler.FileExists(filename))
        {
            settings.Logger.Info("The file '{0}' does not exist.", filename);
            return CreateResponse(HttpStatusCode.NotFound, "File is not deleted");
        }

        settings.FileSystemHandler.DeleteFile(filename);
        return CreateResponse(HttpStatusCode.OK, "File deleted.");
    }

    string GetFileNameFromRequestMessage(IRequestMessage requestMessage)
        => Path.GetFileName(requestMessage.Path[(adminPaths!.Files.Length + 1)..]);

    #endregion
}