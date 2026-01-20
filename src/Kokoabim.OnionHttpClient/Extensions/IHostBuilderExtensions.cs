using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kokoabim.OnionHttpClient;

public static class IHostBuilderExtensions
{
    public static IHostBuilder AddOnionHttpClient(this IHostBuilder hostBuilder)
    {
        return hostBuilder.ConfigureAppConfiguration(static (hostContext, config) =>
        {
            var basePath = hostContext.HostingEnvironment.ContentRootPath;

#if DEBUG
            basePath = AppDomain.CurrentDomain.BaseDirectory;
#endif

            _ = config
                .SetBasePath(basePath)
                .AddJsonFile("onionhttpclient.json", optional: true, reloadOnChange: true)
                .AddJsonFile("onionhttpclient.secrets.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
        })
        .ConfigureServices(static (hostContext, services) =>
        {
            _ = services
                .AddOptions()
                .Configure<TorSharedSettings>(hostContext.Configuration.GetSection("Tor"))
                .Configure<HttpClientSharedSettings>(hostContext.Configuration.GetSection("HttpClient"))
                .AddSingleton<IMultiTorHttpClientFactory, MultiTorHttpClientFactory>()
                .AddScoped<ITorHttpClientFactory, TorHttpClientFactory>()
                .AddScoped<IMultiTorHttpClient, MultiTorHttpClient>()
                .AddScoped<ITorHttpClient, TorHttpClient>()
                .AddScoped<ITorService, TorService>()
                .AddScoped<IExecutor, Executor>();
        });
    }
}