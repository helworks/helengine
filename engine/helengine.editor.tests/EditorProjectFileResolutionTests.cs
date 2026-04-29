using System.Reflection;
using System.Runtime.CompilerServices;
using helengine.editor;
using helengine.projectfile;
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies editor project inputs resolve through the canonical shared `.heproj` contract.
/// </summary>
public sealed class EditorProjectFileResolutionTests : IDisposable {
    /// <summary>
    /// Gets the isolated temporary project root used by the current test instance.
    /// </summary>
    string TempProjectRootPath { get; }

    /// <summary>
    /// Creates one isolated temporary project root for the current test instance.
    /// </summary>
    public EditorProjectFileResolutionTests() {
        TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-project-file-tests", Guid.NewGuid().ToString("N"));
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
    /// Ensures directory-based project inputs resolve to the canonical `project.heproj` file when one exists.
    /// </summary>
    [Fact]
    public void ResolveProjectInput_WhenDirectoryContainsProjectFile_ReturnsCanonicalHeprojPath() {
        string expectedProjectFilePath = Path.Combine(TempProjectRootPath, "project.heproj");
        WriteCanonicalProjectFile(expectedProjectFilePath);

        ProjectFilePathResolver resolver = new ProjectFilePathResolver();

        string resolvedPath = resolver.Resolve(TempProjectRootPath);

        Assert.Equal(expectedProjectFilePath, resolvedPath);
    }

    /// <summary>
    /// Ensures invalid shared project files are rejected during editor project resolution.
    /// </summary>
    [Fact]
    public void ResolveProjectInput_WhenProjectFormatIsUnsupported_ThrowsInvalidOperationException() {
        string projectFilePath = Path.Combine(TempProjectRootPath, "project.heproj");
        File.WriteAllText(
            projectFilePath,
            """
            {
              "projectFormatVersion": 2,
              "name": "Future Project",
              "requiredEngineVersion": "0.4.0",
              "supportedPlatforms": [ "windows" ],
              "created": "2026-04-01T00:00:00Z",
              "lastOpened": "2026-04-20T00:00:00Z",
              "version": "2.0.0"
            }
            """);

        ProjectFilePathResolver resolver = new ProjectFilePathResolver();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => resolver.Resolve(projectFilePath));

        Assert.Equal("Project format version '2' is not supported.", exception.Message);
    }

    /// <summary>
    /// Ensures editor-session display names use the canonical project file name when the caller starts from a project directory.
    /// </summary>
    [Fact]
    public void ResolveProjectDisplayName_WhenDirectoryContainsProjectFile_ReturnsProjectFileName() {
        string projectFilePath = Path.Combine(TempProjectRootPath, "project.heproj");
        WriteCanonicalProjectFile(projectFilePath);
        EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));

        string displayName = (string)InvokePrivate(session, "ResolveProjectDisplayName", TempProjectRootPath);

        Assert.Equal("project.heproj", displayName);
    }

    /// <summary>
    /// Invokes one non-public instance method and returns its result.
    /// </summary>
    /// <param name="target">Target object that owns the method.</param>
    /// <param name="methodName">Method name to invoke.</param>
    /// <param name="arguments">Arguments passed to the method.</param>
    /// <returns>Returned method value.</returns>
    object InvokePrivate(object target, string methodName, params object[] arguments) {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        return method.Invoke(target, arguments);
    }

    /// <summary>
    /// Writes one valid canonical `.heproj` file used by the project-resolution tests.
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
