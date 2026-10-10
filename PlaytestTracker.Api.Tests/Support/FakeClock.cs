namespace PlaytestTracker.Api.Tests.Support;

// A clock that says whatever time the test chooses. The code under test asks a TimeProvider
// for "now" instead of reading the real clock, so a test can create a token "two hours ago"
// without waiting two hours.
public sealed class FakeClock : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FakeClock(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;
}
