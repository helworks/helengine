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
        /// Gets zero; event tracks hold markers, not clips.
        /// </summary>
        public override int ClipCount {
            get {
                return 0;
            }
        }

        /// <summary>
        /// Always throws; event tracks hold markers, not clips.
        /// </summary>
        /// <param name="index">Ignored clip index.</param>
        /// <returns>Never returns.</returns>
        public override TimelineClipAsset GetClip(int index) {
            throw new ArgumentOutOfRangeException(nameof(index), "Event tracks hold markers, not clips.");
        }
    }
}
