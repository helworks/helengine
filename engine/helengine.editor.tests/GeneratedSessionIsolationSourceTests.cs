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

    static string ResolveSourcePath(string fileName) {
        DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null) {
                string candidate = Path.Combine(current.FullName, "helengine.editor", fileName);
                if (File.Exists(candidate)) {
                    return candidate;
                }

                string editorRoot = Path.Combine(current.FullName, "engine", "helengine.editor");
                string[] subdirectories = { string.Empty, "shaders", "managers", Path.Combine("managers", "asset") };
                for (int subdirectoryIndex = 0; subdirectoryIndex < subdirectories.Length; subdirectoryIndex++) {
                    candidate = Path.Combine(editorRoot, subdirectories[subdirectoryIndex], fileName);
                    if (File.Exists(candidate)) {
                        return candidate;
                    }
                }

            current = current.Parent;
        }

        throw new FileNotFoundException(fileName);
    }
}
