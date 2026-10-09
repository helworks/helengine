namespace helengine.timeline.runtime {
    /// <summary>
    /// Flat, console-ready form of a timeline produced by the tools-side cooker (value kind
    /// <see cref="EditorAssetBinaryValueKind.CookedTimelineAsset"/>). Time is in integer ticks at <see cref="TickRate"/>,
    /// nested timelines are already expanded, slots and channels are indices and receivers are integer ids, so a
    /// <see cref="TimelinePlayerComponent"/> plays it without string handling or lookups. Strings survive only in the
    /// slot-name table (for binding tools) and the event string table.
    /// </summary>
    public class CookedTimelineAsset : Asset, IDisposable {
        /// <summary>
        /// File extension of packaged cooked timelines.
        /// </summary>
        public const string FileExtension = ".hctimeline";

        /// <summary>
        /// Gets or sets the number of ticks per second (for example 60 or 50).
        /// </summary>
        public int TickRate { get; set; }

        /// <summary>
        /// Gets or sets the timeline length in ticks.
        /// </summary>
        public int DurationTicks { get; set; }

        /// <summary>
        /// Gets or sets the slot names in slot-index order; the player expects one entity reference per slot.
        /// </summary>
        public string[] SlotNames { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the event string table that <see cref="CookedTimelineMarker"/> indices point into.
        /// </summary>
        public string[] Strings { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the flat tracks, applied in order.
        /// </summary>
        public CookedTimelineTrack[] Tracks { get; set; } = Array.Empty<CookedTimelineTrack>();

        /// <summary>
        /// Gets the number of binding slots.
        /// </summary>
        public int SlotCount {
            get {
                return SlotNames == null ? 0 : SlotNames.Length;
            }
        }

        /// <summary>
        /// Gets the timeline length in seconds.
        /// </summary>
        public double DurationSeconds {
            get {
                return TickRate <= 0 ? 0 : (double)DurationTicks / TickRate;
            }
        }

        /// <summary>
        /// Releases the tracks and tables the asset owns.
        /// </summary>
        public virtual void Dispose() {
            CookedTimelineTrack[] tracks = Tracks;
            string[] slotNames = SlotNames;
            string[] strings = Strings;
            string[] formerIds = FormerAuthoringAssetIds;
            Tracks = null;
            SlotNames = null;
            Strings = null;
            FormerAuthoringAssetIds = null;
            CookedTimelineOwnership.DisposeArray(tracks);
            CookedTimelineOwnership.DeleteArray(slotNames);
            CookedTimelineOwnership.DeleteArray(strings);
            CookedTimelineOwnership.DeleteArray(formerIds);
        }
    }
}
