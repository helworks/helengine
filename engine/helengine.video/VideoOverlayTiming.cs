using helengine.media;

namespace helengine.video {
    /// <summary>
    /// A plain overlay with its resolved interval on the output timeline.
    /// </summary>
    public sealed class VideoOverlayTiming {
        /// <summary>
        /// Creates a timing.
        /// </summary>
        /// <param name="entry">Overlay entry.</param>
        /// <param name="start">Absolute start.</param>
        /// <param name="end">Absolute end (exclusive).</param>
        public VideoOverlayTiming(VideoOverlayEntry entry, MediaTime start, MediaTime end) {
            Entry = entry;
            Start = start;
            End = end;
        }

        /// <summary>
        /// Gets the overlay entry.
        /// </summary>
        public VideoOverlayEntry Entry { get; }

        /// <summary>
        /// Gets the absolute start.
        /// </summary>
        public MediaTime Start { get; }

        /// <summary>
        /// Gets the absolute end (exclusive).
        /// </summary>
        public MediaTime End { get; }
    }
}
