using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Locks renderer-owned editor resource services to explicit session scope.
/// </summary>
public sealed class RendererSessionIsolationSourceTests {
    [Theory]
    [InlineData("components/EditorCameraVisualResources.cs")]
    [InlineData("components/EditorDirectionalLightVisualResources.cs")]
    [InlineData("components/EditorPointLightVisualResources.cs")]
    [InlineData("components/EditorSpotLightVisualResources.cs")]
    [InlineData("components/preview2d/EditorWorldSpace2DPreviewMeshResources.cs")]
    [InlineData("managers/scene/EditorViewportBorderGizmoMeshResources.cs")]
    public void RendererOwnedResourceTypes_AreInstanceBound(string fileName) {
        string source = File.ReadAllText(ResolveSourcePath(fileName));

        Assert.DoesNotContain("public static class", source, StringComparison.Ordinal);
        Assert.DoesNotContain("static RuntimeModel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Core.Instance", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ResetForTests", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AffectedEditorPaths_DoNotUseCoreSingletonForRendererOwnedResources() {
        string[] sourceFiles = {
            "EditorSession.cs",
            Path.Combine("serialization", "scene", "EditorSceneAssetReferenceResolver.cs"),
            Path.Combine("managers", "preview", "ModelPreviewSource.cs"),
            Path.Combine("managers", "project", "EditorWindowsBuildScenePackager.cs"),
            Path.Combine("components", "preview2d", "EditorWorldSpace2DPreviewComponentBase.cs"),
            Path.Combine("components", "preview2d", "EditorExact2DWorldPreviewComponentBase.cs"),
            Path.Combine("components", "EditorViewportBorderGizmoComponent.cs"),
            Path.Combine("managers", "preview", "CameraPreviewSource.cs"),
            Path.Combine("managers", "scene", "EditorExact2DPreviewCaptureService.cs")
        };

        foreach (string relativePath in sourceFiles) {
            string source = File.ReadAllText(ResolveSourcePath(relativePath));
            Assert.DoesNotContain("Core.Instance.RenderManager", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Core.Instance.ObjectManager.Remove", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SaveGraph_RequiresExplicitGeneratedCachesAndNeverFallsBackToNull() {
        string sceneSaveSource = File.ReadAllText(ResolveSourcePath("serialization/scene/SceneSaveService.cs"));
        string blueprintSaveSource = File.ReadAllText(ResolveSourcePath("serialization/blueprint/BlueprintSaveService.cs"));
        string authoringSource = File.ReadAllText(ResolveSourcePath("managers/project/EditorProjectAssetAuthoringService.cs"));
        string inferenceSource = File.ReadAllText(ResolveSourcePath("serialization/scene/SceneAssetReferenceInferenceService.cs"));

        Assert.DoesNotContain(": this(projectRootPath, persistenceRegistry, null)", sceneSaveSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new EditorAssetReferenceResolver(projectRootPath)", sceneSaveSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new EditorAssetReferenceResolver(projectRootPath)", blueprintSaveSource, StringComparison.Ordinal);
        Assert.Contains("generatedModelCache", authoringSource, StringComparison.Ordinal);
        Assert.Contains("generatedMaterialCache", authoringSource, StringComparison.Ordinal);
        Assert.DoesNotContain("public SceneAssetReferenceInferenceService(string projectRootPath)", inferenceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedModelCache != null", inferenceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedMaterialCache?.", inferenceSource, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthoringSessionFactory_RequiresOneBorrowedRegistry() {
        string interfaceSource = File.ReadAllText(ResolveSourcePath("managers/asset/IEditorProjectAuthoringSession.cs"));
        string factorySource = File.ReadAllText(ResolveSourcePath("managers/project/EditorProjectAssetAuthoringServiceFactory.cs"));
        string sessionSource = File.ReadAllText(ResolveSourcePath("managers/asset/EditorProjectAuthoringSession.cs"));

        Assert.DoesNotContain("GeneratedAssetProviderRegistry generatedAssetProviders = null", interfaceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedAssetProviderRegistry generatedAssetProviders = null", factorySource, StringComparison.Ordinal);
        Assert.DoesNotContain("generatedAssetProviders ?? new GeneratedAssetProviderRegistry()", sessionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ShaderLibrary_DisposesCachedAssetsUnderOneSynchronizationBoundary() {
        string source = File.ReadAllText(ResolveSourcePath("shaders/EditorBuiltInShaderAssetLibrary.cs"));

        Assert.Contains("ShaderAsset previousShaderAsset", source, StringComparison.Ordinal);
        Assert.Contains("previousShaderAsset.Dispose()", source, StringComparison.Ordinal);
        Assert.Contains("foreach (ShaderAsset shaderAsset in ShaderAssetsByKey.Values)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("if (IsDisposed) {\n                return;\n            }\n            ShaderAsset shaderAsset", source.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    static string ResolveSourcePath(string relativePath) {
        string editorRoot = new helengine.editor.EditorSourceBuildWorkspaceLocator().ResolveHelEngineRootPath();
        string sourcePath = Path.Combine(editorRoot, "engine", "helengine.editor", relativePath);
        if (!File.Exists(sourcePath)) {
            throw new FileNotFoundException(relativePath, sourcePath);
        }

        return sourcePath;
    }
}
