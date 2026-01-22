using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Kokoabim.OnionHttpClient;

public interface ITorService : IDisposable, IAsyncDisposable
{
    Exception? Error { get; }
    /// <summary>
    /// If a control command is being executed, contains the command being executed. Otherwise, null.
    /// </summary>
    string? ExecutingControlCommand { get; }
    /// <summary>
    /// Indicates whether the Tor service is currently running (i.e., starting, connected, or executing a control command).
    /// </summary>
    bool IsRunning { get; }
    ExecuteResult? Result { get; }
    TorServiceStatus Status { get; }

    /// <summary>
    /// Executes a control command against the running Tor instance. See https://spec.torproject.org/control-spec/commands.html for details.
    /// </summary>
    Task<ExecuteResult> ExecuteControlCommandAsync(string command, CancellationToken cancellationToken = default);
    /// <summary>
    /// Gets the status of the running Tor instance by calling https://check.torproject.org/api/ip and parsing its response.
    /// </summary>
    Task<TorClientStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<bool> RequestCleanCircuitsAsync(CancellationToken cancellationToken = default);
    Task<ExecuteResult> RunAsync(TorInstanceSettings settings, Action<string>? outputHandler = null, CancellationToken cancellationToken = default);
    Task<bool> StartAsync(TorInstanceSettings settings, Action<string>? outputHandler = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Stops the running Tor instance.
    /// </summary>
    Task<ExecuteResult?> StopAsync(CancellationToken cancellationToken = default);
    Task<bool> WaitForStartupAsync(CancellationToken cancellationToken = default);
}

public class TorService : ITorService
{
    public Exception? Error { get; private set; }

    /// <summary>
    /// If a control command is being executed, contains the command being executed. Otherwise, null.
    /// </summary>
    public string? ExecutingControlCommand { get; private set; }

    /// <summary>
    /// Indicates whether the Tor service is currently running (i.e., starting, connected, or executing a control command).
    /// </summary>
    public bool IsRunning => Status is TorServiceStatus.Starting or TorServiceStatus.Connected or TorServiceStatus.ExecutingControlCommand;

    public ExecuteResult? Result { get; private set; }
    public TorServiceStatus Status { get; private set; }

    private CancellationTokenSource? _cts;
    private readonly IExecutor _executor;
    private TorHttpClientHandlerSettings? _getStatusHandlerSettings;
    private AsyncManualResetEvent? _resetEvent;
    private Task<ExecuteResult>? _runningTask;
    private TorInstanceSettings? _torInstanceSettings;
    private readonly TorSharedSettings _torSharedSettings;

    public TorService(IOptions<TorSharedSettings> sharedSettings, IExecutor executor)
    {
        _torSharedSettings = sharedSettings.Value;
        _executor = executor;
    }

    #region methods

    /// <summary>
    /// Executes a control command against the running Tor instance. See https://spec.torproject.org/control-spec/commands.html for details.
    /// </summary>
    public Task<ExecuteResult> ExecuteControlCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        ExecutingControlCommand = command;
        return _executor
            .ExecuteAsync("bash", $"-c \"printf '{TorControlCommand.Authenticate} \\\"{_torSharedSettings.ControlPassword}\\\"\\r\\n{command}\\r\\n' | nc localhost {_torInstanceSettings!.ControlPort}\"", cancellationToken: cancellationToken)
            .ContinueWith(t =>
            {
                ExecutingControlCommand = null;
                return t.Result;
            });
    }

    /// <summary>
    /// Gets the status of the running Tor instance by calling https://check.torproject.org/api/ip (by default) and parsing its response.
    /// </summary>
    public async Task<TorClientStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning) return new TorClientStatus()
        {
            Error = Error ?? new InvalidOperationException("Tor service is not running"),
            TorServiceStatus = Status
        };

        try
        {
            using var handler = new TorHttpClientHandler(_getStatusHandlerSettings!);
            using var httpClient = new HttpClient(handler);

            var response = await httpClient.GetStringAsync(_torSharedSettings.CheckStatusUrl, cancellationToken);
            var jsonDoc = JsonDocument.Parse(response);

            return new TorClientStatus
            {
                IPAddress = jsonDoc.RootElement.GetProperty("IP").GetString() ?? string.Empty,
                IsConnected = jsonDoc.RootElement.GetProperty("IsTor").GetBoolean(),
                IsRunning = true,
                TorServiceStatus = Status
            };
        }
        catch (Exception ex)
        {
            return new TorClientStatus
            {
                Error = ex,
                IsRunning = true,
                TorServiceStatus = Status
            };
        }
    }

    public async Task<bool> RequestCleanCircuitsAsync(CancellationToken cancellationToken = default)
    {
        if (Status != TorServiceStatus.Connected) throw new InvalidOperationException("Tor service is not connected");

        Status = TorServiceStatus.ExecutingControlCommand;

        var controlCommandResult = await ExecuteControlCommandAsync(TorControlCommand.CleanCircuits, cancellationToken);

        Status = TorServiceStatus.Connected;

        var output = (controlCommandResult.Output ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct();

        if (output.SequenceEqual(["250 OK"])) return true;

        Error = new InvalidOperationException($"Tor control command failed: {string.Join("; ", output)}");
        return false;
    }

    public Task<ExecuteResult> RunAsync(TorInstanceSettings settings, Action<string>? outputHandler = null, CancellationToken cancellationToken = default) =>
        !IsRunning ? ExecuteAsync(settings, outputHandler, cancellationToken) : throw new InvalidOperationException("Tor service is already running");

    public Task<bool> StartAsync(TorInstanceSettings settings, Action<string>? outputHandler = null, CancellationToken cancellationToken = default)
    {
        if (IsRunning) throw new InvalidOperationException("Tor service is already running");

        _ = ExecuteAsync(settings, outputHandler, cancellationToken);

        return WaitForStartupAsync(cancellationToken);
    }

    /// <summary>
    /// Stops the running Tor instance.
    /// </summary>
    public async Task<ExecuteResult?> StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning) return Result;

        _cts?.Cancel();

        if (_runningTask != null && !_runningTask.IsCompleted) _ = await _runningTask.WaitAsync(cancellationToken);

        return Result;
    }

    public async Task<bool> WaitForStartupAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning) return false;

        using var ctx = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts!.Token);

        await _resetEvent!.WaitAsync(ctx.Token);

        return Status == TorServiceStatus.Connected;
    }

    ~TorService()
    {
        Dispose(disposing: false);
    }

    private void DeleteDataDirectory()
    {
        if (string.IsNullOrWhiteSpace(_torInstanceSettings?.DataDirectory) || !Directory.Exists(_torInstanceSettings.DataDirectory)) return;

        try { Directory.Delete(_torInstanceSettings.DataDirectory, recursive: true); }
        catch { }
    }

    private async Task<ExecuteResult> ExecuteAsync(TorInstanceSettings torInstanceSettings, Action<string>? outputHandler = null, CancellationToken cancellationToken = default)
    {
        Status = TorServiceStatus.Starting;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _getStatusHandlerSettings = TorHttpClientHandlerSettings.ForTorStatus(torInstanceSettings);
        _torInstanceSettings = torInstanceSettings;
        _resetEvent = new AsyncManualResetEvent();

        var internalOutputHandler = new Action<string>(data =>
        {
            outputHandler?.Invoke(data);

            if (data.Contains("Bootstrapped 100%", StringComparison.OrdinalIgnoreCase))
            {
                Status = TorServiceStatus.Connected;
                _ = _resetEvent.Set();
            }
            else if (_resetEvent.IsSet == false
                && (data.Contains("Could not bind", StringComparison.OrdinalIgnoreCase) || data.Contains("Address already in use", StringComparison.OrdinalIgnoreCase)))
            {
                Error = new InvalidOperationException($"Tor service could not bind to the specified port(s): {data}");
                Status = TorServiceStatus.FailedToConnect;
                _ = _resetEvent.Set();
            }
            else if (_resetEvent.IsSet == false
                && data.Contains("Dying.", StringComparison.OrdinalIgnoreCase))
            {
                Error = new InvalidOperationException($"Tor service failed to start: {data}");
                Status = TorServiceStatus.FailedToConnect;
                _ = _resetEvent.Set();
            }
            else if (_resetEvent.IsSet == false
                && data.Contains("Closing partially-constructed", StringComparison.OrdinalIgnoreCase))
            {
                Error = new InvalidOperationException($"Tor service failed to start: {data}");
                Status = TorServiceStatus.FailedToConnect;
                _ = _resetEvent.Set();
            }
        });

        if (!string.IsNullOrWhiteSpace(torInstanceSettings.DataDirectory))
        {
            try
            {
                if (!Directory.Exists(torInstanceSettings.DataDirectory)) _ = Directory.CreateDirectory(torInstanceSettings.DataDirectory);
            }
            catch (Exception ex)
            {
                Error = new InvalidOperationException($"Failed to create Tor data directory: {torInstanceSettings.DataDirectory}", ex);
                Status = TorServiceStatus.FailedToConnect;
                _ = _resetEvent.Set();
                return new ExecuteResult
                {
                    Exception = Error,
                };
            }
        }

        _runningTask = _executor.ExecuteAsync(
            _torSharedSettings.ExecutablePath.Value ?? throw new InvalidOperationException("Tor executable path is not set"),
            GenerateArguments(torInstanceSettings),
            null,
            internalOutputHandler,
            _cts.Token).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Error = new InvalidOperationException("Tor service failed to start", t.Exception);
                    Status = TorServiceStatus.FailedToConnect;
                    Result = new ExecuteResult
                    {
                        Exception = t.Exception,
                    };
                }
                else if (t.Status == TaskStatus.RanToCompletion)
                {
                    if (Status != TorServiceStatus.FailedToConnect) Status = TorServiceStatus.Stopped;

                    Result ??= t.Result;

                    if (Result.Exception != null && Error == null)
                    {
                        Error = Result.Exception;
                        Status = TorServiceStatus.FailedToConnect;
                    }
                }

                DeleteDataDirectory();

                return Result ?? new ExecuteResult();
            });

        await _resetEvent.WaitAsync(cancellationToken);

        return await _runningTask;
    }

    private string GenerateArguments(TorInstanceSettings instanceSettings)
    {
        var args = _torSharedSettings.Arguments
            .Replace("{cwd}", _torSharedSettings.CurrentDirectory)
            .Replace("{socksPort}", instanceSettings.SocksPort.ToString())
            .Replace("{controlPort}", instanceSettings.ControlPort.ToString());

        if (!string.IsNullOrWhiteSpace(instanceSettings.DataDirectory)) args += $" --DataDirectory \"{instanceSettings.DataDirectory}\"";

        return args;
    }

    #endregion

    #region IDisposable

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts?.Cancel();
            _resetEvent?.Dispose();
            _cts?.Dispose();

            DeleteDataDirectory();
        }
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        await StopAsync().ConfigureAwait(false); // will cancel _cts
        _resetEvent?.Dispose();
        _cts?.Dispose();

        DeleteDataDirectory();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    #endregion
}