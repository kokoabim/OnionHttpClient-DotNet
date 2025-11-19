using System.Diagnostics;

namespace Kokoabim.OnionHttpClient.Tests;

public class TorServiceTests
{
    [Fact]
    public async Task ExecuteControlCommandAsync_GetVersion()
    {
        // arrange
        var appHost = new TestAppHost();
        appHost.Build();

        var target = appHost.GetRequiredService<ITorService>();
        var instanceSettings = new TorInstanceSettings(useRandomDataDirectory: false);

        var didStart = await target.StartAsync(instanceSettings, data =>
        {
            Debug.WriteLine(data);
        });
        Assert.True(didStart);

        // act
        var actual = await target.ExecuteControlCommandAsync("GETINFO version");

        await target.StopAsync();

        // assert
        Assert.Equal(0, actual.ExitCode);
        Assert.Contains("250-version=", actual.Output);
    }

    [Fact]
    public async Task GetStatusAsync()
    {
        // arrange
        var appHost = new TestAppHost();
        appHost.Build();

        var target = appHost.GetRequiredService<ITorService>();
        var instanceSettings = new TorInstanceSettings();

        // act
        var didStart = await target.StartAsync(instanceSettings, data =>
        {
            Debug.WriteLine(data);
        });
        Assert.True(didStart);

        var actual = await target.GetStatusAsync();

        await target.StopAsync();

        // assert
        Assert.NotNull(actual);
        Assert.True(actual.IsRunning);
        Assert.True(actual.IsConnected);
        Assert.NotNull(actual.IPAddress);
    }

    [Fact]
    public async Task RunAsync()
    {
        // arrange
        var appHost = new TestAppHost();
        appHost.Build();

        var target = appHost.GetRequiredService<ITorService>();
        var instanceSettings = new TorInstanceSettings();

        // act
        var task = target.RunAsync(instanceSettings, data =>
        {
            Debug.WriteLine(data);
        });

        var didConnect = await target.WaitForStartupAsync();
        Assert.True(didConnect);

        var actual = await target.StopAsync();

        // assert
        Assert.NotNull(actual);
        Assert.True(actual!.Killed);
        Assert.Equal((int)ExitCode.SIGKILL, actual.ExitCode);
    }
}