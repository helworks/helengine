using helengine.media;
using helengine.timeline;

namespace helengine.video {
    /// <summary>
    /// Publishes overlay timeline support into a capability catalog, so planners know the timeline format, what a video
    /// can bind and compile, and where the contract is documented.
    /// </summary>
    public static class VideoTimelineCapabilities {
        /// <summary>
        /// Repository document that specifies overlay timelines.
        /// </summary>
        public const string Doc = "docs/helengine-media-composition.md#overlay-timelines";

        /// <summary>
        /// Track kinds a video compiles.
        /// </summary>
        public static readonly string[] TrackKinds = ["transform", "value", "activation", "event", "timeline"];

        /// <summary>
        /// Describes overlay timeline support.
        /// </summary>
        /// <returns>Capability descriptor.</returns>
        public static MediaTimelineDescriptor Describe() {
            return new MediaTimelineDescriptor {
                Format = TimelineJson.SchemaId,
                SlotKinds = VideoTimelineValidator.SlotKinds.ToList(),
                TrackKinds = TrackKinds.ToList(),
                ValueChannels = VideoTimelineValidator.ValueChannels.ToList(),
                Curves = [CurveCatalog.Linear, CurveCatalog.Smoothstep, CurveCatalog.EaseOutCubic, CurveCatalog.EaseInQuad, CurveCatalog.EaseOutBack],
                DefaultTextSize = VideoTimelineBinding.DefaultTextSize,
                DefaultMediaSize = VideoTimelineBinding.DefaultMediaSize,
                Doc = Doc
            };
        }

        /// <summary>
        /// Publishes overlay timeline support into a capability catalog, replacing any description it held.
        /// </summary>
        /// <param name="capabilities">Capability catalog to fill.</param>
        public static void Publish(MediaCapabilities capabilities) {
            if (capabilities == null) {
                throw new ArgumentNullException(nameof(capabilities));
            }
            capabilities.Timeline = Describe();
        }
    }
}
