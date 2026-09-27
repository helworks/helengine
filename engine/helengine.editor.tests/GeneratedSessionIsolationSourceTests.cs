using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the retired global asset-reference facade is absent from the project layout.
/// </summary>
public sealed class GeneratedSessionIsolationSourceTests {

    [Fact]
    public void EditorAssetReferenceFactory_PublicStaticFacadeIsRemoved() {
        string editorRoot = TestSourceRepositoryLocator.ResolveHelEngineRootPath();
        string productionPath = Path.Combine(editorRoot, "engine", "helengine.editor", "managers", "asset", "EditorAssetReferenceFactory.cs");
        string testPath = Path.Combine(editorRoot, "engine", "helengine.editor.tests", "managers", "asset", "EditorAssetReferenceFactoryTests.cs");

        Assert.False(File.Exists(productionPath), $"Legacy facade still exists: {productionPath}");
        Assert.False(File.Exists(testPath), $"Legacy facade test still exists: {testPath}");
    }
}
