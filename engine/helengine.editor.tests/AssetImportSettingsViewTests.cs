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
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
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
                "assimp",
                CreateSettings(false, true),
                ["windows", "android"],
                "android",
                AssetEntryKind.Model);

            Assert.Equal(2, view.PlatformTabCount);
            Assert.Equal("android", view.SelectedPlatformId);
        }

        /// <summary>
        /// Ensures the shared platform tab strip reveals the selected platform when the supported platform list overflows the available width.
        /// </summary>
        [Fact]
        public void Show_WhenManyPlatformsAreProvided_RevealsTheSelectedPlatformInTheSharedStrip() {
            AssetImportSettingsView view = new AssetImportSettingsView(CreateFont(), 1);

            view.Show(
                ["assimp"],
                "assimp",
                CreateSettings(false, false),
                ["windows", "ps2", "linux", "gamecube", "wii", "xbox", "switch"],
                "switch",
                AssetEntryKind.Model);
            view.UpdateLayout(0, 0, 220);

            Assert.Equal("switch", view.SelectedPlatformId);
            Assert.True(view.PlatformTabStripView.IsPlatformFullyVisible("switch"));
        }

        /// <summary>
        /// Ensures the processor controls sit inside one attached lower panel that starts directly beneath the platform tabs.
        /// </summary>
        [Fact]
        public void UpdateLayout_WhenProcessorSectionIsVisible_UsesAttachedLowerPanelChrome() {
            AssetImportSettingsView view = new AssetImportSettingsView(CreateFont(), 1);

            view.Show(
                ["assimp"],
                "assimp",
                CreateSettings(false, false),
                ["windows", "android"],
                "windows",
                AssetEntryKind.Model);
            view.UpdateLayout(0, 0, 220);

            EditorEntity processorPanelRoot = GetPrivateField<EditorEntity>(view, "ProcessorPanelRoot");
            RoundedRectComponent processorPanelBackground = GetPrivateField<RoundedRectComponent>(view, "ProcessorPanelBackground");

            Assert.Equal((int)view.PlatformTabStripView.Root.LocalPosition.Y + 21, (int)processorPanelRoot.LocalPosition.Y);
            Assert.Equal(RoundedRectCorners.BottomLeft | RoundedRectCorners.BottomRight, processorPanelBackground.Corners);
            Assert.True(processorPanelBackground.Size.Y > 24);
        }

        /// <summary>
        /// Ensures model assets expose the processor section and bind the flip-winding toggle to the active platform settings.
        /// </summary>
        [Fact]
        public void Show_WhenModelProcessorSettingsExist_UsesTheActivePlatformFlipWindingValue() {
            AssetImportSettingsView view = new AssetImportSettingsView(CreateFont(), 1);

            view.Show(
                ["assimp"],
                "assimp",
                CreateSettings(true, false),
                ["windows", "android"],
                "windows",
                AssetEntryKind.Model);

            Assert.True(view.IsModelProcessorVisible);
            Assert.True(view.CurrentFlipWindingValue);
            Assert.IsType<CheckBoxComponent>(GetPrivateField<CheckBoxComponent>(view, "FlipWindingCheckBox"));
        }

        /// <summary>
        /// Ensures texture assets expose DS color format, alpha precision, and max-resolution controls for the active platform.
        /// </summary>
        [Fact]
        public void Show_WhenTextureProcessorSettingsExist_UsesTheActivePlatformTextureValues() {
            AssetImportSettingsView view = new AssetImportSettingsView(CreateFont(), 1);
            AssetProcessorSettings settings = new AssetProcessorSettings();
            settings.Platforms["ds"] = new AssetPlatformProcessorSettings {
                Texture = new TextureAssetProcessorSettings {
                    MaxResolution = 256,
                    ColorFormat = TextureAssetColorFormat.Indexed8,
                    AlphaPrecision = TextureAssetAlphaPrecision.A4
                }
            };

            view.Show(
                ["pfim"],
                "pfim",
                settings,
                ["windows", "ds"],
                "ds",
                AssetEntryKind.Image);

            Assert.True(view.IsTextureProcessorVisible);
            Assert.Equal(256, view.CurrentTextureMaxResolutionValue);
            Assert.Equal(TextureAssetColorFormat.Indexed8, view.CurrentTextureColorFormatValue);
            Assert.Equal(TextureAssetAlphaPrecision.A4, view.CurrentTextureAlphaPrecisionValue);
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
                "assimp",
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
        /// Ensures changing the importer selection commits the new importer immediately instead of leaving it pending.
        /// </summary>
        [Fact]
        public void Apply_WhenImporterSelectionChanges_RaisesACommitRequestImmediately() {
            AssetImportSettingsView view = new AssetImportSettingsView(CreateFont(), 1);
            AssetImportSettingsApplyRequest raisedRequest = null;
            view.ApplyRequested += request => raisedRequest = request;

            view.Show(
                ["assimp", "custom"],
                "assimp",
                CreateSettings(false, false),
                ["windows", "android"],
                "windows",
                AssetEntryKind.Model);

            InvokePrivate(view, "HandleComboSelectionChanged", 1, "custom");

            Assert.NotNull(raisedRequest);
            Assert.Equal("custom", raisedRequest.ImporterId);
            Assert.Equal("windows", raisedRequest.SelectedPlatformId);
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
                "assimp",
                CreateSettings(false, false),
                ["windows"],
                "windows",
                AssetEntryKind.Model);
            panel.RefreshHierarchy();

            int firstHierarchyCount = GetHierarchyNodeCount(panel);

            view.Show(
                ["assimp"],
                "assimp",
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
                "assimp",
                CreateSettings(false, false),
                ["windows", "android"],
                "windows",
                AssetEntryKind.Model);

            List<EditorEntity> previousTabHosts = new List<EditorEntity>(GetPrivateField<List<EditorEntity>>(view.PlatformTabStripView, "TabHosts"));

            view.Show(
                ["assimp"],
                "assimp",
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
                "assimp",
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
        /// <returns>Configured processor settings document.</returns>
        AssetProcessorSettings CreateSettings(bool windowsFlipWinding, bool androidFlipWinding) {
            AssetProcessorSettings settings = new AssetProcessorSettings();
            settings.Platforms["windows"] = new AssetPlatformProcessorSettings {
                Model = new ModelAssetProcessorSettings {
                    FlipWinding = windowsFlipWinding
                }
            };
            settings.Platforms["android"] = new AssetPlatformProcessorSettings {
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
        /// Counts the flattened hierarchy nodes currently shown by one scene hierarchy panel.
        /// </summary>
        /// <param name="panel">Hierarchy panel to inspect.</param>
        /// <returns>Number of flattened hierarchy nodes.</returns>
        int GetHierarchyNodeCount(SceneHierarchyPanel panel) {
            FieldInfo nodesField = typeof(SceneHierarchyPanel).GetField("nodes", BindingFlags.Instance | BindingFlags.NonPublic);
            var nodes = Assert.IsAssignableFrom<System.Collections.ICollection>(nodesField.GetValue(panel));
            return nodes.Count;
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
