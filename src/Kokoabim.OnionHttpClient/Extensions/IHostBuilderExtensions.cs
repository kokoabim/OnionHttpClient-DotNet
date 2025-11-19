using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kokoabim.OnionHttpClient;

public static class IHostBuilderExtensions
{
    public static IHostBuilder AddOnionHttpClient(this IHostBuilder hostBuilder)
    {
        return hostBuilder.ConfigureAppConfiguration((hostContext, config) =>
        {
            _ = config
                .SetBasePath(hostContext.HostingEnvironment.ContentRootPath)
                .AddJsonFile("onionhttpclient.json", optional: true, reloadOnChange: true)
                .AddJsonFile("onionhttpclient.secrets.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
        })
        .ConfigureServices((hostContext, services) =>
        {
            _ = services
                .AddOptions()
                .Configure<TorSharedSettings>(hostContext.Configuration.GetSection("Tor"))
                .Configure<HttpClientSharedSettings>(hostContext.Configuration.GetSection("HttpClient"))
                .AddScoped<ITorHttpClientFactory, TorHttpClientFactory>()
                .AddScoped<IMultiTorHttpClient, MultiTorHttpClient>()
                .AddScoped<ITorHttpClient, TorHttpClient>()
                .AddScoped<ITorService, TorService>()
                .AddScoped<IExecutor, Executor>();
        });
    }
}