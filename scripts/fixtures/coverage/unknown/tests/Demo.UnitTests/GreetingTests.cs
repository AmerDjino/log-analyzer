namespace Demo.UnitTests;

public sealed class GreetingTests
{
    [Fact]
    [Trait("Scenario", "demo/greeting-is-shown")]
    public void ShowsGreeting() => Assert.Equal("Hello, Ada", $"Hello, {"Ada"}");

    // Typo in the scenario id: the check must report it as unknown.
    [Fact]
    [Trait("Scenario", "demo/greting-is-shown")]
    public void ShowsGreetingAgain() => Assert.Equal("Hello, Ada", $"Hello, {"Ada"}");
}
