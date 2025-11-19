namespace Kokoabim.OnionHttpClient;

public class TorClientStatus
{
    public Exception? Error { get; set; }
    public string? IPAddress { get; set; }
    public bool IsConnected { get; set; }
    public bool IsRunning { get; set; }
    public bool Success => IsRunning && IsConnected && Error == null;

    public override string ToString() =>
        Error != null
            ? Error.GetMessages()
            : !IsRunning
            ? "Tor client is not running"
            : $"Tor client is{(IsConnected ? "" : " not")} connected to the Tor network (IP address {IPAddress})";
}