using System.Reflection;
using System.Runtime.CompilerServices;
using helengine.editor.tests.testing;
using helengine.ui;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies editor-session wiring publishes dock order, routes keyboard focus updates, and resets shared focus state.
    /// </summary>
    public class EditorSessionKeyboardFocusIntegrationTests : IDisposable {
        /// <summary>
        /// Temporary project root used by session keyboard-focus integration tests.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes a temporary project root used by the current test.
        /// </summary>
        public EditorSessionKeyboardFocusIntegrationTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-session-keyboard-focus-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempProjectRootPath);
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "shader-cache"));
        }

        /// <summary>
        /// Resets shared services and removes temporary project state after each test.
        /// </summary>
        public void Dispose() {
            EditorKeyboardFocusService.Reset();
            EditorSelectionService.ClearSelection();
            EditorSceneMutationService.Reset();
            TransformGizmoSnapSettingsService.ResetDefaults();
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures layout updates publish the visible dock traversal order into the keyboard-focus service.
        /// </summary>
        [Fact]
        public void UpdateLayout_WhenCalled_PublishesVisibleDockOrderToTheFocusService() {
            EditorSession session = CreateSessionForKeyboardFocus(
                out DockingManager dockingManager,
                out TestInputManager inputManager,
                out EditorViewport mainViewport,
                out DockableEntity firstSecondaryDock,
                out EditorFocusTarget firstSecondaryTarget,
                out DockableEntity secondSecondaryDock,
                out EditorFocusTarget secondSecondaryTarget);

            try {
                session.UpdateLayout(1280, 720);

                IReadOnlyList<DockableEntity> expectedOrder = dockingManager.Layout.GetVisibleDockablesInTraversalOrder();
                IReadOnlyList<DockableEntity> actualOrder = GetKeyboardFocusDockOrder();

                Assert.Equal(expectedOrder.Count, actualOrder.Count);
                for (int i = 0; i < expectedOrder.Count; i++) {
                    Assert.Same(expectedOrder[i], actualOrder[i]);
                }
            } finally {
                session.Dispose();
            }
        }

        /// <summary>
        /// Ensures a pressed Ctrl+Tab routes through the session update loop and activates the next visible dock target.
        /// </summary>
        [Fact]
        public void Update_WhenCtrlTabIsPressed_MovesActivationToTheNextVisibleDock() {
            EditorSession session = CreateSessionForKeyboardFocus(
                out DockingManager dockingManager,
                out TestInputManager inputManager,
                out EditorViewport mainViewport,
                out DockableEntity firstSecondaryDock,
                out EditorFocusTarget firstSecondaryTarget,
                out DockableEntity secondSecondaryDock,
                out EditorFocusTarget secondSecondaryTarget);

            try {
                session.UpdateLayout(1280, 720);

                IReadOnlyList<DockableEntity> dockOrder = dockingManager.Layout.GetVisibleDockablesInTraversalOrder();
                Assert.True(dockOrder.Count >= 2);

                EditorFocusTarget viewportContentTarget = GetPrivateField<EditorFocusTarget>(mainViewport, "ViewportContentFocusTarget");
                IFocusTarget firstTarget = ResolveTargetForDock(
                    dockOrder[0],
                    mainViewport,
                    viewportContentTarget,
                    firstSecondaryDock,
                    firstSecondaryTarget,
                    secondSecondaryDock,
                    secondSecondaryTarget);
                IFocusTarget secondTarget = ResolveTargetForDock(
                    dockOrder[1],
                    mainViewport,
                    viewportContentTarget,
                    firstSecondaryDock,
                    firstSecondaryTarget,
                    secondSecondaryDock,
                    secondSecondaryTarget);

                EditorKeyboardFocusService.SetFocusedTarget(firstTarget);

                inputManager.SetKeyboardState(new KeyboardState(Keys.LeftControl, Keys.Tab));
                session.Update();

                Assert.Same(secondTarget, GetKeyboardFocusFocusedTarget());
            } finally {
                session.Dispose();
            }
        }

        /// <summary>
        /// Ensures disposing a session clears the static editor keyboard-focus state.
        /// </summary>
        [Fact]
        public void Dispose_WhenCalled_ResetsStaticKeyboardFocusState() {
            EditorSession session = CreateSessionForKeyboardFocus(
                out DockingManager dockingManager,
                out TestInputManager inputManager,
                out EditorViewport mainViewport,
                out DockableEntity firstSecondaryDock,
                out EditorFocusTarget firstSecondaryTarget,
                out DockableEntity secondSecondaryDock,
                out EditorFocusTarget secondSecondaryTarget);

            session.UpdateLayout(1280, 720);

            EditorFocusTarget viewportContentTarget = GetPrivateField<EditorFocusTarget>(mainViewport, "ViewportContentFocusTarget");
            EditorKeyboardFocusService.SetFocusedTarget(viewportContentTarget);

            Assert.NotNull(GetKeyboardFocusFocusedTarget());

            session.Dispose();

            Assert.Null(GetKeyboardFocusFocusedTarget());
            Assert.Null(GetKeyboardFocusActiveRootGroup());
            Assert.Empty(GetKeyboardFocusDockOrder());
        }

        /// <summary>
        /// Creates a partially initialized editor session with enough real collaborators to exercise keyboard-focus wiring.
        /// </summary>
        /// <param name="dockingManager">Docking manager used by the created session.</param>
        /// <param name="inputManager">Input manager used by the created session core.</param>
        /// <param name="mainViewport">Primary viewport dock used as the root dock.</param>
        /// <param name="firstSecondaryDock">First secondary dock registered in the layout.</param>
        /// <param name="firstSecondaryTarget">Default focus target owned by the first secondary dock.</param>
        /// <param name="secondSecondaryDock">Second secondary dock registered in the layout.</param>
        /// <param name="secondSecondaryTarget">Default focus target owned by the second secondary dock.</param>
        /// <returns>Editor session configured for keyboard-focus integration tests.</returns>
        EditorSession CreateSessionForKeyboardFocus(
            out DockingManager dockingManager,
            out TestInputManager inputManager,
            out EditorViewport mainViewport,
            out DockableEntity firstSecondaryDock,
            out EditorFocusTarget firstSecondaryTarget,
            out DockableEntity secondSecondaryDock,
            out EditorFocusTarget secondSecondaryTarget) {
            EditorKeyboardFocusService.Reset();
            EditorSelectionService.ClearSelection();
            EditorSceneMutationService.Reset();
            TransformGizmoSnapSettingsService.ResetDefaults();

            inputManager = new TestInputManager();
            inputManager.SetKeyboardState(new KeyboardState());
            inputManager.SetMouseState(CreateMouseState());

            EditorCore core = new EditorCore(new Project {
                Name = "Keyboard Focus",
                Path = TempProjectRootPath
            });
            core.Initialize(TestDirectX11RenderManager3D.Create(), new TestRenderManager2D(), inputManager);
            core.InputManager.SetKeyboardActive(true);

            FontAsset font = CreateFont();
            EditorEntity uiCameraEntity = new EditorEntity {
                InternalEntity = true
            };
            CameraComponent uiCameraComponent = new CameraComponent();
            uiCameraEntity.AddComponent(uiCameraComponent);
            EditorEntity sceneCameraEntity = new EditorEntity {
                InternalEntity = true,
                Position = new float3(0f, 3f, -8f)
            };
            CameraComponent sceneCameraComponent = new CameraComponent();
            sceneCameraEntity.AddComponent(sceneCameraComponent);
            CameraComponent gizmoCameraComponent = new CameraComponent();
            sceneCameraEntity.AddComponent(gizmoCameraComponent);
            mainViewport = new EditorViewport(sceneCameraComponent, font, font, CreateToolbarIcons());
            mainViewport.Size = new int2(640, 360);

            firstSecondaryDock = CreateDock("Hierarchy", font, out firstSecondaryTarget);
            secondSecondaryDock = CreateDock("Browser", font, out secondSecondaryTarget);

            EditorKeyboardFocusService.RegisterGroup(mainViewport);
            EditorKeyboardFocusService.RegisterGroup(firstSecondaryDock);
            EditorKeyboardFocusService.RegisterGroup(secondSecondaryDock);

            dockingManager = new DockingManager();
            dockingManager.Layout.Add(mainViewport);
            dockingManager.Layout.Add(firstSecondaryDock);
            dockingManager.Layout.Add(secondSecondaryDock);
            dockingManager.Layout.DockAsRoot(mainViewport);
            dockingManager.Layout.DockRelative(firstSecondaryDock, mainViewport, DockInsertDirection.Left, 0.3f);
            dockingManager.Layout.DockRelative(secondSecondaryDock, mainViewport, DockInsertDirection.Bottom, 0.7f);

            var keyboardFocusEntity = new EditorEntity {
                InternalEntity = true,
                Enabled = true,
                LayerMask = EditorLayerMasks.EditorUi
            };
            var keyboardFocusUpdateComponent = new EditorKeyboardFocusUpdateComponent {
                UpdateOrder = core.ObjectManager.GetUpdateOrderForLayer(1)
            };
            keyboardFocusEntity.AddComponent(keyboardFocusUpdateComponent);

            EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
            SetPrivateField(session, "core", core);
            SetPrivateField(session, "titleBar", new EditorTitleBar(font, 1280, 720, "Keyboard Focus"));
            SetPrivateField(session, "dockingManager", dockingManager);
            SetPrivateField(session, "mainViewport", mainViewport);
            SetPrivateField(session, "uiCameraComponent", uiCameraComponent);
            SetPrivateField(session, "sceneCameraComponent", sceneCameraComponent);
            SetPrivateField(session, "gizmoCameraComponent", gizmoCameraComponent);
            SetPrivateField(session, "assetBrowserPanel", new AssetBrowserPanel(font, TempProjectRootPath));
            SetPrivateField(session, "propertiesPanel", new PropertiesPanel(font, core.GetContentManager()));
            SetPrivateField(session, "loggerPanel", new LoggerPanel(font));
            SetPrivateField(session, "assetPickerModal", new AssetPickerModal(font, TempProjectRootPath));
            SetPrivateField(session, "saveFileDialog", new SaveFileDialog(font, TempProjectRootPath));
            SetPrivateField(session, "openFileDialog", new OpenFileDialog(font, TempProjectRootPath));
            SetPrivateField(session, "unsavedChangesDialog", new UnsavedChangesDialog(font));
            SetPrivateField(session, "shaderModuleManager", CreateShaderModuleManager());

            return session;
        }

        /// <summary>
        /// Creates a dockable panel with one registered default focus target.
        /// </summary>
        /// <param name="title">Dock title used for diagnostics.</param>
        /// <param name="font">Font used by the dock title bar.</param>
        /// <param name="focusTarget">Created default focus target for the dock.</param>
        /// <returns>Configured dockable entity.</returns>
        DockableEntity CreateDock(string title, FontAsset font, out EditorFocusTarget focusTarget) {
            DockableEntity dock = new DockableEntity(font) {
                Title = title
            };
            dock.Size = new int2(280, 220);

            focusTarget = new EditorFocusTarget(
                dock,
                0,
                true,
                () => dock.Enabled,
                point => dock.ContainsScreenPoint(point),
                isFocused => { },
                key => false,
                key => { });
            EditorKeyboardFocusService.RegisterTarget(focusTarget);

            return dock;
        }

        /// <summary>
        /// Creates a shader module manager that can be disposed safely during tests without starting file watchers.
        /// </summary>
        /// <returns>Disposable shader module manager for the test session.</returns>
        ShaderModuleManager CreateShaderModuleManager() {
            string shaderRootPath = Path.Combine(TempProjectRootPath, "assets", "Shaders");
            string packageOutputPath = Path.Combine(TempProjectRootPath, "shader-cache");
            Directory.CreateDirectory(shaderRootPath);
            Directory.CreateDirectory(packageOutputPath);

            ShaderTargetBuildOptions targetOptions = new ShaderTargetBuildOptions(ShaderCompileTarget.DirectX11, new ShaderModel(4, 0));
            ShaderPackageBuildOptions buildOptions = new ShaderPackageBuildOptions(
                new[] { targetOptions },
                ShaderBindingPolicies.Default,
                true,
                false,
                false,
                Array.Empty<ShaderDefine>());
            ShaderModuleManagerOptions options = new ShaderModuleManagerOptions(
                shaderRootPath,
                packageOutputPath,
                buildOptions,
                ShaderCompileTarget.DirectX11,
                100);
            return new ShaderModuleManager(options);
        }

        /// <summary>
        /// Resolves the focus target associated with one dock in the current test layout.
        /// </summary>
        /// <param name="dock">Dock whose target should be resolved.</param>
        /// <param name="mainViewport">Viewport dock registered as the root dock.</param>
        /// <param name="viewportContentTarget">Viewport content target used as the dock default target.</param>
        /// <param name="firstSecondaryDock">First secondary dock in the layout.</param>
        /// <param name="firstSecondaryTarget">Default target for the first secondary dock.</param>
        /// <param name="secondSecondaryDock">Second secondary dock in the layout.</param>
        /// <param name="secondSecondaryTarget">Default target for the second secondary dock.</param>
        /// <returns>Resolved focus target for the requested dock.</returns>
        IFocusTarget ResolveTargetForDock(
            DockableEntity dock,
            EditorViewport mainViewport,
            EditorFocusTarget viewportContentTarget,
            DockableEntity firstSecondaryDock,
            EditorFocusTarget firstSecondaryTarget,
            DockableEntity secondSecondaryDock,
            EditorFocusTarget secondSecondaryTarget) {
            if (ReferenceEquals(dock, mainViewport)) {
                return viewportContentTarget;
            } else if (ReferenceEquals(dock, firstSecondaryDock)) {
                return firstSecondaryTarget;
            } else if (ReferenceEquals(dock, secondSecondaryDock)) {
                return secondSecondaryTarget;
            }

            throw new InvalidOperationException("Unexpected dock was encountered while resolving the focus target.");
        }

        /// <summary>
        /// Reads the published dock order from the shared keyboard-focus service.
        /// </summary>
        /// <returns>Current dock order snapshot.</returns>
        IReadOnlyList<DockableEntity> GetKeyboardFocusDockOrder() {
            object value = GetPrivateStaticField(typeof(EditorKeyboardFocusService), "DockOrder");
            return Assert.IsAssignableFrom<IReadOnlyList<DockableEntity>>(value);
        }

        /// <summary>
        /// Reads the currently focused target from the shared keyboard-focus service.
        /// </summary>
        /// <returns>Currently focused target, or null when no target is focused.</returns>
        IFocusTarget GetKeyboardFocusFocusedTarget() {
            object value = GetPrivateStaticField(typeof(EditorKeyboardFocusService), "FocusedTarget");
            if (value == null) {
                return null;
            }

            return Assert.IsAssignableFrom<IFocusTarget>(value);
        }

        /// <summary>
        /// Reads the currently active root group from the shared keyboard-focus service.
        /// </summary>
        /// <returns>Currently active root group, or null when no dock is active.</returns>
        IFocusGroup GetKeyboardFocusActiveRootGroup() {
            object value = GetPrivateStaticField(typeof(EditorKeyboardFocusService), "ActiveRootGroup");
            if (value == null) {
                return null;
            }

            return Assert.IsAssignableFrom<IFocusGroup>(value);
        }

        /// <summary>
        /// Reads one non-public static field.
        /// </summary>
        /// <param name="type">Type that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value.</returns>
        object GetPrivateStaticField(Type type, string fieldName) {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null) {
                throw new InvalidOperationException("Expected private static field was not found.");
            }

            return field.GetValue(null);
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            Type currentType = target.GetType();
            while (currentType != null) {
                FieldInfo field = currentType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null) {
                    object value = field.GetValue(target);
                    return Assert.IsType<T>(value);
                }

                currentType = currentType.BaseType;
            }

            throw new InvalidOperationException("Expected private field was not found.");
        }

        /// <summary>
        /// Assigns one non-public instance field.
        /// </summary>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to assign.</param>
        /// <param name="value">Value assigned to the field.</param>
        void SetPrivateField(object target, string fieldName, object value) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) {
                throw new InvalidOperationException("Expected private field was not found.");
            }

            field.SetValue(target, value);
        }

        /// <summary>
        /// Creates a released mouse state at the origin.
        /// </summary>
        /// <returns>Mouse state used by session-update tests.</returns>
        MouseState CreateMouseState() {
            return new MouseState(
                0,
                0,
                0,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released);
        }

        /// <summary>
        /// Creates a deterministic font asset that satisfies the layout requirements of the current tests.
        /// </summary>
        /// <returns>Font asset with generic glyph metrics.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
            string glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .:-+/>()";
            for (int i = 0; i < glyphs.Length; i++) {
                char glyph = glyphs[i];
                if (characters.ContainsKey(glyph)) {
                    continue;
                }

                float width = 8f;
                if (glyph == ' ') {
                    width = 4f;
                } else if (glyph == '.' || glyph == ':' || glyph == '-' || glyph == '+' || glyph == '/' || glyph == ')' || glyph == '(' || glyph == '>') {
                    width = 4f;
                } else if (glyph == 'M' || glyph == 'm' || glyph == 'W' || glyph == 'w') {
                    width = 10f;
                } else if (glyph == 'i' || glyph == 'l' || glyph == 'I') {
                    width = 4f;
                }

                characters[glyph] = new FontChar(new float4(0f, 0f, width, 12f), 0f, width, 0f, 0f);
            }

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
        /// Creates a minimal toolbar icon set for editor viewport construction.
        /// </summary>
        /// <returns>Toolbar icon set backed by deterministic test textures.</returns>
        EditorViewportToolbarIconSet CreateToolbarIcons() {
            return new EditorViewportToolbarIconSet(
                CreateIconTexture(),
                CreateIconTexture(),
                CreateIconTexture(),
                CreateIconTexture(),
                CreateIconTexture(),
                CreateIconTexture(),
                CreateIconTexture(),
                CreateIconTexture());
        }

        /// <summary>
        /// Creates one deterministic runtime texture used by toolbar icons.
        /// </summary>
        /// <returns>Runtime texture with a stable size.</returns>
        RuntimeTexture CreateIconTexture() {
            return new TestRuntimeTexture {
                Width = 16,
                Height = 16
            };
        }
    }
}
