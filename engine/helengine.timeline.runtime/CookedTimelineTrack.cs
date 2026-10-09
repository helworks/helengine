namespace helengine.timeline.runtime {
    /// <summary>
    /// One flat track of a cooked timeline. A track drives exactly one thing (one transform component, one receiver
    /// channel, one slot's activation, one audio lane, one slot's animation or the event lane), so the player needs no
    /// lookups while playing. Only the array matching <see cref="Kind"/> holds entries; the others stay empty. Every array
    /// is sorted by start tick and, for segments and intervals, never overlaps.
    /// </summary>
    public sealed class CookedTimelineTrack : IDisposable {
        /// <summary>
        /// Gets or sets what the track drives.
        /// </summary>
        public CookedTimelineTrackKind Kind { get; set; }

        /// <summary>
        /// Gets or sets the index of the slot whose entity the track drives, or -1 for event tracks and audio tracks
        /// without an emitter slot.
        /// </summary>
        public int SlotIndex { get; set; } = -1;

        /// <summary>
        /// Gets or sets, for value tracks, the <see cref="ITimelineReceiver.TimelineReceiverId"/> of the receiver
        /// component on the slot entity; 0 for other kinds.
        /// </summary>
        public int ReceiverId { get; set; }

        /// <summary>
        /// Gets or sets the channel index: the receiver's channel for value tracks, a
        /// <see cref="CookedTimelineTransformChannel"/> for transform tracks, 0 otherwise.
        /// </summary>
        public int ChannelIndex { get; set; }

        /// <summary>
        /// Gets or sets how a transform track combines with the bound transform; <see cref="CookedTimelineTransformMode.Absolute"/>
        /// for other kinds.
        /// </summary>
        public CookedTimelineTransformMode Mode { get; set; }

        /// <summary>
        /// Gets or sets the segments of a transform or value track.
        /// </summary>
        public CookedTimelineSegment[] Segments { get; set; } = Array.Empty<CookedTimelineSegment>();

        /// <summary>
        /// Gets or sets the intervals of an activation track.
        /// </summary>
        public CookedTimelineInterval[] Intervals { get; set; } = Array.Empty<CookedTimelineInterval>();

        /// <summary>
        /// Gets or sets the sounds of an audio track.
        /// </summary>
        public CookedTimelineAudioClip[] AudioClips { get; set; } = Array.Empty<CookedTimelineAudioClip>();

        /// <summary>
        /// Gets or sets the animations of an animation track.
        /// </summary>
        public CookedTimelineAnimationClip[] AnimationClips { get; set; } = Array.Empty<CookedTimelineAnimationClip>();

        /// <summary>
        /// Gets or sets the markers of an event track.
        /// </summary>
        public CookedTimelineMarker[] Markers { get; set; } = Array.Empty<CookedTimelineMarker>();

        /// <summary>
        /// Releases the arrays the track owns.
        /// </summary>
        public void Dispose() {
            CookedTimelineSegment[] segments = Segments;
            CookedTimelineInterval[] intervals = Intervals;
            CookedTimelineAudioClip[] audioClips = AudioClips;
            CookedTimelineAnimationClip[] animationClips = AnimationClips;
            CookedTimelineMarker[] markers = Markers;
            Segments = null;
            Intervals = null;
            AudioClips = null;
            AnimationClips = null;
            Markers = null;
            CookedTimelineOwnership.DeleteArray(segments);
            CookedTimelineOwnership.DeleteArray(intervals);
            CookedTimelineOwnership.DeleteArray(audioClips);
            CookedTimelineOwnership.DeleteArray(animationClips);
            CookedTimelineOwnership.DeleteArray(markers);
        }
    }
}
