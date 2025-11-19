namespace Kokoabim.OnionHttpClient.Tests;

public class TorHttpClientTests
{
    [Fact]
    public async Task InitializeAsync()
    {
        // arrange
        var appHost = new TestAppHost();
        appHost.Build();

        var httpClientSettings = new HttpClientInstanceSettings();
        var torSettings = new TorInstanceSettings(useRandomPorts: false, useRandomDataDirectory: false);

        var target = appHost.GetRequiredService<ITorHttpClient>();

        // act
        var actual = await target.InitializeAsync(1, httpClientSettings, torSettings);

        await target.DisconnectAsync();

        // assert
        Assert.True(actual);
        Assert.Equal(0, target.RequestCount);
    }

    [Fact]
    public async Task RequestNewCircuitsAsync_ShouldReturnTrueAndChangeIpAddress()
    {
        // arrange
        var appHost = new TestAppHost();
        appHost.Build();

        var httpClientSettings = new HttpClientInstanceSettings();
        var torSettings = new TorInstanceSettings();

        var target = appHost.GetRequiredService<ITorHttpClient>();

        // act
        var didInit = await target.InitializeAsync(1, httpClientSettings, torSettings);
        Assert.True(didInit);

        var currentIp = target.IPAddress;
        Assert.NotNull(currentIp);

        var actual = await target.RequestCleanCircuitsAsync();

        var newIp = target.IPAddress; ;
        Assert.NotNull(newIp);

        await target.DisconnectAsync();

        // assert
        Assert.True(actual);
        Assert.NotEqual(currentIp, newIp);
    }
}