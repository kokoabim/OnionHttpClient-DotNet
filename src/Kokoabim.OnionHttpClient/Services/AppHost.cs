using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kokoabim.OnionHttpClient;

public interface IAppHost
{
    IHost Host { get; }
    ILoggerFactory LoggerFactory { get; }
    IServiceProvider ServiceProvider { get; }

    IAppHost AddScoped<TService, TImplementation>() where TService : class where TImplementation : class, TService;
    IAppHost AddSingleton<TService, TImplementation>() where TService : class where TImplementation : class, TService;
    IAppHost AddSingleton<TService>(TService implementationInstance) where TService : class;
    IAppHost AddTransient<TService, TImplementation>() where TService : class where TImplementation : class, TService;
    void Build();
    IAppHost ConfigureHost(Action<IHostBuilder> configureHost);
    IAppHost ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureServices);
    T GetRequiredService<T>() where T : notnull;
    T? GetService<T>();
    IEnumerable<T> GetServices<T>();
}

public class AppHost : IAppHost
{
    public IHost Host => _host is not null ? _host : throw new InvalidOperationException("AppHost not built");
    public ILoggerFactory LoggerFactory => _loggerFactory is not null ? _loggerFactory : throw new InvalidOperationException("AppHost not built");
    public IServiceProvider ServiceProvider => _serviceProvider is not null ? _serviceProvider : throw new InvalidOperationException("AppHost not built");

    private IHost? _host;
    private readonly IHostBuilder _hostBuilder;
    private ILoggerFactory? _loggerFactory;
    private IServiceProvider? _serviceProvider;

    public AppHost()
    {
        _hostBuilder = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder();

        AddSingleton<IAppHost>(this);
    }

    public IAppHost AddScoped<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        _ = _hostBuilder.ConfigureServices(static services =>
        {
            _ = services.AddScoped<TService, TImplementation>();
        });

        return this;
    }

    public IAppHost AddSingleton<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        _ = _hostBuilder.ConfigureServices(static services =>
        {
            _ = services.AddSingleton<TService, TImplementation>();
        });

        return this;
    }

    public IAppHost AddSingleton<TService>(TService implementationInstance) where TService : class
    {
        _ = _hostBuilder.ConfigureServices(services =>
        {
            _ = services.AddSingleton(implementationInstance);
        });

        return this;
    }

    public IAppHost AddTransient<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        _ = _hostBuilder.ConfigureServices(static services =>
        {
            _ = services.AddTransient<TService, TImplementation>();
        });

        return this;
    }

    public virtual void Build()
    {
        _host = _hostBuilder.Build();
        _serviceProvider = _host.Services;
        _loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
    }

    public IAppHost ConfigureHost(Action<IHostBuilder> configureHost)
    {
        configureHost(_hostBuilder);
        return this;
    }

    public virtual IAppHost ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureServices)
    {
        _ = _hostBuilder.ConfigureServices((context, services) =>
        {
            configureServices(context, services);
        });

        return this;
    }

    public T GetRequiredService<T>() where T : notnull => _serviceProvider is not null ? _serviceProvider.GetRequiredService<T>() : throw new InvalidOperationException("AppHost not built");

    public T? GetService<T>() => _serviceProvider is not null ? _serviceProvider.GetService<T>() : throw new InvalidOperationException("AppHost not built");

    public IEnumerable<T> GetServices<T>() => _serviceProvider is not null ? _serviceProvider.GetServices<T>() : throw new InvalidOperationException("AppHost not built");
}