namespace helengine.recording.windows;
/// <summary>Distinguishes completed destination transfer from a later work-file cleanup failure.</summary>
sealed class RecordingPublicationException : IOException {
    /// <summary>Records a cleanup failure after a valid destination was fully closed.</summary>
    internal RecordingPublicationException(Exception inner) : base("Output was completed but work-file cleanup failed: " + inner.Message, inner) { }
}
