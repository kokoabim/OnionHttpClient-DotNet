namespace Kokoabim.OnionHttpClient;

public class TorHttpClientHandlerSettings : HttpClientInstanceSettings
{
    /// <summary>
    /// The number of minutes after which to request new clean circuits (new identity). Set to 0 to disable automatic circuit renewal based on time. Default is 0.
    /// </summary>
    public int RequestCleanCircuitsAfterMinutes { get; set; }

    /// <summary>
    /// The number of requests after which to request new clean circuits (new identity). Set to 0 to disable automatic circuit renewal based on request count. Default is 0.
    /// </summary>
    public int RequestCleanCircuitsAfterRequestCount { get; set; }

    /// <summary>
    /// The port number of the SOCKS5 proxy that the Tor instance is listening on.
    /// </summary>
    public int SocksPort { get; set; }

    public static TorHttpClientHandlerSettings ForTorStatus(TorInstanceSettings torInstanceSettings)
    {
        var settings = FromCommonOrDefaultSettings(GetDefault(), torInstanceSettings.SocksPort);
        settings.UseCookies = false;

        return settings;
    }

    public static TorHttpClientHandlerSettings FromCommonOrDefaultSettings(HttpClientCommonSettings httpClientCommonSettings, int socksPort)
    {
        var settings = new TorHttpClientHandlerSettings
        {
            AllowAutoRedirect = httpClientCommonSettings?.AllowAutoRedirect ?? DefaultAllowAutoRedirect,
            AutomaticDecompression = httpClientCommonSettings?.AutomaticDecompression ?? DefaultAutomaticDecompression,
            SocksPort = socksPort,
            SslProtocols = httpClientCommonSettings?.SslProtocols ?? DefaultSslProtocols,
            UseCookies = httpClientCommonSettings?.UseCookies ?? DefaultUseCookies,
        };

        if (settings.UseCookies.HasValue && settings.UseCookies.Value) settings.CookieContainer = new System.Net.CookieContainer();

        return settings;
    }
}