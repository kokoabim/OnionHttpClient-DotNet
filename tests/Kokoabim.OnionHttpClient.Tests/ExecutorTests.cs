namespace Kokoabim.OnionHttpClient.Tests;

public class ExecutorTests
{
    [Fact]
    public void Execute_WithValidCommand_ReturnsSuccess()
    {
        // arrange 
        var target = new Executor();

        // act
        var result = target.Execute("dotnet", "--version");

        // assert
        Assert.NotNull(result.Output);
        Assert.Matches(@"\d+\.\d+\.\d+", result.Output!);
    }
}