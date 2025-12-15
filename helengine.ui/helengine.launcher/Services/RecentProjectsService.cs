using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using helengine.editor.launcher.Models;

namespace helengine.editor.launcher.Services;

public sealed class RecentProjectsService {
    readonly string _projectsFilePath;

    static readonly JsonSerializerOptions SaveOptions = new() { WriteIndented = true };

    public RecentProjectsService() {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var helengineFolder = Path.Combine(appData, "helengine");
        var settingsFolder = Path.Combine(helengineFolder, "settings");
        Directory.CreateDirectory(settingsFolder);
        _projectsFilePath = Path.Combine(settingsFolder, "projects.json");
    }

    public async Task<IReadOnlyList<RecentProject>> LoadAsync() {
        try {
            if (!File.Exists(_projectsFilePath)) {
                return Array.Empty<RecentProject>();
            }

            var json = await File.ReadAllTextAsync(_projectsFilePath);
            var data = JsonSerializer.Deserialize<ProjectsData>(json);
            if (data?.Projects == null) {
                return Array.Empty<RecentProject>();
            }

            return data.Projects
                .Where(p => !string.IsNullOrWhiteSpace(p.Path) && Directory.Exists(p.Path))
                .OrderByDescending(p => p.LastOpened)
                .ToList();
        } catch {
            return Array.Empty<RecentProject>();
        }
    }

    public async Task<IReadOnlyList<RecentProject>> AddOrUpdateAsync(RecentProject project) {
        var projects = (await LoadAsync()).ToList();
        var existing = projects.FirstOrDefault(p => string.Equals(p.Path, project.Path, StringComparison.OrdinalIgnoreCase));
        if (existing != null) {
            existing.Name = string.IsNullOrWhiteSpace(project.Name) ? existing.Name : project.Name;
            existing.Description = string.IsNullOrWhiteSpace(project.Description) ? existing.Description : project.Description;
            existing.Version = string.IsNullOrWhiteSpace(project.Version) ? existing.Version : project.Version;
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

    async Task SaveAsync(List<RecentProject> projects) {
        var data = new ProjectsData {
            Projects = projects,
            LastUpdated = DateTime.UtcNow
        };
        var json = JsonFormatting.SerializeWithIndent(data, SaveOptions);
        await File.WriteAllTextAsync(_projectsFilePath, json);
    }

    sealed class ProjectsData {
        public List<RecentProject> Projects { get; set; } = new();
        public DateTime LastUpdated { get; set; }
        public string Version { get; set; } = "1.0.0";
    }
}
