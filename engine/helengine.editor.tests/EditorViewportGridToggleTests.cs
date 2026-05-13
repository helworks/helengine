using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies viewport-local grid toggle behavior through the viewport settings overlay.
    /// </summary>
    public class EditorViewportGridToggleTests {
        /// <summary>
        /// Ensures the overlay grid toggle target adds and removes only the scene-grid layer.
        /// </summary>
        [Fact]
        public void ActivateOverlayGridToggle_WhenPressed_TogglesViewportGridVisibility() {
            InitializeCore();
            EditorViewport viewport = CreateViewportForGridTesting((ushort)(EditorLayerMasks.SceneObjects | EditorLayerMasks.SceneGizmo));
            EditorViewportSettingsOverlayComponent overlayComponent = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");
            EditorFocusTarget settingsTarget = GetPrivateField<EditorFocusTarget>(viewport, "SettingsButtonFocusTarget");

            settingsTarget.ActivateFromKey(Keys.Enter);
            overlayComponent.GridToggleFocusTarget.ActivateFromKey(Keys.Space);

            Assert.Equal(
                (ushort)(EditorLayerMasks.SceneObjects | EditorLayerMasks.SceneGizmo | EditorLayerMasks.SceneGrid),
                viewport.Camera.LayerMask);

            overlayComponent.GridToggleFocusTarget.ActivateFromKey(Keys.Enter);

            Assert.Equal((ushort)(EditorLayerMasks.SceneObjects | EditorLayerMasks.SceneGizmo), viewport.Camera.LayerMask);
        }

        /// <summary>
        /// Ensures the explicit visibility setter preserves unrelated camera layers while adding and removing the grid layer.
        /// </summary>
        [Fact]
        public void SetGridVisible_WhenCalled_PreservesNonGridViewportLayers() {
            InitializeCore();
            EditorViewport viewport = CreateViewportForGridTesting((ushort)(EditorLayerMasks.SceneObjects | EditorLayerMasks.SceneGizmo));

            InvokePrivateMethod(viewport, "SetGridVisible", true);
            Assert.Equal(
                (ushort)(EditorLayerMasks.SceneObjects | EditorLayerMasks.SceneGizmo | EditorLayerMasks.SceneGrid),
                viewport.Camera.LayerMask);

            InvokePrivateMethod(viewport, "SetGridVisible", false);
            Assert.Equal((ushort)(EditorLayerMasks.SceneObjects | EditorLayerMasks.SceneGizmo), viewport.Camera.LayerMask);
        }

        /// <summary>
        /// Initializes the minimal core services required by viewport overlay tests.
        /// </summary>
        void InitializeCore() {
            TestInputBackend inputManager = new TestInputBackend();
            Core core = new Core();
            core.Initialize(TestDirectX11RenderManager3D.Create(), new TestRenderManager2D(), inputManager, new PlatformInfo("test", "test-version"));
            EditorKeyboardFocusService.Reset();
            TransformGizmoSnapSettingsService.ResetDefaults();
        }

        /// <summary>
        /// Creates one viewport instance with deterministic toolbar assets and the requested camera layer mask.
        /// </summary>
        /// <param name="initialLayerMask">Initial camera layer mask assigned to the viewport.</param>
        /// <returns>Viewport prepared for isolated grid-toggle testing.</returns>
        EditorViewport CreateViewportForGridTesting(ushort initialLayerMask) {
            EditorEntity cameraEntity = new EditorEntity();
            CameraComponent camera = new CameraComponent();
            camera.LayerMask = initialLayerMask;
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
        /// Creates a deterministic font asset for overlay tests.
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
        /// Creates a minimal toolbar icon set for overlay tests.
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

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) {
                throw new InvalidOperationException("Expected private field was not found.");
            }

            object value = field.GetValue(target);
            return Assert.IsType<T>(value);
        }

        /// <summary>
        /// Invokes one non-public instance method with the provided argument list.
        /// </summary>
        /// <param name="target">Object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <param name="arguments">Arguments passed to the invoked method.</param>
        void InvokePrivateMethod(object target, string methodName, params object[] arguments) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) {
                throw new InvalidOperationException("Expected private method was not found.");
            }

            method.Invoke(target, arguments);
        }
    }
}
