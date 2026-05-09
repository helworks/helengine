using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies generated engine assets remain browseable under asset-browser extension filters.
    /// </summary>
    public class AssetBrowserViewGeneratedAssetTests : IDisposable {
        /// <summary>
        /// Temporary project root used by generated asset browser tests.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes an isolated project root and the core services required by the asset browser view.
        /// </summary>
        public AssetBrowserViewGeneratedAssetTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-asset-browser-generated-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = ProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes temporary test state and clears provider registrations.
        /// </summary>
        public void Dispose() {
            GeneratedAssetProviderRegistry.ResetForTests();
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the material extension filter keeps generated material entries visible inside the engine material folder.
        /// </summary>
        [Fact]
        public void RefreshEntries_WhenMaterialExtensionFilterIsActive_KeepsGeneratedMaterialEntriesVisible() {
            GeneratedAssetProviderRegistry.Register(new EngineGeneratedAssetProvider());
            AssetBrowserView browserView = new AssetBrowserView(CreateFont(), ProjectRootPath, EditorLayerMasks.EditorUi, 1, 2, 3, 4);

            Assert.True(browserView.TryNavigateTo(EngineGeneratedAssetProvider.EngineRootPath));
            Assert.True(browserView.TryNavigateTo(EngineGeneratedAssetProvider.EngineMaterialsPath));

            browserView.SetExtensionFilter(EditorFileTemplateRegistry.MaterialExtension);
            browserView.RefreshEntries();

            List<AssetBrowserEntry> entries = GetPrivateField<List<AssetBrowserEntry>>(browserView, "Entries");
            AssetBrowserEntry materialEntry = Assert.Single(entries);
            Assert.Equal("Standard", materialEntry.Name);
            Assert.True(materialEntry.IsGenerated);
            Assert.Equal(AssetEntryKind.Material, materialEntry.EntryKind);
        }

        /// <summary>
        /// Ensures the generated engine root row stays visible at the top of the root list and is labeled as read-only.
        /// </summary>
        [Fact]
        public void RefreshEntries_WhenEngineRootIsPresent_PinsItFirstAndMarksItReadOnly() {
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Aardvark"));
            GeneratedAssetProviderRegistry.Register(new EngineGeneratedAssetProvider());
            AssetBrowserView browserView = new AssetBrowserView(CreateFont(), ProjectRootPath, EditorLayerMasks.EditorUi, 1, 2, 3, 4);

            browserView.UpdateLayout(320, 240);

            List<AssetBrowserEntry> entries = GetPrivateField<List<AssetBrowserEntry>>(browserView, "Entries");
            List<AssetBrowserRow> rows = GetPrivateField<List<AssetBrowserRow>>(browserView, "Rows");

            Assert.Equal("Engine", entries[0].Name);
            Assert.True(entries[0].IsEngineGeneratedRootDirectory);
            Assert.Equal("Engine/ [read-only]", rows[0].Label.Text);
            Assert.Equal(ThemeManager.Colors.StateWarning, rows[0].Background.Color);
        }

        /// <summary>
        /// Ensures multi-extension picker filters match any of the authored extensions in the list.
        /// </summary>
        [Fact]
        public void DoesEntryMatchExtensionFilter_when_multiple_extensions_are_provided_matches_any_token() {
            AssetBrowserView browserView = new AssetBrowserView(CreateFont(), ProjectRootPath, EditorLayerMasks.EditorUi, 1, 2, 3, 4);
            browserView.SetExtensionFilter(".png;.jpg");

            MethodInfo method = typeof(AssetBrowserView).GetMethod("DoesEntryMatchExtensionFilter", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);

            AssetBrowserEntry pngEntry = AssetBrowserEntry.CreateFileSystemFile("Diffuse.png", "Textures/Diffuse.png", Path.Combine(ProjectRootPath, "assets", "Textures", "Diffuse.png"), ".png", AssetEntryKind.Model);
            AssetBrowserEntry jpgEntry = AssetBrowserEntry.CreateFileSystemFile("Diffuse.jpg", "Textures/Diffuse.jpg", Path.Combine(ProjectRootPath, "assets", "Textures", "Diffuse.jpg"), ".jpg", AssetEntryKind.Model);
            AssetBrowserEntry gifEntry = AssetBrowserEntry.CreateFileSystemFile("Diffuse.gif", "Textures/Diffuse.gif", Path.Combine(ProjectRootPath, "assets", "Textures", "Diffuse.gif"), ".gif", AssetEntryKind.Model);

            Assert.True(InvokeBooleanPrivate(browserView, method, pngEntry));
            Assert.True(InvokeBooleanPrivate(browserView, method, jpgEntry));
            Assert.False(InvokeBooleanPrivate(browserView, method, gifEntry));
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
        /// Invokes one private boolean method on an object instance.
        /// </summary>
        /// <param name="target">Object that owns the method.</param>
        /// <param name="method">Private method to invoke.</param>
        /// <param name="argument">Method argument.</param>
        /// <returns>Boolean result returned by the private method.</returns>
        static bool InvokeBooleanPrivate(object target, MethodInfo method, object argument) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            } else if (method == null) {
                throw new ArgumentNullException(nameof(method));
            }

            object result = method.Invoke(target, [argument]);
            return Assert.IsType<bool>(result);
        }

        /// <summary>
        /// Creates a small font asset that can satisfy the layout requirements of the asset browser.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['E'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['M'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                [' '] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['-'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['['] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                [']'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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
