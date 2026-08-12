namespace Kokoabim.OnionHttpClient;

public class MultiTorHttpClientInfo
{
    public Exception? Error { get; set; }
    public int ClientCount { get; set; }
    public bool DoesNotExistForUser { get; set; }
    public int RequestCount { get; set; }
    public TorHttpClientStatus Status { get; set; }
    public TorHttpClientInfo[] TorHttpClientsInfo { get; set; } = [];
}