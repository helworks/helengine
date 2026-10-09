using System.Text.Json;

namespace helengine.video {
    /// <summary>
    /// Motion graphics for an overlay authored as a Helengine Timeline (<c>helengine.timeline.v1</c>): the timeline
    /// definition, the edit element bound to each of its slots and the scene moment each of its cues moves to. The
    /// compiler lays the timeline out in the overlay box (its region, or the free part of the frame), scales it to fit and
    /// turns its tracks into composition layers and property animations.
    /// </summary>
    public sealed class VideoOverlayTimeline {
        /// <summary>
        /// Inline timeline JSON (<c>helengine.timeline.v1</c>). Exactly one of this and <see cref="Library"/> is given;
        /// a validated edit always carries the definition.
        /// </summary>
        public JsonElement? Definition { get; set; }

        /// <summary>
        /// Reference to a timeline kept in the host's library; the host must replace it with <see cref="Definition"/>
        /// before validation, otherwise the edit reports <c>unresolved_timeline</c>.
        /// </summary>
        public VideoTimelineLibraryReference Library { get; set; }

        /// <summary>
        /// What every slot of the timeline shows, keyed by slot name: a text, an image or video of the edit, or a solid
        /// rectangle. Every slot is bound exactly once.
        /// </summary>
        public Dictionary<string, VideoTimelineBinding> Bindings { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Scene moment (usually a spoken word) each named cue moves to; clips anchored on a cue shift with it and keep
        /// their length. Cues left out keep their authored time.
        /// </summary>
        public Dictionary<string, VideoMoment> Cues { get; set; } = new(StringComparer.Ordinal);
    }
}
