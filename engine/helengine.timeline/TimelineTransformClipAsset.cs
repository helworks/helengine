namespace helengine.timeline {
    /// <summary>
    /// A transform clip: independent keyframe channels for position, rotation (degrees) and scale. An empty channel leaves
    /// that part of the transform alone, so a clip may animate only scale for a pop.
    /// </summary>
    public class TimelineTransformClipAsset : TimelineClipAsset {
        /// <summary>
        /// Gets or sets the position keyframes in clip time.
        /// </summary>
        public List<TimelineVectorKeyframeAsset> Position { get; set; } = new List<TimelineVectorKeyframeAsset>();

        /// <summary>
        /// Gets or sets the rotation keyframes in clip time, as Euler angles in degrees.
        /// </summary>
        public List<TimelineVectorKeyframeAsset> Rotation { get; set; } = new List<TimelineVectorKeyframeAsset>();

        /// <summary>
        /// Gets or sets the scale keyframes in clip time.
        /// </summary>
        public List<TimelineVectorKeyframeAsset> Scale { get; set; } = new List<TimelineVectorKeyframeAsset>();
    }
}
