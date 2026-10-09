namespace Demo.UnitTests;

// This test has no Scenario trait, so the check must report demo/greeting-is-shown as missing.
public sealed class GreetingTests
{
    [Fact]
    public void ShowsGreeting() => Assert.Equal("Hello, Ada", $"Hello, {"Ada"}");
}
