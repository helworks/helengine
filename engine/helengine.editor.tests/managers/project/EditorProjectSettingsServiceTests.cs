using helengine.projectfile;
using Xunit;

namespace helengine.editor.tests.managers.project {
    /// <summary>
    /// Verifies the project settings service changes only the name and description of a project file and keeps
    /// every other field intact.
    /// </summary>
    public sealed class EditorProjectSettingsServiceTests : IDisposable {
        /// <summary>
        /// Temporary project root for the current test instance.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Creates an isolated project root.
        /// </summary>
        public EditorProjectSettingsServiceTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-project-settings-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempProjectRootPath);
        }

        /// <summary>
        /// Deletes the temporary project root.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures load returns the name and description from the project file, with an absent description read as empty.
        /// </summary>
        [Fact]
        public void Load_ReturnsNameAndDescriptionFromProjectFile() {
            string projectFilePath = WriteProjectFile("city", null);
            EditorProjectSettingsService service = new EditorProjectSettingsService(projectFilePath);

            EditorProjectSettings settings = service.Load();

            Assert.Equal("city", settings.Name);
            Assert.Equal(string.Empty, settings.Description);
        }

        /// <summary>
        /// Ensures save rewrites name and description and preserves the engine version, platforms, dates and scene routing.
        /// </summary>
        [Fact]
        public void Save_ChangesNameAndDescriptionAndKeepsEveryOtherField() {
            string projectFilePath = WriteProjectFile("city", "old text");
            EditorProjectSettingsService service = new EditorProjectSettingsService(projectFilePath);

            service.Save(new EditorProjectSettings {
                Name = "  Demo Disc  ",
                Description = " Twelve consoles, one disc. "
            });

            ProjectFileDocument document = ReadProjectFile(projectFilePath);
            Assert.Equal("Demo Disc", document.Name);
            Assert.Equal("Twelve consoles, one disc.", document.Description);
            Assert.Equal("1.0.0", document.Version);
            Assert.Equal("1.0.0+abc123", document.RequiredEngineVersion);
            Assert.Equal(new[] { "windows", "ps1" }, document.SupportedPlatforms);
            Assert.Equal(new DateTime(2026, 4, 8, 22, 28, 0, DateTimeKind.Utc), document.Created);
            Assert.NotNull(document.SceneRouting);
            Assert.Equal("boot", document.SceneRouting.Platforms["ps1"].BootSceneId);
        }

        /// <summary>
        /// Ensures a blank description is stored as absent rather than as an empty string.
        /// </summary>
        [Fact]
        public void Save_WhenDescriptionIsBlank_StoresItAsAbsent() {
            string projectFilePath = WriteProjectFile("city", "old text");
            EditorProjectSettingsService service = new EditorProjectSettingsService(projectFilePath);

            service.Save(new EditorProjectSettings { Name = "city", Description = "   " });

            Assert.Null(ReadProjectFile(projectFilePath).Description);
        }

        /// <summary>
        /// Ensures a blank name is rejected before anything is written.
        /// </summary>
        [Fact]
        public void Save_WhenNameIsBlank_ThrowsAndLeavesTheFileUntouched() {
            string projectFilePath = WriteProjectFile("city", "keep me");
            EditorProjectSettingsService service = new EditorProjectSettingsService(projectFilePath);

            Assert.Throws<ArgumentException>(() => service.Save(new EditorProjectSettings { Name = " ", Description = "x" }));

            ProjectFileDocument document = ReadProjectFile(projectFilePath);
            Assert.Equal("city", document.Name);
            Assert.Equal("keep me", document.Description);
        }

        /// <summary>
        /// Writes a project file with every optional section populated so preservation can be asserted.
        /// </summary>
        string WriteProjectFile(string name, string description) {
            string projectFilePath = Path.Combine(TempProjectRootPath, "project.heproj");
            ProjectFileDocument document = new ProjectFileDocument {
                Name = name,
                Version = "1.0.0",
                RequiredEngineVersion = "1.0.0+abc123",
                SupportedPlatforms = new List<string> { "windows", "ps1" },
                Created = new DateTime(2026, 4, 8, 22, 28, 0, DateTimeKind.Utc),
                LastOpened = new DateTime(2026, 4, 28, 16, 20, 48, DateTimeKind.Utc),
                Description = description,
                SceneRouting = new ProjectSceneRoutingDocument()
            };
            document.SceneRouting.Platforms["ps1"] = new ProjectPlatformSceneRoutingDocument { BootSceneId = "boot" };
            new ProjectFileWriter().WriteAsync(projectFilePath, document).GetAwaiter().GetResult();
            return projectFilePath;
        }

        /// <summary>
        /// Reads the project file back through the shared reader.
        /// </summary>
        static ProjectFileDocument ReadProjectFile(string projectFilePath) {
            ProjectFileReadResult result = new ProjectFileReader().ReadAsync(projectFilePath).GetAwaiter().GetResult();
            Assert.True(result.Succeeded, result.Succeeded ? string.Empty : result.Errors[0].Message);
            return result.Document;
        }
    }
}
