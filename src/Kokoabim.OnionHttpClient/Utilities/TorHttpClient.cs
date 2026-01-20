using System.Net;
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
    /// Gets information about the Tor HTTP client and its connection to the Tor network.
    /// </summary>
    Task<TorHttpClientInfo> GetTorHttpClientInfoAsync(CancellationToken cancellationToken = default);
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
    /// <returns>Information about the Tor HTTP client and its connection to the Tor network.</returns>
    Task<TorHttpClientInfo> RequestCleanCircuitsAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Sets the cookies for the Tor HTTP client.
    /// </summary>
    void SetCookies(IEnumerable<Cookie> cookies);
    bool SetHeader(string name, string value, bool overwrite = true);
    bool SetHeader(string name, IEnumerable<string> values, bool overwrite = true);
}

public class TorHttpClient : ITorHttpClient
{
    public const int NoId = -1;

    public Exception? Error { get; private set; }
    public int Id { get; private set; } = NoId;
    public string? IPAddress { get; private set; }
    public int RequestCount => _requestHandler?.RequestCount ?? 0;
    public TorHttpClientStatus Status { get; private set; }

    private bool _hasInitialized;
    private HttpClient? _httpClient;
    private HttpClientInstanceSettings? _httpClientInstanceSettings;
    private readonly HttpClientSharedSettings _httpClientSharedSettings;
    private readonly ILogger<TorHttpClient> _logger;
    private TorHttpClientHandler? _requestHandler;
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
    /// Gets information about the Tor HTTP client and its connection to the Tor network.
    /// </summary>
    public async Task<TorHttpClientInfo> GetTorHttpClientInfoAsync(CancellationToken cancellationToken = default)
    {
        if (Status == TorHttpClientStatus.Uninitialized) return new TorHttpClientInfo
        {
            Error = new InvalidOperationException("Tor HTTP client is uninitialized"),
            HttpClientStatus = TorHttpClientStatus.Uninitialized
        };

        var torClientStatus = await _torService!.GetStatusAsync(cancellationToken);

        IPAddress = torClientStatus.IPAddress;

        if (torClientStatus.Success)
        {
            Status = _hasInitialized ? TorHttpClientStatus.ClientIsReady : TorHttpClientStatus.ConnectedToTor;
            return TorHttpClientInfo.Create(this, _torService, torClientStatus);
        }

        Error = new Exception($"Tor HTTP client is not connected: {torClientStatus}", torClientStatus.Error);
        Status = TorHttpClientStatus.FailedToConnectToTor;

        Log(LogLevel.Error, "Failed to get Tor client status: {Error}", Error.GetMessages());

        return TorHttpClientInfo.Create(this, _torService, torClientStatus);
    }

    /// <summary>
    /// Initializes the Tor HTTP client with the specified settings.
    /// </summary>
    public Task<bool> InitializeAsync(HttpClientInstanceSettings httpClientSettings, TorInstanceSettings torSettings, CancellationToken cancellationToken = default) =>
        InitializeAsync(NoId, httpClientSettings, torSettings, cancellationToken);

    /// <summary>
    /// Initializes the Tor HTTP client with the specified settings.
    /// </summary>
    public async Task<bool> InitializeAsync(int id, HttpClientInstanceSettings httpClientSettings, TorInstanceSettings torSettings, CancellationToken cancellationToken = default)
    {
        if (Status != TorHttpClientStatus.Uninitialized) throw new InvalidOperationException($"Tor HTTP client has already been initialized");

        Id = id;
        Status = TorHttpClientStatus.ConnectingToTor;

        _httpClientInstanceSettings = httpClientSettings.AddCommonOrDefaultSettings(_httpClientSharedSettings!);

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

        var torHttpClientInfo = await GetTorHttpClientInfoAsync(cancellationToken);
        if (!torHttpClientInfo.IsTorClientOK) return false;

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
        _hasInitialized = true;

        return true;
    }

    /// <summary>
    /// Requests new Tor circuits (new identity). Tor MAY rate-limit its response to this signal.
    /// </summary>
    /// <returns>Information about the Tor HTTP client and its connection to the Tor network.</returns>
    public async Task<TorHttpClientInfo> RequestCleanCircuitsAsync(CancellationToken cancellationToken = default)
    {
        if (Status != TorHttpClientStatus.ClientIsReady) return new TorHttpClientInfo
        {
            Error = new InvalidOperationException("Tor HTTP client is not ready"),
            HttpClientStatus = Status
        };

        Status = TorHttpClientStatus.RequestingCleanCircuits;

        Exception? exception = null;
        var requestResult = await _torService!.RequestCleanCircuitsAsync(cancellationToken);
        if (!requestResult)
        {
            exception = new Exception("Failed to request new Tor identity", _torService.Error);
            Log(LogLevel.Error, "{Error}", exception.GetMessages());
        }

        var torHttpClientInfo = await GetTorHttpClientInfoAsync(cancellationToken);

        if (torHttpClientInfo.IsTorClientOK) Status = _hasInitialized ? TorHttpClientStatus.ClientIsReady : TorHttpClientStatus.ConnectedToTor;

        if (exception is not null) Error = torHttpClientInfo.Error = exception;

        return torHttpClientInfo;
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

    /// <summary>
    /// Sets the cookies for the Tor HTTP client.
    /// </summary>
    public void SetCookies(IEnumerable<Cookie> cookies)
    {
        if (_requestHandler?.UseCookies != true) return;

        foreach (var cookie in cookies) _requestHandler.CookieContainer.Add(cookie);
    }

    public bool SetHeader(string name, string value, bool overwrite = true)
    {
        return _httpClient == null
            ? throw new InvalidOperationException("Tor HTTP client is not initialized")
            : _httpClient.SetHeader(name, value, overwrite);
    }

    public bool SetHeader(string name, IEnumerable<string> values, bool overwrite = true)
    {
        return _httpClient == null
            ? throw new InvalidOperationException("Tor HTTP client is not initialized")
            : _httpClient.SetHeader(name, values, overwrite);
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