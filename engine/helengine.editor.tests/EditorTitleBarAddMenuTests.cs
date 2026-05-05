using System.Collections.Generic;
using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the editor title bar exposes the Add menu beside File.
    /// </summary>
    public class EditorTitleBarAddMenuTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the title-bar tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the title bar.
        /// </summary>
        public EditorTitleBarAddMenuTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-titlebar-add-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes temporary test state after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the Add button is laid out immediately to the right of File.
        /// </summary>
        [Fact]
        public void UpdateLayout_PlacesAddButtonImmediatelyToTheRightOfFile() {
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Hel");

            EditorEntity fileButton = GetPrivateField<EditorEntity>(titleBar, "FileMenuButtonEntity");
            EditorEntity addButton = GetPrivateField<EditorEntity>(titleBar, "AddMenuButtonEntity");
            int fileButtonWidth = GetPrivateField<int>(titleBar, "FileMenuButtonWidth");

            Assert.Equal(fileButton.Position.X + fileButtonWidth, addButton.Position.X);
        }

        /// <summary>
        /// Ensures the Add menu shows Empty, Cube, Plane, Camera, and Light and hides File when opened.
        /// </summary>
        [Fact]
        public void ToggleAddMenu_ShowsExpectedItemsAndHidesFileMenu() {
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Hel");

            InvokePrivate(titleBar, "ToggleFileMenu");
            InvokePrivate(titleBar, "ToggleAddMenu");

            ContextMenu fileMenu = GetPrivateField<ContextMenu>(titleBar, "FileMenu");
            ContextMenu addMenu = GetPrivateField<ContextMenu>(titleBar, "AddMenu");
            List<ContextMenuItem> activeItems = GetPrivateField<List<ContextMenuItem>>(addMenu, "ActiveItems");

            Assert.False(fileMenu.IsVisible);
            Assert.True(addMenu.IsVisible);
            Assert.Collection(
                activeItems,
                item => Assert.Equal("Empty", item.Label),
                item => Assert.Equal("Cube", item.Label),
                item => Assert.Equal("Plane", item.Label),
                item => Assert.Equal("Camera", item.Label),
                item => Assert.Equal("Light", item.Label));
        }

        /// <summary>
        /// Ensures the Light submenu shows Spot Light, Point Light, and Directional Light when hovered.
        /// </summary>
        [Fact]
        public void ToggleAddMenu_WhenLightHovered_ShowsExpectedLightSubmenu() {
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Hel");

            InvokePrivate(titleBar, "ToggleAddMenu");

            ContextMenu addMenu = GetPrivateField<ContextMenu>(titleBar, "AddMenu");
            ContextMenu lightMenu = GetPrivateField<ContextMenu>(titleBar, "LightMenu");
            List<ContextMenuItem> addMenuItems = GetPrivateField<List<ContextMenuItem>>(addMenu, "ActiveItems");

            addMenuItems[4].HoverAction();

            List<ContextMenuItem> activeLightItems = GetPrivateField<List<ContextMenuItem>>(lightMenu, "ActiveItems");

            Assert.True(addMenu.IsVisible);
            Assert.True(lightMenu.IsVisible);
            Assert.Collection(
                activeLightItems,
                item => Assert.Equal("Spot Light", item.Label),
                item => Assert.Equal("Point Light", item.Label),
                item => Assert.Equal("Directional Light", item.Label));
        }

        /// <summary>
        /// Ensures the File menu exposes `Open Map...` between `New Map` and `Save Map`.
        /// </summary>
        [Fact]
        public void ToggleFileMenu_ShowsOpenMapBetweenNewAndSave() {
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Hel");

            InvokePrivate(titleBar, "ToggleFileMenu");

            ContextMenu fileMenu = GetPrivateField<ContextMenu>(titleBar, "FileMenu");
            List<ContextMenuItem> activeItems = GetPrivateField<List<ContextMenuItem>>(fileMenu, "ActiveItems");

            Assert.Collection(
                activeItems,
                item => Assert.Equal("New Map", item.Label),
                item => Assert.Equal("Open Map...", item.Label),
                item => Assert.Equal("Save Map", item.Label),
                item => Assert.Equal("Save Map As...", item.Label),
                item => Assert.Equal("Preferences...", item.Label));
        }

        /// <summary>
        /// Ensures the File menu exposes `Preferences...` after `Save Map As...`.
        /// </summary>
        [Fact]
        public void ToggleFileMenu_ShowsPreferencesAfterSaveMapAs() {
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Hel");

            InvokePrivate(titleBar, "ToggleFileMenu");

            ContextMenu fileMenu = GetPrivateField<ContextMenu>(titleBar, "FileMenu");
            List<ContextMenuItem> activeItems = GetPrivateField<List<ContextMenuItem>>(fileMenu, "ActiveItems");

            Assert.Collection(
                activeItems,
                item => Assert.Equal("New Map", item.Label),
                item => Assert.Equal("Open Map...", item.Label),
                item => Assert.Equal("Save Map", item.Label),
                item => Assert.Equal("Save Map As...", item.Label),
                item => Assert.Equal("Preferences...", item.Label));
        }

        /// <summary>
        /// Ensures hovering Add while File is open switches the visible top-level menu.
        /// </summary>
        [Fact]
        public void MenuStrip_WhenAddButtonHoveredWhileFileMenuOpen_SwitchesToAddMenu() {
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Hel");

            InvokePrivate(titleBar, "ToggleFileMenu");
            EditorEntity addButton = GetPrivateField<EditorEntity>(titleBar, "AddMenuButtonEntity");
            InteractableComponent addInteractable = FindComponent<InteractableComponent>(addButton);

            addInteractable.OnCursor(new int2(1, 1), new int2(0, 0), PointerInteraction.Hover);

            ContextMenu fileMenu = GetPrivateField<ContextMenu>(titleBar, "FileMenu");
            ContextMenu addMenu = GetPrivateField<ContextMenu>(titleBar, "AddMenu");

            Assert.False(fileMenu.IsVisible);
            Assert.True(addMenu.IsVisible);
        }

        /// <summary>
        /// Ensures hovering File while Add is open switches the visible top-level menu.
        /// </summary>
        [Fact]
        public void MenuStrip_WhenFileButtonHoveredWhileAddMenuOpen_SwitchesToFileMenu() {
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Hel");

            InvokePrivate(titleBar, "ToggleAddMenu");
            EditorEntity fileButton = GetPrivateField<EditorEntity>(titleBar, "FileMenuButtonEntity");
            InteractableComponent fileInteractable = FindComponent<InteractableComponent>(fileButton);

            fileInteractable.OnCursor(new int2(1, 1), new int2(0, 0), PointerInteraction.Hover);

            ContextMenu fileMenu = GetPrivateField<ContextMenu>(titleBar, "FileMenu");
            ContextMenu addMenu = GetPrivateField<ContextMenu>(titleBar, "AddMenu");

            Assert.True(fileMenu.IsVisible);
            Assert.False(addMenu.IsVisible);
        }

        /// <summary>
        /// Ensures menu-strip hover switching does not open a menu until the menu strip is active.
        /// </summary>
        [Fact]
        public void MenuStrip_WhenNoMenuIsOpen_DoesNotOpenMenuOnHover() {
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Hel");
            EditorEntity addButton = GetPrivateField<EditorEntity>(titleBar, "AddMenuButtonEntity");
            InteractableComponent addInteractable = FindComponent<InteractableComponent>(addButton);

            addInteractable.OnCursor(new int2(1, 1), new int2(0, 0), PointerInteraction.Hover);

            ContextMenu fileMenu = GetPrivateField<ContextMenu>(titleBar, "FileMenu");
            ContextMenu addMenu = GetPrivateField<ContextMenu>(titleBar, "AddMenu");

            Assert.False(fileMenu.IsVisible);
            Assert.False(addMenu.IsVisible);
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
        /// Invokes one non-public instance method.
        /// </summary>
        /// <param name="target">Target object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        void InvokePrivate(object target, string methodName) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, Array.Empty<object>());
        }

        /// <summary>
        /// Creates a small font asset that can satisfy title-bar layout.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['A'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['D'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['C'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['E'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['G'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['H'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['L'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['M'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['O'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                [' '] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['.'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['b'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
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
