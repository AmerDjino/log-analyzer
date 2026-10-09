namespace Demo.UnitTests;

public sealed class GreetingTests
{
    [Fact]
    [Trait("Scenario", "demo/greeting-is-shown")]
    public void ShowsGreeting() => Assert.Equal("Hello, Ada", $"Hello, {"Ada"}");

    [Fact]
    [Trait("Scenario", "demo/farewell-is-shown")]
    public void ShowsFarewell() => Assert.Equal("Bye, Ada", $"Bye, {"Ada"}");
}
