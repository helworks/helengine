namespace helengine {
    /// <summary>
    /// Declares one intermediate render target that passes write and later passes sample.
    /// </summary>
    public class EffectTargetAsset : IDisposable {
        /// <summary>
        /// Gets or sets the target name passes read and write by.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the target size as a fraction of the main input; 0.5 renders a cheaper half-resolution blur.
        /// </summary>
        public float Scale { get; set; } = 1f;

        /// <summary>
        /// Gets or sets the pixel format of the target.
        /// </summary>
        public EffectTargetFormat Format { get; set; } = EffectTargetFormat.Rgba16Float;

        /// <summary>
        /// Initializes an empty target for serializers and object initializers.
        /// </summary>
        public EffectTargetAsset() { }

        /// <summary>
        /// Initializes one named intermediate target.
        /// </summary>
        /// <param name="name">Target name passes read and write by.</param>
        /// <param name="scale">Fraction of the main input size.</param>
        /// <param name="format">Pixel format of the target.</param>
        public EffectTargetAsset(string name, float scale, EffectTargetFormat format) {
            Name = name;
            Scale = scale;
            Format = format;
        }

        /// <summary>
        /// Releases nothing; a target owns no nested allocations beyond its name.
        /// </summary>
        public void Dispose() { }
    }
}
