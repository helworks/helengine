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
        /// Configurable input manager used to drive pointer-routing assertions.
        /// </summary>
        readonly TestInputManager Input;

        /// <summary>
        /// Initializes an isolated project root and the core services required by the dialog.
        /// </summary>
        public OpenFileDialogTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-open-file-dialog-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Scenes"));
            EditorInputCaptureService.Reset();

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = ProjectRootPath
            });
            Input = new TestInputManager();
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), Input);
        }

        /// <summary>
        /// Deletes temporary test state after each test.
        /// </summary>
        public void Dispose() {
            EditorInputCaptureService.Reset();
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
        /// Ensures clicking one scene row leaves that row visibly selected until the modal selection changes.
        /// </summary>
        [Fact]
        public void ActivateRow_WhenSceneFileIsClicked_KeepsTheMatchingRowHighlighted() {
            string scenesDirectoryPath = Path.Combine(ProjectRootPath, "assets", "Scenes");
            File.WriteAllText(Path.Combine(scenesDirectoryPath, "Level01.helen"), "scene");
            File.WriteAllText(Path.Combine(scenesDirectoryPath, "Level02.helen"), "scene");
            OpenFileDialog dialog = new OpenFileDialog(CreateFont(), ProjectRootPath);
            dialog.Show("Scenes");
            dialog.UpdateLayout(1280, 720);

            AssetBrowserView browserView = GetPrivateField<AssetBrowserView>(dialog, "BrowserView");
            AssetBrowserRow selectedRow = FindRow(browserView, "Scenes/Level01.helen");
            AssetBrowserRow unselectedRow = FindRow(browserView, "Scenes/Level02.helen");

            InvokePrivate(browserView, "ActivateRow", selectedRow);

            Assert.Equal(ThemeManager.Colors.AccentSecondary, selectedRow.Background.Color);
            Assert.Equal(unselectedRow.BaseColor, unselectedRow.Background.Color);
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
        /// Ensures blank dialog background space absorbs hover instead of leaking through to lower editor controls.
        /// </summary>
        [Fact]
        public void Update_WhenPointerMovesAcrossDialogBackground_DoesNotHoverInteractablesBehindDialog() {
            CreateUiCamera(1280, 720);

            InteractableComponent behindInteractable = CreateInteractableEntity(
                new float3(0f, 0f, 0f),
                new int2(1280, 720),
                RenderOrder2D.PanelSurface);
            int behindHoverCount = 0;
            behindInteractable.CursorEvent += (pos, delta, state) => {
                if (state == PointerInteraction.Hover) {
                    behindHoverCount++;
                }
            };

            OpenFileDialog dialog = new OpenFileDialog(CreateFont(), ProjectRootPath);
            dialog.Show("Scenes");
            dialog.UpdateLayout(1280, 720);

            int2 panelPosition = GetPrivateField<int2>(dialog, "PanelPosition");
            int2 panelSize = GetPrivateField<int2>(dialog, "PanelSize");
            int pointerX = panelPosition.X + OpenFileDialog.PanelPadding;
            int pointerY = panelPosition.Y + panelSize.Y - OpenFileDialog.PanelPadding - (OpenFileDialog.FooterHeight / 2);
            Input.SetMouseState(new MouseState(pointerX, pointerY, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

            Input.EarlyUpdate();
            Input.Update();

            Assert.NotSame(behindInteractable, Input.Hovering);
            Assert.Equal(0, behindHoverCount);
        }

        /// <summary>
        /// Ensures the modal backdrop outside the panel also absorbs hover instead of leaking through to lower editor controls.
        /// </summary>
        [Fact]
        public void Update_WhenPointerMovesAcrossBackdropOutsidePanel_DoesNotHoverInteractablesBehindDialog() {
            CreateUiCamera(1280, 720);

            InteractableComponent behindInteractable = CreateInteractableEntity(
                new float3(0f, 0f, 0f),
                new int2(1280, 720),
                RenderOrder2D.PanelSurface);
            int behindHoverCount = 0;
            behindInteractable.CursorEvent += (pos, delta, state) => {
                if (state == PointerInteraction.Hover) {
                    behindHoverCount++;
                }
            };

            OpenFileDialog dialog = new OpenFileDialog(CreateFont(), ProjectRootPath);
            dialog.Show("Scenes");
            dialog.UpdateLayout(1280, 720);

            Input.SetMouseState(new MouseState(8, 8, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

            Input.EarlyUpdate();
            Input.Update();

            Assert.NotSame(behindInteractable, Input.Hovering);
            Assert.Equal(0, behindHoverCount);
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
        /// Invokes one non-public instance method with a single argument.
        /// </summary>
        /// <param name="target">Target object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <param name="argument">Argument forwarded to the invoked method.</param>
        void InvokePrivate(object target, string methodName, object argument) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, new[] { argument });
        }

        /// <summary>
        /// Finds one visible asset-browser row by its project-relative path.
        /// </summary>
        /// <param name="browserView">Browser view that owns the rows.</param>
        /// <param name="relativePath">Relative path assigned to the desired row.</param>
        /// <returns>Matching row currently displayed in the browser.</returns>
        AssetBrowserRow FindRow(AssetBrowserView browserView, string relativePath) {
            List<AssetBrowserRow> rows = GetPrivateField<List<AssetBrowserRow>>(browserView, "Rows");

            AssetBrowserRow row = rows.FirstOrDefault(candidate =>
                candidate.Entry != null &&
                string.Equals(candidate.Entry.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));

            return Assert.IsType<AssetBrowserRow>(row);
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
        /// Creates the UI camera used to evaluate modal hit testing in window space.
        /// </summary>
        /// <param name="width">Viewport width in pixels.</param>
        /// <param name="height">Viewport height in pixels.</param>
        void CreateUiCamera(int width, int height) {
            EditorEntity cameraEntity = new EditorEntity {
                InternalEntity = true,
                LayerMask = EditorLayerMasks.EditorUi
            };

            CameraComponent camera = new CameraComponent {
                LayerMask = EditorLayerMasks.EditorUi,
                CameraDrawOrder = 255,
                Viewport = new float4(0f, 0f, width, height)
            };
            cameraEntity.AddComponent(camera);
        }

        /// <summary>
        /// Creates one visible interactable entity for pointer-routing tests.
        /// </summary>
        /// <param name="position">Top-left position in window coordinates.</param>
        /// <param name="size">Interactable size in pixels.</param>
        /// <param name="renderOrder">Render order assigned to the visible surface.</param>
        /// <returns>Interactable component attached to the new entity.</returns>
        InteractableComponent CreateInteractableEntity(float3 position, int2 size, byte renderOrder) {
            EditorEntity entity = new EditorEntity {
                InternalEntity = true,
                LayerMask = EditorLayerMasks.EditorUi,
                Position = position
            };

            SpriteComponent sprite = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Size = size,
                RenderOrder2D = renderOrder
            };
            entity.AddComponent(sprite);

            InteractableComponent interactable = new InteractableComponent {
                Size = size
            };
            entity.AddComponent(interactable);
            return interactable;
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
