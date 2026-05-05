using System.Reflection;
using helengine;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies viewport settings overlay lifetime and focus behavior.
    /// </summary>
    public class EditorViewportSettingsOverlayTests : IDisposable {
        /// <summary>
        /// Resets shared keyboard-focus state after each overlay test.
        /// </summary>
        public void Dispose() {
            EditorKeyboardFocusService.Reset();
            TransformGizmoSnapSettingsService.ResetDefaults();
        }

        /// <summary>
        /// Ensures keyboard activation of the settings button opens the overlay and focuses the first overlay control.
        /// </summary>
        [Fact]
        public void ActivateSettingsButton_WhenOverlayIsClosed_OpensOverlayAndFocusesGridToggle() {
            InitializeCore();
            EditorViewport viewport = CreateViewport();
            EditorFocusTarget settingsTarget = GetPrivateField<EditorFocusTarget>(viewport, "SettingsButtonFocusTarget");
            EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");

            settingsTarget.ActivateFromKey(Keys.Enter);

            Assert.True(overlayComponent.IsOpen);
            Assert.Same(
                overlayComponent.GridToggleFocusTarget,
                GetPrivateStaticField<IFocusTarget>(typeof(EditorKeyboardFocusService), "FocusedTarget"));
        }

        /// <summary>
        /// Ensures tab traversal moves through the overlay controls in the expected order.
        /// </summary>
        [Fact]
        public void HandleTab_WhenOverlayIsOpen_TraversesGridNearFarAndCloseControlsInOrder() {
            InitializeCore();
            EditorViewport viewport = CreateViewport();
            EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");
            EditorFocusTarget settingsTarget = GetPrivateField<EditorFocusTarget>(viewport, "SettingsButtonFocusTarget");

            settingsTarget.ActivateFromKey(Keys.Enter);
            EditorKeyboardFocusService.HandleTab(true);
            Assert.Same(
                overlayComponent.CanvasWidthFocusTarget,
                GetPrivateStaticField<IFocusTarget>(typeof(EditorKeyboardFocusService), "FocusedTarget"));

            EditorKeyboardFocusService.HandleTab(true);
            Assert.Same(
                overlayComponent.CanvasHeightFocusTarget,
                GetPrivateStaticField<IFocusTarget>(typeof(EditorKeyboardFocusService), "FocusedTarget"));

            EditorKeyboardFocusService.HandleTab(true);
            Assert.Same(
                overlayComponent.PixelsPerWorldUnitFocusTarget,
                GetPrivateStaticField<IFocusTarget>(typeof(EditorKeyboardFocusService), "FocusedTarget"));

            EditorKeyboardFocusService.HandleTab(true);
            Assert.Same(
                overlayComponent.NearPlaneFocusTarget,
                GetPrivateStaticField<IFocusTarget>(typeof(EditorKeyboardFocusService), "FocusedTarget"));

            EditorKeyboardFocusService.HandleTab(true);
            Assert.Same(
                overlayComponent.FarPlaneFocusTarget,
                GetPrivateStaticField<IFocusTarget>(typeof(EditorKeyboardFocusService), "FocusedTarget"));

            EditorKeyboardFocusService.HandleTab(true);
            Assert.Same(
                overlayComponent.CloseButtonFocusTarget,
                GetPrivateStaticField<IFocusTarget>(typeof(EditorKeyboardFocusService), "FocusedTarget"));
        }

        /// <summary>
        /// Ensures each viewport starts with the default canvas preview settings used by the world-space 2D plane.
        /// </summary>
        [Fact]
        public void CreateViewport_WhenConstructed_UsesDefaultCanvasPreviewSettings() {
            InitializeCore();
            EditorViewport viewport = CreateViewport();

            Assert.Equal(1280, viewport.CanvasPreviewSettings.CanvasWidth);
            Assert.Equal(720, viewport.CanvasPreviewSettings.CanvasHeight);
            Assert.Equal(100, viewport.CanvasPreviewSettings.PixelsPerWorldUnit);
        }

        /// <summary>
        /// Ensures the settings overlay opens directly below the settings button instead of leaving a vertical gap.
        /// </summary>
        [Fact]
        public void LayoutToolbar_WhenViewportIsSized_AnchorsSettingsOverlayDirectlyBelowSettingsButton() {
            InitializeCore();
            EditorViewport viewport = CreateViewport();
            viewport.Size = new int2(640, 360);

            InvokePrivateMethod(viewport, "UpdateViewport");

            EditorEntity toolbarRoot = GetPrivateField<EditorEntity>(viewport, "ToolbarRoot");
            EditorEntity settingsButtonRoot = GetPrivateField<EditorEntity>(viewport, "SettingsButtonRoot");
            InteractableComponent settingsButtonInteractable = GetPrivateField<InteractableComponent>(viewport, "SettingsButtonInteractable");
            EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");
            EditorEntity overlayRoot = GetPrivateField<EditorEntity>(overlayComponent, "OverlayRoot");

            float expectedOverlayY = toolbarRoot.LocalPosition.Y + settingsButtonRoot.LocalPosition.Y + settingsButtonInteractable.Size.Y;
            Assert.Equal(expectedOverlayY, overlayRoot.LocalPosition.Y, 3);
        }

        /// <summary>
        /// Ensures the grid row uses the shared checkbox control instead of a custom button implementation.
        /// </summary>
        [Fact]
        public void GridToggleRow_WhenOverlayIsCreated_UsesCheckBoxComponent() {
            InitializeCore();
            EditorViewport viewport = CreateViewport();
            EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");

            _ = GetPrivateField<CheckBoxComponent>(overlayComponent, "GridToggleCheckBox");
        }

        /// <summary>
        /// Ensures the overlay background itself owns a full-panel interactable hit area.
        /// </summary>
        [Fact]
        public void OverlayBackground_WhenOverlayIsCreated_UsesFullPanelInteractable() {
            InitializeCore();
            EditorViewport viewport = CreateViewport();
            EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");

            RoundedRectComponent overlayBackground = GetPrivateField<RoundedRectComponent>(overlayComponent, "OverlayBackground");
            InteractableComponent overlayBackgroundInteractable = GetPrivateField<InteractableComponent>(overlayComponent, "OverlayBackgroundInteractable");

            Assert.Equal(overlayBackground.Size.X, overlayBackgroundInteractable.Size.X);
            Assert.Equal(overlayBackground.Size.Y, overlayBackgroundInteractable.Size.Y);
        }

        /// <summary>
        /// Ensures clicking the empty overlay background does not close the settings panel.
        /// </summary>
        [Fact]
        public void HandleOutsidePointerPressed_WhenClickFallsInsideBackgroundKeepsOverlayOpen() {
            InitializeCore();
            EditorViewport viewport = CreateViewport();
            EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");
            EditorFocusTarget settingsTarget = GetPrivateField<EditorFocusTarget>(viewport, "SettingsButtonFocusTarget");
            EditorEntity overlayRoot = GetPrivateField<EditorEntity>(overlayComponent, "OverlayRoot");

            settingsTarget.ActivateFromKey(Keys.Enter);
            int2 insideBackgroundPoint = new int2(
                (int)Math.Round(overlayRoot.Position.X + 4f),
                (int)Math.Round(overlayRoot.Position.Y + 4f));
            overlayComponent.HandleOutsidePointerPressed(insideBackgroundPoint, settingsTarget);

            Assert.True(overlayComponent.IsOpen);
        }

        /// <summary>
        /// Ensures the live overlay update path keeps the panel open when the pointer press lands inside the grid row.
        /// </summary>
        [Fact]
        public void Update_WhenPointerPressesInsideGridRow_KeepsOverlayOpen() {
            TestInputBackend inputManager = InitializeCore();
            EditorViewport viewport = CreateViewport();
            EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");
            EditorFocusTarget settingsTarget = GetPrivateField<EditorFocusTarget>(viewport, "SettingsButtonFocusTarget");
            EditorEntity overlayRoot = GetPrivateField<EditorEntity>(overlayComponent, "OverlayRoot");

            settingsTarget.ActivateFromKey(Keys.Enter);

            int pointerX = (int)Math.Round(overlayRoot.Position.X + 16f);
            int pointerY = (int)Math.Round(overlayRoot.Position.Y + 16f);
            AdvanceInputFrame(inputManager, CreateMouseState(pointerX, pointerY, ButtonState.Released));
            overlayComponent.Update();
            AdvanceInputFrame(inputManager, CreateMouseState(pointerX, pointerY, ButtonState.Pressed));
            overlayComponent.Update();

            Assert.True(overlayComponent.IsOpen);
        }

        /// <summary>
        /// Ensures clicking outside the overlay closes it and returns focus to the settings button.
        /// </summary>
        [Fact]
        public void Update_WhenPointerClicksOutsideOverlay_ClosesOverlayAndRestoresSettingsButtonFocus() {
            InitializeCore();
            EditorViewport viewport = CreateViewport();
            EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");
            EditorFocusTarget settingsTarget = GetPrivateField<EditorFocusTarget>(viewport, "SettingsButtonFocusTarget");

            settingsTarget.ActivateFromKey(Keys.Enter);
            overlayComponent.HandleOutsidePointerPressed(new int2(4, 4), settingsTarget);

            Assert.False(overlayComponent.IsOpen);
            Assert.Same(
                settingsTarget,
                GetPrivateStaticField<IFocusTarget>(typeof(EditorKeyboardFocusService), "FocusedTarget"));
        }

        /// <summary>
        /// Ensures the overlay also closes from the keyboard escape path.
        /// </summary>
        [Fact]
        public void HandleEscape_WhenOverlayIsOpen_ClosesOverlayAndRestoresSettingsButtonFocus() {
            InitializeCore();
            EditorViewport viewport = CreateViewport();
            EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");
            EditorFocusTarget settingsTarget = GetPrivateField<EditorFocusTarget>(viewport, "SettingsButtonFocusTarget");

            settingsTarget.ActivateFromKey(Keys.Enter);
            overlayComponent.HandleEscapeKey(settingsTarget);

            Assert.False(overlayComponent.IsOpen);
            Assert.Same(
                settingsTarget,
                GetPrivateStaticField<IFocusTarget>(typeof(EditorKeyboardFocusService), "FocusedTarget"));
        }

        /// <summary>
        /// Ensures keyboard activation of the close button closes the overlay without changing viewport ownership.
        /// </summary>
        [Fact]
        public void ActivateCloseButton_WhenOverlayIsOpen_ClosesOverlay() {
            InitializeCore();
            EditorViewport viewport = CreateViewport();
            EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");
            EditorFocusTarget settingsTarget = GetPrivateField<EditorFocusTarget>(viewport, "SettingsButtonFocusTarget");

            settingsTarget.ActivateFromKey(Keys.Enter);

            overlayComponent.CloseButtonFocusTarget.ActivateFromKey(Keys.Enter);

            Assert.False(overlayComponent.IsOpen);
        }

        /// <summary>
        /// Ensures near-plane slider changes update the viewport camera immediately.
        /// </summary>
        [Fact]
        public void SetNearPlaneSliderValue_WhenChanged_UpdatesCameraImmediately() {
            InitializeCore();
            EditorViewport viewport = CreateViewport();
            EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");

            overlayComponent.Open();
            overlayComponent.NearPlaneSlider.SetValue(0.75);

            Assert.Equal(0.75f, viewport.Camera.NearPlaneDistance, 3);
        }

        /// <summary>
        /// Ensures far-plane slider changes update the viewport camera immediately.
        /// </summary>
        [Fact]
        public void SetFarPlaneSliderValue_WhenChanged_UpdatesCameraImmediately() {
            InitializeCore();
            EditorViewport viewport = CreateViewport();
            EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");

            overlayComponent.Open();
            overlayComponent.FarPlaneSlider.SetValue(750.0);

            Assert.Equal(750f, viewport.Camera.FarPlaneDistance, 3);
        }

        /// <summary>
        /// Ensures canvas preview sliders update viewport-local preview settings immediately.
        /// </summary>
        [Fact]
        public void SetCanvasPreviewSliderValues_WhenChanged_UpdatesViewportSettingsImmediately() {
            InitializeCore();
            EditorViewport viewport = CreateViewport();
            EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");

            overlayComponent.Open();
            overlayComponent.CanvasWidthSlider.SetValue(1920);
            overlayComponent.CanvasHeightSlider.SetValue(1080);
            overlayComponent.PixelsPerWorldUnitSlider.SetValue(200);

            Assert.Equal(1920, viewport.CanvasPreviewSettings.CanvasWidth);
            Assert.Equal(1080, viewport.CanvasPreviewSettings.CanvasHeight);
            Assert.Equal(200, viewport.CanvasPreviewSettings.PixelsPerWorldUnit);
        }

        /// <summary>
        /// Ensures near-plane edits clamp against the current far plane instead of allowing invalid projection state.
        /// </summary>
        [Fact]
        public void SetNearPlaneSliderValue_WhenItCrossesFarPlane_ClampsToMinimumSeparation() {
            InitializeCore();
            EditorViewport viewport = CreateViewport();
            viewport.Camera.FarPlaneDistance = 1.5f;
            EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");

            overlayComponent.Open();
            overlayComponent.NearPlaneSlider.SetValue(5.0);

            Assert.Equal(1.49f, viewport.Camera.NearPlaneDistance, 2);
        }

        /// <summary>
        /// Ensures far-plane edits clamp above the current near plane instead of allowing invalid projection state.
        /// </summary>
        [Fact]
        public void SetFarPlaneSliderValue_WhenItCrossesNearPlane_ClampsToMinimumSeparation() {
            InitializeCore();
            EditorViewport viewport = CreateViewport();
            viewport.Camera.NearPlaneDistance = 2.0f;
            EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");

            overlayComponent.Open();
            overlayComponent.FarPlaneSlider.SetValue(1.0);

            Assert.Equal(2.01f, viewport.Camera.FarPlaneDistance, 2);
        }

        /// <summary>
        /// Initializes the core services required by overlay tests.
        /// </summary>
        TestInputBackend InitializeCore() {
            TestInputBackend inputManager = new TestInputBackend();
            Core core = new Core();
            core.Initialize(TestDirectX11RenderManager3D.Create(), new TestRenderManager2D(), inputManager);
            EditorKeyboardFocusService.Reset();
            TransformGizmoSnapSettingsService.ResetDefaults();
            return inputManager;
        }

        /// <summary>
        /// Creates one viewport instance with deterministic toolbar assets.
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
        /// Invokes one non-public instance method on the supplied target.
        /// </summary>
        /// <param name="target">Object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        void InvokePrivateMethod(object target, string methodName) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) {
                throw new InvalidOperationException("Expected private method was not found.");
            }

            method.Invoke(target, null);
        }

        /// <summary>
        /// Advances one input frame using the supplied mouse state.
        /// </summary>
        /// <param name="inputManager">Input backend receiving the frame.</param>
        /// <param name="mouseState">Mouse state to expose for the frame.</param>
        void AdvanceInputFrame(TestInputBackend inputManager, MouseState mouseState) {
            inputManager.SetMouseState(mouseState);
            inputManager.EarlyUpdate();
            inputManager.Update();
        }

        /// <summary>
        /// Reads one non-public static field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="type">Type that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateStaticField<T>(Type type, string fieldName) {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null) {
                throw new InvalidOperationException("Expected private static field was not found.");
            }

            object value = field.GetValue(null);
            return Assert.IsAssignableFrom<T>(value);
        }

        /// <summary>
        /// Creates one mouse state with the supplied left-button state.
        /// </summary>
        /// <param name="x">Pointer X coordinate in window pixels.</param>
        /// <param name="y">Pointer Y coordinate in window pixels.</param>
        /// <param name="leftButtonState">State applied to the left mouse button.</param>
        /// <returns>Mouse state used by overlay pointer tests.</returns>
        MouseState CreateMouseState(int x, int y, ButtonState leftButtonState) {
            return new MouseState(
                x,
                y,
                0,
                leftButtonState,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released);
        }

        /// <summary>
        /// Creates a deterministic font asset for overlay labels and buttons.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['C'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['G'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['N'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['R'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['+'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['-'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['.'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['0'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['1'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['2'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['3'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['4'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['5'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['6'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['7'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['8'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['9'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['x'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['z'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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
        /// Creates a minimal toolbar icon set for viewport construction.
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
