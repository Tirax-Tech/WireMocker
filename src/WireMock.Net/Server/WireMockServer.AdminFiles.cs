// Copyright © WireMock.Net

using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using WireMock.Types;
using WireMock.Util;

namespace WireMock.Server;

public partial class WireMockServer
{
    private static readonly Encoding[] FileBodyIsString = { Encoding.UTF8, Encoding.ASCII };

    #region Files/{filename}
    private IResponseMessage FilePost(IRequestMessage requestMessage)
    {
        if (requestMessage.BodyAsBytes is null)
        {
            return ResponseMessageBuilder.Create(HttpStatusCode.BadRequest, "Body is null");
        }

        var filename = GetFileNameFromRequestMessage(requestMessage);

        var mappingFolder = settings.FileSystemHandler.GetMappingFolder();
        if (!settings.FileSystemHandler.FolderExists(mappingFolder))
        {
            settings.FileSystemHandler.CreateFolder(mappingFolder);
        }

        settings.FileSystemHandler.WriteFile(filename, requestMessage.BodyAsBytes);

        return ResponseMessageBuilder.Create(HttpStatusCode.OK, "File created");
    }

    private IResponseMessage FilePut(IRequestMessage requestMessage)
    {
        if (requestMessage.BodyAsBytes is null)
        {
            return ResponseMessageBuilder.Create(HttpStatusCode.BadRequest, "Body is null");
        }

        var filename = GetFileNameFromRequestMessage(requestMessage);

        if (!settings.FileSystemHandler.FileExists(filename))
        {
            settings.Logger.Info("The file '{0}' does not exist, updating file will be skipped.", filename);
            return ResponseMessageBuilder.Create(HttpStatusCode.NotFound, "File is not found");
        }

        settings.FileSystemHandler.WriteFile(filename, requestMessage.BodyAsBytes);

        return ResponseMessageBuilder.Create(HttpStatusCode.OK, "File updated");
    }

    private IResponseMessage FileGet(IRequestMessage requestMessage)
    {
        var filename = GetFileNameFromRequestMessage(requestMessage);

        if (!settings.FileSystemHandler.FileExists(filename))
        {
            settings.Logger.Info("The file '{0}' does not exist.", filename);
            return ResponseMessageBuilder.Create(HttpStatusCode.NotFound, "File is not found");
        }

        var bytes = settings.FileSystemHandler.ReadFile(filename);
        var response = new ResponseMessage
        {
            StatusCode = 200,
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
    private IResponseMessage FileHead(IRequestMessage requestMessage)
    {
        var filename = GetFileNameFromRequestMessage(requestMessage);

        if (!settings.FileSystemHandler.FileExists(filename))
        {
            settings.Logger.Info("The file '{0}' does not exist.", filename);
            return ResponseMessageBuilder.Create(HttpStatusCode.NotFound);
        }

        return ResponseMessageBuilder.Create(HttpStatusCode.NoContent);
    }

    private IResponseMessage FileDelete(IRequestMessage requestMessage)
    {
        var filename = GetFileNameFromRequestMessage(requestMessage);

        if (!settings.FileSystemHandler.FileExists(filename))
        {
            settings.Logger.Info("The file '{0}' does not exist.", filename);
            return ResponseMessageBuilder.Create(HttpStatusCode.NotFound, "File is not deleted");
        }

        settings.FileSystemHandler.DeleteFile(filename);
        return ResponseMessageBuilder.Create(HttpStatusCode.OK, "File deleted.");
    }

    private string GetFileNameFromRequestMessage(IRequestMessage requestMessage)
    {
        return Path.GetFileName(requestMessage.Path.Substring(_adminPaths!.Files.Length + 1));
    }
    #endregion
}