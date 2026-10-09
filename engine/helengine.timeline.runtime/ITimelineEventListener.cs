namespace helengine.timeline.runtime {
    /// <summary>
    /// A component, on the same entity as a <see cref="TimelinePlayerComponent"/>, that receives the timeline's event
    /// markers as playback crosses them.
    /// </summary>
    public interface ITimelineEventListener {
        /// <summary>
        /// Called once each time playback crosses a marker. The strings come from the cooked asset's table, so no string
        /// is created per call.
        /// </summary>
        /// <param name="name">Event name, e.g. <c>shake</c>.</param>
        /// <param name="value">Event value as authored (empty when the marker had none).</param>
        void OnTimelineEvent(string name, string value);
    }
}
