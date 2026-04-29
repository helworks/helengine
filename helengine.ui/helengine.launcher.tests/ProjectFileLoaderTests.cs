using helengine.editor.launcher.Models;
using helengine.editor.launcher.Services;
using Xunit;

namespace helengine.editor.launcher.tests;

/// <summary>
/// Verifies the launcher loads recent-project entries from canonical `.heproj` files.
/// </summary>
public sealed class ProjectFileLoaderTests : IDisposable {
    readonly string TempDirectoryPath;

    /// <summary>
    /// Creates one isolated temporary directory for the current test instance.
    /// </summary>
    public ProjectFileLoaderTests() {
        TempDirectoryPath = Path.Combine(Path.GetTempPath(), "helengine-launcher-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempDirectoryPath);
    }

    /// <summary>
    /// Deletes the isolated temporary directory after the test completes.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempDirectoryPath)) {
            Directory.Delete(TempDirectoryPath, true);
        }
    }

    /// <summary>
    /// Ensures missing project files are rejected instead of creating synthetic recent entries.
    /// </summary>
    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_ThrowsInvalidOperationException() {
        ProjectFileLoader loader = new ProjectFileLoader();

        await Assert.ThrowsAsync<InvalidOperationException>(() => loader.LoadAsync(Path.Combine(TempDirectoryPath, "missing.heproj")));
    }

    /// <summary>
    /// Ensures only `.heproj` files are accepted as launcher projects.
    /// </summary>
    [Fact]
    public async Task LoadAsync_WhenExtensionIsNotHeproj_ThrowsInvalidOperationException() {
        string invalidProjectPath = Path.Combine(TempDirectoryPath, "sample-project.json");
        await File.WriteAllTextAsync(invalidProjectPath, "{}");

        ProjectFileLoader loader = new ProjectFileLoader();

        await Assert.ThrowsAsync<InvalidOperationException>(() => loader.LoadAsync(invalidProjectPath));
    }

    /// <summary>
    /// Ensures incomplete canonical project files are rejected instead of silently fabricating launcher metadata.
    /// </summary>
    [Fact]
    public async Task LoadAsync_WhenCanonicalFieldsAreMissing_ThrowsInvalidOperationException() {
        string projectFilePath = Path.Combine(TempDirectoryPath, "sample-project.heproj");
        await File.WriteAllTextAsync(projectFilePath, "{}");

        ProjectFileLoader loader = new ProjectFileLoader();

        await Assert.ThrowsAsync<InvalidOperationException>(() => loader.LoadAsync(projectFilePath));
    }

    /// <summary>
    /// Ensures the selected project file drives the recent-project metadata when it contains explicit values.
    /// </summary>
    [Fact]
    public async Task LoadAsync_WhenMetadataExists_PopulatesRecentProjectFromProjectFile() {
        string projectFilePath = Path.Combine(TempDirectoryPath, "sample-project.heproj");
        await File.WriteAllTextAsync(
            projectFilePath,
            """
            {
              "projectFormatVersion": 1,
              "name": "Project From File",
              "requiredEngineVersion": "0.4.0",
              "supportedPlatforms": [ "windows", "future-console" ],
              "created": "2026-04-01T00:00:00Z",
              "lastOpened": "2026-04-20T00:00:00Z",
              "description": "project metadata description",
              "version": "2.5.0"
            }
            """);

        ProjectFileLoader loader = new ProjectFileLoader();

        RecentProject project = await loader.LoadAsync(projectFilePath);

        Assert.Equal("Project From File", project.Name);
        Assert.Equal(projectFilePath, project.Path);
        Assert.Equal(DateTime.Parse("2026-04-01T00:00:00Z").ToUniversalTime(), project.Created);
        Assert.Equal(DateTime.Parse("2026-04-20T00:00:00Z").ToUniversalTime(), project.LastOpened);
        Assert.Equal("project metadata description", project.Description);
        Assert.Equal("2.5.0", project.Version);
        Assert.Equal("0.4.0", project.RequiredEngineVersion);
        Assert.Equal(new[] { "windows", "future-console" }, project.SupportedPlatforms);
        Assert.Equal(1, project.TimesOpened);
    }
}
