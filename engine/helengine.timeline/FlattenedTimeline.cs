namespace helengine.timeline {
    /// <summary>
    /// A timeline with nesting, cues and blends resolved, in seconds of the root timeline, produced by
    /// <see cref="TimelineFlattener"/>. Every track targets a root slot by name. It is the shared middle step of the game
    /// cooker (which converts it to ticks and indices) and of other compilers such as the video bridge, which can turn
    /// its segments straight into keyframed animations.
    /// </summary>
    public sealed class FlattenedTimeline {
        /// <summary>
        /// Gets or sets the root timeline id.
        /// </summary>
        public string TimelineId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the root duration in seconds.
        /// </summary>
        public double DurationSeconds { get; set; }

        /// <summary>
        /// Gets the root slots, in declaration order.
        /// </summary>
        public List<TimelineSlotAsset> Slots { get; } = new List<TimelineSlotAsset>();

        /// <summary>
        /// Gets the merged transform-component and value-channel tracks, in order of first appearance.
        /// </summary>
        public List<FlattenedCurveTrack> CurveTracks { get; } = new List<FlattenedCurveTrack>();

        /// <summary>
        /// Gets the activation of each slot that has activation clips, in order of first appearance.
        /// </summary>
        public List<FlattenedActivationTrack> ActivationTracks { get; } = new List<FlattenedActivationTrack>();

        /// <summary>
        /// Gets the animation of each slot that has animation clips, in order of first appearance.
        /// </summary>
        public List<FlattenedAnimationTrack> AnimationTracks { get; } = new List<FlattenedAnimationTrack>();

        /// <summary>
        /// Gets every sound, sorted by start.
        /// </summary>
        public List<FlattenedAudioClip> AudioClips { get; } = new List<FlattenedAudioClip>();

        /// <summary>
        /// Gets every event, sorted by time (authoring order among equal times).
        /// </summary>
        public List<FlattenedEventMarker> Events { get; } = new List<FlattenedEventMarker>();

        /// <summary>
        /// Returns the index of a root slot.
        /// </summary>
        /// <param name="name">Slot name.</param>
        /// <returns>The index, or -1 when the slot is not declared.</returns>
        public int IndexOfSlot(string name) {
            for (int index = 0; index < Slots.Count; index++) {
                if (string.Equals(Slots[index].Name, name, StringComparison.Ordinal)) {
                    return index;
                }
            }
            return -1;
        }
    }
}
