using System.Collections.Generic;
using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies render-order and contrast behavior for the main editor title bar.
    /// </summary>
    public class EditorTitleBarTests : IDisposable {
        /// <summary>
        /// Stores the theme that was active before the test modified it.
        /// </summary>
        readonly ThemeManager.ThemePalette OriginalTheme;

        /// <summary>
        /// Captures the original theme so it can be restored after each test.
        /// </summary>
        public EditorTitleBarTests() {
            OriginalTheme = ThemeManager.Current;
        }

        /// <summary>
        /// Restores shared theme state after each test.
        /// </summary>
        public void Dispose() {
            ThemeManager.SetTheme(OriginalTheme);
        }

        /// <summary>
        /// Ensures the main title text uses a high-contrast color against the dark title-bar surface.
        /// </summary>
        [Fact]
        public void Constructor_UsesAccentQuaternaryForMainTitleText() {
            InitializeCore();
            ThemeManager.SetTheme(ThemeManager.CreateNeon90s());
            const string title = "Main Editor Title";

            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, title);

            TextComponent titleText = FindTextComponent(titleBar.Entity, title);

            Assert.Equal(ThemeManager.Colors.AccentQuaternary, titleText.Color);
        }

        /// <summary>
        /// Ensures the main File menu renders above docked panel content and labels.
        /// </summary>
        [Fact]
        public void FileMenu_UsesOverlayRenderOrdersAboveDockPanels() {
            InitializeCore();
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Main Editor Title");
            ContextMenu fileMenu = GetPrivateField<ContextMenu>(titleBar, "FileMenu");

            fileMenu.Show(
                new[] {
                    new ContextMenuItem("Main", HandleMenuItemActivated)
                },
                new int2(0, 0),
                new int2(1280, 720));

            RoundedRectComponent menuBackground = FindComponent<RoundedRectComponent>(fileMenu.Entity);
            TextComponent menuItemText = FindTextComponent(fileMenu.Entity, "Main");

            Assert.Equal(RenderOrder2D.OverlayBackground, menuBackground.RenderOrder2D);
            Assert.Equal(RenderOrder2D.OverlayForeground, menuItemText.RenderOrder2D);
        }

        /// <summary>
        /// Ensures the Add menu uses the same overlay orders as the File menu and stays above docked panels.
        /// </summary>
        [Fact]
        public void AddMenu_UsesOverlayRenderOrdersAboveDockPanels() {
            InitializeCore();
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Main Editor Title");
            ContextMenu addMenu = GetPrivateField<ContextMenu>(titleBar, "AddMenu");

            addMenu.Show(
                new[] {
                    new ContextMenuItem("Cube", HandleMenuItemActivated)
                },
                new int2(0, 0),
                new int2(1280, 720));

            RoundedRectComponent menuBackground = FindComponent<RoundedRectComponent>(addMenu.Entity);
            TextComponent menuItemText = FindTextComponent(addMenu.Entity, "Cube");

            Assert.Equal(RenderOrder2D.OverlayBackground, menuBackground.RenderOrder2D);
            Assert.Equal(RenderOrder2D.OverlayForeground, menuItemText.RenderOrder2D);
        }

        /// <summary>
        /// Initializes a core instance with the minimum services required by title-bar UI controls.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Creates a small font asset that can satisfy title-bar text layout in tests.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['M'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['E'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['T'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['C'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['b'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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

        /// <summary>
        /// Handles a test context-menu activation without side effects.
        /// </summary>
        void HandleMenuItemActivated() {
        }

        /// <summary>
        /// Finds a text component in an entity hierarchy by exact displayed text.
        /// </summary>
        /// <param name="entity">Root entity to inspect.</param>
        /// <param name="text">Exact text to locate.</param>
        /// <returns>Matching text component.</returns>
        TextComponent FindTextComponent(Entity entity, string text) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (string.IsNullOrWhiteSpace(text)) {
                throw new ArgumentException("Text to locate must be provided.", nameof(text));
            }

            List<Entity> pendingEntities = new List<Entity> {
                entity
            };

            for (int entityIndex = 0; entityIndex < pendingEntities.Count; entityIndex++) {
                Entity currentEntity = pendingEntities[entityIndex];
                if (currentEntity.Components != null) {
                    for (int componentIndex = 0; componentIndex < currentEntity.Components.Count; componentIndex++) {
                        if (currentEntity.Components[componentIndex] is TextComponent textComponent &&
                            textComponent.Text == text) {
                            return textComponent;
                        }
                    }
                }

                if (currentEntity.Children != null) {
                    for (int childIndex = 0; childIndex < currentEntity.Children.Count; childIndex++) {
                        pendingEntities.Add(currentEntity.Children[childIndex]);
                    }
                }
            }

            throw new InvalidOperationException("Expected to find the requested text component in the title-bar hierarchy.");
        }

        /// <summary>
        /// Finds the first component of the requested type in an entity hierarchy.
        /// </summary>
        /// <typeparam name="T">Component type to locate.</typeparam>
        /// <param name="entity">Root entity to inspect.</param>
        /// <returns>Matching component instance.</returns>
        T FindComponent<T>(Entity entity) where T : Component {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            List<Entity> pendingEntities = new List<Entity> {
                entity
            };

            for (int entityIndex = 0; entityIndex < pendingEntities.Count; entityIndex++) {
                Entity currentEntity = pendingEntities[entityIndex];
                if (currentEntity.Components != null) {
                    for (int componentIndex = 0; componentIndex < currentEntity.Components.Count; componentIndex++) {
                        if (currentEntity.Components[componentIndex] is T component) {
                            return component;
                        }
                    }
                }

                if (currentEntity.Children != null) {
                    for (int childIndex = 0; childIndex < currentEntity.Children.Count; childIndex++) {
                        pendingEntities.Add(currentEntity.Children[childIndex]);
                    }
                }
            }

            throw new InvalidOperationException("Expected to find the requested component type in the entity hierarchy.");
        }

        /// <summary>
        /// Reads a private reference-type field from an object using reflection.
        /// </summary>
        /// <typeparam name="T">Field type.</typeparam>
        /// <param name="instance">Object containing the private field.</param>
        /// <param name="fieldName">Exact field name to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object instance, string fieldName) where T : class {
            if (instance == null) {
                throw new ArgumentNullException(nameof(instance));
            }
            if (string.IsNullOrWhiteSpace(fieldName)) {
                throw new ArgumentException("Field name must be provided.", nameof(fieldName));
            }

            FieldInfo fieldInfo = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (fieldInfo == null) {
                throw new InvalidOperationException("Expected the requested private field to exist.");
            }

            object value = fieldInfo.GetValue(instance);
            if (value is T typedValue) {
                return typedValue;
            }

            throw new InvalidOperationException("Expected the requested private field to match the requested type.");
        }
    }
}
