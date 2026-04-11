using System.Reflection;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the editor open-file dialog used for scene loading.
    /// </summary>
    public class OpenFileDialogTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the open dialog.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes an isolated project root and the core services required by the dialog.
        /// </summary>
        public OpenFileDialogTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-open-file-dialog-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Scenes"));

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = ProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes temporary test state after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the open dialog only raises `.helen` files.
        /// </summary>
        [Fact]
        public void HandleOpenClicked_WhenSceneFileIsSelected_RaisesScenePath() {
            string scenesDirectoryPath = Path.Combine(ProjectRootPath, "assets", "Scenes");
            string expectedPath = Path.Combine(scenesDirectoryPath, "Level01.helen");
            File.WriteAllText(expectedPath, "scene");
            OpenFileDialog dialog = new OpenFileDialog(CreateFont(), ProjectRootPath);
            string raisedPath = string.Empty;
            dialog.OpenRequested += path => raisedPath = path;
            dialog.Show("Scenes");
            dialog.UpdateLayout(1280, 720);

            SetSelectedEntry(dialog, CreateFileEntry(expectedPath, "Scenes/Level01.helen"));
            InvokePrivate(dialog, "HandleOpenClicked");

            Assert.Equal(expectedPath, raisedPath);
        }

        /// <summary>
        /// Ensures the open dialog hides non-scene files from selection.
        /// </summary>
        [Fact]
        public void RefreshEntries_WhenOpenDialogIsVisible_FiltersToHelenFiles() {
            string scenesDirectoryPath = Path.Combine(ProjectRootPath, "assets", "Scenes");
            File.WriteAllText(Path.Combine(scenesDirectoryPath, "Level01.helen"), "scene");
            File.WriteAllText(Path.Combine(scenesDirectoryPath, "Preview.png"), "image");
            OpenFileDialog dialog = new OpenFileDialog(CreateFont(), ProjectRootPath);
            dialog.Show("Scenes");
            dialog.UpdateLayout(1280, 720);

            AssetBrowserView browserView = GetPrivateField<AssetBrowserView>(dialog, "BrowserView");
            List<AssetBrowserEntry> entries = GetPrivateField<List<AssetBrowserEntry>>(browserView, "Entries");

            Assert.Contains(entries, entry => string.Equals(entry.Extension, ".helen", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(entries, entry => string.Equals(entry.Extension, ".png", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Ensures the open-scene dialog uses modal render orders above non-modal overlays.
        /// </summary>
        [Fact]
        public void Constructor_UsesModalRenderOrdersAboveOverlayBand() {
            OpenFileDialog dialog = new OpenFileDialog(CreateFont(), ProjectRootPath);

            RoundedRectComponent panelBackground = GetPrivateField<RoundedRectComponent>(dialog, "PanelBackground");

            Assert.Equal(RenderOrder2D.ModalBackground, panelBackground.RenderOrder2D);
        }

        /// <summary>
        /// Assigns the selected browser entry directly on the dialog.
        /// </summary>
        /// <param name="dialog">Dialog whose selection should be updated.</param>
        /// <param name="entry">Entry assigned as selected.</param>
        void SetSelectedEntry(OpenFileDialog dialog, AssetBrowserEntry entry) {
            FieldInfo field = dialog.GetType().GetField("SelectedEntry", BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(dialog, entry);
        }

        /// <summary>
        /// Creates one filesystem file entry for dialog selection tests.
        /// </summary>
        /// <param name="fullPath">Absolute path to the file.</param>
        /// <param name="relativePath">Assets-relative path displayed by the browser.</param>
        /// <returns>File-backed browser entry.</returns>
        AssetBrowserEntry CreateFileEntry(string fullPath, string relativePath) {
            return AssetBrowserEntry.CreateFileSystemFile(
                Path.GetFileName(fullPath),
                relativePath,
                fullPath,
                Path.GetExtension(fullPath),
                AssetEntryKind.Scene);
        }

        /// <summary>
        /// Invokes one non-public instance method.
        /// </summary>
        /// <param name="target">Target object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        void InvokePrivate(object target, string methodName) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, Array.Empty<object>());
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
        /// Creates a small font asset that can satisfy the layout requirements of the open dialog.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['O'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['D'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['L'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                [' '] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['.'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f)
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
