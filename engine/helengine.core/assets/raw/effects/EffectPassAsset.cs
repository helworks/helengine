namespace helengine {
    /// <summary>
    /// Describes one fullscreen draw of an effect: which shader runs, which images it samples and where it writes.
    /// </summary>
    public class EffectPassAsset : IDisposable {
        /// <summary>
        /// Gets or sets the HLSL file, relative to the shader root of the catalog that owns the effect.
        /// </summary>
        public string ShaderPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the pixel shader entry point; the vertex stage is always the shared fullscreen triangle.
        /// </summary>
        public string PixelEntryPoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the input or target names bound to texture registers t0, t1, ... in order.
        /// </summary>
        public string[] Reads { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the target this pass renders into, or <see cref="EffectAsset.OutputTargetName"/>.
        /// </summary>
        public string Writes { get; set; } = EffectAsset.OutputTargetName;

        /// <summary>
        /// Gets or sets constants fixed for this pass, such as a blur direction, so one shader serves several passes.
        /// </summary>
        public float4 PassConstants { get; set; }

        /// <summary>
        /// Releases the owned read-name array.
        /// </summary>
        public void Dispose() {
            string[] reads = Reads;
            Reads = null;
            AnimationClipAsset.DeleteOwnedArray(reads);
        }
    }
}
