namespace helengine.timeline.runtime {
    /// <summary>
    /// What a <see cref="TimelinePlayerComponent"/> does when playback reaches the end of the timeline.
    /// </summary>
    public enum TimelineEndMode {
        /// <summary>
        /// Apply the final frame once, stop all timeline sounds and stop playing; targets keep the final values and are
        /// free to be moved by other code. The next <see cref="TimelinePlayerComponent.Play"/> starts from the beginning.
        /// </summary>
        Stop = 0,
        /// <summary>
        /// Keep playing at the end: the final frame is re-applied every update, so targets stay where the timeline left
        /// them until <see cref="TimelinePlayerComponent.Stop"/> is called.
        /// </summary>
        Hold = 1,
        /// <summary>
        /// Wrap around to the beginning and keep playing; events and sounds at the start fire again.
        /// </summary>
        Loop = 2
    }
}
