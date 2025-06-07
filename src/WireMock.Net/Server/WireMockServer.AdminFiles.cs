// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using WireMock.Models;
using WireMock.Types;
using WireMock.Util;

namespace WireMock.Server;

public partial class WireMockServer
{
    static readonly Encoding[] FileBodyIsString = [Encoding.UTF8, Encoding.ASCII];

    #region Files/{filename}

    public ResponseMessage FilePost(IRequestMessage requestMessage)
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

    public ResponseMessage FilePut(IRequestMessage requestMessage)
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

    public ResponseMessage FileGet(IRequestMessage requestMessage)
    {
        var filename = GetFileNameFromRequestMessage(requestMessage);

        if (!settings.FileSystemHandler.FileExists(filename))
        {
            settings.Logger.Info("The file '{0}' does not exist.", filename);
            return CreateResponse(HttpStatusCode.NotFound, "File is not found");
        }

        var bytes = settings.FileSystemHandler.ReadFile(filename);
        var encoding = BytesEncodingUtils.TryGetEncoding(bytes);
        var isText = encoding is not null && FileBodyIsString.Select(x => x.Equals(encoding)).Any();

        return new ResponseMessage {
            Timestamp = clock.GetUtcNow(),
            StatusCode = HttpStatusCode.OK,
            BodyData = new BodyData {
                BodyAsBytes = bytes,
                BodyType = isText ? BodyType.String : BodyType.Bytes,
                ContentType = isText ? ContentTypes.Text : ContentTypes.OctetStream,
                BodyAsString = isText ? encoding?.GetString(bytes) : null
            }
        };
    }

    /// <summary>
    /// Checks if file exists.
    /// Note: Response is returned with no body as a head request doesn't accept a body, only the status code.
    /// </summary>
    /// <param name="requestMessage">The request message.</param>
    public ResponseMessage FileHead(IRequestMessage requestMessage)
    {
        var filename = GetFileNameFromRequestMessage(requestMessage);

        if (!settings.FileSystemHandler.FileExists(filename))
        {
            settings.Logger.Info("The file '{0}' does not exist.", filename);
            return CreateResponse(HttpStatusCode.NotFound);
        }

        return CreateResponse(HttpStatusCode.NoContent);
    }

    public ResponseMessage FileDelete(IRequestMessage requestMessage)
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

    string GetFileNameFromRequestMessage(IRequestMessage requestMessage) {
        var adminPaths = new AdminPaths(settings.AdminPath);
        return Path.GetFileName(requestMessage.Path[(adminPaths.Files.Length + 1)..]);
    }

    #endregion
}