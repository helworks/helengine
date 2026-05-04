namespace helengine {
    /// <summary>
    /// Describes how a backend handles unsupported renderer features.
    /// </summary>
    public enum RendererFeatureDowngradeMode {
        /// <summary>
        /// The feature must be supported and should fail otherwise.
        /// </summary>
        Required,

        /// <summary>
        /// The feature may be reduced to a simpler implementation.
        /// </summary>
        Degrade,

        /// <summary>
        /// The feature may be dropped entirely.
        /// </summary>
        Drop
    }
}
