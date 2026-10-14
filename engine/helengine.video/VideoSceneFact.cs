namespace helengine.video {
    /// <summary>
    /// Measured facts about one scene (duration, spoken words, silences and take handles) that are sent to an AI model so
    /// it can plan cuts without reading the whole edit document.
    /// </summary>
    public sealed class VideoSceneFact {
        /// <summary>
        /// Scene id.
        /// </summary>
        public string SceneId { get; set; } = "";

        /// <summary>
        /// Editorial section of the scene; null when the scene has none.
        /// </summary>
        public string Section { get; set; }

        /// <summary>
        /// Whether the scene opens a new non-empty section compared with the previous scene; false for the first scene.
        /// </summary>
        public bool SectionChanges { get; set; } = false;

        /// <summary>
        /// Resolved scene length in seconds.
        /// </summary>
        public double DurationSec { get; set; } = 0;

        /// <summary>
        /// Whether the scene plays a recorded take.
        /// </summary>
        public bool HasTake { get; set; } = false;

        /// <summary>
        /// Dominant visual: image, take or text.
        /// </summary>
        public string Visual { get; set; } = "text";

        /// <summary>
        /// Spoken words inside the kept take segments, in scene-relative seconds.
        /// </summary>
        public List<VideoFactWord> Words { get; set; } = [];

        /// <summary>
        /// Gaps of at least a quarter second between speech and scene edges, in scene-relative seconds.
        /// </summary>
        public List<VideoFactSilence> Silences { get; set; } = [];

        /// <summary>
        /// Unused source seconds before the first take segment that can still be revealed; zero without a take.
        /// </summary>
        public double HandleBeforeSec { get; set; } = 0;

        /// <summary>
        /// Unused source seconds after the last take segment that can still be revealed; zero without a take.
        /// </summary>
        public double HandleAfterSec { get; set; } = 0;

        /// <summary>
        /// Playback speed of the take (source seconds per scene second); zero without a take.
        /// </summary>
        public double Speed { get; set; } = 0;

        /// <summary>
        /// Kept segments of the take in playback order, with their scene times and raw source seconds, so a planner can
        /// propose new ones; a take without segments reports its single interval. Empty without a take.
        /// </summary>
        public List<VideoFactSegment> Segments { get; set; } = [];

        /// <summary>
        /// Scene-relative spans, each [start, end] in seconds, where the speaker's voice is heard; null when the take media
        /// has no voice analysis.
        /// </summary>
        public List<double[]> Voiced { get; set; }

        /// <summary>
        /// Scene-relative spans, each [start, end] in seconds, where the speaker looks away from the camera; null when the
        /// take media has no gaze analysis.
        /// </summary>
        public List<double[]> LookAway { get; set; }
    }
}
