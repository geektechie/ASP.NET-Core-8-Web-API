using ExpenseTracker.Application.Interfaces;

namespace ExpenseTracker.UnitTests.Support;

/// <summary>A clock the test moves by hand, so date rules need no waiting.</summary>
public sealed class FakeDateTimeProvider : IDateTimeProvider
{
    public FakeDateTimeProvider(DateTimeOffset? start = null) =>
        UtcNow = start ?? new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    public DateTimeOffset UtcNow { get; private set; }

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);

    public void MoveTo(DateOnly date) =>
        UtcNow = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
