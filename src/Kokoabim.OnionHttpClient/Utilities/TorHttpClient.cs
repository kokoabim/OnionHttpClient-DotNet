using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kokoabim.OnionHttpClient;

public interface ITorHttpClient : IHttpClient
{
    Exception? Error { get; }
    int Id { get; }
    string? IPAddress { get; }
    TorHttpClientStatus Status { get; }

    /// <summary>
    /// Stops the running Tor instance.
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Gets the status of the running Tor instance by calling https://check.torproject.org/api/ip (by default) and parsing its response.
    /// </summary>
    Task<bool> GetTorClientStatusAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Initializes the Tor HTTP client with the specified settings.
    /// </summary>
    Task<bool> InitializeAsync(HttpClientInstanceSettings httpClientSettings, TorInstanceSettings torSettings, CancellationToken cancellationToken = default);
    /// <summary>
    /// Initializes the Tor HTTP client with the specified settings.
    /// </summary>
    Task<bool> InitializeAsync(int id, HttpClientInstanceSettings httpClientSettings, TorInstanceSettings torSettings, CancellationToken cancellationToken = default);
    /// <summary>
    /// Requests new Tor circuits (new identity). Tor MAY rate-limit its response to this signal.
    /// </summary>
    Task<bool> RequestCleanCircuitsAsync(CancellationToken cancellationToken = default);
}

public class TorHttpClient : ITorHttpClient
{
    public const int NoId = -1;

    public Exception? Error { get; private set; }
    public int Id { get; private set; } = NoId;
    public string? IPAddress { get; private set; }
    public int RequestCount => _requestHandler?.RequestCount ?? 0;
    public TorHttpClientStatus Status { get; private set; }

    private HttpClient? _httpClient;
    private HttpClientInstanceSettings? _httpClientInstanceSettings;
    private readonly HttpClientSharedSettings _httpClientSharedSettings;
    private readonly ILogger<TorHttpClient> _logger;
    private TorHttpClientHandler? _requestHandler;
    private TorInstanceSettings? _torInstanceSettings;
    private ITorService? _torService;

    public TorHttpClient(ILogger<TorHttpClient> logger, IOptions<HttpClientSharedSettings> sharedSettings, ITorService torService)
    {
        _logger = logger;
        _httpClientSharedSettings = sharedSettings.Value;
        _torService = torService;
    }

    #region methods

    /// <summary>
    /// Stops the running Tor instance.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (Status == TorHttpClientStatus.Uninitialized) return;

        _ = await _torService!.StopAsync(cancellationToken);

        Status = TorHttpClientStatus.Disconnected;

        Log(LogLevel.Information, "Tor HTTP client disconnected");
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

    /// <summary>
    /// Gets the status of the running Tor instance by calling https://check.torproject.org/api/ip (by default) and parsing its response.
    /// </summary>
    public async Task<bool> GetTorClientStatusAsync(CancellationToken cancellationToken = default)
    {
        if (Status == TorHttpClientStatus.Uninitialized) return false;

        var torClientStatus = await _torService!.GetStatusAsync(cancellationToken);

        IPAddress = torClientStatus.IPAddress;

        if (torClientStatus.Success) return true;

        Error = new Exception($"Tor HTTP client is not connected: {torClientStatus}", torClientStatus.Error);
        Status = TorHttpClientStatus.FailedToConnectToTor;

        Log(LogLevel.Error, "Failed to get Tor client status: {Error}", Error.GetMessages());

        return false;
    }

    /// <summary>
    /// Initializes the Tor HTTP client with the specified settings.
    /// </summary>
    public Task<bool> InitializeAsync(HttpClientInstanceSettings httpClientInstanceSettings, TorInstanceSettings torInstanceSettings, CancellationToken cancellationToken = default) =>
        InitializeAsync(NoId, httpClientInstanceSettings, torInstanceSettings, cancellationToken);

    /// <summary>
    /// Initializes the Tor HTTP client with the specified settings.
    /// </summary>
    public async Task<bool> InitializeAsync(int id, HttpClientInstanceSettings httpClientSettings, TorInstanceSettings torSettings, CancellationToken cancellationToken = default)
    {
        if (Status != TorHttpClientStatus.Uninitialized) throw new InvalidOperationException($"Tor HTTP client has already been initialized");

        Id = id;
        Status = TorHttpClientStatus.ConnectingToTor;

        _httpClientInstanceSettings = httpClientSettings.AddCommonOrDefaultSettings(_httpClientSharedSettings!);
        _torInstanceSettings = torSettings;

        Log(LogLevel.Information, "Initializing Tor HTTP client with Tor on port {SocksPort}", torSettings.SocksPort);

        var didConnect = await _torService!.StartAsync(torSettings, _logger.IsEnabled(LogLevel.Debug) ? s => { Log(LogLevel.Debug, s); } : null, cancellationToken);
        if (!didConnect)
        {
            Error = new Exception("Failed to start Tor service", _torService.Error);
            Status = TorHttpClientStatus.FailedToConnectToTor;

            Log(LogLevel.Error, "Failed to start Tor service: {Error}", _torService.Error?.GetMessages());
            return false;
        }
        else if (_torService.Status != TorServiceStatus.Connected)
        {
            Error = new Exception($"Tor service is not connected: {_torService.Status}", _torService.Error);
            Status = TorHttpClientStatus.FailedToConnectToTor;

            Log(LogLevel.Error, "Failed to connect to Tor service: {Error}", Error.GetMessages());
            return false;
        }

        var isTorConnected = await GetTorClientStatusAsync(cancellationToken);
        if (!isTorConnected) return false;

        Status = TorHttpClientStatus.ConnectedToTor;

        _requestHandler = new TorHttpClientHandler(
            TorHttpClientHandlerSettings.FromCommonOrDefaultSettings(_httpClientInstanceSettings, torSettings.SocksPort),
            this);

        _httpClient = new HttpClient(_requestHandler, disposeHandler: true)
        {
            Timeout = _httpClientInstanceSettings.Timeout!.Value,
        };

        foreach (var kvp in _httpClientInstanceSettings.DefaultHeaders)
        {
            _ = _httpClient.SetHeader(kvp.Key, kvp.Value, overwrite: true);
        }

        if (!string.IsNullOrWhiteSpace(_httpClientInstanceSettings.BaseAddress))
        {
            _httpClient.BaseAddress = new Uri(_httpClientInstanceSettings.BaseAddress!);

            if (_httpClientInstanceSettings.SetBaseAddressRelatedHeaders.HasValue && _httpClientInstanceSettings.SetBaseAddressRelatedHeaders.Value)
            {
                _ = _httpClient.SetHeader("Host", _httpClient.BaseAddress!.Host);
                _ = _httpClient.SetHeader("Origin", _httpClient.BaseAddress!.GetLeftPart(UriPartial.Authority));
                _ = _httpClient.SetHeader("Referer", _httpClient.BaseAddress!.GetLeftPart(UriPartial.Authority));
            }
        }

        if (!string.IsNullOrWhiteSpace(_httpClientInstanceSettings.Accept)) _ = _httpClient.SetHeader("Accept", _httpClientInstanceSettings.Accept!);

        if (!string.IsNullOrWhiteSpace(_httpClientInstanceSettings.UserAgent)) _ = _httpClient.SetHeader("User-Agent", _httpClientInstanceSettings.UserAgent!);

        Log(LogLevel.Information, "Tor HTTP client initialized and connected via Tor on port {SocksPort} with IP address {IPAddress}", torSettings.SocksPort, IPAddress);
        Log(LogLevel.Debug, "Tor HTTP client data directory: {DataDirectory}", torSettings.DataDirectory ?? "(default)");

        Status = TorHttpClientStatus.ClientIsReady;

        return true;
    }

    /// <summary>
    /// Requests new Tor circuits (new identity). Tor MAY rate-limit its response to this signal.
    /// </summary>
    public async Task<bool> RequestCleanCircuitsAsync(CancellationToken cancellationToken = default)
    {
        if (Status != TorHttpClientStatus.ClientIsReady) throw new InvalidOperationException($"Tor HTTP client is not ready");

        Status = TorHttpClientStatus.RequestingCleanCircuits;

        bool result;

        var requestResult = await _torService!.RequestCleanCircuitsAsync(cancellationToken);
        if (!requestResult)
        {
            Error = new Exception("Failed to request new Tor identity", _torService.Error);
            Status = TorHttpClientStatus.FailedToConnectToTor;

            Log(LogLevel.Error, "Failed to request new Tor identity: {Error}", Error.GetMessages());
            result = false;
        }
        else
        {
            var isTorConnected = await GetTorClientStatusAsync(cancellationToken);
            result = isTorConnected;
        }

        Status = TorHttpClientStatus.ClientIsReady;
        return result;
    }

    public HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Status != TorHttpClientStatus.ClientIsReady
            ? throw new InvalidOperationException($"Tor HTTP client is not ready")
            : _httpClient!.Send(request, cancellationToken);
    }

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Status != TorHttpClientStatus.ClientIsReady
            ? throw new InvalidOperationException($"Tor HTTP client is not ready")
            : _httpClient!.SendAsync(request, cancellationToken);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _httpClient?.Dispose();
            _torService?.Dispose();
        }
    }

    ~TorHttpClient()
    {
        Dispose(disposing: false);
    }

    private void Log(LogLevel level, string? message, params object?[] args)
    {
#pragma warning disable CA2254
        if (_logger.IsEnabled(level)) _logger.Log(level, (Id != NoId ? $"[{Id}] " : "") + message, args);
#pragma warning restore CA2254
    }

    #endregion 
}