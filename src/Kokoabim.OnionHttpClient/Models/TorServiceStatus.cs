namespace Kokoabim.OnionHttpClient;

public enum TorServiceStatus
{
    NotStarted,
    Starting,
    Connected,
    ExecutingControlCommand,
    Stopped,
    FailedToConnect
}