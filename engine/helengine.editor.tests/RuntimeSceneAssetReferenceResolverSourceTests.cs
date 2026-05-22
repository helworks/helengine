namespace helengine.editor.tests;

/// <summary>
/// Verifies the runtime scene-asset resolver source branches on generic runtime-generation symbols instead of platform-name-specific symbols.
/// </summary>
public sealed class RuntimeSceneAssetReferenceResolverSourceTests {
    /// <summary>
    /// Ensures cooked platform-owned material resolution uses the shared runtime-generation symbol.
    /// </summary>
    [Fact]
    public void RuntimeSceneAssetReferenceResolver_source_uses_generic_cooked_material_resolution_symbol() {
        string sourcePath = Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.core",
            "scene",
            "runtime",
            "RuntimeSceneAssetReferenceResolver.cs");

        string source = File.ReadAllText(sourcePath);

        Assert.Contains("#if HELENGINE_RUNTIME_MATERIAL_RESOLUTION_COOKED_PLATFORM_OWNED", source, StringComparison.Ordinal);
        Assert.Contains("BuildMaterialFromCooked(fullPath)", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures cooked platform-owned model and texture resolution use shared runtime-generation symbols.
    /// </summary>
    [Fact]
    public void RuntimeSceneAssetReferenceResolver_source_uses_generic_cooked_model_and_texture_resolution_symbols() {
        string sourcePath = Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.core",
            "scene",
            "runtime",
            "RuntimeSceneAssetReferenceResolver.cs");

        string source = File.ReadAllText(sourcePath);

        Assert.Contains("#if HELENGINE_RUNTIME_MODEL_RESOLUTION_COOKED_PLATFORM_OWNED", source, StringComparison.Ordinal);
        Assert.Contains("BuildModelFromCooked(fullPath)", source, StringComparison.Ordinal);
        Assert.Contains("#if HELENGINE_RUNTIME_TEXTURE_RESOLUTION_COOKED_PLATFORM_OWNED", source, StringComparison.Ordinal);
        Assert.Contains("BuildTextureFromCooked(fullPath)", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures rooted packaged-path allowance uses the shared runtime-generation symbol.
    /// </summary>
    [Fact]
    public void RuntimeSceneAssetReferenceResolver_source_uses_generic_rooted_packaged_path_symbol() {
        string sourcePath = Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.core",
            "scene",
            "runtime",
            "RuntimeSceneAssetReferenceResolver.cs");

        string source = File.ReadAllText(sourcePath);

        Assert.Contains("#if HELENGINE_RUNTIME_ALLOW_ROOTED_PACKAGED_PATHS", source, StringComparison.Ordinal);
        Assert.Contains("if (Path.IsPathRooted(reference.RelativePath))", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures packaged generated references can still resolve cooked file paths through the shared file-backed path helper.
    /// </summary>
    [Fact]
    public void RuntimeSceneAssetReferenceResolver_source_accepts_generated_references_for_file_backed_packaged_paths() {
        string sourcePath = Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.core",
            "scene",
            "runtime",
            "RuntimeSceneAssetReferenceResolver.cs");

        string source = File.ReadAllText(sourcePath);

        Assert.Contains("reference.SourceKind != SceneAssetReferenceSourceKind.FileSystem", source, StringComparison.Ordinal);
        Assert.Contains("&& reference.SourceKind != SceneAssetReferenceSourceKind.Generated", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures generated model references are tracked as scene-owned runtime models and only use the generated cache within one load scope.
    /// </summary>
    [Fact]
    public void RuntimeSceneAssetReferenceResolver_source_tracks_generated_models_and_clears_generated_cache_between_loads() {
        string sourcePath = Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.core",
            "scene",
            "runtime",
            "RuntimeSceneAssetReferenceResolver.cs");

        string source = File.ReadAllText(sourcePath);

        Assert.Contains("if (reference.SourceKind == SceneAssetReferenceSourceKind.Generated)", source, StringComparison.Ordinal);
        Assert.Contains("ActiveGeneratedModelsByKey.TryGetValue", source, StringComparison.Ordinal);
        Assert.Contains("ActiveGeneratedModelsByKey.Add(generatedAssetKey, generatedModel);", source, StringComparison.Ordinal);
        Assert.Contains("TrackOwnedModel(generatedRuntimeModel);", source, StringComparison.Ordinal);
        Assert.Contains("TrackOwnedModel(generatedModel);", source, StringComparison.Ordinal);
        Assert.Contains("ActiveGeneratedModelsByKey.Clear();", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures generated and cooked material references are tracked as scene-owned runtime materials and generated caches are cleared between loads.
    /// </summary>
    [Fact]
    public void RuntimeSceneAssetReferenceResolver_source_tracks_generated_and_cooked_materials_and_clears_generated_cache_between_loads() {
        string sourcePath = Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.core",
            "scene",
            "runtime",
            "RuntimeSceneAssetReferenceResolver.cs");

        string source = File.ReadAllText(sourcePath);

        Assert.Contains("ActiveGeneratedMaterialsByKey.TryGetValue", source, StringComparison.Ordinal);
        Assert.Contains("ActiveGeneratedMaterialsByKey.Add(generatedAssetKey, generatedCookedRuntimeMaterial);", source, StringComparison.Ordinal);
        Assert.Contains("ActiveGeneratedMaterialsByKey.Add(generatedAssetKey, generatedRawRuntimeMaterial);", source, StringComparison.Ordinal);
        Assert.Contains("TrackOwnedMaterial(generatedRuntimeMaterial);", source, StringComparison.Ordinal);
        Assert.Contains("TrackOwnedMaterial(generatedCookedRuntimeMaterial);", source, StringComparison.Ordinal);
        Assert.Contains("TrackOwnedMaterial(generatedRawRuntimeMaterial);", source, StringComparison.Ordinal);
        Assert.Contains("TrackOwnedMaterial(runtimeMaterial);", source, StringComparison.Ordinal);
        Assert.Contains("ActiveGeneratedMaterialsByKey.Clear();", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves the helengine repository root from the current test assembly location.
    /// </summary>
    /// <returns>Absolute repository root path.</returns>
    static string ResolveRepositoryRootPath() {
        string currentPath = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(currentPath)) {
            string rootMarkerPath = Path.Combine(currentPath, "engine", "helengine.editor", "helengine.editor.csproj");
            if (File.Exists(rootMarkerPath)) {
                return currentPath;
            }

            DirectoryInfo parentDirectory = Directory.GetParent(currentPath);
            if (parentDirectory == null) {
                break;
            }

            currentPath = parentDirectory.FullName;
        }

        throw new InvalidOperationException("Could not resolve the helengine repository root from the current test assembly location.");
    }
}
