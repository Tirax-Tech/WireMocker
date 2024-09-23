// Copyright © WireMock.Net

using System.Net;
using System.Net.Http;
using WireMock.HttpsCertificate;
using WireMock.Settings;

namespace WireMock.Http;

internal static class HttpClientBuilder
{
    public static HttpClient Build(HttpClientSettings settings)
    {
        var handler = new HttpClientHandler
        {
            CheckCertificateRevocationList = false,
            SslProtocols = System.Security.Authentication.SslProtocols.Tls12,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        if (!string.IsNullOrEmpty(settings.ClientX509Certificate2ThumbprintOrSubjectName))
        {
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;

            var x509Certificate2 = CertificateLoader.LoadCertificate(settings.ClientX509Certificate2ThumbprintOrSubjectName!);
            handler.ClientCertificates.Add(x509Certificate2);
        }
        else if (settings.Certificate != null)
        {
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            handler.ClientCertificates.Add(settings.Certificate);
        }

        handler.AllowAutoRedirect = settings.AllowAutoRedirect == true;

        // If UseCookies enabled, httpClient ignores Cookie header
        handler.UseCookies = false;

        if (settings.WebProxySettings != null)
        {
            handler.UseProxy = true;

            handler.Proxy = new WebProxy(settings.WebProxySettings.Address);
            if (settings.WebProxySettings.UserName != null && settings.WebProxySettings.Password != null)
                handler.Proxy.Credentials = new NetworkCredential(settings.WebProxySettings.UserName, settings.WebProxySettings.Password);
        }

        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        ServicePointManager.ServerCertificateValidationCallback = (_, _, _, _) => true;

        return HttpClientFactory2.Create(handler);
    }
}