using System.Net;
using Microsoft.Extensions.Logging;

namespace Kokoabim.OnionHttpClient;

public interface IMultiTorHttpClient : IHttpClient
{
    int ClientCount { get; }
    IReadOnlyCollection<ITorHttpClient> Clients { get; }
    TorHttpClientStatus Status { get; }

    /// <summary>
    /// Stops the running Tor instances.
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Initializes the Multi Tor HTTP client with the specified settings.
    /// </summary>
    Task<bool> InitializeAsync(MultiTorHttpClientSettings multiTorHttpClientSettings, HttpClientCommonSettings httpClientCommonSettings, CancellationToken cancellationToken = default);
    /// <summary>
    /// Sets the cookies for the Multi Tor HTTP client.
    /// </summary>
    void SetCookies(IEnumerable<Cookie> cookies);
    bool SetHeader(string name, string value, bool overwrite = true);
    bool SetHeader(string name, IEnumerable<string> values, bool overwrite = true);
}

public class MultiTorHttpClient : IMultiTorHttpClient
{
    public int ClientCount => _clients.Count;
    public IReadOnlyCollection<ITorHttpClient> Clients => _clients.AsReadOnly();
    public int RequestCount => _clients.Sum(static c => c.RequestCount);
    public TorHttpClientStatus Status { get; private set; }

    private volatile int _clientIndex = -1;
    private readonly List<ITorHttpClient> _clients = [];
    private HttpClientCommonSettings? _httpClientCommonSettings;
    private readonly ILogger<MultiTorHttpClient> _logger;
    private MultiTorHttpClientSettings? _multiTorHttpClientSettings;
    private readonly ITorHttpClientFactory _torHttpClientFactory;

    public MultiTorHttpClient(ILogger<MultiTorHttpClient> logger, ITorHttpClientFactory torHttpClientFactory)
    {
        _logger = logger;
        _torHttpClientFactory = torHttpClientFactory;
    }

    #region methods

    /// <summary>
    /// Stops the running Tor instances.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (Status == TorHttpClientStatus.Uninitialized || ClientCount == 0) return;

        await Task.WhenAll(_clients.Select(c => c.DisconnectAsync(cancellationToken)));

        Status = TorHttpClientStatus.Disconnected;

        _logger.LogInformation("Multi Tor HTTP client disconnected");
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public IHttpClientResponse GetResponse(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var httpClientResponse = new HttpClientResponse(request);

        try
        {
            var response = Send(request, cancellationToken);
            httpClientResponse.SetHttpResponse(response);
        }
        catch (Exception ex)
        {
            httpClientResponse.Exception = ex;
        }

        return httpClientResponse;
    }

    public async Task<IHttpClientResponse> GetResponseAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var httpClientResponse = new HttpClientResponse(request);

        try
        {
            var response = await SendAsync(request, cancellationToken);
            httpClientResponse.SetHttpResponse(response);
        }
        catch (Exception ex)
        {
            httpClientResponse.Exception = ex;
        }

        return httpClientResponse;
    }

    public async Task<bool> InitializeAsync(MultiTorHttpClientSettings multiTorHttpClientSettings, HttpClientCommonSettings httpClientCommonSettings, CancellationToken cancellationToken = default)
    {
        if (Status != TorHttpClientStatus.Uninitialized) throw new InvalidOperationException($"Multi Tor HTTP client has already been initialized");
        if (multiTorHttpClientSettings.ClientCount < 1) throw new ArgumentOutOfRangeException(nameof(multiTorHttpClientSettings), "Client count must be at least 1.");

        Status = TorHttpClientStatus.ConnectingToTor;

        _multiTorHttpClientSettings = multiTorHttpClientSettings;
        _httpClientCommonSettings = httpClientCommonSettings;

        _logger.LogInformation("Initializing Multi Tor HTTP client with {ClientCount} Tor HTTP clients using {BalanceStrategy} balance strategy", multiTorHttpClientSettings.ClientCount, multiTorHttpClientSettings.BalanceStrategy);

        List<Task<(bool DidInitialize, ITorHttpClient TorHttpClient)>> tasks = [];

        for (var i = 0; i < multiTorHttpClientSettings.ClientCount; i++)
        {
            var task = multiTorHttpClientSettings.RandomizeTorPorts
                // Randomize ports and User-Agent
                ? _torHttpClientFactory.CreateAndInitializeAsync(httpClientCommonSettings, cancellationToken)
                // Use specified ports and random User-Agent (if not already set in common settings)
                : _torHttpClientFactory.CreateAndInitializeAsync(httpClientCommonSettings, new TorInstanceSettings(useRandomPorts: false)
                {
                    ControlPort = multiTorHttpClientSettings.StartingControlPort + i,
                    SocksPort = multiTorHttpClientSettings.StartingSocksPort + i,
                }, cancellationToken);

            tasks.Add(task);
        }

        _ = await Task.WhenAll(tasks);

        _clients.AddRange(tasks.Select(static t => t.Result.TorHttpClient));

        var initializedCount = tasks.Count(static t => t.Result.DidInitialize);
        var initializedAll = initializedCount == _multiTorHttpClientSettings.ClientCount;

        Status = initializedAll ? TorHttpClientStatus.ConnectedToTor : TorHttpClientStatus.FailedToConnectToTor;

        _logger.LogInformation("Multi Tor HTTP client initialized {InitializedCount} of {ClientCount} Tor HTTP clients", initializedCount, _multiTorHttpClientSettings.ClientCount);

        return initializedAll;
    }

    public HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        if (Status != TorHttpClientStatus.ClientIsReady) throw new InvalidOperationException($"Tor HTTP client is not ready");

        using var ctx = new CancellationTokenSource();
        ctx.CancelAfter(_multiTorHttpClientSettings!.GetAvailableClientTimeoutOnSendMilliseconds);

        ITorHttpClient client;
        do
        {
            client = _clients[GetNextClientIndex()];
            if (client.Status == TorHttpClientStatus.ClientIsReady) break;

            if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("No available Tor HTTP clients. Waiting for a client to become available...");

            try { Task.Delay(_multiTorHttpClientSettings!.GetAvailableClientDelayOnSendMilliseconds, ctx.Token).Wait(ctx.Token); } // 🤮 yuck.
            catch (Exception) { }

        } while (!cancellationToken.IsCancellationRequested && !ctx.IsCancellationRequested);

        if (ctx.IsCancellationRequested) throw new TimeoutException("Timed out waiting for an available Tor HTTP client.");
        else if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException("Operation was canceled while waiting for an available Tor HTTP client.", cancellationToken);

        return Send(request, cancellationToken);
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        if (Status != TorHttpClientStatus.ClientIsReady) throw new InvalidOperationException($"Tor HTTP client is not ready");

        using var ctx = new CancellationTokenSource();
        ctx.CancelAfter(_multiTorHttpClientSettings!.GetAvailableClientTimeoutOnSendMilliseconds);

        ITorHttpClient client;
        do
        {
            client = _clients[GetNextClientIndex()];
            if (client.Status == TorHttpClientStatus.ClientIsReady) break;

            if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("No available Tor HTTP clients. Waiting for a client to become available...");

            try { await Task.Delay(_multiTorHttpClientSettings!.GetAvailableClientDelayOnSendMilliseconds, ctx.Token); }
            catch (Exception) { }

        } while (!cancellationToken.IsCancellationRequested && !ctx.IsCancellationRequested);

        if (ctx.IsCancellationRequested) throw new TimeoutException("Timed out waiting for an available Tor HTTP client.");
        else if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException("Operation was canceled while waiting for an available Tor HTTP client.", cancellationToken);

        return await client.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Sets the cookies for the Multi Tor HTTP client.
    /// </summary>
    public void SetCookies(IEnumerable<Cookie> cookies)
    {
        foreach (var client in _clients) client.SetCookies(cookies);
    }

    public bool SetHeader(string name, string value, bool overwrite = true)
    {
        var result = true;

        foreach (var client in _clients)
        {
            var didSet = client.SetHeader(name, value, overwrite);
            if (!didSet) result = false;
        }

        return result;
    }

    public bool SetHeader(string name, IEnumerable<string> values, bool overwrite = true)
    {
        var result = true;

        foreach (var client in _clients)
        {
            var didSet = client.SetHeader(name, values, overwrite);
            if (!didSet) result = false;
        }

        return result;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var client in _clients) client.Dispose();
            _clients.Clear();
        }
    }

    ~MultiTorHttpClient()
    {
        Dispose(disposing: false);
    }

    private int GetNextClientIndex() => _multiTorHttpClientSettings!.BalanceStrategy switch
    {
        MultiTorHttpClientBalanceStrategy.RoundRobin => Interlocked.Increment(ref _clientIndex) % _clients.Count,
        MultiTorHttpClientBalanceStrategy.Random => Random.Shared.Next(0, _clients.Count),
        _ => throw new NotSupportedException($"Balance strategy {_multiTorHttpClientSettings.BalanceStrategy} is not supported"),
    };

    #endregion
}