using System.Text.Json;
using helengine.editor.launcher.Models;
using helengine.editor.launcher.Services;
using Xunit;

namespace helengine.editor.launcher.tests;

/// <summary>
/// Verifies launcher recent-project persistence uses the project file as the canonical identity.
/// </summary>
public sealed class RecentProjectsServiceTests : IDisposable {
    readonly string TempDirectoryPath;

    /// <summary>
    /// Creates one isolated temporary directory for the current test instance.
    /// </summary>
    public RecentProjectsServiceTests() {
        TempDirectoryPath = Path.Combine(Path.GetTempPath(), "helengine-launcher-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempDirectoryPath);
    }

    /// <summary>
    /// Deletes the temporary test directory after each test completes.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempDirectoryPath)) {
            Directory.Delete(TempDirectoryPath, true);
        }
    }

    /// <summary>
    /// Ensures folder-based recent entries drop out once recent-project validity is based on project files.
    /// </summary>
    [Fact]
    public async Task LoadAsync_WhenRecentPathIsDirectory_OmitsTheEntry() {
        string projectDirectoryPath = Path.Combine(TempDirectoryPath, "sample-project");
        Directory.CreateDirectory(projectDirectoryPath);

        string projectsFilePath = Path.Combine(TempDirectoryPath, "projects.json");
        WriteProjectsFile(
            projectsFilePath,
            new RecentProject {
                Name = "Sample Project",
                Path = projectDirectoryPath,
                Created = DateTime.UtcNow.AddDays(-2),
                LastOpened = DateTime.UtcNow.AddDays(-1),
                TimesOpened = 1,
                Description = "directory-backed recent entry"
            });

        RecentProjectsService service = new RecentProjectsService(projectsFilePath);

        IReadOnlyList<RecentProject> projects = await service.LoadAsync();

        Assert.Empty(projects);
    }

    /// <summary>
    /// Ensures an existing project file still loads as a recent-project entry.
    /// </summary>
    [Fact]
    public async Task LoadAsync_WhenRecentProjectFileExists_ReturnsTheEntry() {
        string projectFilePath = Path.Combine(TempDirectoryPath, "sample-project.heproj");
        await File.WriteAllTextAsync(projectFilePath, "{}");

        string projectsFilePath = Path.Combine(TempDirectoryPath, "projects.json");
        WriteProjectsFile(
            projectsFilePath,
            new RecentProject {
                Name = "Sample Project",
                Path = projectFilePath,
                Created = DateTime.UtcNow.AddDays(-2),
                LastOpened = DateTime.UtcNow.AddDays(-1),
                TimesOpened = 2,
                Description = "file-backed recent entry"
            });

        RecentProjectsService service = new RecentProjectsService(projectsFilePath);

        IReadOnlyList<RecentProject> projects = await service.LoadAsync();

        RecentProject project = Assert.Single(projects);
        Assert.Equal(projectFilePath, project.Path);
        Assert.Equal("Sample Project", project.Name);
    }

    /// <summary>
    /// Ensures updating an existing project file entry replaces its metadata instead of duplicating it.
    /// </summary>
    [Fact]
    public async Task AddOrUpdateAsync_WhenFilePathMatchesExisting_ReplacesExistingEntry() {
        string projectFilePath = Path.Combine(TempDirectoryPath, "sample-project.heproj");
        await File.WriteAllTextAsync(projectFilePath, "{}");

        DateTime createdAt = DateTime.UtcNow.AddDays(-3);
        DateTime lastOpenedAt = DateTime.UtcNow.AddDays(-2);

        string projectsFilePath = Path.Combine(TempDirectoryPath, "projects.json");
        WriteProjectsFile(
            projectsFilePath,
            new RecentProject {
                Name = "Old Name",
                Path = projectFilePath,
                Created = createdAt,
                LastOpened = lastOpenedAt,
                TimesOpened = 1,
                Description = "old description",
                Version = "1.0.0",
                RequiredEngineVersion = "engine-old",
                SupportedPlatforms = ["windows"]
            });

        RecentProjectsService service = new RecentProjectsService(projectsFilePath);

        IReadOnlyList<RecentProject> projects = await service.AddOrUpdateAsync(
            new RecentProject {
                Name = "New Name",
                Path = projectFilePath,
                LastOpened = DateTime.UtcNow,
                TimesOpened = 1,
                Description = "new description",
                Version = "2.0.0",
                RequiredEngineVersion = "engine-custom+abc123",
                SupportedPlatforms = ["windows", "linux-custom"]
            });

        RecentProject project = Assert.Single(projects);
        Assert.Equal("New Name", project.Name);
        Assert.Equal(projectFilePath, project.Path);
        Assert.Equal("new description", project.Description);
        Assert.Equal("2.0.0", project.Version);
        Assert.Equal("engine-custom+abc123", project.RequiredEngineVersion);
        Assert.Equal(["windows", "linux-custom"], project.SupportedPlatforms);
        Assert.Equal(createdAt, project.Created);
        Assert.True(project.LastOpened > lastOpenedAt);
        Assert.Equal(2, project.TimesOpened);
    }

    /// <summary>
    /// Writes one launcher recent-project payload to disk using the production JSON shape.
    /// </summary>
    /// <param name="projectsFilePath">Destination recent-project JSON path.</param>
    /// <param name="project">Single recent-project entry to persist.</param>
    static void WriteProjectsFile(string projectsFilePath, RecentProject project) {
        string json = JsonSerializer.Serialize(
            new ProjectsFileModel {
                Projects = new List<RecentProject> { project },
                LastUpdated = DateTime.UtcNow,
                Version = "1.0.0"
            },
            new JsonSerializerOptions {
                WriteIndented = true
            });

        File.WriteAllText(projectsFilePath, json);
    }

    /// <summary>
    /// Mirrors the launcher recent-project document shape for test serialization.
    /// </summary>
    sealed class ProjectsFileModel {
        /// <summary>
        /// Gets or sets the stored recent-project entries.
        /// </summary>
        public List<RecentProject> Projects { get; set; } = new();

        /// <summary>
        /// Gets or sets the last update time written to disk.
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Gets or sets the payload version.
        /// </summary>
        public string Version { get; set; } = string.Empty;
    }
}
