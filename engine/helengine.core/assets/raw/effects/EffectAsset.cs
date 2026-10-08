namespace helengine {
    /// <summary>
    /// Describes one data-driven multi-pass image effect: the named inputs it samples, the intermediate targets it
    /// allocates, the ordered fullscreen shader passes that read and write them, and the typed parameters it exposes.
    /// The same definition is executed by the media compositor, the offline VFX exporter and, later, camera post-processing.
    /// </summary>
    public class EffectAsset : Asset, IDisposable {
        /// <summary>
        /// Reserved target name a pass writes to when it produces the effect's final image.
        /// </summary>
        public const string OutputTargetName = "Output";

        /// <summary>
        /// File extension used by editor-authored effect assets.
        /// </summary>
        public const string FileExtension = ".heffect";

        /// <summary>
        /// Gets or sets the stable effect identifier compositions reference, such as <c>rainbow-expand</c>.
        /// </summary>
        public string EffectId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable effect name shown in help text and editors.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the effect contract version; a composition pins this number so incompatible edits are detected.
        /// </summary>
        public int EffectVersion { get; set; } = 1;

        /// <summary>
        /// Gets or sets whether the effect processes a layer or blends two scenes as a transition.
        /// </summary>
        public EffectCategory Category { get; set; } = EffectCategory.Layer;

        /// <summary>
        /// Gets or sets how a platform without programmable pixel shaders treats this effect.
        /// </summary>
        public RendererFeatureDowngradeMode DowngradeMode { get; set; } = RendererFeatureDowngradeMode.Required;

        /// <summary>
        /// Gets or sets the named input images; the first entry is the main input whose size defines the output size.
        /// </summary>
        public EffectInputAsset[] Inputs { get; set; } = Array.Empty<EffectInputAsset>();

        /// <summary>
        /// Gets or sets the intermediate render targets the passes exchange data through.
        /// </summary>
        public EffectTargetAsset[] Targets { get; set; } = Array.Empty<EffectTargetAsset>();

        /// <summary>
        /// Gets or sets the fullscreen passes, executed in order; the last pass must write <see cref="OutputTargetName"/>.
        /// </summary>
        public EffectPassAsset[] Passes { get; set; } = Array.Empty<EffectPassAsset>();

        /// <summary>
        /// Gets or sets the typed parameters packed into the shared constant buffer for every pass.
        /// </summary>
        public EffectParameterAsset[] Parameters { get; set; } = Array.Empty<EffectParameterAsset>();

        /// <summary>
        /// Releases the deserialized definition tree once no executor borrows it anymore.
        /// </summary>
        public virtual void Dispose() {
            EffectInputAsset[] inputs = Inputs;
            EffectTargetAsset[] targets = Targets;
            EffectPassAsset[] passes = Passes;
            EffectParameterAsset[] parameters = Parameters;
            string[] formerIds = FormerAuthoringAssetIds;
            Inputs = null;
            Targets = null;
            Passes = null;
            Parameters = null;
            FormerAuthoringAssetIds = null;
            AnimationClipAsset.DisposeOwnedTracks(inputs);
            AnimationClipAsset.DisposeOwnedTracks(targets);
            AnimationClipAsset.DisposeOwnedTracks(passes);
            AnimationClipAsset.DisposeOwnedTracks(parameters);
            AnimationClipAsset.DeleteOwnedArray(formerIds);
        }
    }
}
