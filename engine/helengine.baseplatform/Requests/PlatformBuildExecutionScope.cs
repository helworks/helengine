namespace helengine.baseplatform.Requests {

    /// <summary>
    /// Selects how much of the platform build pipeline one build request runs.
    /// </summary>
    public enum PlatformBuildExecutionScope {
        /// <summary>
        /// Runs the whole pipeline: cook content, compile native code and package the platform image.
        /// </summary>
        Full = 0,

        /// <summary>
        /// Cooks content only and stops before native compilation and packaging; used by editor Play.
        /// </summary>
        AssetsOnly = 1
    }
}
