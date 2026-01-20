namespace Kokoabim.OnionHttpClient;

public class TorHttpClientInfo
{
    public Exception? Error { get; set; }
    public TorHttpClientStatus HttpClientStatus { get; set; }
    public int Id { get; set; }
    public string? IPAddress { get; set; }
    public bool IsTorClientConnected { get; set; }
    public bool IsTorClientRunning { get; set; }
    public bool IsTorClientOK => IsTorClientRunning && IsTorClientConnected;
    public TorServiceStatus TorServiceStatus { get; set; }

    public static TorHttpClientInfo Create(
        ITorHttpClient torHttpClient,
        ITorService torService,
        TorClientStatus torClientStatus) =>
        new()
        {
            Error = torHttpClient.Error ?? torClientStatus.Error ?? torService.Error,
            HttpClientStatus = torHttpClient.Status,
            Id = torHttpClient.Id,
            IPAddress = torClientStatus.IPAddress,
            IsTorClientConnected = torClientStatus.IsConnected,
            IsTorClientRunning = torClientStatus.IsRunning,
            TorServiceStatus = torService.Status
        };
}
