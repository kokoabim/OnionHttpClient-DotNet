namespace Kokoabim.OnionHttpClient.Tests;

public class AppHostTests
{
    [Fact]
    public void Build()
    {
        // arrange
        var target = new AppHost();

        // act
        target.Build();

        // assert
        Assert.NotNull(target);
        Assert.NotNull(target.Host);
        Assert.NotNull(target.LoggerFactory);
        Assert.NotNull(target.ServiceProvider);
    }
}