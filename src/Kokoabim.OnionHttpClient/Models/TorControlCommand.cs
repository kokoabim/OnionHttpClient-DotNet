namespace Kokoabim.OnionHttpClient;

public static class TorControlCommand
{
    public const string Authenticate = "AUTHENTICATE";
    public const string CleanCircuits = "SIGNAL NEWNYM";
    public const string ControlledShutdown = "SIGNAL SHUTDOWN";
    public const string DumpStats = "DEBUG DUMP";
    public const string ImmediateShutdown = "SIGNAL HALT";
}