namespace Kokoabim.OnionHttpClient;

public enum TorHttpClientStatus
{
    Uninitialized,
    ConnectingToTor,
    FailedToConnectToTor,
    ConnectedToTor,
    ClientIsReady,
    RequestingCleanCircuits,
    Disconnected
}