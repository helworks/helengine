namespace helengine.timeline.runtime.tests {
    /// <summary>
    /// Event listener double that records every event as <c>name=value</c>.
    /// </summary>
    public sealed class RecordingEventListener : Component, ITimelineEventListener {
        /// <summary>
        /// Gets the received events in order.
        /// </summary>
        public List<string> Events { get; } = new List<string>();

        /// <summary>
        /// Records one event.
        /// </summary>
        /// <param name="name">Event name.</param>
        /// <param name="value">Event value.</param>
        public void OnTimelineEvent(string name, string value) {
            Events.Add(name + "=" + value);
        }
    }
}
