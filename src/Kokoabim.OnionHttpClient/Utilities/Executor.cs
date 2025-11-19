using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Kokoabim.OnionHttpClient;

public interface IExecutor
{
    ExecuteResult Execute(string fileName, string? arguments = null, string? workingDirectory = null, Action<string>? outputHandler = null, CancellationToken cancellationToken = default);
    Task<ExecuteResult> ExecuteAsync(string fileName, string? arguments = null, string? workingDirectory = null, Action<string>? outputHandler = null, CancellationToken cancellationToken = default);
}

public class Executor : IExecutor
{
    public ExecuteResult Execute(string fileName, string? arguments = null, string? workingDirectory = null, Action<string>? outputHandler = null, CancellationToken cancellationToken = default)
    {
        var execResult = new ExecuteResult();
        var output = new StringBuilder();

        using Process process = new();

        if (cancellationToken != default) _ = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                    execResult.Killed = true;
                }
            }
            catch { }
        });

        process.StartInfo = new()
        {
            Arguments = arguments,
            CreateNoWindow = true,
            FileName = fileName,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data is not null)
            {
                if (outputHandler == null) output.AppendLine(e.Data);
                else outputHandler(e.Data);
            }
        };

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data is not null)
            {
                if (outputHandler == null) output.AppendLine(e.Data);
                else outputHandler(e.Data);
            }
        };

        try
        {

            if (!process.Start())
            {
                execResult.Exception = new InvalidOperationException($"Failed to start process: {fileName}{(string.IsNullOrWhiteSpace(workingDirectory) ? null : $" ({workingDirectory})")}");
                return execResult;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            execResult.ExitCode = process.ExitCode;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 0x00000002)
        {
            execResult.Exception = new FileNotFoundException($"File not found: {fileName}{(string.IsNullOrWhiteSpace(workingDirectory) ? null : $" ({workingDirectory})")}", ex);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 0x00000005)
        {
            execResult.Exception = new UnauthorizedAccessException($"Access denied: {fileName}{(string.IsNullOrWhiteSpace(workingDirectory) ? null : $" ({workingDirectory})")}", ex);
        }
        catch (Exception ex)
        {
            execResult.Exception = ex;
        }

        execResult.Output = output.ToString().TrimEnd();
        return execResult;
    }

    public Task<ExecuteResult> ExecuteAsync(string fileName, string? arguments = null, string? workingDirectory = null, Action<string>? outputHandler = null, CancellationToken cancellationToken = default) =>
        Task.Run(() => Execute(fileName, arguments, workingDirectory, outputHandler, cancellationToken));
}