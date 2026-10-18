namespace helengine.baseplatform.Builders {
    /// <summary>Resolves the presentation metrics that this backend actually builds and launches.</summary>
    public interface IPlatformLayoutTargetProvider {
        /// <summary>Resolves known output geometry; unresolved required backend options fail explicitly.</summary>
        /// <param name="request">Effective selected graphics options and video mode.</param>
        /// <returns>The backend's presentation target, with physical density marked known or unknown.</returns>
        PlatformLayoutTarget ResolveLayoutTarget(PlatformLayoutTargetRequest request);
    }
}
