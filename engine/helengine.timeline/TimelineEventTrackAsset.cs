namespace helengine.timeline {
    /// <summary>
    /// Holds named markers delivered to event listeners. Event tracks have no slot and no clips.
    /// </summary>
    public class TimelineEventTrackAsset : TimelineTrackAsset {
        /// <summary>
        /// Gets <see cref="TimelineTrackKind.Event"/>.
        /// </summary>
        public override TimelineTrackKind Kind {
            get {
                return TimelineTrackKind.Event;
            }
        }

        /// <summary>
        /// Gets or sets the markers in time order.
        /// </summary>
        public List<TimelineEventMarkerAsset> Markers { get; set; } = new List<TimelineEventMarkerAsset>();

        /// <summary>
        /// Gets an empty view; event tracks hold markers, not clips.
        /// </summary>
        public override IReadOnlyList<TimelineClipAsset> ClipView {
            get {
                return Array.Empty<TimelineClipAsset>();
            }
        }
    }
}
