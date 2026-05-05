using System.Reflection;
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
        void InitializeCore() {
            TestInputBackend inputManager = new TestInputBackend();
            Core core = new Core();
            core.Initialize(TestDirectX11RenderManager3D.Create(), new TestRenderManager2D(), inputManager);
            EditorKeyboardFocusService.Reset();
            TransformGizmoSnapSettingsService.ResetDefaults();
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
