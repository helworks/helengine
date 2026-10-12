namespace helengine {
    /// <summary>Begins platform-owned native code loading without exposing platform addresses or relocation formats to shared scene scheduling.</summary>
    public interface IRuntimeCodeModuleProvider {
        /// <summary>Creates an owned incremental load operation for an exact validated manifest entry; adapters and their vtables must remain resident.</summary>
        /// <param name="entry">Manifest identity, specialization and dependency policy for the requested module.</param>
        /// <returns>An owned operation whose disposal releases any unpublished code allocation.</returns>
        [NativeOwnedReturn]
        IRuntimeCodeModuleLoadOperation BeginLoad(RuntimeCodeModuleManifestEntry entry);
    }
}
