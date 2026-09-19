using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the project platform group settings service.
    /// </summary>
    public sealed class EditorProjectPlatformGroupsServiceTests : IDisposable {
        readonly string ProjectRootPath;

        public EditorProjectPlatformGroupsServiceTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-platform-groups-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ProjectRootPath);
        }

        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        [Fact]
        public void Load_WhenFileIsMissing_SeedsAnEmptyTreeWithTheDefaultOrder() {
            EditorProjectPlatformGroupsService service = new EditorProjectPlatformGroupsService(ProjectRootPath);

            EditorProjectPlatformGroupsDocument document = service.Load();

            Assert.Empty(document.Groups);
            Assert.Equal(EditorOverrideLevelOrder.Default, document.DefaultLevelOrder);
            Assert.True(File.Exists(Path.Combine(ProjectRootPath, "settings", "platform-groups.json")));
        }

        /// <summary>
        /// Ensures reading against a missing file returns the seeded default order without creating the settings file.
        /// </summary>
        [Fact]
        public void Read_WhenFileIsMissing_ReturnsTheDefaultOrderWithoutCreatingTheFile() {
            EditorProjectPlatformGroupsService service = new EditorProjectPlatformGroupsService(ProjectRootPath);

            EditorProjectPlatformGroupsDocument document = service.Read();

            Assert.Empty(document.Groups);
            Assert.Equal(EditorOverrideLevelOrder.Default, document.DefaultLevelOrder);
            Assert.False(File.Exists(Path.Combine(ProjectRootPath, "settings", "platform-groups.json")));
        }

        /// <summary>
        /// Ensures a malformed settings file is never overwritten and that the parse failure reaches the caller.
        /// </summary>
        [Fact]
        public void LoadAndTryRead_WhenFileIsMalformed_KeepTheFileAndReportTheParseFailure() {
            string settingsFilePath = Path.Combine(ProjectRootPath, "settings", "platform-groups.json");
            Directory.CreateDirectory(Path.GetDirectoryName(settingsFilePath));
            byte[] authoredBytes = System.Text.Encoding.UTF8.GetBytes("{ not json");
            File.WriteAllBytes(settingsFilePath, authoredBytes);
            EditorProjectPlatformGroupsService service = new EditorProjectPlatformGroupsService(ProjectRootPath);

            EditorProjectPlatformGroupsDocument document = service.Load();

            Assert.Empty(document.Groups);
            Assert.Equal(EditorOverrideLevelOrder.Default, document.DefaultLevelOrder);
            Assert.Equal(authoredBytes, File.ReadAllBytes(settingsFilePath));

            Assert.False(service.TryRead(out EditorProjectPlatformGroupsDocument readDocument, out string error));
            Assert.Empty(readDocument.Groups);
            Assert.Contains(settingsFilePath, error);
        }

        /// <summary>
        /// Ensures reading a well-formed settings file reports success and the authored tree.
        /// </summary>
        [Fact]
        public void TryRead_WhenFileIsWellFormed_ReturnsTheAuthoredTree() {
            EditorProjectPlatformGroupsService service = new EditorProjectPlatformGroupsService(ProjectRootPath);
            EditorProjectPlatformGroupsDocument saved = service.Load();
            service.AddGroup(saved, null, "handheld");
            service.Save(saved);

            Assert.True(service.TryRead(out EditorProjectPlatformGroupsDocument document, out string error));
            Assert.Equal("handheld", Assert.Single(document.Groups).Id);
            Assert.Empty(error);
        }

        [Fact]
        public void AddAssignAndSave_RoundTripsANestedTree() {
            EditorProjectPlatformGroupsService service = new EditorProjectPlatformGroupsService(ProjectRootPath);
            EditorProjectPlatformGroupsDocument document = service.Load();

            service.AddGroup(document, null, "consoles");
            service.AddGroup(document, "consoles", "handheld");
            service.AssignPlatform(document, "handheld", "ds");
            service.AssignPlatform(document, "handheld", "psp");
            service.AssignPlatform(document, "consoles", "ps1");
            service.Save(document);

            EditorProjectPlatformGroupsDocument loaded = service.Load();
            EditorProjectPlatformGroupDefinition consoles = Assert.Single(loaded.Groups);
            Assert.Equal("consoles", consoles.Id);
            Assert.Equal(new[] { "ps1" }, consoles.PlatformIds);
            Assert.Equal(new[] { "ds", "psp" }, Assert.Single(consoles.Children).PlatformIds);
            Assert.Equal(new[] { "consoles", "handheld" }, EditorProjectPlatformGroupsService.FindGroupChain(loaded, "PSP"));
            Assert.Empty(EditorProjectPlatformGroupsService.FindGroupChain(loaded, "windows"));
        }

        [Fact]
        public void AssignPlatform_MovesThePlatformOutOfItsPreviousGroup() {
            EditorProjectPlatformGroupsService service = new EditorProjectPlatformGroupsService(ProjectRootPath);
            EditorProjectPlatformGroupsDocument document = service.Load();
            service.AddGroup(document, null, "a");
            service.AddGroup(document, null, "b");
            service.AssignPlatform(document, "a", "ds");

            service.AssignPlatform(document, "b", "ds");

            Assert.Empty(EditorProjectPlatformGroupsService.FindGroup(document, "a").PlatformIds);
            Assert.Equal(new[] { "b" }, EditorProjectPlatformGroupsService.FindGroupChain(document, "ds"));
        }

        [Fact]
        public void AddGroup_RejectsIdsThatCollideWithGroupsOrArePlatformsOnValidate() {
            EditorProjectPlatformGroupsService service = new EditorProjectPlatformGroupsService(ProjectRootPath);
            EditorProjectPlatformGroupsDocument document = service.Load();
            service.AddGroup(document, null, "handheld");

            Assert.Throws<InvalidOperationException>(() => service.AddGroup(document, null, "Handheld"));
            Assert.Throws<ArgumentException>(() => service.AddGroup(document, null, " "));

            service.AddGroup(document, null, "ps1");
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => EditorProjectPlatformGroupsService.Validate(document, new[] { "ps1", "ds" }));
            Assert.Contains("ps1", error.Message);
        }

        [Fact]
        public void Validate_NamesBothGroupsWhenAPlatformAppearsTwice() {
            EditorProjectPlatformGroupsDocument document = new EditorProjectPlatformGroupsDocument {
                Groups = [
                    new EditorProjectPlatformGroupDefinition { Id = "a", PlatformIds = ["ds"] },
                    new EditorProjectPlatformGroupDefinition { Id = "b", PlatformIds = ["DS"] }
                ]
            };

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => EditorProjectPlatformGroupsService.Validate(document, new[] { "ds" }));

            Assert.Contains("'a'", error.Message);
            Assert.Contains("'b'", error.Message);
            Assert.Contains("ds", error.Message);
        }

        [Fact]
        public void RenameAndDelete_UpdateTheTree() {
            EditorProjectPlatformGroupsService service = new EditorProjectPlatformGroupsService(ProjectRootPath);
            EditorProjectPlatformGroupsDocument document = service.Load();
            service.AddGroup(document, null, "consoles");
            service.AddGroup(document, "consoles", "handheld");
            service.AssignPlatform(document, "handheld", "ds");

            service.RenameGroup(document, "handheld", "portable");
            Assert.Equal(new[] { "consoles", "portable" }, EditorProjectPlatformGroupsService.FindGroupChain(document, "ds"));

            service.DeleteGroup(document, "consoles");
            Assert.Empty(document.Groups);
            Assert.Empty(EditorProjectPlatformGroupsService.FindGroupChain(document, "ds"));
        }
    }
}
