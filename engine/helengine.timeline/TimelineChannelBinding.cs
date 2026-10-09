namespace helengine.timeline {
    /// <summary>
    /// Where a value channel goes at runtime: the receiver component (by <c>ITimelineReceiver.TimelineReceiverId</c>) and
    /// the channel's index in that receiver's declared channels.
    /// </summary>
    public sealed class TimelineChannelBinding {
        /// <summary>
        /// Initializes a binding.
        /// </summary>
        /// <param name="receiverId">Receiver id.</param>
        /// <param name="channelIndex">Channel index within the receiver.</param>
        public TimelineChannelBinding(int receiverId, int channelIndex) {
            ReceiverId = receiverId;
            ChannelIndex = channelIndex;
        }

        /// <summary>
        /// Gets the receiver id.
        /// </summary>
        public int ReceiverId { get; }

        /// <summary>
        /// Gets the channel index within the receiver.
        /// </summary>
        public int ChannelIndex { get; }
    }
}
