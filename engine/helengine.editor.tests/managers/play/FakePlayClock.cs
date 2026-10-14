namespace helengine.editor.tests.managers.play;

/// <summary>
/// Test double for the Play clock that returns a fixed sequence of readings, one per read, so each Play stage gets an
/// exact, known duration. Reading past the end of the sequence fails the test.
/// </summary>
public sealed class FakePlayClock : IEditorPlayClock {
    /// <summary>
    /// Readings still to be returned, in order.
    /// </summary>
    readonly Queue<TimeSpan> Readings;

    /// <summary>
    /// Initializes the fake with the readings, in seconds, returned by successive reads.
    /// </summary>
    /// <param name="readingsInSeconds">Clock readings in seconds, in read order.</param>
    public FakePlayClock(params double[] readingsInSeconds) {
        Readings = new Queue<TimeSpan>();
        for (int i = 0; i < readingsInSeconds.Length; i++) {
            Readings.Enqueue(TimeSpan.FromSeconds(readingsInSeconds[i]));
        }
    }

    /// <summary>
    /// Gets the number of readings not read yet.
    /// </summary>
    public int RemainingReadings {
        get {
            return Readings.Count;
        }
    }

    /// <summary>
    /// Returns the next reading of the sequence.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when every reading was already returned.</exception>
    public TimeSpan Now {
        get {
            if (Readings.Count == 0) {
                throw new InvalidOperationException("The fake Play clock ran out of readings.");
            }

            return Readings.Dequeue();
        }
    }
}
