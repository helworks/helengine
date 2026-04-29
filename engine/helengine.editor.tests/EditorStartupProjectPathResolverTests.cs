using helengine.editor;
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies editor startup project arguments resolve through the shared canonical `.heproj` contract.
/// </summary>
public sealed class EditorStartupProjectPathResolverTests : IDisposable {
    /// <summary>
    /// Gets the isolated temporary project root used by the current test instance.
    /// </summary>
    string TempProjectRootPath { get; }

    /// <summary>
    /// Creates one isolated temporary project root for the current test instance.
    /// </summary>
    public EditorStartupProjectPathResolverTests() {
        TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-startup-project-path-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempProjectRootPath);
    }

    /// <summary>
    /// Deletes the isolated temporary project root after the current test completes.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempProjectRootPath)) {
            Directory.Delete(TempProjectRootPath, true);
        }
    }

    /// <summary>
    /// Ensures startup argument resolution fails explicitly when no project path argument is supplied.
    /// </summary>
    [Fact]
    public void Resolve_WhenProjectArgumentIsMissing_ThrowsInvalidOperationException() {
        EditorStartupProjectPathResolver resolver = new EditorStartupProjectPathResolver();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => resolver.Resolve(Array.Empty<string>()));

        Assert.Equal("Project path argument is required.", exception.Message);
    }

    /// <summary>
    /// Ensures startup argument resolution returns the canonical `.heproj` file path for one valid project argument.
    /// </summary>
    [Fact]
    public void Resolve_WhenProjectArgumentIsValid_ReturnsCanonicalProjectFilePath() {
        string projectFilePath = Path.Combine(TempProjectRootPath, "project.heproj");
        WriteCanonicalProjectFile(projectFilePath);
        EditorStartupProjectPathResolver resolver = new EditorStartupProjectPathResolver();

        string resolvedPath = resolver.Resolve([projectFilePath]);

        Assert.Equal(projectFilePath, resolvedPath);
    }

    /// <summary>
    /// Writes one valid canonical `.heproj` file used by the startup project-resolution tests.
    /// </summary>
    /// <param name="projectFilePath">Project file path to create.</param>
    void WriteCanonicalProjectFile(string projectFilePath) {
        File.WriteAllText(
            projectFilePath,
            """
            {
              "projectFormatVersion": 1,
              "name": "Sample Project",
              "requiredEngineVersion": "0.4.0",
              "supportedPlatforms": [ "windows" ],
              "created": "2026-04-01T00:00:00Z",
              "lastOpened": "2026-04-20T00:00:00Z",
              "version": "2.0.0"
            }
            """);
    }
}
