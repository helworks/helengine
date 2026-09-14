using Xunit;

namespace helengine.architecture.tests;

/// <summary>Enforces the platform-neutral source boundaries that have landed in the migration.</summary>
public sealed class SharedSourceBoundaryTests {
    [Fact]
    public void RemovedNintendoDsDebugFontHelperIsNotTracked() {
        string root = RepositorySourceLocator.FindRepositoryRoot();
        Assert.False(File.Exists(Path.Combine(root, "helengine.ui", "helengine.editor.app", "NintendoDsDebugFontFactory.cs")));
    }

    [Fact]
    public void SharedMaterialFactoriesDoNotNameConcreteRendererBackends() {
        string root = RepositorySourceLocator.FindRepositoryRoot();
        string[] files = Directory.GetFiles(Path.Combine(root, "engine", "helengine.editor"), "*MaterialFactory.cs", SearchOption.AllDirectories);
        foreach (string file in files) {
            string source = File.ReadAllText(file);
            Assert.DoesNotContain("helengine.directx11", source, StringComparison.Ordinal);
            Assert.DoesNotContain("helengine.vulkan", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void GeneratedBootIdentifierIsOwnedByEngineIdentifiers() {
        string root = RepositorySourceLocator.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root, "engine", "helengine.core", "content", "PlatformMenuSceneResolver.cs"));
        Assert.DoesNotContain("GeneratedBootSceneId", source, StringComparison.Ordinal);
        Assert.Contains("GeneratedBootSceneId", File.ReadAllText(Path.Combine(root, "engine", "helengine.core", "content", "EngineSceneIdentifiers.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void SharedMutationScopeContainsNoNativeImportDeclarations() {
        string root = RepositorySourceLocator.FindRepositoryRoot();
        string scopeSource = File.ReadAllText(Path.Combine(root, "engine", "helengine.editor", "managers", "asset", "EditorAuthoringMutationScope.cs"));
        Assert.DoesNotContain("DllImport", scopeSource, StringComparison.Ordinal);
        string nativeSource = File.ReadAllText(Path.Combine(root, "engine", "helengine.editor", "managers", "asset", "EditorAuthoringNativeMethods.cs"));
        Assert.Contains("DllImport", nativeSource, StringComparison.Ordinal);
    }}