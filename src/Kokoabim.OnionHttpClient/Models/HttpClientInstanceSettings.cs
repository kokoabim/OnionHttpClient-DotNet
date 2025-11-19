using System.Net;

namespace Kokoabim.OnionHttpClient;

public class HttpClientInstanceSettings : HttpClientCommonSettings
{
    public CookieContainer? CookieContainer { get; protected set; }
    public string? UserAgent { get; set; }

    public HttpClientInstanceSettings AddCommonOrDefaultSettings(HttpClientCommonSettings commonSettings)
    {
        // ! NOTE: do not set instance-specific settings like CookieContainer or UserAgent

        Accept = commonSettings.Accept ?? DefaultAccept;
        AllowAutoRedirect = commonSettings.AllowAutoRedirect ?? DefaultAllowAutoRedirect;
        AutomaticDecompression = commonSettings.AutomaticDecompression ?? DefaultAutomaticDecompression;
        BaseAddress = commonSettings.BaseAddress;
        SslProtocols = commonSettings.SslProtocols ?? DefaultSslProtocols;
        Timeout = commonSettings.Timeout ?? DefaultTimeout;
        UseCookies = commonSettings.UseCookies ?? DefaultUseCookies;

        if (commonSettings.UseCookies.HasValue && commonSettings.UseCookies.Value) CookieContainer = new CookieContainer();

        foreach (var kvp in commonSettings.DefaultHeaders)
        {
            DefaultHeaders[kvp.Key] = new HashSet<string>(kvp.Value, StringComparer.OrdinalIgnoreCase);
        }

        SetBaseAddressRelatedHeaders = commonSettings.SetBaseAddressRelatedHeaders;

        return this;
    }

    public static HttpClientInstanceSettings CreateWithCommonOrDefaultSettings(HttpClientCommonSettings commonSettings, string? userAgent)
    {
        var settings = new HttpClientInstanceSettings();

        _ = settings.AddCommonOrDefaultSettings(commonSettings);

        settings.UserAgent ??= userAgent;

        return settings;
    }
}