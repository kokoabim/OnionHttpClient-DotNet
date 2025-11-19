namespace Kokoabim.OnionHttpClient;

public class TorInstanceSettings
{
    public int ControlPort { get; set; } = 9051;
    public string? DataDirectory { get; set; }
    public int SocksPort { get; set; } = 9050;

    /// <summary>
    /// Initializes a new instance of the <see cref="TorInstanceSettings"/> class.
    /// </summary>
    /// <param name="useRandomPorts">If true, assigns random ports for ControlPort (between 9000 and 9499) and SocksPort (between 9500 and 9999). Otherwise, uses default ports (9050 for SocksPort and 9051 for ControlPort).</param>
    /// <param name="useRandomDataDirectory">If true, assigns a random temporary data directory for Tor. Otherwise, leaves DataDirectory as null (which is not added to the Tor arguments on startup).</param>
    public TorInstanceSettings(bool useRandomPorts = true, bool useRandomDataDirectory = true)
    {
        if (useRandomPorts)
        {
            ControlPort = Randoms.UniqueInt(9000, 9499);
            SocksPort = Randoms.UniqueInt(9500, 9999);
        }

        if (useRandomDataDirectory) DataDirectory ??= Path.Combine(Path.GetTempPath(), nameof(OnionHttpClient), Randoms.UniqueString(8));
    }
}