namespace RachelTower.Tests;

/// <summary>A clock the tests can set to any moment, forwards or backwards.</summary>
public sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void SetUtcNow(DateTimeOffset value) => _now = value;

    public void Advance(TimeSpan by) => _now += by;
}
