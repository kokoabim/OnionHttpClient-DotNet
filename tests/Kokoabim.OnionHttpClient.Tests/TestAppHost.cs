using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kokoabim.OnionHttpClient.Tests;

public class TestAppHost : AppHost
{
    public override void Build()
    {
        _ = ConfigureHost(static builder =>
        {
            _ = builder.AddOnionHttpClient();
        });

        _ = ConfigureServices(static (context, services) =>
        {
            _ = services.AddLogging(static config =>
            {
                _ = config.SetMinimumLevel(LogLevel.Debug);
            });
        });

        base.Build();
    }
}