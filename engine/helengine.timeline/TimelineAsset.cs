namespace helengine.timeline {
    /// <summary>
    /// Authoring form of one timeline (<c>.htimeline</c>): a sequence of tracks that animate abstract binding slots over a
    /// fixed duration. Whoever plays the timeline binds the slots (a game binds scene entities, the video pipeline binds
    /// elements of an edit), so the same asset drives an in-game cutscene or a motion graphic. This rich form keeps names,
    /// cues and nesting; a cooker flattens it into the cooked runtime form.
    /// </summary>
    public class TimelineAsset : Asset {
        /// <summary>
        /// File extension used by authored timeline assets.
        /// </summary>
        public const string FileExtension = ".htimeline";

        /// <summary>
        /// Gets or sets the stable timeline identifier libraries and edits reference, such as <c>contrast_three</c>.
        /// </summary>
        public string TimelineId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the contract version; a reference pins this number so incompatible changes are detected.
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// Gets or sets the human-readable name shown in editors and libraries.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the explanation planners and editors read to decide when the timeline fits.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total length in seconds; every clip, cue and marker lies within zero..duration.
        /// </summary>
        public double DurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the named binding slots the tracks animate; the player binds each slot to a concrete target.
        /// </summary>
        public List<TimelineSlotAsset> Slots { get; set; } = new List<TimelineSlotAsset>();

        /// <summary>
        /// Gets or sets the named time points clips and markers may anchor on, such as the moment a word is spoken.
        /// </summary>
        public List<TimelineCueAsset> Cues { get; set; } = new List<TimelineCueAsset>();

        /// <summary>
        /// Gets or sets the tracks in declaration order; later tracks apply after earlier ones on the same target.
        /// </summary>
        public List<TimelineTrackAsset> Tracks { get; set; } = new List<TimelineTrackAsset>();

        /// <summary>
        /// Looks up the authored time of a cue by its exact name.
        /// </summary>
        /// <param name="name">Cue name.</param>
        /// <param name="timeSeconds">Cue time when found; zero otherwise.</param>
        /// <returns>True when a cue with that name exists.</returns>
        public bool TryGetCueTime(string name, out double timeSeconds) {
            for (int index = 0; index < Cues.Count; index++) {
                TimelineCueAsset cue = Cues[index];
                if (cue != null && string.Equals(cue.Name, name, StringComparison.Ordinal)) {
                    timeSeconds = cue.TimeSeconds;
                    return true;
                }
            }
            timeSeconds = 0;
            return false;
        }

        /// <summary>
        /// Finds a binding slot by its exact name.
        /// </summary>
        /// <param name="name">Slot name.</param>
        /// <returns>The slot, or null when the timeline declares no slot with that name.</returns>
        public TimelineSlotAsset FindSlot(string name) {
            for (int index = 0; index < Slots.Count; index++) {
                TimelineSlotAsset slot = Slots[index];
                if (slot != null && string.Equals(slot.Name, name, StringComparison.Ordinal)) {
                    return slot;
                }
            }
            return null;
        }
    }
}
