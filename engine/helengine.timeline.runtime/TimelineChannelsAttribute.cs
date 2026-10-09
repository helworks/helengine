namespace helengine.timeline.runtime {
    /// <summary>
    /// Declares, on an <see cref="ITimelineReceiver"/> component type, the value channels it accepts and its stable
    /// receiver id. Only tools read it (the cooker's receiver catalog), turning a channel name into its index in
    /// <see cref="Channels"/>; the runtime never uses reflection.
    /// <example><code>
    /// [TimelineChannels("intensity", "range", ReceiverId = 12)]
    /// public sealed class LampComponent : Component, ITimelineReceiver {
    ///     public int TimelineReceiverId { get { return 12; } }
    ///     public void SetTimelineValue(int channel, double value) { ... }
    /// }
    /// </code></example>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TimelineChannelsAttribute : Attribute {
        /// <summary>
        /// Initializes the channel declaration.
        /// </summary>
        /// <param name="channels">Channel names in index order.</param>
        public TimelineChannelsAttribute(params string[] channels) {
            if (channels == null) {
                throw new ArgumentNullException(nameof(channels));
            } else if (channels.Length == 0) {
                throw new ArgumentException("A receiver must declare at least one channel.", nameof(channels));
            }

            Channels = channels;
        }

        /// <summary>
        /// Gets the channel names; a channel's index in this list is the index passed to
        /// <see cref="ITimelineReceiver.SetTimelineValue"/>.
        /// </summary>
        public string[] Channels { get; }

        /// <summary>
        /// Gets or sets the receiver id; must be positive and equal the type's
        /// <see cref="ITimelineReceiver.TimelineReceiverId"/>.
        /// </summary>
        public int ReceiverId { get; set; }
    }
}
