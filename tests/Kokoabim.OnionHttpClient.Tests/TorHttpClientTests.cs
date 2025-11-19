namespace Kokoabim.OnionHttpClient.Tests;

public class TorHttpClientTests
{
    [Fact]
    public async Task GetResponseAsync()
    {
        // arrange
        var appHost = new TestAppHost();
        appHost.Build();

        var httpClientSettings = new HttpClientInstanceSettings("https://check.torproject.org");
        var torSettings = TorInstanceSettings.GetDefaults();

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/api/ip");

        using var target = appHost.GetRequiredService<ITorHttpClient>();
        var didInit = await target.InitializeAsync(httpClientSettings, torSettings);
        Assert.True(didInit);

        // act
        var actual = await target.GetResponseAsync(httpRequest);

        // assert
        Assert.NotNull(actual);
        Assert.True(actual.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetResponseAsync_ContentAsJsonDocumentAsync()
    {
        // arrange
        var appHost = new TestAppHost();
        appHost.Build();

        var httpClientSettings = new HttpClientInstanceSettings("https://check.torproject.org");
        var torSettings = TorInstanceSettings.GetDefaults();

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/api/ip");

        using var target = appHost.GetRequiredService<ITorHttpClient>();
        var didInit = await target.InitializeAsync(httpClientSettings, torSettings);
        Assert.True(didInit);

        // act
        var response = await target.GetResponseAsync(httpRequest);
        var actual = await response.ContentAsJsonDocumentAsync();

        // assert
        Assert.NotNull(actual);
        Assert.True(actual.RootElement.GetProperty("IsTor").GetBoolean());
        Assert.NotNull(actual.RootElement.GetProperty("IP").GetString());
    }

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
}