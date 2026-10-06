using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Locks renderer-owned editor resource services to explicit session scope.
/// </summary>
public sealed class RendererSessionIsolationSourceTests {

    static string ResolveSourcePath(string relativePath) {
        string editorRoot = TestSourceRepositoryLocator.ResolveHelEngineRootPath();
        string sourcePath = Path.Combine(editorRoot, "engine", "helengine.editor", relativePath);
        if (!File.Exists(sourcePath)) {
            throw new FileNotFoundException(relativePath, sourcePath);
        }

        return sourcePath;
    }
}
