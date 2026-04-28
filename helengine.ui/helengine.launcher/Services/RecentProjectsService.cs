using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using helengine.editor.launcher.Models;

namespace helengine.editor.launcher.Services;

/// <summary>
/// Persists launcher recent projects using the canonical project-file path for identity and validity checks.
/// </summary>
public sealed class RecentProjectsService {
    readonly string ProjectsFilePath;

    /// <summary>
    /// Defines the JSON formatting used when recent-project data is written to disk.
    /// </summary>
    static readonly JsonSerializerOptions SaveOptions = new() { WriteIndented = true };

    /// <summary>
    /// Creates the service using the standard launcher appdata location.
    /// </summary>
    public RecentProjectsService() {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var helengineFolder = Path.Combine(appData, "helengine");
        var settingsFolder = Path.Combine(helengineFolder, "settings");
        Directory.CreateDirectory(settingsFolder);
        ProjectsFilePath = Path.Combine(settingsFolder, "projects.json");
    }

    /// <summary>
    /// Creates the service using an explicit recent-project storage file path.
    /// </summary>
    /// <param name="projectsFilePath">Absolute JSON path used for recent-project persistence.</param>
    public RecentProjectsService(string projectsFilePath) {
        if (string.IsNullOrWhiteSpace(projectsFilePath)) {
            throw new ArgumentException("A projects file path is required.", nameof(projectsFilePath));
        }

        var directoryPath = Path.GetDirectoryName(projectsFilePath);
        if (string.IsNullOrWhiteSpace(directoryPath)) {
            throw new InvalidOperationException("A projects file path must include a parent directory.");
        }

        Directory.CreateDirectory(directoryPath);
        ProjectsFilePath = projectsFilePath;
    }

    /// <summary>
    /// Loads recent projects whose stored project files still exist.
    /// </summary>
    /// <returns>Valid recent-project entries ordered by most recently opened first.</returns>
    public async Task<IReadOnlyList<RecentProject>> LoadAsync() {
        try {
            if (!File.Exists(ProjectsFilePath)) {
                return Array.Empty<RecentProject>();
            }

            var json = await File.ReadAllTextAsync(ProjectsFilePath);
            var data = JsonSerializer.Deserialize<ProjectsData>(json);
            if (data?.Projects == null) {
                return Array.Empty<RecentProject>();
            }

            return data.Projects
                .Where(p => !string.IsNullOrWhiteSpace(p.Path) && File.Exists(p.Path))
                .OrderByDescending(p => p.LastOpened)
                .ToList();
        } catch {
            return Array.Empty<RecentProject>();
        }
    }

    /// <summary>
    /// Adds a recent project or updates the matching project-file entry when it already exists.
    /// </summary>
    /// <param name="project">Recent-project entry keyed by its canonical project-file path.</param>
    /// <returns>The updated recent-project list ordered by most recently opened first.</returns>
    public async Task<IReadOnlyList<RecentProject>> AddOrUpdateAsync(RecentProject project) {
        var projects = (await LoadAsync()).ToList();
        var existing = projects.FirstOrDefault(p => string.Equals(p.Path, project.Path, StringComparison.OrdinalIgnoreCase));
        if (existing != null) {
            existing.Name = string.IsNullOrWhiteSpace(project.Name) ? existing.Name : project.Name;
            existing.Description = string.IsNullOrWhiteSpace(project.Description) ? existing.Description : project.Description;
            existing.Version = string.IsNullOrWhiteSpace(project.Version) ? existing.Version : project.Version;
            existing.RequiredEngineVersion = string.IsNullOrWhiteSpace(project.RequiredEngineVersion) ? existing.RequiredEngineVersion : project.RequiredEngineVersion;
            if (project.SupportedPlatforms.Count > 0) {
                existing.SupportedPlatforms = project.SupportedPlatforms.ToArray();
            }

            existing.LastOpened = project.LastOpened == default ? existing.LastOpened : project.LastOpened;
            existing.Created = project.Created == default ? existing.Created : project.Created;
            existing.TimesOpened = Math.Max(existing.TimesOpened + 1, project.TimesOpened);
        } else {
            projects.Insert(0, project);
        }

        projects = projects.OrderByDescending(p => p.LastOpened).ToList();
        await SaveAsync(projects);
        return projects;
    }

    /// <summary>
    /// Saves the supplied recent-project list to disk.
    /// </summary>
    /// <param name="projects">Recent-project entries to persist.</param>
    async Task SaveAsync(List<RecentProject> projects) {
        var data = new ProjectsData {
            Projects = projects,
            LastUpdated = DateTime.UtcNow
        };
        var json = JsonFormatting.SerializeWithIndent(data, SaveOptions);
        await File.WriteAllTextAsync(ProjectsFilePath, json);
    }

    /// <summary>
    /// Represents the serialized launcher recent-project document.
    /// </summary>
    sealed class ProjectsData {
        /// <summary>
        /// Gets or sets the stored recent-project entries.
        /// </summary>
        public List<RecentProject> Projects { get; set; } = new();

        /// <summary>
        /// Gets or sets the last update time of the recent-project document.
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Gets or sets the schema version written to disk.
        /// </summary>
        public string Version { get; set; } = "1.0.0";
    }
}
