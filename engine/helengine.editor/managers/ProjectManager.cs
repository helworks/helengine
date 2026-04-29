using System.Text;
using System.Text.Json;

namespace helengine.ui.managers {
    /// <summary>
    /// Manages the list of helengine projects, including creation, loading, and persistence.
    /// </summary>
    public class ProjectManager {
        private readonly string _settingsFolder;
        private readonly string _projectsFilePath;
        private List<Project> _projects = new();

        /// <summary>
        /// JSON serializer options used for persisting project metadata.
        /// </summary>
        static readonly JsonSerializerOptions SaveOptions = new() { WriteIndented = true };
        
        /// <summary>
        /// Initializes a new instance of the project manager and ensures the settings directory exists.
        /// </summary>
        public ProjectManager() {
            // Create helengine folder in roaming folder
            var roamingFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var helengineFolder = Path.Combine(roamingFolder, "helengine");
            _settingsFolder = Path.Combine(helengineFolder, "settings");
            _projectsFilePath = Path.Combine(_settingsFolder, "projects.json");
            
            EnsureSettingsFolderExists();
        }
        
        /// <summary>
        /// Ensures the settings folder exists on disk.
        /// </summary>
        private void EnsureSettingsFolderExists() {
            if (!Directory.Exists(_settingsFolder)) {
                Directory.CreateDirectory(_settingsFolder);
            }
        }
        
        /// <summary>
        /// Loads the saved list of projects from disk, validating the paths and ordering by last opened.
        /// </summary>
        /// <returns>Ordered list of valid projects.</returns>
        public async Task<List<Project>> LoadProjectsAsync() {
            try {
                if (!File.Exists(_projectsFilePath)) {
                    _projects = new List<Project>();
                    return _projects;
                }
                
                var json = await File.ReadAllTextAsync(_projectsFilePath);
                var projectsData = JsonSerializer.Deserialize<ProjectsData>(json);
                _projects = projectsData?.Projects ?? new List<Project>();
                
                // Validate that project folders still exist
                _projects = _projects.Where(p => Directory.Exists(p.Path)).ToList();
                
                // Sort by last opened (most recent first)
                _projects = _projects.OrderByDescending(p => p.LastOpened).ToList();
                
                return _projects;
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error loading projects: {ex.Message}");
                _projects = new List<Project>();
                return _projects;
            }
        }
        
        /// <summary>
        /// Persists the current project list to disk.
        /// </summary>
        /// <returns>A task that completes when the file is saved.</returns>
        public async Task SaveProjectsAsync() {
            try {
                var projectsData = new ProjectsData {
                    Projects = _projects,
                    LastUpdated = DateTime.UtcNow
                };
                
                var json = SerializeIndented(projectsData);
                await File.WriteAllTextAsync(_projectsFilePath, json);
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error saving projects: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Creates a new project with folders and metadata, then saves the updated list.
        /// </summary>
        /// <param name="projectName">Name of the project to create.</param>
        /// <param name="projectPath">Path to the empty directory where the project will be created.</param>
        /// <returns>The created project.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown when the target directory does not exist.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the target directory is not empty.</exception>
        public async Task<Project> CreateProjectAsync(string projectName, string projectPath) {
            try {
                // Validate project path
                if (!Directory.Exists(projectPath)) {
                    throw new DirectoryNotFoundException($"Project directory does not exist: {projectPath}");
                }
                
                // Check if directory is empty
                if (Directory.GetFileSystemEntries(projectPath).Length > 0) {
                    throw new InvalidOperationException("Project directory must be empty");
                }
                
                // Create project folder structure
                var assetsFolder = Path.Combine(projectPath, "assets");
                var cacheFolder = Path.Combine(projectPath, "cache");
                var settingsFolder = Path.Combine(projectPath, "settings");
                
                Directory.CreateDirectory(assetsFolder);
                Directory.CreateDirectory(cacheFolder);
                Directory.CreateDirectory(settingsFolder);
                
                // Create project metadata file
                var createdAt = DateTime.UtcNow;
                var projectMetadata = new {
                    Name = projectName,
                    Version = "1.0.0",
                    Created = createdAt,
                    EngineVersion = "helengine v0.1"
                };
                
                var metadataPath = Path.Combine(settingsFolder, "project.json");
                var metadataJson = SerializeIndented(projectMetadata);
                await File.WriteAllTextAsync(metadataPath, metadataJson);
                
                // Create new project object
                var project = new Project {
                    Name = projectName,
                    Path = projectPath,
                    Created = createdAt,
                    LastOpened = createdAt,
                    TimesOpened = 1,
                    Description = $"new project created on {createdAt:MMM dd, yyyy}",
                    Version = "1.0.0"
                };
                
                // Add to projects list and save
                _projects.Insert(0, project); // Add at beginning (most recent)
                await SaveProjectsAsync();
                
                return project;
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error creating project: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Adds an existing project from disk to the recent list, updating metadata when possible.
        /// </summary>
        /// <param name="projectPath">Path to the existing project root.</param>
        /// <returns>The added or existing project.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown when the project path does not exist.</exception>
        public async Task<Project> AddExistingProjectAsync(string projectPath) {
            try {
                if (!Directory.Exists(projectPath)) {
                    throw new DirectoryNotFoundException($"Project directory does not exist: {projectPath}");
                }
                
                // Check if project already exists in list
                var existingProject = _projects.FirstOrDefault(p => string.Equals(p.Path, projectPath, StringComparison.OrdinalIgnoreCase));
                if (existingProject != null) {
                    // Update last opened and increment times opened
                    existingProject.LastOpened = DateTime.UtcNow;
                    existingProject.TimesOpened++;
                    await SaveProjectsAsync();
                    return existingProject;
                }
                
                // Try to read existing project metadata
                var metadataPath = Path.Combine(projectPath, "settings", "project.json");
                string projectName = Path.GetFileName(projectPath);
                DateTime created = Directory.GetCreationTimeUtc(projectPath);
                
                if (File.Exists(metadataPath)) {
                    try {
                        var metadataJson = await File.ReadAllTextAsync(metadataPath);
                        var metadata = JsonSerializer.Deserialize<JsonElement>(metadataJson);
                        
                        if (metadata.TryGetProperty("Name", out var nameElement)) {
                            projectName = nameElement.GetString() ?? projectName;
                        }
                        if (metadata.TryGetProperty("Created", out var createdElement)) {
                            DateTime.TryParse(createdElement.GetString(), out created);
                        }
                    } catch {
                        // If metadata reading fails, use defaults
                    }
                }
                
                var project = new Project {
                    Name = projectName,
                    Path = projectPath,
                    Created = created,
                    LastOpened = DateTime.UtcNow,
                    TimesOpened = 1,
                    Description = "imported existing project",
                    Version = "1.0.0"
                };
                
                _projects.Insert(0, project);
                await SaveProjectsAsync();
                
                return project;
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error adding existing project: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Updates last opened metadata for the specified project and moves it to the top of the list.
        /// </summary>
        /// <param name="project">Project to update.</param>
        /// <returns>A task that completes when the project list is saved.</returns>
        public async Task UpdateProjectLastOpenedAsync(Project project) {
            try {
                project.LastOpened = DateTime.UtcNow;
                project.TimesOpened++;
                
                // Move to front of list
                _projects.Remove(project);
                _projects.Insert(0, project);
                
                await SaveProjectsAsync();
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error updating project: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Removes a project from the list and persists the change.
        /// </summary>
        /// <param name="project">Project to remove.</param>
        /// <returns>A task that completes when the change is saved.</returns>
        public async Task RemoveProjectAsync(Project project) {
            try {
                _projects.Remove(project);
                await SaveProjectsAsync();
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error removing project: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Gets the current ordered list of recent projects.
        /// </summary>
        /// <returns>Copy of the recent projects list.</returns>
        public List<Project> GetRecentProjects() {
            return _projects.ToList();
        }

        /// <summary>
        /// Serializes a value to JSON using indented formatting.
        /// </summary>
        /// <typeparam name="T">Type of the value being serialized.</typeparam>
        /// <param name="value">Value to serialize.</param>
        /// <returns>Indented JSON string.</returns>
        static string SerializeIndented<T>(T value) {
            string json = JsonSerializer.Serialize(value, SaveOptions);
            return ReindentJson(json);
        }

        /// <summary>
        /// Converts two-space indentation to four-space indentation in a JSON string.
        /// </summary>
        /// <param name="json">JSON to reindent.</param>
        /// <returns>Reindented JSON string.</returns>
        static string ReindentJson(string json) {
            var builder = new StringBuilder(json.Length);
            bool inString = false;
            bool escape = false;

            for (int i = 0; i < json.Length; i++) {
                char ch = json[i];

                if (!inString && ch == '\n') {
                    builder.Append(ch);

                    int spaceStart = i + 1;
                    int spaces = 0;
                    while (spaceStart + spaces < json.Length && json[spaceStart + spaces] == ' ') {
                        spaces++;
                    }

                    int indentLevel = spaces / 2;
                    int remainder = spaces % 2;
                    builder.Append(' ', indentLevel * 4 + remainder);
                    i = spaceStart + spaces - 1;
                    escape = false;
                    continue;
                }

                builder.Append(ch);

                if (escape) {
                    escape = false;
                    continue;
                }

                if (ch == '\\' && inString) {
                    escape = true;
                    continue;
                }

                if (ch == '"') {
                    inString = !inString;
                }
            }

            return builder.ToString();
        }
    }
    
    /// <summary>
    /// Serializable wrapper for storing project metadata on disk.
    /// </summary>
    public class ProjectsData {
        /// <summary>
        /// Gets or sets the list of projects.
        /// </summary>
        public List<Project> Projects { get; set; } = new();

        /// <summary>
        /// Gets or sets the last time the project list was updated.
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Gets or sets the schema version of the saved data.
        /// </summary>
        public string Version { get; set; } = "1.0.0";
    }
}
