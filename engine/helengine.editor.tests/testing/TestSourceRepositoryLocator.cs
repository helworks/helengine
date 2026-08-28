namespace helengine.editor.tests;

internal static class TestSourceRepositoryLocator {
    public static string ResolveHelEngineRootPath() {
        string sourceRoot = GeneratedHelengineSourceRoot.Path.Trim();
        if (string.IsNullOrWhiteSpace(sourceRoot)) {
            throw new InvalidOperationException("The embedded HelEngine source root manifest is empty.");
        }

        string fullSourceRoot = Path.GetFullPath(sourceRoot);
        string editorProjectPath = Path.Combine(fullSourceRoot, "engine", "helengine.editor", "helengine.editor.csproj");
        if (!File.Exists(editorProjectPath)) {
            throw new DirectoryNotFoundException($"Embedded HelEngine source root is invalid: {fullSourceRoot}");
        }

        return fullSourceRoot;
    }
}
