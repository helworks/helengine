namespace helengine.video {
    /// <summary>
    /// One visual layer of a scene.
    /// </summary>
    public sealed class VideoLayer {
        /// <summary>
        /// Layer id, unique within its scene.
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// take, media or text.
        /// </summary>
        public string Kind { get; set; } = "media";

        /// <summary>
        /// Media id for media layers.
        /// </summary>
        public string Media { get; set; }

        /// <summary>
        /// Text for text layers.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Name of the text style in the edit text styles, for text layers.
        /// </summary>
        public string TextStyle { get; set; }

        /// <summary>
        /// Composite order; higher draws on top.
        /// </summary>
        public int Order { get; set; } = 0;

        /// <summary>
        /// Layout preset or explicit viewport.
        /// </summary>
        public VideoLayout Layout { get; set; }

        /// <summary>
        /// contain or cover.
        /// </summary>
        public string Fit { get; set; } = "contain";

        /// <summary>
        /// Static transform.
        /// </summary>
        public VideoTransform Transform { get; set; }

        /// <summary>
        /// Motion preset.
        /// </summary>
        public VideoMotion Motion { get; set; }

        /// <summary>
        /// Raw keyframe animations in scene time.
        /// </summary>
        public List<VideoAnimation> Animations { get; set; } = [];

        /// <summary>
        /// Catalog layer effects in order.
        /// </summary>
        public List<VideoEffect> Effects { get; set; } = [];

        /// <summary>
        /// Color drawn around a fitted image inside its viewport, as #RRGGBBAA; transparent when absent.
        /// </summary>
        public string PaddingColor { get; set; }

        /// <summary>
        /// Optional mask.
        /// </summary>
        public VideoMask Mask { get; set; }

        /// <summary>
        /// human when locked against AI replanning.
        /// </summary>
        public string By { get; set; }

    }
}
