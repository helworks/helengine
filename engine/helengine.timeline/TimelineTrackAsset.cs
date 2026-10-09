namespace helengine.timeline {
    /// <summary>
    /// Base of every timeline track: its kind, an optional label and the binding slot it animates. Kind-specific subclasses
    /// own their clip or marker lists; the base exposes clips uniformly so validators and cookers can walk any track.
    /// </summary>
    public abstract class TimelineTrackAsset {
        /// <summary>
        /// Gets the fixed kind of this track.
        /// </summary>
        public abstract TimelineTrackKind Kind { get; }

        /// <summary>
        /// Gets or sets an optional human-readable label used in editors and diagnostics.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the binding slot the track animates; empty for event and nested timeline tracks, and optional for
        /// audio tracks (an empty slot plays non-positional audio).
        /// </summary>
        public string Slot { get; set; } = string.Empty;

        /// <summary>
        /// Gets the track's clips through their shared base, so timing rules can be checked for every track kind alike. It
        /// is the subclass's own list seen through a covariant read-only view (null when that list is missing); event tracks
        /// hold markers instead and return an empty view.
        /// </summary>
        public abstract IReadOnlyList<TimelineClipAsset> ClipView { get; }
    }
}
