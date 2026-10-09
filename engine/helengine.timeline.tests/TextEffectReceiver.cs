using helengine.timeline.runtime;

namespace helengine.timeline.tests {
    /// <summary>
    /// Receiver for the text and rectangle slots of the samples: opacity and reveal. Records the last value per channel.
    /// </summary>
    [TimelineChannels("opacity", "reveal", ReceiverId = TextEffectReceiver.Id)]
    public sealed class TextEffectReceiver : Component, ITimelineReceiver {
        /// <summary>
        /// Receiver id the type declares.
        /// </summary>
        public const int Id = 21;

        /// <summary>
        /// Gets the last value received per channel.
        /// </summary>
        public double[] Values { get; } = new double[2];

        /// <summary>
        /// Gets the receiver id.
        /// </summary>
        public int TimelineReceiverId {
            get { return Id; }
        }

        /// <summary>
        /// Records a channel value.
        /// </summary>
        /// <param name="channel">Channel index.</param>
        /// <param name="value">Value.</param>
        public void SetTimelineValue(int channel, double value) {
            Values[channel] = value;
        }
    }
}
