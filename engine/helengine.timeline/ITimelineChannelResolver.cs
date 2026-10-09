namespace helengine.timeline {
    /// <summary>
    /// Tells the cooker which receiver and channel index a named value channel of a slot maps to.
    /// <see cref="TimelineReceiverCatalog"/> implements it from <c>[TimelineChannels]</c> attributes.
    /// </summary>
    public interface ITimelineChannelResolver {
        /// <summary>
        /// Resolves a channel; implementations throw with a readable message when it cannot be resolved.
        /// </summary>
        /// <param name="slot">Root slot name.</param>
        /// <param name="channel">Channel name, e.g. <c>intensity</c>.</param>
        /// <returns>The binding.</returns>
        TimelineChannelBinding Resolve(string slot, string channel);
    }
}
