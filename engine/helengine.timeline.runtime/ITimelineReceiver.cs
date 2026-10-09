namespace helengine.timeline.runtime {
    /// <summary>
    /// A component that timeline value tracks can drive. A receiver declares its channels for the tools with
    /// <see cref="TimelineChannelsAttribute"/> (whose <see cref="TimelineChannelsAttribute.ReceiverId"/> must equal
    /// <see cref="TimelineReceiverId"/>); the cooker turns channel names into indices into that list, so at runtime the
    /// player calls <see cref="SetTimelineValue"/> with plain integers and no reflection.
    /// </summary>
    public interface ITimelineReceiver {
        /// <summary>
        /// Gets the stable receiver id this component type declares. Cooked value tracks name their receiver by this id,
        /// so it must never change once timelines are cooked against it and must be unique among receiver types.
        /// </summary>
        int TimelineReceiverId { get; }

        /// <summary>
        /// Receives the current value of one channel. Called every evaluated frame while the channel has a value.
        /// </summary>
        /// <param name="channel">Index of the channel in the type's <see cref="TimelineChannelsAttribute"/> list.</param>
        /// <param name="value">Current channel value.</param>
        void SetTimelineValue(int channel, double value);
    }
}
