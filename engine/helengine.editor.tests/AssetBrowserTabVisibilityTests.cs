using System.Collections;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies that inactive tabbed asset-browser panels do not leak newly created visuals into the active tab.
    /// </summary>
    public class AssetBrowserTabVisibilityTests : IDisposable {
        /// <summary>
        /// Temporary project root used to generate asset-browser entries during the test.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes the temporary project assets folder and the core services required for docked-panel tests.
        /// </summary>
        public AssetBrowserTabVisibilityTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-asset-browser-tab-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets"));

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempProjectRootPath
            });
            core.Initialize(null, new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes the temporary project root after the test completes.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures refreshing the asset browser while its tab is inactive does not re-register its newly created rows for draw or input.
        /// </summary>
        [Fact]
        public void RefreshEntries_WhenAssetBrowserTabIsInactive_DoesNotRegisterNewRowsForRenderingOrInput() {
            FontAsset font = CreateFont();
            AssetBrowserPanel assetBrowserPanel = new AssetBrowserPanel(font, TempProjectRootPath);
            LoggerPanel loggerPanel = new LoggerPanel(font);
            DockLayoutEngine layout = new DockLayoutEngine();

            layout.DockAsRoot(assetBrowserPanel);
            layout.DockRelative(loggerPanel, assetBrowserPanel, DockInsertDirection.Fill);
            layout.Layout(new int2(640, 360));

            Assert.False(assetBrowserPanel.Enabled);
            Assert.Equal(0, CountOwnedDrawables(assetBrowserPanel));
            Assert.Equal(0, CountOwnedInteractables(assetBrowserPanel));

            File.WriteAllText(Path.Combine(TempProjectRootPath, "assets", "Test.txt"), "asset");
            assetBrowserPanel.RefreshEntries();

            Assert.Equal(0, CountOwnedDrawables(assetBrowserPanel));
            Assert.Equal(0, CountOwnedInteractables(assetBrowserPanel));
            Assert.True(GetRowCount(assetBrowserPanel) > 0);
        }

        /// <summary>
        /// Counts registered 2D drawables that belong to the provided dockable hierarchy.
        /// </summary>
        /// <param name="owner">Dockable entity whose descendants should be counted.</param>
        /// <returns>Number of registered drawables owned by the dockable hierarchy.</returns>
        int CountOwnedDrawables(Entity owner) {
            int count = 0;
            IList drawables = Core.Instance.ObjectManager.Drawables2D;
            for (int i = 0; i < drawables.Count; i++) {
                IDrawable2D drawable = Assert.IsAssignableFrom<IDrawable2D>(drawables[i]);
                if (BelongsToHierarchy(drawable.Parent, owner)) {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Counts registered interactables that belong to the provided dockable hierarchy.
        /// </summary>
        /// <param name="owner">Dockable entity whose descendants should be counted.</param>
        /// <returns>Number of registered interactables owned by the dockable hierarchy.</returns>
        int CountOwnedInteractables(Entity owner) {
            int count = 0;
            IList interactables = Core.Instance.ObjectManager.Interactables;
            for (int i = 0; i < interactables.Count; i++) {
                IInteractable2D interactable = Assert.IsAssignableFrom<IInteractable2D>(interactables[i]);
                if (BelongsToHierarchy(interactable.Parent, owner)) {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Determines whether the provided entity belongs to the requested ancestor hierarchy.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <param name="owner">Expected ancestor entity.</param>
        /// <returns>True when the entity belongs to the owner's hierarchy.</returns>
        bool BelongsToHierarchy(Entity entity, Entity owner) {
            Entity current = entity;
            while (current != null) {
                if (ReferenceEquals(current, owner)) {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }

        /// <summary>
        /// Reads the current number of pooled asset-browser rows through the private browser-view field.
        /// </summary>
        /// <param name="assetBrowserPanel">Panel whose row pool should be inspected.</param>
        /// <returns>Number of pooled rows currently owned by the view.</returns>
        int GetRowCount(AssetBrowserPanel assetBrowserPanel) {
            object browserView = GetPrivateField<object>(assetBrowserPanel, "BrowserView");
            IList rows = GetPrivateField<IList>(browserView, "Rows");
            return rows.Count;
        }

        /// <summary>
        /// Reads one private instance field from an object.
        /// </summary>
        /// <typeparam name="T">Requested field type.</typeparam>
        /// <param name="instance">Object containing the field.</param>
        /// <param name="fieldName">Exact field name to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object instance, string fieldName) {
            if (instance == null) {
                throw new ArgumentNullException(nameof(instance));
            }
            if (string.IsNullOrWhiteSpace(fieldName)) {
                throw new ArgumentException("Field name must be provided.", nameof(fieldName));
            }

            var fieldInfo = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (fieldInfo == null) {
                throw new InvalidOperationException("Expected private field was not found.");
            }

            object value = fieldInfo.GetValue(instance);
            return Assert.IsAssignableFrom<T>(value);
        }

        /// <summary>
        /// Creates a font asset with the glyphs used by the asset browser and logger tabs.
        /// </summary>
        /// <returns>Font asset with deterministic metrics for test layout.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['A'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['L'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['T'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['U'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['b'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['x'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
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
