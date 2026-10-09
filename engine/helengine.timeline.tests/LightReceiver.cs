using helengine.timeline.runtime;

namespace helengine.timeline.tests {
    /// <summary>
    /// Second receiver type that also declares <c>opacity</c>, used to exercise ambiguous channel resolution.
    /// </summary>
    [TimelineChannels("intensity", "range", "opacity", ReceiverId = LightReceiver.Id)]
    public sealed class LightReceiver : Component, ITimelineReceiver {
        /// <summary>
        /// Receiver id the type declares.
        /// </summary>
        public const int Id = 30;

        /// <summary>
        /// Gets the receiver id.
        /// </summary>
        public int TimelineReceiverId {
            get { return Id; }
        }

        /// <summary>
        /// Ignores values; the tests only resolve channels against this type.
        /// </summary>
        /// <param name="channel">Channel index.</param>
        /// <param name="value">Value.</param>
        public void SetTimelineValue(int channel, double value) {
        }
    }
}
