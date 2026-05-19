using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies keyboard-focus behavior and shortcut gating for editor viewports.
    /// </summary>
    public class EditorViewportKeyboardFocusTests : IDisposable {
        /// <summary>
        /// Clears shared keyboard-focus and snap state after each test.
        /// </summary>
        public void Dispose() {
            EditorKeyboardFocusService.Reset();
            TransformGizmoSnapSettingsService.ResetDefaults();
        }

        /// <summary>
        /// Ensures the focused viewport content target maps W, R, and S to the viewport tool modes.
        /// </summary>
        [Fact]
        public void EditorViewport_WhenContentTargetIsFocused_WAndRAndSChangeToolMode() {
            TestInputBackend inputManager = InitializeCore();
            EditorViewport viewport = CreateViewport();
            EditorFocusTarget contentTarget = GetPrivateField<EditorFocusTarget>(viewport, "ViewportContentFocusTarget");
            inputManager.SetMouseState(CreateMouseState(ButtonState.Released));
            Core.Instance.InputSystem.EarlyUpdate();

            viewport.ToolMode = EditorViewportToolMode.Scale;
            EditorKeyboardFocusService.SetFocusedTarget(contentTarget);

            EditorKeyboardFocusService.HandleActivationKey(Keys.W);
            Assert.Equal(EditorViewportToolMode.Translate, viewport.ToolMode);

            EditorKeyboardFocusService.HandleActivationKey(Keys.R);
            Assert.Equal(EditorViewportToolMode.Rotate, viewport.ToolMode);

            EditorKeyboardFocusService.HandleActivationKey(Keys.S);
            Assert.Equal(EditorViewportToolMode.Scale, viewport.ToolMode);
        }

        /// <summary>
        /// Ensures the focused viewport content target routes F to the viewport selection-focus action.
        /// </summary>
        [Fact]
        public void EditorViewport_WhenContentTargetIsFocused_FInvokesSelectionFocusRequest() {
            TestInputBackend inputManager = InitializeCore();
            EditorViewport viewport = CreateViewport();
            EditorFocusTarget contentTarget = GetPrivateField<EditorFocusTarget>(viewport, "ViewportContentFocusTarget");
            inputManager.SetMouseState(CreateMouseState(ButtonState.Released));
            Core.Instance.InputSystem.EarlyUpdate();

            bool wasRequested = false;
            viewport.FocusSelectionRequested = () => wasRequested = true;
            EditorKeyboardFocusService.SetFocusedTarget(contentTarget);

            EditorKeyboardFocusService.HandleActivationKey(Keys.F);

            Assert.True(wasRequested);
        }

        /// <summary>
        /// Ensures scale shortcuts are ignored while the right mouse button is pressed for viewport camera input.
        /// </summary>
        [Fact]
        public void EditorViewport_WhenRightMouseButtonIsPressed_SIsIgnored() {
            TestInputBackend inputManager = InitializeCore();
            EditorViewport viewport = CreateViewport();
            EditorFocusTarget contentTarget = GetPrivateField<EditorFocusTarget>(viewport, "ViewportContentFocusTarget");
            inputManager.SetMouseState(CreateMouseState(ButtonState.Pressed));
            Core.Instance.InputSystem.EarlyUpdate();

            viewport.ToolMode = EditorViewportToolMode.Rotate;
            EditorKeyboardFocusService.SetFocusedTarget(contentTarget);
            EditorKeyboardFocusService.HandleActivationKey(Keys.S);

            Assert.Equal(EditorViewportToolMode.Rotate, viewport.ToolMode);
        }

        /// <summary>
        /// Ensures focused toolbar targets activate tool-mode and snap adjustment paths from Enter and Space.
        /// </summary>
        [Fact]
        public void EditorViewport_WhenToolbarButtonsReceiveFocus_EnterAndSpaceActivateThem() {
            InitializeCore();
            EditorViewport viewport = CreateViewport();
            EditorFocusTarget[] toolTargets = GetPrivateField<EditorFocusTarget[]>(viewport, "ToolButtonFocusTargets");
            EditorFocusTarget[] snapIncreaseTargets = GetPrivateField<EditorFocusTarget[]>(viewport, "SnapIncreaseFocusTargets");
            EditorFocusTarget[] snapDecreaseTargets = GetPrivateField<EditorFocusTarget[]>(viewport, "SnapDecreaseFocusTargets");
            bool[] toolKeyboardStates = GetPrivateField<bool[]>(viewport, "ToolButtonKeyboardFocusStates");
            bool[] snapIncreaseKeyboardStates = GetPrivateField<bool[]>(viewport, "SnapIncreaseKeyboardFocusStates");
            bool[] snapDecreaseKeyboardStates = GetPrivateField<bool[]>(viewport, "SnapDecreaseKeyboardFocusStates");

            viewport.ToolMode = EditorViewportToolMode.Translate;
            double initialSnapValue = TransformGizmoSnapSettingsService.GetSnapValue(EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap1);

            toolTargets[1].SetTargetFocused(true);
            Assert.True(toolKeyboardStates[1]);
            toolTargets[1].ActivateFromKey(Keys.Enter);
            Assert.Equal(EditorViewportToolMode.Rotate, viewport.ToolMode);

            toolTargets[2].ActivateFromKey(Keys.Space);
            Assert.Equal(EditorViewportToolMode.Scale, viewport.ToolMode);

            viewport.ToolMode = EditorViewportToolMode.Translate;
            snapIncreaseTargets[0].SetTargetFocused(true);
            Assert.True(snapIncreaseKeyboardStates[0]);
            snapIncreaseTargets[0].ActivateFromKey(Keys.Enter);
            Assert.Equal(initialSnapValue * 2.0, TransformGizmoSnapSettingsService.GetSnapValue(EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap1));

            snapDecreaseTargets[0].SetTargetFocused(true);
            Assert.True(snapDecreaseKeyboardStates[0]);
            snapDecreaseTargets[0].ActivateFromKey(Keys.Space);
            Assert.Equal(initialSnapValue, TransformGizmoSnapSettingsService.GetSnapValue(EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap1));
        }

        /// <summary>
        /// Ensures the viewport declares dedicated state used by the settings toolbar button.
        /// </summary>
        [Fact]
        public void EditorViewport_DefinesDedicatedSettingsToolbarMembers() {
            FieldInfo focusTargetField = typeof(EditorViewport).GetField("SettingsButtonFocusTarget", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo backgroundField = typeof(EditorViewport).GetField("SettingsButtonBackground", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo interactableField = typeof(EditorViewport).GetField("SettingsButtonInteractable", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(focusTargetField);
            Assert.NotNull(backgroundField);
            Assert.NotNull(interactableField);
        }

        /// <summary>
        /// Ensures the settings button is positioned near the right edge of the toolbar instead of inline with the left tool cluster.
        /// </summary>
        [Fact]
        public void LayoutToolbar_WhenViewportIsSized_RightAlignsSettingsButton() {
            InitializeCore();
            EditorViewport viewport = CreateViewport();
            viewport.Size = new int2(640, 360);

            InvokePrivateMethod(viewport, "UpdateViewport");

            EditorEntity settingsButtonRoot = GetPrivateField<EditorEntity>(viewport, "SettingsButtonRoot");
            Assert.True(settingsButtonRoot.Position.X > 560f, "Expected the settings button to sit near the right edge of a 640px toolbar.");
        }

        /// <summary>
        /// Ensures clicking the viewport content activates the viewport dock and focuses the content shortcut target.
        /// </summary>
        [Fact]
        public void EditorViewport_WhenMouseHitsContent_TheViewportDockBecomesActiveAndContentTargetFocused() {
            InitializeCore();
            EditorViewport viewport = CreateViewport();
            EditorKeyboardFocusService.RegisterGroup(viewport);
            EditorKeyboardFocusService.SetDockOrder(new[] { viewport });

            EditorKeyboardFocusService.HandlePointerPressed(new int2(60, 90), false);

            RoundedRectComponent panelOutline = GetPrivateField<RoundedRectComponent>(viewport, "panelOutline");
            bool isViewportContentFocused = GetPrivateField<bool>(viewport, "IsViewportContentFocused");

            Assert.Equal(ThemeManager.Colors.AccentPrimary, panelOutline.BorderColor);
            Assert.True(isViewportContentFocused);
        }

        /// <summary>
        /// Initializes the core services required by viewport keyboard-focus tests.
        /// </summary>
        /// <returns>Configurable input system used by the test.</returns>
        TestInputBackend InitializeCore() {
            TestInputBackend inputManager = new TestInputBackend();
            Core core = new Core();
            core.Initialize(TestDirectX11RenderManager3D.Create(), new TestRenderManager2D(), inputManager, new PlatformInfo("test", "test-version"));
            EditorKeyboardFocusService.Reset();
            TransformGizmoSnapSettingsService.ResetDefaults();
            return inputManager;
        }

        /// <summary>
        /// Creates one viewport instance with a live camera component and deterministic toolbar assets.
        /// </summary>
        /// <returns>Configured editor viewport.</returns>
        EditorViewport CreateViewport() {
            EditorEntity cameraEntity = new EditorEntity();
            CameraComponent camera = new CameraComponent();
            cameraEntity.AddComponent(camera);

            EditorViewport viewport = new EditorViewport(
                camera,
                CreateFont(),
                CreateFont(),
                CreateToolbarIcons());
            viewport.Position = new float3(20f, 20f, 0f);
            viewport.Size = new int2(400, 280);
            return viewport;
        }

        /// <summary>
        /// Creates one mouse state with the supplied right-button state.
        /// </summary>
        /// <param name="rightButtonState">State applied to the right mouse button.</param>
        /// <returns>Mouse state used by the viewport shortcut gate tests.</returns>
        MouseState CreateMouseState(ButtonState rightButtonState) {
            return new MouseState(
                0,
                0,
                0,
                ButtonState.Released,
                ButtonState.Released,
                rightButtonState,
                ButtonState.Released,
                ButtonState.Released);
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
        /// Invokes one non-public instance method with no arguments.
        /// </summary>
        /// <param name="target">Object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        void InvokePrivateMethod(object target, string methodName) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) {
                throw new InvalidOperationException("Expected private method was not found.");
            }

            method.Invoke(target, Array.Empty<object>());
        }

        /// <summary>
        /// Creates a deterministic font asset for viewport toolbar labels.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['C'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['H'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['I'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['L'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['R'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['T'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['W'] = new FontChar(new float4(0f, 0f, 12f, 12f), 0f, 12f, 0f, 0f),
                ['x'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['z'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['+'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['-'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['f'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f)
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
        /// Creates a minimal toolbar icon set for viewport button construction.
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

