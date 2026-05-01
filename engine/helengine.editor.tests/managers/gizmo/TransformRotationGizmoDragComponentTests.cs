using System.Reflection;
using helengine;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies rotation gizmo drag snapping uses the fixed snap grid instead of preserving initial angle offsets.
    /// </summary>
    public class TransformRotationGizmoDragComponentTests : IDisposable {
        /// <summary>
        /// Converts degrees into radians for test inputs and expectations.
        /// </summary>
        const double DegreesToRadians = Math.PI / 180.0;
        /// <summary>
        /// Tolerance used when comparing floating-point snapped angles.
        /// </summary>
        const double AngleTolerance = 0.000000001;

        /// <summary>
        /// Restores the shared snap configuration before and after each test.
        /// </summary>
        public TransformRotationGizmoDragComponentTests() {
            TransformGizmoSnapSettingsService.ResetDefaults();
        }

        /// <summary>
        /// Restores the shared snap configuration after each test.
        /// </summary>
        public void Dispose() {
            TransformGizmoSnapSettingsService.ResetDefaults();
        }

        /// <summary>
        /// Ensures a starting twist offset still snaps the final rotation to a fixed absolute grid.
        /// </summary>
        [Fact]
        public void ResolveActiveRotationAngle_WhenStartAngleHasOffset_SnapsTheFinalAngleToTheFixedGrid() {
            TransformGizmoSnapSettingsService.ResetDefaults();
            TransformGizmoSnapSettingsService.DecreaseSnapValue(EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap1);

            TestInputManager input = new TestInputManager();
            input.SetKeyboardState(new KeyboardState(Keys.LeftControl));

            CameraComponent sceneCamera = new CameraComponent();
            TransformRotationGizmoDragComponent component = new TransformRotationGizmoDragComponent(sceneCamera);
            double startAngleRadians = 0.25 * DegreesToRadians;
            double accumulatedAngleRadians = 2.75 * DegreesToRadians;
            SetPrivateField(component, "DragStartRotationAngle", startAngleRadians);
            SetPrivateField(component, "DragAccumulatedAngle", accumulatedAngleRadians);

            double resolvedDeltaRadians = (double)InvokePrivate(component, "ResolveActiveRotationAngle", input);

            Assert.Equal(2.25 * DegreesToRadians, resolvedDeltaRadians, AngleTolerance);
            Assert.Equal(2.5 * DegreesToRadians, resolvedDeltaRadians + startAngleRadians, AngleTolerance);
        }

        /// <summary>
        /// Sets one private field value through reflection for focused drag-state seeding.
        /// </summary>
        /// <param name="target">Object whose private field should be updated.</param>
        /// <param name="fieldName">Private field name to update.</param>
        /// <param name="value">Value assigned to the private field.</param>
        static void SetPrivateField(object target, string fieldName, object value) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(target, value);
        }

        /// <summary>
        /// Invokes one private method through reflection and returns its result.
        /// </summary>
        /// <param name="target">Object whose private method should be invoked.</param>
        /// <param name="methodName">Private method name to invoke.</param>
        /// <param name="arguments">Arguments supplied to the method.</param>
        /// <returns>Return value from the private method invocation.</returns>
        static object InvokePrivate(object target, string methodName, params object[] arguments) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            return method.Invoke(target, arguments);
        }
    }
}
