namespace helengine {
    /// <summary>Prepares exact scene asset dependencies asynchronously before synchronous scene deserialization opens memory-backed streams.</summary>
    public interface IRuntimeSceneContentProvider {
        /// <summary>Begins one owned cache lease for all declared asset dependencies of the selected packaged scene.</summary>
        /// <param name="entry">Exact scene catalog entry used to resolve the platform's packaged dependency table.</param>
        /// <returns>Owned preload operation retaining source bytes until scene preparation and loaded callbacks finish. Later raw reads require a fresh completed preload scope.</returns>
        [NativeOwnedReturn]
        IRuntimeSceneContentLoadOperation BeginPreloadScene(RuntimeSceneCatalogEntry entry);
    }
}
