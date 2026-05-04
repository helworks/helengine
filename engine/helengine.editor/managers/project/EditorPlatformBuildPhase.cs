namespace helengine.editor {
    /// <summary>
    /// Enumerates the high-level phases of the shared platform build graph.
    /// </summary>
    internal enum EditorPlatformBuildPhase {
        RegenerateCore = 0,
        CookAssets = 1,
        CompileCode = 2,
        ResolveVariants = 3,
        LayoutMedia = 4,
        WriteContainers = 5,
        PackagePlatform = 6
    }
}
