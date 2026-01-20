using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kokoabim.OnionHttpClient;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddOnionHttpClient(this IServiceCollection source, IConfigurationManager configuration, IHostEnvironment hostEnvironment)
    {
        var basePath = hostEnvironment.ContentRootPath;
#if DEBUG
        basePath = AppDomain.CurrentDomain.BaseDirectory;
#endif

        _ = configuration
            .SetBasePath(basePath)
            .AddJsonFile("onionhttpclient.json", optional: true, reloadOnChange: true)
            .AddJsonFile("onionhttpclient.secrets.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        _ = source
            .AddOptions()
            .Configure<TorSharedSettings>(configuration.GetSection("Tor"))
            .Configure<HttpClientSharedSettings>(configuration.GetSection("HttpClient"))
            .AddSingleton<IMultiTorHttpClientFactory, MultiTorHttpClientFactory>()
            .AddScoped<ITorHttpClientFactory, TorHttpClientFactory>()
            .AddScoped<IMultiTorHttpClient, MultiTorHttpClient>()
            .AddScoped<ITorHttpClient, TorHttpClient>()
            .AddScoped<ITorService, TorService>()
            .AddScoped<IExecutor, Executor>();

        return source;
    }
}