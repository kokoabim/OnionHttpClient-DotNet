namespace Kokoabim.OnionHttpClient;

public class OSValue<T>
{
    public T? Linux { get; set; }
    public T? MacOS { get; set; }
    public T? Value => OperatingSystem.IsMacOS() ? MacOS : OperatingSystem.IsLinux() ? Linux : OperatingSystem.IsWindows() ? Windows : default;
    public T? Windows { get; set; }
}