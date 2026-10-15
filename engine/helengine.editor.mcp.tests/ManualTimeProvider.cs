namespace helengine.editor.mcp.tests;

/// <summary>
/// Provides a controllable monotonic timestamp source for coordinator lifecycle tests.
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    /// <summary>Gets the number of elapsed ticks in this test clock.</summary>
    public long Timestamp { get; private set; }

    /// <summary>Gets the frequency used to interpret test clock timestamps.</summary>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <summary>Returns the current monotonic test timestamp.</summary>
    /// <returns>The current timestamp in ticks.</returns>
    public override long GetTimestamp()
    {
        return Timestamp;
    }

    /// <summary>Advances the clock by an elapsed duration.</summary>
    /// <param name="Duration">The positive duration to advance.</param>
    public void Advance(TimeSpan Duration)
    {
        if (Duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Duration));
        }

        Timestamp += Duration.Ticks;
    }
}
