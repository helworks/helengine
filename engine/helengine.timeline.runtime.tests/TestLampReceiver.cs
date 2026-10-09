namespace helengine.timeline.runtime.tests {
    /// <summary>
    /// Receiver double with two channels that records the last value and the call count of each channel.
    /// </summary>
    [TimelineChannels("intensity", "range", ReceiverId = TestLampReceiver.Id)]
    public sealed class TestLampReceiver : Component, ITimelineReceiver {
        /// <summary>
        /// Receiver id the double declares.
        /// </summary>
        public const int Id = 7;

        /// <summary>
        /// Gets the last value received per channel.
        /// </summary>
        public double[] Values { get; } = new double[2];

        /// <summary>
        /// Gets how many values each channel received.
        /// </summary>
        public int[] Calls { get; } = new int[2];

        /// <summary>
        /// Gets the receiver id.
        /// </summary>
        public int TimelineReceiverId {
            get { return Id; }
        }

        /// <summary>
        /// Records one channel value.
        /// </summary>
        /// <param name="channel">Channel index.</param>
        /// <param name="value">Channel value.</param>
        public void SetTimelineValue(int channel, double value) {
            Values[channel] = value;
            Calls[channel]++;
        }
    }
}
