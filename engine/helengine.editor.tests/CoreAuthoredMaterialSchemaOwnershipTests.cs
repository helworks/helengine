namespace helengine.editor.tests;

/// <summary>
/// Verifies core-authored materials no longer own shader-specific schema meaning.
/// </summary>
public sealed class CoreAuthoredMaterialSchemaOwnershipTests {

    /// <summary>
    /// Resolves the repository root from the current test assembly location.
    /// </summary>
    /// <returns>Absolute helengine repository root path.</returns>
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
