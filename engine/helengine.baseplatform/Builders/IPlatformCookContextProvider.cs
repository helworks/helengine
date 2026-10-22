namespace helengine.baseplatform.Builders {
    /// <summary>Lets SDK-dependent platform builders capture one immutable cook snapshot before compilation or cache lookup.</summary>
    public interface IPlatformCookContextProvider {
        /// <summary>Resolves and validates the platform's SDK once before any shader registration or cache lookup for a build.</summary>
        PlatformCookContext CaptureCookContext();
    }
}
