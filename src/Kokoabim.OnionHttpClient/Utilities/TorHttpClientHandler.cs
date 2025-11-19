using System.Net;

namespace Kokoabim.OnionHttpClient;

public interface ITorHttpClientHandler
{
    DateTime LastCleanCircuitsRequest { get; }
    int RequestCount { get; }
}

public class TorHttpClientHandler : HttpClientHandler, ITorHttpClientHandler
{
    public DateTime LastCleanCircuitsRequest { get; private set; } = DateTime.Now;
    public int RequestCount => _requestCount;

    private readonly TimeSpan _cleanCircuitsAfterTimeSpan;
    private int _requestCount;
    private readonly TorHttpClientHandlerSettings _settings;
    private readonly ITorHttpClient? _torHttpClient;

    public TorHttpClientHandler(TorHttpClientHandlerSettings settings, ITorHttpClient torHttpClient)
        : this(settings)
    {
        _torHttpClient = torHttpClient;
    }

    public TorHttpClientHandler(TorHttpClientHandlerSettings settings)
    {
        // NOTE: _torHttpClient is not set in this constructor (used for Tor status checks)

        _settings = settings;

        AllowAutoRedirect = settings.AllowAutoRedirect!.Value;
        AutomaticDecompression = settings.AutomaticDecompression!.Value;
        Proxy = new WebProxy(new Uri($"socks5://localhost:{settings.SocksPort}"));
        SslProtocols = settings.SslProtocols!.Value;
        UseCookies = settings.UseCookies!.Value;

        if (settings.UseCookies.HasValue && settings.UseCookies.Value) CookieContainer = settings.CookieContainer!;

        if (settings.RequestCleanCircuitsAfterMinutes > 0) _cleanCircuitsAfterTimeSpan = TimeSpan.FromMinutes(settings.RequestCleanCircuitsAfterMinutes);
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _requestCount);

        try { return base.Send(request, cancellationToken); }
        finally { RequestCleanCircuitsIfNeeded(); }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _requestCount);

        try { return await base.SendAsync(request, cancellationToken); }
        finally { RequestCleanCircuitsIfNeeded(); }
    }

    private void RequestCleanCircuitsIfNeeded(CancellationToken cancellationToken = default)
    {
        if (_torHttpClient == null) return;

        if ((_settings.RequestCleanCircuitsAfterMinutes > 0 && DateTime.Now - LastCleanCircuitsRequest >= _cleanCircuitsAfterTimeSpan)
            ||
            (_settings.RequestCleanCircuitsAfterRequestCount > 0 && _requestCount % _settings.RequestCleanCircuitsAfterRequestCount == 0))
        {
            _ = _torHttpClient.RequestCleanCircuitsAsync(cancellationToken);
            LastCleanCircuitsRequest = DateTime.Now;
        }
    }
}