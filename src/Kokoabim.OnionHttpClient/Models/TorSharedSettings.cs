namespace Kokoabim.OnionHttpClient;

public class TorSharedSettings
{
    public string Arguments { get; set; } = null!;
    public string CheckStatusUrl { get; set; } = "https://check.torproject.org/api/ip";
    public string ControlPassword { get; set; } = null!;
    public string CurrentDirectory { get; } = AppContext.BaseDirectory;
    public string DefaultConfigPath { get; set; } = null!;
    public OSValue<string> ExecutablePath { get; set; } = new() { Linux = "/usr/bin/tor", MacOS = "/opt/homebrew/bin/tor" };
}