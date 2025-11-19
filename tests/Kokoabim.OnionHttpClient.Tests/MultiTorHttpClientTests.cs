namespace Kokoabim.OnionHttpClient.Tests;

public class MultiTorHttpClientTests
{
    [Fact]
    public async Task InitializeAsync()
    {
        // arrange
        var appHost = new TestAppHost();
        appHost.Build();

        var multiTorHttpClientSettings = new MultiTorHttpClientSettings
        {
            ClientCount = 2,
        };
        var httpClientCommonSettings = new HttpClientCommonSettings();

        using var target = appHost.GetRequiredService<IMultiTorHttpClient>();

        // act
        var actual = await target.InitializeAsync(multiTorHttpClientSettings, httpClientCommonSettings);

        await target.DisconnectAsync();

        // assert
        Assert.True(actual);
        Assert.Equal(multiTorHttpClientSettings.ClientCount, target.ClientCount);
    }
}