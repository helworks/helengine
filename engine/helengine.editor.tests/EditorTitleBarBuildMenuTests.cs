using System.Collections.Generic;
using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the editor title bar exposes the Build menu commands.
    /// </summary>
    public class EditorTitleBarBuildMenuTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the title-bar tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the title bar.
        /// </summary>
        public EditorTitleBarBuildMenuTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-titlebar-build-tests", Guid.NewGuid().ToString("N"));
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
        /// Ensures the Build button is laid out immediately to the right of Add.
        /// </summary>
        [Fact]
        public void UpdateLayout_PlacesBuildButtonImmediatelyToTheRightOfAdd() {
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Hel");

            EditorEntity addButton = GetPrivateField<EditorEntity>(titleBar, "AddMenuButtonEntity");
            EditorEntity buildButton = GetPrivateField<EditorEntity>(titleBar, "BuildMenuButtonEntity");
            int addButtonWidth = GetPrivateField<int>(titleBar, "AddMenuButtonWidth");

            Assert.Equal(addButton.Position.X + addButtonWidth, buildButton.Position.X);
        }

        /// <summary>
        /// Ensures the Build menu shows Platforms and Build while hiding the other title-bar menus.
        /// </summary>
        [Fact]
        public void ToggleBuildMenu_ShowsBuildSettingsAndProfilesAndHidesOtherMenus() {
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Hel");

            InvokePrivate(titleBar, "ToggleFileMenu");
            InvokePrivate(titleBar, "ToggleBuildMenu");

            ContextMenu fileMenu = GetPrivateField<ContextMenu>(titleBar, "FileMenu");
            ContextMenu addMenu = GetPrivateField<ContextMenu>(titleBar, "AddMenu");
            ContextMenu buildMenu = GetPrivateField<ContextMenu>(titleBar, "BuildMenu");
            List<ContextMenuItem> activeItems = GetPrivateField<List<ContextMenuItem>>(buildMenu, "ActiveItems");

            Assert.False(fileMenu.IsVisible);
            Assert.False(addMenu.IsVisible);
            Assert.True(buildMenu.IsVisible);
            Assert.Collection(
                activeItems,
                item => Assert.Equal("Platforms...", item.Label),
                item => Assert.Equal("Profiles...", item.Label),
                item => Assert.Equal("Build...", item.Label),
                item => Assert.Equal("Build Scripts...", item.Label),
                item => Assert.Equal("Open in IDE...", item.Label));
        }

        /// <summary>
        /// Ensures activating Platforms raises the public command event.
        /// </summary>
        [Fact]
        public void BuildMenu_WhenPlatformsActivated_RaisesPlatformsRequested() {
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Hel");
            bool raised = false;
            bool buildSettingsRaised = false;
            titleBar.PlatformsRequested += () => raised = true;
            titleBar.BuildSettingsRequested += () => buildSettingsRaised = true;

            InvokePrivate(titleBar, "ToggleBuildMenu");

            ContextMenu buildMenu = GetPrivateField<ContextMenu>(titleBar, "BuildMenu");
            List<ContextMenuItem> activeItems = GetPrivateField<List<ContextMenuItem>>(buildMenu, "ActiveItems");

            activeItems[0].Action();

            Assert.True(raised);
            Assert.False(buildSettingsRaised);
            Assert.False(buildMenu.IsVisible);
        }

        /// <summary>
        /// Ensures activating Profiles raises the public command event.
        /// </summary>
        [Fact]
        public void BuildMenu_WhenProfilesActivated_RaisesProfilesRequested() {
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Hel");
            bool raised = false;
            titleBar.ProfilesRequested += () => raised = true;

            InvokePrivate(titleBar, "ToggleBuildMenu");

            ContextMenu buildMenu = GetPrivateField<ContextMenu>(titleBar, "BuildMenu");
            List<ContextMenuItem> activeItems = GetPrivateField<List<ContextMenuItem>>(buildMenu, "ActiveItems");

            activeItems[1].Action();

            Assert.True(raised);
            Assert.False(buildMenu.IsVisible);
        }

        /// <summary>
        /// Ensures activating Build raises the public command event.
        /// </summary>
        [Fact]
        public void BuildMenu_WhenBuildActivated_RaisesBuildRequested() {
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Hel");
            bool raised = false;
            titleBar.BuildRequested += () => raised = true;

            InvokePrivate(titleBar, "ToggleBuildMenu");

            ContextMenu buildMenu = GetPrivateField<ContextMenu>(titleBar, "BuildMenu");
            List<ContextMenuItem> activeItems = GetPrivateField<List<ContextMenuItem>>(buildMenu, "ActiveItems");

            activeItems[2].Action();

            Assert.True(raised);
            Assert.False(buildMenu.IsVisible);
        }

        /// <summary>
        /// Ensures activating Build Scripts raises the public command event.
        /// </summary>
        [Fact]
        public void BuildMenu_WhenBuildScriptsActivated_RaisesBuildScriptsRequested() {
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Hel");
            bool raised = false;
            titleBar.BuildScriptsRequested += () => raised = true;

            InvokePrivate(titleBar, "ToggleBuildMenu");

            ContextMenu buildMenu = GetPrivateField<ContextMenu>(titleBar, "BuildMenu");
            List<ContextMenuItem> activeItems = GetPrivateField<List<ContextMenuItem>>(buildMenu, "ActiveItems");

            activeItems[3].Action();

            Assert.True(raised);
            Assert.False(buildMenu.IsVisible);
        }

        /// <summary>
        /// Ensures activating Open in IDE raises the public command event.
        /// </summary>
        [Fact]
        public void BuildMenu_WhenOpenInIDEActivated_RaisesOpenInIDERequested() {
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Hel");
            bool raised = false;
            titleBar.OpenInIDERequested += () => raised = true;

            InvokePrivate(titleBar, "ToggleBuildMenu");

            ContextMenu buildMenu = GetPrivateField<ContextMenu>(titleBar, "BuildMenu");
            List<ContextMenuItem> activeItems = GetPrivateField<List<ContextMenuItem>>(buildMenu, "ActiveItems");

            activeItems[4].Action();

            Assert.True(raised);
            Assert.False(buildMenu.IsVisible);
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
                ['B'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['D'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['E'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['H'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['I'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['b'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['O'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['.'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
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
