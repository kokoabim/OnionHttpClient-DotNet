using System.Net;
using System.Security.Authentication;

namespace Kokoabim.OnionHttpClient;

public class HttpClientCommonSettings
{
    public const string DefaultAccept = "*/*";
    public const bool DefaultAllowAutoRedirect = true;
    public const DecompressionMethods DefaultAutomaticDecompression = DecompressionMethods.All;
    public const bool DefaultSetBaseAddressRelatedHeaders = false;
    public const SslProtocols DefaultSslProtocols = System.Security.Authentication.SslProtocols.None;
    public const bool DefaultUseCookies = true;

    public string? Accept { get; set; }
    public bool? AllowAutoRedirect { get; set; }
    public DecompressionMethods? AutomaticDecompression { get; set; }
    public string? BaseAddress { get; set; }
    public Dictionary<string, HashSet<string>> DefaultHeaders { get; set; } = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// If true, sets Host, Origin, and Referer headers based on BaseAddress.
    /// </summary>
    public bool SetBaseAddressRelatedHeaders { get; set; }

    public SslProtocols? SslProtocols { get; set; }
    public TimeSpan? Timeout { get; set; }
    public bool? UseCookies { get; set; }

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(100);

    public static HttpClientCommonSettings GetDefault() =>
        new()
        {
            Accept = DefaultAccept,
            AllowAutoRedirect = DefaultAllowAutoRedirect,
            AutomaticDecompression = DefaultAutomaticDecompression,
            SetBaseAddressRelatedHeaders = DefaultSetBaseAddressRelatedHeaders,
            SslProtocols = DefaultSslProtocols,
            Timeout = DefaultTimeout,
            UseCookies = DefaultUseCookies,
        };
}