using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Locks the generated asset and shader composition boundary to session-owned
/// state. These source contracts fail until the process-global implementations
/// are replaced by instance services.
/// </summary>
public sealed class GeneratedSessionIsolationSourceTests {
    [Fact]
    public void BuiltInShaderLibrary_DoesNotExposeStaticRuntimeLoadersOrMutableState() {
        string source = File.ReadAllText(ResolveSourcePath("EditorBuiltInShaderAssetLibrary.cs"));

        Assert.DoesNotContain("static ShaderAsset LoadShaderAsset", source, StringComparison.Ordinal);
        Assert.DoesNotContain("static bool TryLoadShaderAssetById", source, StringComparison.Ordinal);
        Assert.DoesNotContain("static readonly", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratedMaterialCache_IsInstanceBoundAndDoesNotReadCoreSingleton() {
        string source = File.ReadAllText(ResolveSourcePath("EngineGeneratedMaterialCache.cs"));

        Assert.DoesNotContain("public static class EngineGeneratedMaterialCache", source, StringComparison.Ordinal);
        Assert.DoesNotContain("static readonly Dictionary", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Core.Instance", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ResetForTests", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratedProviderRegistry_IsInstanceBoundWithoutStaticRegistrationApi() {
        string source = File.ReadAllText(ResolveSourcePath("GeneratedAssetProviderRegistry.cs"));

        Assert.DoesNotContain("public static class GeneratedAssetProviderRegistry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("static readonly Dictionary", source, StringComparison.Ordinal);
        Assert.DoesNotContain("public static void Register", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ResetForTests", source, StringComparison.Ordinal);
    }

    [Fact]
    public void EditorAssetReferenceFactory_PublicStaticFacadeIsRemoved() {
        string editorRoot = TestSourceRepositoryLocator.ResolveHelEngineRootPath();
        string productionPath = Path.Combine(editorRoot, "engine", "helengine.editor", "managers", "asset", "EditorAssetReferenceFactory.cs");
        string testPath = Path.Combine(editorRoot, "engine", "helengine.editor.tests", "managers", "asset", "EditorAssetReferenceFactoryTests.cs");

        Assert.False(File.Exists(productionPath), $"Legacy facade still exists: {productionPath}");
        Assert.False(File.Exists(testPath), $"Legacy facade test still exists: {testPath}");
    }

    static string ResolveSourcePath(string fileName) {
        string editorRoot = TestSourceRepositoryLocator.ResolveHelEngineRootPath();
        string[] candidates = {
            Path.Combine(editorRoot, "engine", "helengine.editor", fileName),
            Path.Combine(editorRoot, "engine", "helengine.editor", "shaders", fileName),
            Path.Combine(editorRoot, "engine", "helengine.editor", "managers", fileName),
            Path.Combine(editorRoot, "engine", "helengine.editor", "managers", "asset", fileName)
        };
        for (int index = 0; index < candidates.Length; index++) {
            if (File.Exists(candidates[index])) {
                return candidates[index];
            }
        }

        throw new FileNotFoundException(fileName, editorRoot);
    }
}
