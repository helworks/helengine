namespace helengine.architecture.tests;

/// <summary>Locates the current engine checkout without scanning sibling repositories.</summary>
public static class RepositorySourceLocator {
    /// <summary>Finds the checkout containing the expected engine project files.</summary>
    public static string FindRepositoryRoot() {
        DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null) {
            string editorProject = Path.Combine(current.FullName, "engine", "helengine.editor", "helengine.editor.csproj");
            string coreProject = Path.Combine(current.FullName, "engine", "helengine.core", "helengine.core.csproj");
            if (File.Exists(editorProject) && File.Exists(coreProject)) {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the current helengine checkout.");
    }
}