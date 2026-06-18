namespace helengine.editor.tests;

/// <summary>
/// Verifies layout source stays within the reflection APIs supported by native generated-core builds.
/// </summary>
public sealed class LayoutComponentSourceTests {
    /// <summary>
    /// Ensures ancestor provider lookup avoids reflection helpers that are missing from generated native <c>Type</c>.
    /// </summary>
    [Fact]
    public void Layout_component_source_avoids_reflection_based_provider_matching_for_native_codegen() {
        string sourcePath = Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.core",
            "components",
            "LayoutComponent.cs");
        string source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("providerType.IsInstanceOfType(component)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("providerType.IsAssignableFrom(component.GetType())", source, StringComparison.Ordinal);
        Assert.Contains("ResolveAncestorReferenceCanvasProvider()", source, StringComparison.Ordinal);
        Assert.Contains("ResolveAncestorViewportProvider()", source, StringComparison.Ordinal);
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
