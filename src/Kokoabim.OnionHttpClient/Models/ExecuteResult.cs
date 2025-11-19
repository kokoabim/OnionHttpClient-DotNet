namespace Kokoabim.OnionHttpClient;

public class ExecuteResult
{
    public Exception? Exception { get; set; }
    public int ExitCode { get; set; } = -1;
    public bool Killed { get; set; }
    public string? Output { get; set; }

    public override string ToString() => Output ?? Exception?.GetMessages() ?? (Killed ? "Process was killed" : $"Exit code {ExitCode}");
}