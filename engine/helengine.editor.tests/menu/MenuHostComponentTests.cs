using helengine.editor.tests.testing;
using helengine.files;
using Xunit;

namespace helengine.editor.tests.menu {
    /// <summary>
    /// Verifies menu-host runtime navigation behavior for keyboard-driven menu panels.
    /// </summary>
    public class MenuHostComponentTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the menu-host runtime tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Configurable input backend used to drive the menu host through deterministic frames.
        /// </summary>
        readonly TestInputBackend InputBackend;

        /// <summary>
        /// Initializes runtime services and packaged menu fonts for the current test.
        /// </summary>
        public MenuHostComponentTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-menu-host-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
            InputBackend = new TestInputBackend();

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), InputBackend);
            WriteFontAsset("fonts/title.hefont");
            WriteFontAsset("fonts/body.hefont");
            WriteSceneAsset("Scenes/TestPlayableScene.helen");
        }

        /// <summary>
        /// Deletes the temporary content root after each test run.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures keyboard navigation switches panels and returns through the Back action.
        /// </summary>
        [Fact]
        public void Update_WhenKeyboardNavigatesPanels_TracksPanelAndSelectionState() {
            Entity rootEntity = new Entity();
            rootEntity.InitComponents();
            rootEntity.InitChildren();
            MenuHostComponent component = new MenuHostComponent {
                ProviderTypeName = typeof(TestMenuDefinitionProvider).AssemblyQualifiedName
            };
            rootEntity.AddComponent(component);

            Assert.Equal("main", component.ActivePanelId);
            Assert.Equal("select-scene", component.SelectedItemId);

            AdvanceFrame(new KeyboardState(Keys.Down));
            AdvanceFrame(new KeyboardState());
            Assert.Equal("options", component.SelectedItemId);

            AdvanceFrame(new KeyboardState(Keys.Enter));
            AdvanceFrame(new KeyboardState());
            Assert.Equal("options", component.ActivePanelId);
            Assert.Equal("options-back", component.SelectedItemId);

            AdvanceFrame(new KeyboardState(Keys.Escape));
            AdvanceFrame(new KeyboardState());
            Assert.Equal("main", component.ActivePanelId);
            Assert.Equal("options", component.SelectedItemId);
        }

        /// <summary>
        /// Ensures menu-host components fail fast when no provider type name is configured.
        /// </summary>
        [Fact]
        public void ComponentAdded_WhenProviderTypeNameIsMissing_Throws() {
            Entity rootEntity = new Entity();
            rootEntity.InitComponents();
            rootEntity.InitChildren();
            MenuHostComponent component = new MenuHostComponent();

            ArgumentException exception = Assert.Throws<ArgumentException>(() => rootEntity.AddComponent(component));

            Assert.Contains("provider", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Advances one deterministic input frame for the active core instance.
        /// </summary>
        /// <param name="keyboardState">Keyboard state exposed by the backend for the next frame.</param>
        void AdvanceFrame(KeyboardState keyboardState) {
            InputBackend.SetKeyboardState(keyboardState);
            Core.Instance.Update();
        }

        /// <summary>
        /// Writes one deterministic packaged font asset to the temporary content root.
        /// </summary>
        /// <param name="relativePath">Project-relative font asset path.</param>
        void WriteFontAsset(string relativePath) {
            string fullPath = Path.Combine(TempRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string directoryPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Font asset directory could not be resolved.");
            }

            Directory.CreateDirectory(directoryPath);
            FontAsset fontAsset = CreateFont(Path.GetFileNameWithoutExtension(relativePath));
            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            FontAssetBinarySerializer.Serialize(stream, fontAsset);
        }

        /// <summary>
        /// Creates one packaged font asset with deterministic glyph metrics for the current test.
        /// </summary>
        /// <param name="name">Friendly font name.</param>
        /// <returns>Runtime font asset that can satisfy menu text layout.</returns>
        FontAsset CreateFont(string name) {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
            string glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .:-+/>()";
            for (int index = 0; index < glyphs.Length; index++) {
                char glyph = glyphs[index];
                if (characters.ContainsKey(glyph)) {
                    continue;
                }

                float width = 8f;
                if (glyph == ' ') {
                    width = 4f;
                } else if (glyph == 'M' || glyph == 'm' || glyph == 'W' || glyph == 'w') {
                    width = 10f;
                } else if (glyph == 'i' || glyph == 'l' || glyph == 'I' || glyph == '.' || glyph == ':' || glyph == '-' || glyph == '+' || glyph == '/' || glyph == ')' || glyph == '(' || glyph == '>') {
                    width = 4f;
                }

                characters[glyph] = new FontChar(new float4(0f, 0f, width, 12f), 0f, width, 0f, 0f);
            }

            TextureAsset textureAsset = new TextureAsset {
                Width = 1,
                Height = 1,
                Colors = new byte[] { 255, 255, 255, 255 }
            };
            FontAsset fontAsset = new FontAsset(
                new FontInfo(name, 16, 4f),
                new TestRuntimeTexture {
                    Width = 1,
                    Height = 1
                },
                characters,
                16f,
                1,
                1) {
                SourceTextureAsset = textureAsset
            };
            return fontAsset;
        }

        /// <summary>
        /// Writes one empty packaged scene asset referenced by the menu-definition test provider.
        /// </summary>
        /// <param name="relativePath">Project-relative packaged scene path.</param>
        void WriteSceneAsset(string relativePath) {
            string fullPath = Path.Combine(TempRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string directoryPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Scene asset directory could not be resolved.");
            }

            Directory.CreateDirectory(directoryPath);
            SceneAsset sceneAsset = new SceneAsset {
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "root-entity",
                        Name = "Root",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = Array.Empty<SceneComponentAssetRecord>(),
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };
            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
        }
    }
}
