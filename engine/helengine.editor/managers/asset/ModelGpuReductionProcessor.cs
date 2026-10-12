namespace helengine.editor {
    /// <summary>Builds deterministic paired-wedge edge-collapse render variants with strict target limits.</summary>
    public static class ModelGpuReductionProcessor {
        /// <summary>Changes whenever the output algorithm or seam policy changes, invalidating GPU variants.</summary>
        public const string AlgorithmVersion = "paired-endpoint-quadric-v1";
        /// <summary>Reduces only the supplied independent render asset; impossible seam-safe limits fail explicitly.</summary>
        public static void Apply(ModelAsset asset, ModelGpuReductionSettings settings) {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.Validate();
            if (!settings.Enabled) return;
            new ModelGpuReductionWorkspace(asset, settings).Reduce();
        }
    }
}
