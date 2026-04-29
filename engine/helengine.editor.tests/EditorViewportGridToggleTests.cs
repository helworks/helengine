using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies viewport-local grid toggle behavior without requiring a live rendering backend.
    /// </summary>
    public class EditorViewportGridToggleTests {
        /// <summary>
        /// Ensures the dedicated grid toolbar target toggles only the scene-grid layer and keeps button visuals in sync.
        /// </summary>
        [Fact]
        public void HandleGridButtonCursor_WhenHoveredPressedAndReleased_TogglesViewportGridVisibility() {
            EditorViewport viewport = CreateViewportForGridTesting((ushort)(EditorLayerMasks.SceneObjects | EditorLayerMasks.SceneGizmo));
            SpriteComponent background = GetPrivateField<SpriteComponent>(viewport, "GridButtonBackground");
            SpriteComponent icon = GetPrivateField<SpriteComponent>(viewport, "GridButtonIcon");

            InvokePrivateMethod(viewport, "HandleGridButtonCursor", PointerInteraction.Hover);
            InvokePrivateMethod(viewport, "HandleGridButtonCursor", PointerInteraction.Press);
            InvokePrivateMethod(viewport, "HandleGridButtonCursor", PointerInteraction.Release);

            Assert.Equal(
                (ushort)(EditorLayerMasks.SceneObjects | EditorLayerMasks.SceneGizmo | EditorLayerMasks.SceneGrid),
                viewport.Camera.LayerMask);
            Assert.Equal(ThemeManager.Colors.AccentPrimary, background.Color);
            Assert.Equal(new byte4(255, 255, 255, 255), icon.Color);

            InvokePrivateMethod(viewport, "HandleGridButtonCursor", PointerInteraction.Press);
            InvokePrivateMethod(viewport, "HandleGridButtonCursor", PointerInteraction.Release);

            Assert.Equal((ushort)(EditorLayerMasks.SceneObjects | EditorLayerMasks.SceneGizmo), viewport.Camera.LayerMask);
            Assert.Equal(ThemeManager.Colors.AccentSecondary, background.Color);
            Assert.Equal(new byte4(255, 255, 255, 255), icon.Color);

            InvokePrivateMethod(viewport, "HandleGridButtonCursor", PointerInteraction.Leave);

            Assert.Equal(ThemeManager.Colors.SurfaceInput, background.Color);
            Assert.Equal(new byte4(255, 255, 255, 224), icon.Color);
        }

        /// <summary>
        /// Ensures the explicit visibility setter preserves unrelated camera layers while adding and removing the grid layer.
        /// </summary>
        [Fact]
        public void SetGridVisible_WhenCalled_PreservesNonGridViewportLayers() {
            EditorViewport viewport = CreateViewportForGridTesting((ushort)(EditorLayerMasks.SceneObjects | EditorLayerMasks.SceneGizmo));

            InvokePrivateMethod(viewport, "SetGridVisible", true);
            Assert.Equal(
                (ushort)(EditorLayerMasks.SceneObjects | EditorLayerMasks.SceneGizmo | EditorLayerMasks.SceneGrid),
                viewport.Camera.LayerMask);

            InvokePrivateMethod(viewport, "SetGridVisible", false);
            Assert.Equal((ushort)(EditorLayerMasks.SceneObjects | EditorLayerMasks.SceneGizmo), viewport.Camera.LayerMask);
        }

        /// <summary>
        /// Creates one partially initialized viewport containing only the state required by grid-toggle methods.
        /// </summary>
        /// <param name="initialLayerMask">Initial camera layer mask assigned to the viewport.</param>
        /// <returns>Viewport prepared for isolated grid-toggle testing.</returns>
        EditorViewport CreateViewportForGridTesting(ushort initialLayerMask) {
            EditorViewport viewport = (EditorViewport)RuntimeHelpers.GetUninitializedObject(typeof(EditorViewport));
            CameraComponent camera = (CameraComponent)RuntimeHelpers.GetUninitializedObject(typeof(CameraComponent));
            camera.LayerMask = initialLayerMask;

            SetPrivateField(viewport, "<Camera>k__BackingField", camera);
            SetPrivateField(viewport, "GridButtonBackground", new SpriteComponent());
            SetPrivateField(viewport, "GridButtonIcon", new SpriteComponent());
            SetPrivateField(viewport, "GridButtonHoverState", false);
            SetPrivateField(viewport, "GridButtonPressedState", false);
            SetPrivateField(viewport, "GridButtonKeyboardFocusState", false);

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
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) {
                throw new InvalidOperationException("Expected private field was not found.");
            }

            object value = field.GetValue(target);
            return Assert.IsType<T>(value);
        }

        /// <summary>
        /// Assigns one non-public instance field to a provided value.
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
