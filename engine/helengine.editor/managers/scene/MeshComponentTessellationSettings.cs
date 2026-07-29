namespace helengine.editor {
    /// <summary>
    /// Represents editor-only tessellation settings applied to one MeshComponent for one target platform during cooking.
    /// </summary>
    public sealed class MeshComponentTessellationSettings {
        /// <summary>
        /// Gets the default maximum world-space edge length used when a platform has no explicit component tessellation metadata.
        /// </summary>
        public const double DefaultTessellationMaxEdgeLength = 1.0d;

        /// <summary>
        /// Gets a value indicating whether component-specific model tessellation is enabled during scene cooking.
        /// </summary>
        public bool Tessellate { get; }

        /// <summary>
        /// Gets the maximum world-space triangle edge length permitted before cooking subdivides the component model.
        /// </summary>
        public double TessellationMaxEdgeLength { get; }

        /// <summary>
        /// Gets whether the target platform cooks the owning entity's static scale directly into its render-model variant.
        /// </summary>
        public bool BakeScale { get; }

        /// <summary>
        /// Gets whether enabled tessellation runs while the target platform is packaged instead of while its scene loads.
        /// </summary>
        public bool TessellateAtCookTime { get; }

        /// <summary>
        /// Gets whether enabled scale baking runs while the target platform is packaged instead of while its scene loads.
        /// </summary>
        public bool BakeScaleAtCookTime { get; }

        /// <summary>
        /// Initializes disabled settings with the standard maximum edge length.
        /// </summary>
        public MeshComponentTessellationSettings() : this(false, DefaultTessellationMaxEdgeLength, false, true, true) {
        }

        /// <summary>
        /// Initializes component tessellation settings after validating the requested world-space edge length.
        /// </summary>
        /// <param name="tessellate">Whether cooking should generate a tessellated model variant.</param>
        /// <param name="tessellationMaxEdgeLength">Maximum permitted world-space triangle edge length.</param>
        public MeshComponentTessellationSettings(bool tessellate, double tessellationMaxEdgeLength) : this(tessellate, tessellationMaxEdgeLength, false, true, true) {
        }

        /// <summary>
        /// Initializes component tessellation and static render-scale baking settings.
        /// </summary>
        /// <param name="tessellate">Whether cooking should generate a tessellated model variant.</param>
        /// <param name="tessellationMaxEdgeLength">Maximum permitted world-space triangle edge length.</param>
        /// <param name="bakeScale">Whether cooking should bake the static render scale into the model variant.</param>
        public MeshComponentTessellationSettings(bool tessellate, double tessellationMaxEdgeLength, bool bakeScale) : this(tessellate, tessellationMaxEdgeLength, bakeScale, true, true) {
        }

        /// <summary>
        /// Initializes component tessellation and static render-scale baking settings with independent package-time execution choices.
        /// </summary>
        /// <param name="tessellate">Whether component-specific tessellation is enabled.</param>
        /// <param name="tessellationMaxEdgeLength">Maximum permitted world-space triangle edge length.</param>
        /// <param name="bakeScale">Whether static render scale baking is enabled.</param>
        /// <param name="tessellateAtCookTime">Whether enabled tessellation runs during platform packaging.</param>
        /// <param name="bakeScaleAtCookTime">Whether enabled scale baking runs during platform packaging.</param>
        public MeshComponentTessellationSettings(bool tessellate, double tessellationMaxEdgeLength, bool bakeScale, bool tessellateAtCookTime, bool bakeScaleAtCookTime) {
            ValidateTessellationMaxEdgeLength(tessellationMaxEdgeLength);
            Tessellate = tessellate;
            TessellationMaxEdgeLength = tessellationMaxEdgeLength;
            BakeScale = bakeScale;
            TessellateAtCookTime = tessellateAtCookTime;
            BakeScaleAtCookTime = bakeScaleAtCookTime;
        }

        /// <summary>
        /// Validates that a maximum edge length can safely be used by the geometric subdivision process.
        /// </summary>
        /// <param name="tessellationMaxEdgeLength">Maximum permitted world-space triangle edge length.</param>
        public static void ValidateTessellationMaxEdgeLength(double tessellationMaxEdgeLength) {
            if (!double.IsFinite(tessellationMaxEdgeLength) || tessellationMaxEdgeLength <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(tessellationMaxEdgeLength), "Tessellation maximum edge length must be finite and greater than zero.");
            }
        }
    }
}
