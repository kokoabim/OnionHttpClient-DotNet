namespace Kokoabim.OnionHttpClient;

public class MultiTorHttpClientSettings
{
    /// <summary>
    /// The strategy to use for balancing requests across multiple Tor HTTP clients. Default is RoundRobin.
    /// </summary>
    public MultiTorHttpClientBalanceStrategy BalanceStrategy { get; set; } = MultiTorHttpClientBalanceStrategy.RoundRobin;

    /// <summary>
    /// The number of Tor HTTP clients to create. Default is 1.
    /// </summary>
    public int ClientCount { get; set; } = 1;

    /// <summary>
    /// The maximum time in milliseconds to delay for an available Tor HTTP client when sending a request. Default is 25 ms.
    /// </summary>
    public int GetAvailableClientDelayOnSendMilliseconds { get; set; } = 25;

    /// <summary>
    /// The maximum time in milliseconds to timeout for an available Tor HTTP client when sending a request. Default is 10 seconds (10,000 ms).
    /// </summary>
    public int GetAvailableClientTimeoutOnSendMilliseconds { get; set; } = 10_000;

    /// <summary>
    /// Whether to randomize Tor ports for each Tor HTTP client. Default is true.
    /// </summary>
    public bool RandomizeTorPorts { get; set; } = true;

    /// <summary>
    /// If <see cref="RandomizeTorPorts"/> is false, the starting ControlPort for the first Tor instance. Subsequent instances will increment from this value. Default is 9000.
    /// </summary>
    public int StartingControlPort { get; set; } = 9000;

    /// <summary>
    /// If <see cref="RandomizeTorPorts"/> is false, the starting SocksPort for the first Tor instance. Subsequent instances will increment from this value. Default is 9500.
    /// </summary>
    public int StartingSocksPort { get; set; } = 9500;
}