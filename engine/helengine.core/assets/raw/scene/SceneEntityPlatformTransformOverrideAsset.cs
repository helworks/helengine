namespace helengine {
    /// <summary>
    /// Stores one editor-authored entity transform override authored on one override scope path inside a serialized scene asset.
    /// </summary>
    public class SceneEntityPlatformTransformOverrideAsset {
        /// <summary>
        /// Gets or sets the scope path this override is authored on. Empty is Common.
        /// </summary>
        public SceneOverrideScopeStepAsset[] Scope { get; set; } = Array.Empty<SceneOverrideScopeStepAsset>();

        /// <summary>
        /// Gets or sets a value indicating whether the local-position override is authored for this scope.
        /// </summary>
        public bool HasLocalPositionOverride { get; set; }

        /// <summary>
        /// Gets or sets the overridden local position used when <see cref="HasLocalPositionOverride"/> is true.
        /// </summary>
        public float3 LocalPosition { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the local-scale override is authored for this scope.
        /// </summary>
        public bool HasLocalScaleOverride { get; set; }

        /// <summary>
        /// Gets or sets the overridden local scale used when <see cref="HasLocalScaleOverride"/> is true.
        /// </summary>
        public float3 LocalScale { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the local-orientation override is authored for this scope.
        /// </summary>
        public bool HasLocalOrientationOverride { get; set; }

        /// <summary>
        /// Gets or sets the overridden local orientation used when <see cref="HasLocalOrientationOverride"/> is true.
        /// </summary>
        public float4 LocalOrientation { get; set; }
    }
}
