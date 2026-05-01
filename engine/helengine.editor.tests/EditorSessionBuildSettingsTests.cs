using System.Reflection;
using System.Runtime.CompilerServices;
using helengine.platforms;
using helengine.projectfile;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies editor-session wiring for the Build Settings workflow.
    /// </summary>
    public class EditorSessionBuildSettingsTests : IDisposable {
        /// <summary>
        /// Temporary project root used for Build Settings session tests.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Canonical project file path used by the current test project.
        /// </summary>
        readonly string ProjectFilePath;

        /// <summary>
        /// Initializes one isolated project root and canonical project file for Build Settings session tests.
        /// </summary>
        public EditorSessionBuildSettingsTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-build-settings-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempProjectRootPath);
            ProjectFilePath = Path.Combine(TempProjectRootPath, "project.heproj");

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes the temporary project directories after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures confirming Build Settings rewrites the project file and preserves the active platform when it remains selected.
        /// </summary>
        [Fact]
        public async Task HandleBuildSettingsDialogConfirmed_WhenActivePlatformStillSupported_RewritesProjectFileAndPreservesActivePlatform() {
            await WriteProjectFileAsync(new List<string> { "windows", "linux" }, "1.0.0-custom");
            WritePlatformManifest(
                "1.0.0-custom",
                new List<AvailablePlatformDescriptor> {
                    new AvailablePlatformDescriptor("windows", "Windows DirectX", string.Empty, "platforms/windows", true),
                    new AvailablePlatformDescriptor("linux", "Linux Vulkan", string.Empty, "platforms/linux", true)
                },
                new List<string> { "windows", "linux" });
            EditorProjectLocalSettingsService localSettingsService = new EditorProjectLocalSettingsService(TempProjectRootPath, new List<string> { "windows", "linux" });
            localSettingsService.SaveActivePlatform("linux");
            EditorSession session = CreateSession(new List<string> { "windows", "linux" }, localSettingsService, "linux");

            InvokePrivate(session, "HandleBuildSettingsDialogConfirmed", new BuildSettingsSelection(new List<string> { "linux" }));

            ProjectFileReadResult readResult = await new ProjectFileReader().ReadAsync(ProjectFilePath);
            Assert.True(readResult.Succeeded);
            Assert.Equal(new List<string> { "linux" }, readResult.Document.SupportedPlatforms);
            Assert.Equal("linux", session.CurrentProjectPlatform);
            Assert.Equal("linux", GetPrivateField<EditorProjectLocalSettingsService>(session, "ProjectLocalSettingsService").LoadActivePlatform());
            Assert.Equal("linux", GetPrivateField<AssetImportManager>(session, "assetImportManager").CurrentPlatformId);
            Assert.Equal(new List<string> { "linux" }, session.SupportedPlatforms);
        }

        /// <summary>
        /// Ensures confirming Build Settings falls back to the first selected platform when the previously active platform is removed.
        /// </summary>
        [Fact]
        public async Task HandleBuildSettingsDialogConfirmed_WhenActivePlatformRemoved_FallsBackToFirstSelectedPlatformAndPersistsIt() {
            await WriteProjectFileAsync(new List<string> { "windows", "linux" }, "1.0.0-custom");
            WritePlatformManifest(
                "1.0.0-custom",
                new List<AvailablePlatformDescriptor> {
                    new AvailablePlatformDescriptor("windows", "Windows DirectX", string.Empty, "platforms/windows", true),
                    new AvailablePlatformDescriptor("linux", "Linux Vulkan", string.Empty, "platforms/linux", false)
                },
                new List<string> { "windows" });
            EditorProjectLocalSettingsService localSettingsService = new EditorProjectLocalSettingsService(TempProjectRootPath, new List<string> { "windows", "linux" });
            localSettingsService.SaveActivePlatform("linux");
            EditorSession session = CreateSession(new List<string> { "windows", "linux" }, localSettingsService, "linux");

            InvokePrivate(session, "HandleBuildSettingsDialogConfirmed", new BuildSettingsSelection(new List<string> { "windows" }));

            ProjectFileReadResult readResult = await new ProjectFileReader().ReadAsync(ProjectFilePath);
            Assert.True(readResult.Succeeded);
            Assert.Equal(new List<string> { "windows" }, readResult.Document.SupportedPlatforms);
            Assert.Equal("windows", session.CurrentProjectPlatform);
            Assert.Equal("windows", GetPrivateField<EditorProjectLocalSettingsService>(session, "ProjectLocalSettingsService").LoadActivePlatform());
            Assert.Equal("windows", GetPrivateField<AssetImportManager>(session, "assetImportManager").CurrentPlatformId);
            Assert.Equal(new List<string> { "windows" }, session.SupportedPlatforms);
        }

        /// <summary>
        /// Ensures opening Build Settings shows the available platforms resolved for the current engine version and checks the currently supported project platforms.
        /// </summary>
        [Fact]
        public async Task HandleBuildSettingsRequested_WhenInvoked_ShowsDialogWithAvailablePlatformsAndCurrentSupportedPlatforms() {
            await WriteProjectFileAsync(new List<string> { "windows" }, "1.0.0-custom");
            WritePlatformManifest(
                "1.0.0-custom",
                new List<AvailablePlatformDescriptor> {
                    new AvailablePlatformDescriptor("windows", "Windows DirectX", string.Empty, "platforms/windows", true),
                    new AvailablePlatformDescriptor("linux", "Linux Vulkan", string.Empty, "platforms/linux", false)
                },
                new List<string> { "windows" });
            EditorProjectLocalSettingsService localSettingsService = new EditorProjectLocalSettingsService(TempProjectRootPath, new List<string> { "windows" });
            localSettingsService.SaveActivePlatform("windows");
            EditorSession session = CreateSession(new List<string> { "windows" }, localSettingsService, "windows");
            BuildSettingsDialog dialog = GetPrivateField<BuildSettingsDialog>(session, "buildSettingsDialog");

            InvokePrivate(session, "HandleBuildSettingsRequested");

            Assert.True(dialog.IsVisible);
            List<CheckBoxComponent> checkBoxes = GetPrivateField<List<CheckBoxComponent>>(dialog, "PlatformCheckBoxes");
            List<TextComponent> labels = GetPrivateField<List<TextComponent>>(dialog, "PlatformLabelTexts");
            Assert.Equal(2, checkBoxes.Count);
            Assert.Equal(new[] { "Windows DirectX", "Linux Vulkan (missing)" }, labels.Select(label => label.Text).ToArray());
            Assert.True(checkBoxes[0].IsChecked);
            Assert.False(checkBoxes[1].IsChecked);
        }

        /// <summary>
        /// Creates one partially initialized editor session containing the collaborators used by Build Settings handling.
        /// </summary>
        /// <param name="supportedPlatforms">Project-supported platforms for the test session.</param>
        /// <param name="localSettingsService">Local-settings service used to persist the active platform.</param>
        /// <param name="activePlatform">Current active platform for the test session.</param>
        /// <returns>Editor session configured for Build Settings tests.</returns>
        EditorSession CreateSession(IReadOnlyList<string> supportedPlatforms, EditorProjectLocalSettingsService localSettingsService, string activePlatform) {
            EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
            AssetImportManager assetImportManager = new AssetImportManager(TempProjectRootPath, new ContentManager(Path.Combine(TempProjectRootPath, "assets")));
            assetImportManager.CurrentPlatformId = activePlatform;

            SetPrivateField(session, "projectPath", TempProjectRootPath);
            SetPrivateField(session, "CanonicalProjectFilePath", ProjectFilePath);
            SetPrivateField(session, "RequiredEngineVersion", "1.0.0-custom");
            SetPrivateField(session, "ProjectSupportedPlatforms", supportedPlatforms);
            SetPrivateField(session, "ProjectLocalSettingsService", localSettingsService);
            SetPrivateField(session, "ActiveProjectPlatform", activePlatform);
            SetPrivateField(session, "assetImportManager", assetImportManager);
            SetPrivateField(session, "buildSettingsDialog", new BuildSettingsDialog(CreateFont()));
            SetPrivateField(session, "availablePlatformProviderResolver", new AvailablePlatformProviderResolver(new PlatformDiscoveryOptions(TempProjectRootPath), new WindowsLauncherInstallRootLocator()));

            return session;
        }

        /// <summary>
        /// Writes one canonical project file for the temporary test project.
        /// </summary>
        /// <param name="supportedPlatforms">Supported platforms that should be written into the project file.</param>
        /// <param name="requiredEngineVersion">Required engine version written into the project file.</param>
        async Task WriteProjectFileAsync(IReadOnlyList<string> supportedPlatforms, string requiredEngineVersion) {
            ProjectFileDocument document = new ProjectFileDocument {
                ProjectFormatVersion = ProjectFileDocument.SupportedProjectFormatVersion,
                Name = "BuildSettingsTestProject",
                Version = "1.0.0",
                RequiredEngineVersion = requiredEngineVersion,
                SupportedPlatforms = new List<string>(supportedPlatforms),
                Created = DateTime.UtcNow,
                LastOpened = DateTime.UtcNow,
                Description = "Build Settings test project"
            };

            await new ProjectFileWriter().WriteAsync(ProjectFilePath, document);
        }

        /// <summary>
        /// Writes one engine-level platform manifest for the requested engine version.
        /// </summary>
        /// <param name="engineVersion">Exact engine version whose available platforms should be published.</param>
        /// <param name="platforms">Available platforms that should be discoverable.</param>
        /// <param name="installedPlatformIds">Platform identifiers whose payload roots should exist on disk.</param>
        void WritePlatformManifest(string engineVersion, IReadOnlyList<AvailablePlatformDescriptor> platforms, IReadOnlyList<string> installedPlatformIds) {
            string sharedToolchainRootPath = TempProjectRootPath;
            string manifestPath = Path.Combine(sharedToolchainRootPath, "platforms.json");
            Directory.CreateDirectory(sharedToolchainRootPath);

            List<string> manifestEntries = new List<string>(platforms.Count);
            for (int i = 0; i < platforms.Count; i++) {
                AvailablePlatformDescriptor platform = platforms[i];
                manifestEntries.Add($$"""
                {
                  "engineVersion": "{{engineVersion}}",
                  "platformId": "{{platform.Id}}",
                  "displayName": "{{platform.DisplayName}}",
                  "builderAssemblyPath": "{{platform.BuilderAssemblyPath}}",
                  "playerSourceRootPath": "{{platform.PlayerSourceRootPath}}"
                }
                """);

                if (installedPlatformIds.Contains(platform.Id)) {
                    string resolvedPlayerSourceRootPath = Path.Combine(sharedToolchainRootPath, platform.PlayerSourceRootPath);
                    Directory.CreateDirectory(resolvedPlayerSourceRootPath);
                }
            }

            string json = """
            {
              "platforms": [
            """ + string.Join(",\n", manifestEntries) + """
              ]
            }
            """;

            File.WriteAllText(manifestPath, json);
        }

        /// <summary>
        /// Invokes one non-public instance method with the supplied arguments.
        /// </summary>
        /// <param name="target">Object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <param name="arguments">Arguments passed to the method.</param>
        void InvokePrivate(object target, string methodName, params object[] arguments) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, arguments);
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<T>(field.GetValue(target));
        }

        /// <summary>
        /// Assigns one non-public instance field.
        /// </summary>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to assign.</param>
        /// <param name="value">Value assigned to the field.</param>
        void SetPrivateField(object target, string fieldName, object value) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        /// <summary>
        /// Creates a minimal font asset that satisfies dialog layout requirements for the tests.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['B'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['x'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                16f,
                64,
                64);
        }
    }
}
