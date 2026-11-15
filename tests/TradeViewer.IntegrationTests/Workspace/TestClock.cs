namespace TradeViewer.IntegrationTests.Workspace;

/// <summary>A clock the test sets.</summary>
public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow()
    {
        return Now;
    }
}
