using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using helengine.editor.tests.testing;
using helengine.platforms;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies editor-session wiring for the project platforms workflow.
    /// </summary>
    public sealed class EditorSessionPlatformsTests : IDisposable {
        /// <summary>
        /// Temporary project root used for project-platform session tests.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes one isolated project root for the current test instance.
        /// </summary>
        public EditorSessionPlatformsTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-platforms-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempProjectRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Deletes the temporary project directory after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures opening Platforms loads `settings/platforms.json` instead of reading supported platforms from `.heproj`.
        /// </summary>
        [Fact]
        public void HandlePlatformsRequested_WhenInvoked_ShowsPlatformsDialogFromProjectSettingsFile() {
            WritePlatformsSettingsFile("windows", "ps2");
            WritePlatformManifest(
                "1.0.0-custom",
                [
                    new AvailablePlatformDescriptor("windows", "Windows DirectX", string.Empty, "platforms/windows", true),
                    new AvailablePlatformDescriptor("ps2", "PlayStation 2", string.Empty, "platforms/ps2", true)
                ],
                ["windows", "ps2"]);
            EditorProjectLocalSettingsService localSettingsService = new EditorProjectLocalSettingsService(TempProjectRootPath, ["windows", "ps2"]);
            localSettingsService.SaveActivePlatform("windows");
            EditorSession session = CreateSession(["windows"], localSettingsService, "windows");

            InvokePrivate(session, "HandlePlatformsRequested");

            PlatformsDialog dialog = GetPrivateField<PlatformsDialog>(session, "platformsDialog");
            ComboBoxComponent activePlatformComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ActivePlatformComboBox");
            List<CheckBoxComponent> checkBoxes = GetPrivateField<List<CheckBoxComponent>>(dialog, "PlatformCheckBoxes");
            Assert.True(dialog.Enabled);
            Assert.Equal("windows", activePlatformComboBox.SelectedItem);
            Assert.Equal(2, checkBoxes.Count);
            Assert.True(checkBoxes[0].IsChecked);
            Assert.True(checkBoxes[1].IsChecked);
        }

        /// <summary>
        /// Ensures saving Platforms writes `settings/platforms.json` and updates the user-local active platform explicitly chosen by the dialog.
        /// </summary>
        [Fact]
        public void HandlePlatformsDialogConfirmed_WhenSelectionIsValid_WritesProjectPlatformsAndUserActivePlatform() {
            WritePlatformsSettingsFile("windows", "ps2");
            WritePlatformManifest(
                "1.0.0-custom",
                [
                    new AvailablePlatformDescriptor("windows", "Windows DirectX", string.Empty, "platforms/windows", true),
                    new AvailablePlatformDescriptor("ps2", "PlayStation 2", string.Empty, "platforms/ps2", true)
                ],
                ["windows", "ps2"]);
            EditorProjectLocalSettingsService localSettingsService = new EditorProjectLocalSettingsService(TempProjectRootPath, ["windows", "ps2"]);
            localSettingsService.SaveActivePlatform("windows");
            EditorSession session = CreateSession(["windows", "ps2"], localSettingsService, "windows");

            InvokePrivate(session, "HandlePlatformsDialogConfirmed", new PlatformsSelection(["ps2"], "ps2"));

            Assert.Equal(["ps2"], ReadSupportedPlatformsFromDisk());
            Assert.Equal("ps2", session.CurrentProjectPlatform);
            Assert.Equal("ps2", GetPrivateField<EditorProjectLocalSettingsService>(session, "ProjectLocalSettingsService").LoadActivePlatform());
            Assert.Equal("ps2", GetPrivateField<AssetImportManager>(session, "assetImportManager").CurrentPlatformId);
            Assert.Equal(["ps2"], session.SupportedPlatforms);
        }

        /// <summary>
        /// Ensures switching the active project platform refreshes the host title suffix immediately.
        /// </summary>
        [Fact]
        public void SetActiveProjectPlatform_WhenPlatformChanges_RefreshesWindowTitle() {
            WritePlatformsSettingsFile("windows", "ps2");
            WritePlatformManifest(
                "1.0.0-custom",
                [
                    new AvailablePlatformDescriptor("windows", "Windows DirectX", string.Empty, "platforms/windows", true),
                    new AvailablePlatformDescriptor("ps2", "PlayStation 2", string.Empty, "platforms/ps2", true)
                ],
                ["windows", "ps2"]);
            EditorProjectLocalSettingsService localSettingsService = new EditorProjectLocalSettingsService(TempProjectRootPath, ["windows", "ps2"]);
            localSettingsService.SaveActivePlatform("windows");
            EditorSession session = CreateSession(["windows", "ps2"], localSettingsService, "windows");

            session.SetActiveProjectPlatform("ps2");

            Assert.Equal("helengine - project.heproj [PS2]", session.WindowTitle);
        }

        /// <summary>
        /// Ensures dirty scene titles preserve the active platform suffix.
        /// </summary>
        [Fact]
        public void RefreshWindowTitle_WhenSceneIsDirtyAndPlatformIsActive_AppendsDirtyMarkerBeforePlatformSuffix() {
            WritePlatformsSettingsFile("windows", "ps2");
            WritePlatformManifest(
                "1.0.0-custom",
                [
                    new AvailablePlatformDescriptor("windows", "Windows DirectX", string.Empty, "platforms/windows", true),
                    new AvailablePlatformDescriptor("ps2", "PlayStation 2", string.Empty, "platforms/ps2", true)
                ],
                ["windows", "ps2"]);
            EditorProjectLocalSettingsService localSettingsService = new EditorProjectLocalSettingsService(TempProjectRootPath, ["windows", "ps2"]);
            localSettingsService.SaveActivePlatform("windows");
            EditorSession session = CreateSession(["windows", "ps2"], localSettingsService, "windows");
            string currentScenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "DirtyScene.helen");

            SetPrivateField(session, "CurrentScenePath", currentScenePath);
            SetPrivateField(session, "ActiveProjectPlatform", "ps2");
            InvokePrivate(session, "HandleSceneMutated");
            InvokePrivate(session, "RefreshWindowTitle");

            Assert.Equal("DirtyScene* - helengine - project.heproj [PS2]", session.WindowTitle);
        }

        /// <summary>
        /// Ensures one invalid persisted project platform forces the Platforms dialog to remain open until the user chooses a replacement.
        /// </summary>
        [Fact]
        public void PromptForPlatformSelectionIfRequired_WhenActivePlatformIsUnavailable_ShowsPlatformsDialogWithoutImplicitFallback() {
            WritePlatformsSettingsFile("windows", "ps2");
            WritePlatformManifest(
                "1.0.0-custom",
                [
                    new AvailablePlatformDescriptor("windows", "Windows DirectX", string.Empty, "platforms/windows", true),
                    new AvailablePlatformDescriptor("ps2", "PlayStation 2", string.Empty, "platforms/ps2", true)
                ],
                ["windows"]);
            EditorProjectLocalSettingsService localSettingsService = new EditorProjectLocalSettingsService(TempProjectRootPath, ["windows", "ps2"]);
            localSettingsService.SaveActivePlatform("ps2");
            EditorSession session = CreateSession(["windows", "ps2"], localSettingsService, "ps2");

            InvokePrivate(session, "PromptForPlatformSelectionIfRequired");

            PlatformsDialog dialog = GetPrivateField<PlatformsDialog>(session, "platformsDialog");
            ComboBoxComponent activePlatformComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ActivePlatformComboBox");
            Assert.True(dialog.Enabled);
            Assert.False(activePlatformComboBox.HasSelection);

            InvokePrivate(session, "HandlePlatformsDialogCancelRequested");

            Assert.True(dialog.Enabled);
            Assert.False(activePlatformComboBox.HasSelection);
        }

        /// <summary>
        /// Creates one partially initialized editor session containing the collaborators used by project-platform handling.
        /// </summary>
        /// <param name="supportedPlatforms">Project-supported platforms for the test session.</param>
        /// <param name="localSettingsService">Local-settings service used to persist the active platform.</param>
        /// <param name="activePlatform">Current active platform for the test session.</param>
        /// <returns>Editor session configured for project-platform tests.</returns>
        EditorSession CreateSession(IReadOnlyList<string> supportedPlatforms, EditorProjectLocalSettingsService localSettingsService, string activePlatform) {
            EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
            AssetImportManager assetImportManager = new AssetImportManager(TempProjectRootPath, new ContentManager(Path.Combine(TempProjectRootPath, "assets")));
            assetImportManager.CurrentPlatformId = activePlatform;

            SetPrivateField(session, "projectPath", TempProjectRootPath);
            SetPrivateField(session, "RequiredEngineVersion", "1.0.0-custom");
            SetPrivateField(session, "ProjectSupportedPlatforms", supportedPlatforms);
            SetPrivateField(session, "ProjectLocalSettingsService", localSettingsService);
            SetPrivateField(session, "ActiveProjectPlatform", activePlatform);
            SetPrivateField(session, "ProjectDisplayName", "project.heproj");
            SetPrivateField(session, "assetImportManager", assetImportManager);
            SetPrivateField(session, "titleBar", new EditorTitleBar(CreateFont(), 1280, 720, "helengine - project.heproj [WINDOWS]"));
            SetPrivateField(session, "platformsDialog", new PlatformsDialog(CreateFont()));
            SetPrivateField(session, "projectPlatformsService", new EditorProjectPlatformsService(TempProjectRootPath));
            SetPrivateField(session, "availablePlatformProviderResolver", new AvailablePlatformProviderResolver(new PlatformDiscoveryOptions(TempProjectRootPath)));

            return session;
        }

        /// <summary>
        /// Writes one project-scoped supported-platforms settings file.
        /// </summary>
        /// <param name="supportedPlatforms">Supported platform identifiers written into `settings/platforms.json`.</param>
        void WritePlatformsSettingsFile(params string[] supportedPlatforms) {
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "settings"));
            string json = JsonSerializer.Serialize(
                new EditorProjectPlatformsDocument {
                    SupportedPlatforms = supportedPlatforms.ToList()
                },
                new JsonSerializerOptions {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            File.WriteAllText(Path.Combine(TempProjectRootPath, "settings", "platforms.json"), json);
        }

        /// <summary>
        /// Reads the persisted supported-platform identifiers from `settings/platforms.json`.
        /// </summary>
        /// <returns>Supported platform identifiers stored on disk.</returns>
        IReadOnlyList<string> ReadSupportedPlatformsFromDisk() {
            string json = File.ReadAllText(Path.Combine(TempProjectRootPath, "settings", "platforms.json"));
            using JsonDocument document = JsonDocument.Parse(json);
            List<string> supportedPlatforms = [];
            foreach (JsonElement element in document.RootElement.GetProperty("supportedPlatforms").EnumerateArray()) {
                supportedPlatforms.Add(element.GetString());
            }

            return supportedPlatforms;
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
            for (int index = 0; index < platforms.Count; index++) {
                AvailablePlatformDescriptor platform = platforms[index];
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
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['f'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 11f, 12f), 0f, 11f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['2'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                [' '] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f)
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
