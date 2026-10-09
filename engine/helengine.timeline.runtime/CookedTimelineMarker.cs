namespace helengine.timeline.runtime {
    /// <summary>
    /// One event of a cooked event track. Names and values are indices into
    /// <see cref="CookedTimelineAsset.Strings"/>, so markers carry no strings of their own.
    /// </summary>
    public sealed class CookedTimelineMarker {
        /// <summary>
        /// Gets or sets the tick at which the event fires.
        /// </summary>
        public int Tick { get; set; }

        /// <summary>
        /// Gets or sets the index of the event name in the string table.
        /// </summary>
        public int NameIndex { get; set; }

        /// <summary>
        /// Gets or sets the index of the event value in the string table (the empty string when the marker had none).
        /// </summary>
        public int ValueIndex { get; set; }
    }
}
