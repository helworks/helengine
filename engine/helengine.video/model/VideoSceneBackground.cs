namespace helengine.video {
    /// <summary>
    /// What happens behind the speaker of a scene's take: the original background, a blurred copy of it, or a replacement
    /// image or video. Blur and replace need the take media to carry a matte.
    /// </summary>
    public sealed class VideoSceneBackground {
        /// <summary>
        /// One of original, blur or replace.
        /// </summary>
        public string Mode { get; set; } = "original";

        /// <summary>
        /// Blur radius in output pixels, from 0 to 96; used by blur.
        /// </summary>
        public double Radius { get; set; } = 24;

        /// <summary>
        /// Image or video media id shown behind the speaker; used by replace.
        /// </summary>
        public string Media { get; set; } = "";

        /// <summary>
        /// human when locked against AI replanning.
        /// </summary>
        public string By { get; set; }

    }
}
