namespace helengine {
    /// <summary>
    /// Stores one editor-authored per-platform entity transform override inside a serialized scene asset.
    /// </summary>
    public class SceneEntityPlatformTransformOverrideAsset {
        /// <summary>
        /// Gets or sets the platform identifier that owns this transform override.
        /// </summary>
        public string PlatformId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the local-position override is authored for this platform.
        /// </summary>
        public bool HasLocalPositionOverride { get; set; }

        /// <summary>
        /// Gets or sets the overridden local position used when <see cref="HasLocalPositionOverride"/> is true.
        /// </summary>
        public float3 LocalPosition { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the local-scale override is authored for this platform.
        /// </summary>
        public bool HasLocalScaleOverride { get; set; }

        /// <summary>
        /// Gets or sets the overridden local scale used when <see cref="HasLocalScaleOverride"/> is true.
        /// </summary>
        public float3 LocalScale { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the local-orientation override is authored for this platform.
        /// </summary>
        public bool HasLocalOrientationOverride { get; set; }

        /// <summary>
        /// Gets or sets the overridden local orientation used when <see cref="HasLocalOrientationOverride"/> is true.
        /// </summary>
        public float4 LocalOrientation { get; set; }
    }
}
