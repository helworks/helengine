namespace helengine.editor.tests.testing;

/// <summary>
/// Shared explicit test double for session-only tests that do not exercise
/// legacy asset-service operations.  Keeping the rejected operations here
/// lets those tests satisfy the session's deliberately broad authoring
/// surface without introducing ambient or optional production adapters.
/// </summary>
public abstract class TestEditorProjectAuthoringSessionBase : IEditorProjectAuthoringSession {
    public virtual string ProjectRootPath => throw Unsupported();
    public virtual Core OwningCore => throw Unsupported();
    public virtual GeneratedAssetProviderRegistry GeneratedAssetProviders => throw Unsupported();
    public virtual EngineGeneratedModelCache GeneratedModelCache => throw Unsupported();
    public virtual EngineGeneratedMaterialCache GeneratedMaterialCache => throw Unsupported();
    public virtual EditorSessionRendererResources RendererResources => throw Unsupported();
    public virtual EditorAssetRepairReport RepairReport { get; } = new EditorAssetRepairReport();

    public virtual SceneAssetReference CreateReference(string relativePath, AssetEntryKind expectedKind) => throw Unsupported();
    public virtual AssetReferenceResolution ResolveReference(SceneAssetReference reference, AssetEntryKind expectedKind) => throw Unsupported();
    public virtual RuntimeModel LoadImportedRuntimeModel(string relativePath) => throw Unsupported();
    public virtual ShaderAsset LoadBuiltInShaderAsset(string shaderFileName) => throw Unsupported();
    public virtual ShaderAsset LoadBuiltInShaderAssetById(string shaderAssetId) => throw Unsupported();
    public virtual EditorAssetWriteResult WriteAsset(string relativePath, Asset asset) => throw Unsupported();
    public virtual EditorAssetWriteResult WriteGeneratedMaterial(string relativePath, GeneratedMaterialAssetDefinition definition, EditorAuthoringTransaction transaction) => throw Unsupported();
    public virtual EditorAssetWriteResult WriteGeneratedFile(string projectRelativePath, byte[] bytes, string expectedPriorContentHash, EditorGeneratedFileKind fileKind, EditorAuthoringTransaction transaction) => throw Unsupported();
    public virtual EditorAssetWriteResult WriteGeneratedCacheAsset(string relativePath, Asset asset, EditorAuthoringTransaction transaction) => throw Unsupported();
    public virtual byte[] ReadStagedFile(string projectRelativePath, EditorAuthoringTransaction transaction) => throw Unsupported();

    public virtual ShaderMaterialAsset LoadMaterialAsset(string relativePath, string platformId, EditorAuthoringTransaction transaction) => throw Unsupported();

    public virtual MaterialAssetProcessorSettings LoadMaterialPlatformSettings(string relativePath, string platformId, EditorAuthoringTransaction transaction) => throw Unsupported();
    public virtual void WriteNativeBlueprint(string relativePath, ComponentPersistenceRegistry persistenceRegistry, string authoringAssetId, EditorAuthoringTransaction transaction) => throw Unsupported();
    public virtual EditorAuthoringTransaction BeginTransaction() => throw Unsupported();
    public virtual bool OwnsTransaction(EditorAuthoringTransaction transaction) => throw Unsupported();
    public virtual void RefreshExternalChanges() { }
    public virtual void Dispose() { }

    public virtual TextureAssetImportSettings LoadOrCreateTextureImportSettings(string sourcePath) => throw Unsupported();
    public virtual void SaveTextureImportSettings(string sourcePath, TextureAssetImportSettings settings) => throw Unsupported();
    public virtual ModelAssetImportSettings LoadOrCreateModelImportSettings(string sourcePath) => throw Unsupported();
    public virtual AudioAssetImportSettings LoadOrCreateAudioImportSettings(string sourcePath) => throw Unsupported();
    public virtual AssetImportSettings LoadOrCreateSectionedImportSettings(string sourcePath) => throw Unsupported();
    public virtual void SaveModelImportSettings(string sourcePath, ModelAssetImportSettings settings) => throw Unsupported();
    public virtual void SaveAudioImportSettings(string sourcePath, AudioAssetImportSettings settings) => throw Unsupported();
    public virtual void SaveSectionedImportSettings(string sourcePath, AssetImportSettings settings) => throw Unsupported();
    public virtual RuntimeModel ResolveRuntimeModel(string sourcePath) => throw Unsupported();
    public virtual FontAsset ResolveFontAsset(string sourcePath) => throw Unsupported();
    public virtual TextureAsset ResolveTextureAsset(string sourcePath) => throw Unsupported();
    public virtual ISceneAssetReferenceResolver CreateSceneAssetReferenceResolver() => throw Unsupported();
    public virtual void WriteNativeAsset(string relativePath, Asset asset) => throw Unsupported();
    public virtual void WriteNativeAsset(string relativePath, Asset asset, string authoringAssetId) => throw Unsupported();
    public virtual void WriteNativeScene(string relativePath, SceneSettingsAsset sceneSettings, Entity[] roots, ComponentPersistenceRegistry persistenceRegistry, string authoringAssetId) => throw Unsupported();
    public virtual bool CanonicalizeAssetReferences(Component component, EntityComponentSaveState saveState) => throw Unsupported();
    public virtual void WriteNativeBlueprint(string relativePath, ComponentPersistenceRegistry persistenceRegistry) => throw Unsupported();
    public virtual void WriteNativeBlueprint(string relativePath, ComponentPersistenceRegistry persistenceRegistry, string authoringAssetId) => throw Unsupported();
    public virtual void WriteGeneratedCacheAsset(string relativePath, Asset asset) => throw Unsupported();
    public virtual void WriteNativeMaterial(string relativePath, GeneratedMaterialAssetDefinition definition) => throw Unsupported();
    public virtual void WriteNativeMaterial(string relativePath, GeneratedMaterialAssetDefinition definition, string authoringAssetId) => throw Unsupported();
    public virtual SceneAssetReference CreateFileReference(string relativePath, AssetEntryKind expectedKind) => throw Unsupported();
    public virtual TAsset LoadNativeAsset<TAsset>(string relativePath) where TAsset : Asset => throw Unsupported();
    public virtual bool TryLoadImportedTextureAsset(string assetId, out TextureAsset textureAsset) {
        textureAsset = null;
        throw Unsupported();
    }
    public virtual IReadOnlyList<string> GetSupportedPlatformIds() => throw Unsupported();

    static NotSupportedException Unsupported() => new NotSupportedException("This session test double does not perform the requested authoring operation.");
}
