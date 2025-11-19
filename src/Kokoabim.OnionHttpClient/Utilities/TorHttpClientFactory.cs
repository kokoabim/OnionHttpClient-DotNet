using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Kokoabim.OnionHttpClient;

public interface ITorHttpClientFactory : IDisposable
{
    /// <summary>
    /// Creates a Tor HTTP client.
    /// </summary>
    ITorHttpClient Create();
    /// <summary>
    /// Creates and initializes a Tor HTTP client with random Tor settings and a random User-Agent.
    /// </summary>
    Task<(bool DidInitialize, ITorHttpClient TorHttpClient)> CreateAndInitializeAsync(HttpClientCommonSettings commonSettings, CancellationToken cancellationToken = default);
    /// <summary>
    /// Creates and initializes a Tor HTTP client with random Tor settings and a random User-Agent.
    /// </summary>
    Task<(bool DidInitialize, ITorHttpClient TorHttpClient)> CreateAndInitializeAsync(HttpClientInstanceSettings instanceSettings, CancellationToken cancellationToken = default);
    /// <summary>
    /// Creates and initializes a Tor HTTP client.
    /// </summary>
    Task<(bool DidInitialize, ITorHttpClient TorHttpClient)> CreateAndInitializeAsync(HttpClientCommonSettings commonSettings, TorInstanceSettings torSettings, CancellationToken cancellationToken = default);
    /// <summary>
    /// Creates and initializes a Tor HTTP client.
    /// </summary>
    Task<(bool DidInitialize, ITorHttpClient TorHttpClient)> CreateAndInitializeAsync(HttpClientInstanceSettings instanceSettings, TorInstanceSettings torSettings, CancellationToken cancellationToken = default);
}

public class TorHttpClientFactory : ITorHttpClientFactory
{
    private readonly List<IDisposable> _disposables = [];
    private readonly HttpClientSharedSettings _httpClientSharedSettings;
    private static int _nextHttpClientId = 0;
    private static readonly Random _random = new();
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public TorHttpClientFactory(IServiceScopeFactory serviceScopeFactory, IOptions<HttpClientSharedSettings> sharedSettings)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _httpClientSharedSettings = sharedSettings.Value;
    }

    /// <summary>
    /// Creates a Tor HTTP client.
    /// </summary>
    public ITorHttpClient Create()
    {
        var scope = _serviceScopeFactory.CreateScope();
        _disposables.Add(scope);

        return scope.ServiceProvider.GetRequiredService<ITorHttpClient>();
    }

    /// <summary>
    /// Creates and initializes a Tor HTTP client with random Tor settings and a random User-Agent.
    /// </summary>
    public Task<(bool DidInitialize, ITorHttpClient TorHttpClient)> CreateAndInitializeAsync(HttpClientInstanceSettings instanceSettings, CancellationToken cancellationToken = default)
    {
        instanceSettings.UserAgent = _httpClientSharedSettings.UserAgents[_random.Next(_httpClientSharedSettings.UserAgents.Length)];

        return CreateAndInitializeAsync(instanceSettings, new TorInstanceSettings(), cancellationToken);
    }

    /// <summary>
    /// Creates and initializes a Tor HTTP client with random Tor settings and a random User-Agent.
    /// </summary>
    public Task<(bool DidInitialize, ITorHttpClient TorHttpClient)> CreateAndInitializeAsync(HttpClientCommonSettings commonSettings, CancellationToken cancellationToken = default) =>
        CreateAndInitializeAsync(HttpClientInstanceSettings.CreateWithCommonOrDefaultSettings(commonSettings, _httpClientSharedSettings.UserAgents[_random.Next(_httpClientSharedSettings.UserAgents.Length)]), new TorInstanceSettings(), cancellationToken);

    /// <summary>
    /// Creates and initializes a Tor HTTP client.
    /// </summary>
    public Task<(bool DidInitialize, ITorHttpClient TorHttpClient)> CreateAndInitializeAsync(HttpClientCommonSettings commonSettings, TorInstanceSettings torSettings, CancellationToken cancellationToken = default) =>
        CreateAndInitializeAsync(HttpClientInstanceSettings.CreateWithCommonOrDefaultSettings(commonSettings, _httpClientSharedSettings.UserAgents[_random.Next(_httpClientSharedSettings.UserAgents.Length)]), torSettings, cancellationToken);

    /// <summary>
    /// Creates and initializes a Tor HTTP client.
    /// </summary>
    public async Task<(bool DidInitialize, ITorHttpClient TorHttpClient)> CreateAndInitializeAsync(HttpClientInstanceSettings instanceSettings, TorInstanceSettings torSettings, CancellationToken cancellationToken = default)
    {
        var torHttpClient = Create();
        var didInitialized = await torHttpClient.InitializeAsync(Interlocked.Increment(ref _nextHttpClientId), instanceSettings, torSettings, cancellationToken);

        return (didInitialized, torHttpClient);
    }

    ~TorHttpClientFactory()
    {
        Dispose(disposing: false);
    }

    #region IDisposable

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion 
}