using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies platform-aware asset import settings UI behavior and forwarding.
    /// </summary>
    public class AssetImportSettingsViewTests : IDisposable {
        /// <summary>
        /// Temporary content root used by view and panel tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by editor UI controls.
        /// </summary>
        public AssetImportSettingsViewTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-asset-import-settings-view-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes temporary test content after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures one platform tab is rendered for each supported project platform and the saved active platform is selected initially.
        /// </summary>
        [Fact]
        public void Show_WhenSupportedPlatformsAreProvided_CreatesTabsAndSelectsTheActivePlatform() {
            AssetImportSettingsView view = new AssetImportSettingsView(CreateFont(), 1);

            view.Show(
                ["assimp", "custom"],
                CreateSettings(false, true),
                ["windows", "android"],
                "android",
                AssetEntryKind.Model);

            Assert.Equal(2, view.PlatformTabCount);
            Assert.Equal("android", view.SelectedPlatformId);
        }

        /// <summary>
        /// Ensures model assets expose the processor section and bind the flip-winding toggle to the active platform settings.
        /// </summary>
        [Fact]
        public void Show_WhenModelProcessorSettingsExist_UsesTheActivePlatformFlipWindingValue() {
            AssetImportSettingsView view = new AssetImportSettingsView(CreateFont(), 1);

            view.Show(
                ["assimp"],
                CreateSettings(true, false),
                ["windows", "android"],
                "windows",
                AssetEntryKind.Model);

            Assert.True(view.IsModelProcessorVisible);
            Assert.True(view.CurrentFlipWindingValue);
            Assert.IsType<CheckBoxComponent>(GetPrivateField<CheckBoxComponent>(view, "FlipWindingCheckBox"));
        }

        /// <summary>
        /// Ensures applying pending importer and processor changes raises one rich request payload.
        /// </summary>
        [Fact]
        public void Apply_WhenPendingSettingsChanged_RaisesOneRichSettingsRequest() {
            AssetImportSettingsView view = new AssetImportSettingsView(CreateFont(), 1);
            AssetImportSettingsApplyRequest raisedRequest = null;
            view.ApplyRequested += request => raisedRequest = request;

            view.Show(
                ["assimp", "custom"],
                CreateSettings(false, false),
                ["windows", "android"],
                "windows",
                AssetEntryKind.Model);

            InvokePrivate(view, "HandleComboSelectionChanged", 1, "custom");
            InvokePrivate(view, "HandleFlipWindingCheckedChanged", true);
            InvokePrivate(view, "HandleApplyClicked");

            Assert.NotNull(raisedRequest);
            Assert.Equal("custom", raisedRequest.ImporterId);
            Assert.Equal("windows", raisedRequest.SelectedPlatformId);
            Assert.True(raisedRequest.ProcessorSettings.Platforms["windows"].Model.FlipWinding);
        }

        /// <summary>
        /// Ensures repeated model-setting refreshes do not leak platform-tab entities into the scene hierarchy.
        /// </summary>
        [Fact]
        public void Show_WhenModelSettingsAreShownRepeatedly_DoesNotLeakPlatformTabEntitiesIntoSceneHierarchy() {
            AssetImportSettingsView view = new AssetImportSettingsView(CreateFont(), 1);
            SceneHierarchyPanel panel = new SceneHierarchyPanel(CreateFont());

            view.Show(
                ["assimp"],
                CreateSettings(false, false),
                ["windows"],
                "windows",
                AssetEntryKind.Model);
            panel.RefreshHierarchy();

            int firstHierarchyCount = GetHierarchyNodeCount(panel);

            view.Show(
                ["assimp"],
                CreateSettings(false, false),
                ["windows"],
                "windows",
                AssetEntryKind.Model);
            panel.RefreshHierarchy();

            int secondHierarchyCount = GetHierarchyNodeCount(panel);

            Assert.Equal(0, firstHierarchyCount);
            Assert.Equal(0, secondHierarchyCount);
        }

        /// <summary>
        /// Ensures rebuilding the platform tabs disposes the previous tab hosts instead of leaving detached live entities behind.
        /// </summary>
        [Fact]
        public void Show_WhenPlatformTabsAreRebuilt_DisposesPreviousTabHosts() {
            AssetImportSettingsView view = new AssetImportSettingsView(CreateFont(), 1);

            view.Show(
                ["assimp"],
                CreateSettings(false, false),
                ["windows", "android"],
                "windows",
                AssetEntryKind.Model);

            List<EditorEntity> previousTabHosts = new List<EditorEntity>(GetPrivateField<List<EditorEntity>>(view, "PlatformTabButtonHosts"));

            view.Show(
                ["assimp"],
                CreateSettings(false, false),
                ["windows"],
                "windows",
                AssetEntryKind.Model);

            Assert.All(previousTabHosts, host => Assert.DoesNotContain(host, Core.Instance.ObjectManager.Entities));
            Assert.All(previousTabHosts, host => Assert.Empty(host.Components));
            Assert.All(previousTabHosts, host => Assert.DoesNotContain(Core.Instance.ObjectManager.Drawables2D, drawable => ReferenceEquals(drawable.Parent, host)));
            Assert.All(previousTabHosts, host => Assert.DoesNotContain(Core.Instance.ObjectManager.Interactables, interactable => ReferenceEquals(interactable.Parent, host)));
        }

        /// <summary>
        /// Ensures the properties panel forwards the richer apply payload emitted by the asset settings view.
        /// </summary>
        [Fact]
        public void PropertiesPanel_WhenAssetSettingsAreApplied_ForwardsTheRichApplyPayload() {
            ContentManager contentManager = new ContentManager(TempRootPath);
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), contentManager);
            AssetBrowserEntry entry = AssetBrowserEntry.CreateFileSystemFile(
                "Sponza.obj",
                "Models/Sponza.obj",
                Path.Combine(TempRootPath, "Models", "Sponza.obj"),
                ".obj",
                AssetEntryKind.Model);
            AssetImportSettingsApplyRequest raisedRequest = null;
            AssetBrowserEntry raisedEntry = null;
            panel.ImportSettingsApplyRequested += (forwardedEntry, request) => {
                raisedEntry = forwardedEntry;
                raisedRequest = request;
            };

            panel.ShowImportSettings(
                entry,
                CreateSettings(false, false),
                ["assimp", "custom"],
                ["windows"],
                "windows");

            AssetImportSettingsView view = GetPrivateField<AssetImportSettingsView>(panel, "importSettingsView");
            InvokePrivate(view, "HandleFlipWindingCheckedChanged", true);
            InvokePrivate(view, "HandleApplyClicked");

            Assert.Same(entry, raisedEntry);
            Assert.NotNull(raisedRequest);
            Assert.Equal("windows", raisedRequest.SelectedPlatformId);
            Assert.True(raisedRequest.ProcessorSettings.Platforms["windows"].Model.FlipWinding);
        }

        /// <summary>
        /// Creates one asset import settings document with per-platform flip-winding values.
        /// </summary>
        /// <param name="windowsFlipWinding">Windows processor flip-winding value.</param>
        /// <param name="androidFlipWinding">Android processor flip-winding value.</param>
        /// <returns>Configured asset import settings document.</returns>
        AssetImportSettings CreateSettings(bool windowsFlipWinding, bool androidFlipWinding) {
            AssetImportSettings settings = new AssetImportSettings();
            settings.Importer.ImporterId = "assimp";
            settings.Importer.SourceChecksum = "checksum";
            settings.Importer.AssetId = "asset-id";
            settings.Processor.Platforms["windows"] = new AssetPlatformProcessorSettings {
                Model = new ModelAssetProcessorSettings {
                    FlipWinding = windowsFlipWinding
                }
            };
            settings.Processor.Platforms["android"] = new AssetPlatformProcessorSettings {
                Model = new ModelAssetProcessorSettings {
                    FlipWinding = androidFlipWinding
                }
            };
            return settings;
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
        /// Invokes one non-public instance method with the supplied arguments.
        /// </summary>
        /// <param name="target">Target object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <param name="arguments">Arguments passed to the target method.</param>
        void InvokePrivate(object target, string methodName, params object[] arguments) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, arguments);
        }

        /// <summary>
        /// Counts the visible rows currently shown by one scene hierarchy panel.
        /// </summary>
        /// <param name="panel">Hierarchy panel to inspect.</param>
        /// <returns>Number of visible hierarchy rows.</returns>
        int GetHierarchyNodeCount(SceneHierarchyPanel panel) {
            FieldInfo rowsField = typeof(SceneHierarchyPanel).GetField("rows", BindingFlags.Instance | BindingFlags.NonPublic);
            var rows = Assert.IsType<List<SceneHierarchyRow>>(rowsField.GetValue(panel));
            return rows.Count;
        }

        /// <summary>
        /// Creates a small font asset that can satisfy the layout requirements of the asset settings view.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['A'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['C'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['I'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['W'] = new FontChar(new float4(0f, 0f, 11f, 12f), 0f, 11f, 0f, 0f),
                ['X'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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
