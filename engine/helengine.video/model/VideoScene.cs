namespace helengine.video {
    /// <summary>
    /// One scene: its duration source, optional take, entry, voice, visual layers and overlays.
    /// </summary>
    public sealed class VideoScene {
        /// <summary>
        /// Stable scene id.
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Optional script beat this scene realizes.
        /// </summary>
        public string ScriptBeat { get; set; }

        /// <summary>
        /// Optional editorial section, e.g. abertura or pergunta.
        /// </summary>
        public string Section { get; set; }

        /// <summary>
        /// Optional editorial purpose of the scene.
        /// </summary>
        public string Purpose { get; set; }

        /// <summary>
        /// Where the scene length comes from.
        /// </summary>
        public VideoSceneDuration Duration { get; set; } = new();

        /// <summary>
        /// Optional take supplying the video and voice.
        /// </summary>
        public VideoTake Take { get; set; }

        /// <summary>
        /// How the scene enters; absent means a hard cut.
        /// </summary>
        public VideoEntry Entry { get; set; }

        /// <summary>
        /// Adjustments to the take audio.
        /// </summary>
        public VideoVoice Voice { get; set; }

        /// <summary>
        /// Visual layers, composited by order.
        /// </summary>
        public List<VideoLayer> Layers { get; set; } = [];

        /// <summary>
        /// Timed text overlays.
        /// </summary>
        public List<VideoOverlay> Overlays { get; set; } = [];

    }
}
