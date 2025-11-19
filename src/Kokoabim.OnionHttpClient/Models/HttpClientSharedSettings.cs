namespace Kokoabim.OnionHttpClient;

public class HttpClientSharedSettings : HttpClientCommonSettings
{
    public string[] UserAgents { get; set; } = [];
}