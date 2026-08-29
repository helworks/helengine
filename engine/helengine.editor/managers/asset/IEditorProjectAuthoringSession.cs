namespace helengine.editor {
    /// <summary>
    /// Provides one disposable, project-scoped boundary for editor asset authoring operations.
    /// </summary>
    public interface IEditorProjectAuthoringSession : IDisposable {
        /// <summary>Gets the canonical project root owned by this authoring session.</summary>
        string ProjectRootPath { get; }

        /// <summary>Gets the core that owns this session's generated and renderer graph.</summary>
        Core OwningCore { get; }

        /// <summary>Gets the generated-provider registry owned by this session graph.</summary>
        GeneratedAssetProviderRegistry GeneratedAssetProviders { get; }

        /// <summary>Gets the generated-model cache owned by this session graph.</summary>
        EngineGeneratedModelCache GeneratedModelCache { get; }

        /// <summary>Gets the generated-material cache owned by this session graph.</summary>
        EngineGeneratedMaterialCache GeneratedMaterialCache { get; }

        /// <summary>Gets the renderer-resource graph owned by this session graph.</summary>
        EditorSessionRendererResources RendererResources { get; }

        /// <summary>
        /// Creates a canonical reference for an assets-relative authored file.
        /// </summary>
        /// <param name="relativePath">Path relative to the project assets root.</param>
        /// <param name="expectedKind">Expected editor asset category.</param>
        /// <returns>Canonical reference containing the current asset identity and recovery hash.</returns>
        SceneAssetReference CreateReference(string relativePath, AssetEntryKind expectedKind);

        /// <summary>
        /// Resolves and canonicalizes one saved authored asset reference.
        /// </summary>
        /// <param name="reference">Saved authored asset reference.</param>
        /// <param name="expectedKind">Expected editor asset category.</param>
        /// <returns>Resolution result describing the selected file and canonical reference.</returns>
        AssetReferenceResolution ResolveReference(SceneAssetReference reference, AssetEntryKind expectedKind);

        /// <summary>
        /// Loads one imported model through the host-configured importer pipeline.
        /// </summary>
        /// <param name="relativePath">Path relative to the project assets root.</param>
        /// <returns>Imported runtime model.</returns>
        RuntimeModel LoadImportedRuntimeModel(string relativePath);

        /// <summary>
        /// Writes one current native asset through this session's authoring boundary.
        /// </summary>
        /// <param name="relativePath">Path relative to the project assets root.</param>
        /// <param name="asset">Native asset payload.</param>
        /// <returns>Basic result describing the authored destination.</returns>
        EditorAssetWriteResult WriteAsset(string relativePath, Asset asset);

        /// <summary>
        /// Begins one project-scoped authoring transaction.
        /// </summary>
        /// <returns>New authoring transaction owned by this session.</returns>
        EditorAuthoringTransaction BeginTransaction();

        /// <summary>
        /// Refreshes externally changed authored files before subsequent authoring operations.
        /// </summary>
        void RefreshExternalChanges();

        /// <summary>
        /// Gets the append-only report for automatic repairs performed by this session.
        /// </summary>
        EditorAssetRepairReport RepairReport { get; }
    }

    /// <summary>
    /// Creates project-scoped authoring sessions using host-owned importer registrations.
    /// </summary>
    public interface IEditorProjectAuthoringSessionFactory {
        /// <summary>
        /// Creates one authoring session for a project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        /// <param name="generatedAssetProviders">Scoped provider registry borrowed from the enclosing editor or CLI graph.</param>
        /// <param name="generatedModelCache">Scoped generated model cache shared by this session's save and resolver paths.</param>
        /// <param name="generatedMaterialCache">Scoped generated material cache shared by this session's save and resolver paths.</param>
        /// <param name="rendererResources">Scoped renderer/resource graph shared by this session's resolver and preview paths.</param>
        /// <returns>Disposable project-scoped authoring session.</returns>
        IEditorProjectAuthoringSession CreateSession(
            string projectRootPath,
            GeneratedAssetProviderRegistry generatedAssetProviders,
            EngineGeneratedModelCache generatedModelCache,
            EngineGeneratedMaterialCache generatedMaterialCache,
            EditorSessionRendererResources rendererResources);
    }
}
