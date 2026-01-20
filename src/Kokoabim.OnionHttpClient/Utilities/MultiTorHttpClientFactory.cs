using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kokoabim.OnionHttpClient;

public interface IMultiTorHttpClientFactory : IDisposable
{
    IMultiTorHttpClient Create(HttpClientSharedSettings httpClientSharedSettings);
    Task<(bool DidInitialize, IMultiTorHttpClient MultiTorHttpClient)> CreateAndInitializeOrGetAsync(string id, MultiTorHttpClientSettings multiTorHttpClientSettings, HttpClientSharedSettings httpClientSharedSettings, CancellationToken cancellationToken = default);
    IMultiTorHttpClient? GetOrDefault(string id);
}

public class MultiTorHttpClientFactory : IMultiTorHttpClientFactory
{
    private readonly Dictionary<string, IMultiTorHttpClient> _instances = [];
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public MultiTorHttpClientFactory(IServiceScopeFactory serviceScopeFactory, ILoggerFactory loggerFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _loggerFactory = loggerFactory;
    }

    public IMultiTorHttpClient Create(HttpClientSharedSettings httpClientSharedSettings) =>
        new MultiTorHttpClient(_loggerFactory.CreateLogger<MultiTorHttpClient>(), new TorHttpClientFactory(_serviceScopeFactory, Options.Create(httpClientSharedSettings)));

    public async Task<(bool DidInitialize, IMultiTorHttpClient MultiTorHttpClient)> CreateAndInitializeOrGetAsync(string id, MultiTorHttpClientSettings multiTorHttpClientSettings, HttpClientSharedSettings httpClientSharedSettings, CancellationToken cancellationToken = default)
    {
        IMultiTorHttpClient? instance = _instances.GetValueOrDefault(id);

        if (instance == null)
        {
            _instances[id] = instance = Create(httpClientSharedSettings);

            _ = await instance.InitializeAsync(multiTorHttpClientSettings, httpClientSharedSettings, cancellationToken);
        }

        return (instance.Status == TorHttpClientStatus.ClientIsReady, instance);
    }

    public IMultiTorHttpClient? GetOrDefault(string id) => _instances.GetValueOrDefault(id);

    ~MultiTorHttpClientFactory()
    {
        Dispose(disposing: false);
    }

    #region IDisposable

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var disposable in _instances.Values) disposable.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}