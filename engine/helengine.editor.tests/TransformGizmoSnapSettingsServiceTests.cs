using helengine.editor;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies per-tool transform-gizmo snap configuration and modifier-slot selection.
    /// </summary>
    public class TransformGizmoSnapSettingsServiceTests {
        readonly helengine.editor.EditorSessionInteractionServices InteractionServices = new helengine.editor.EditorSessionInteractionServices();
        /// <summary>
        /// Restores default snap values before each test.
        /// </summary>
        public TransformGizmoSnapSettingsServiceTests() {
            InteractionServices.TransformSnap.ResetDefaults();
        }

        /// <summary>
        /// Ensures each tool mode starts with its intended default snap values.
        /// </summary>
        [Fact]
        public void GetSnapValue_ReturnsPerToolDefaults() {
            Assert.Equal(0.25, InteractionServices.TransformSnap.GetSnapValue(EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap1));
            Assert.Equal(1.0, InteractionServices.TransformSnap.GetSnapValue(EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap2));
            Assert.Equal(5.0, InteractionServices.TransformSnap.GetSnapValue(EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap1));
            Assert.Equal(15.0, InteractionServices.TransformSnap.GetSnapValue(EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap2));
            Assert.Equal(0.1, InteractionServices.TransformSnap.GetSnapValue(EditorViewportToolMode.Scale, TransformGizmoSnapSlot.Snap1));
            Assert.Equal(0.25, InteractionServices.TransformSnap.GetSnapValue(EditorViewportToolMode.Scale, TransformGizmoSnapSlot.Snap2));
        }

        /// <summary>
        /// Ensures increasing and decreasing a snap slot affects only the requested tool-mode slot.
        /// </summary>
        [Fact]
        public void AdjustSnapValue_ChangesOnlyRequestedToolSlot() {
            InteractionServices.TransformSnap.IncreaseSnapValue(EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap1);
            InteractionServices.TransformSnap.DecreaseSnapValue(EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap2);

            Assert.Equal(0.5, InteractionServices.TransformSnap.GetSnapValue(EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap1));
            Assert.Equal(1.0, InteractionServices.TransformSnap.GetSnapValue(EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap2));
            Assert.Equal(5.0, InteractionServices.TransformSnap.GetSnapValue(EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap1));
            Assert.Equal(7.5, InteractionServices.TransformSnap.GetSnapValue(EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap2));
        }

        /// <summary>
        /// Ensures snap-slot resolution prefers shift when both snap modifiers are held.
        /// </summary>
        [Fact]
        public void ResolveActiveSnapSlot_PrefersShiftOverControl() {
            TransformGizmoSnapSlot noSlot = InteractionServices.TransformSnap.ResolveActiveSnapSlot(false, false);
            TransformGizmoSnapSlot snap1Slot = InteractionServices.TransformSnap.ResolveActiveSnapSlot(true, false);
            TransformGizmoSnapSlot snap2Slot = InteractionServices.TransformSnap.ResolveActiveSnapSlot(false, true);
            TransformGizmoSnapSlot preferredSlot = InteractionServices.TransformSnap.ResolveActiveSnapSlot(true, true);

            Assert.Equal(TransformGizmoSnapSlot.None, noSlot);
            Assert.Equal(TransformGizmoSnapSlot.Snap1, snap1Slot);
            Assert.Equal(TransformGizmoSnapSlot.Snap2, snap2Slot);
            Assert.Equal(TransformGizmoSnapSlot.Snap2, preferredSlot);
        }

        /// <summary>
        /// Ensures the active snap value follows the modifier-selected slot for the current tool mode.
        /// </summary>
        [Fact]
        public void GetActiveSnapValue_ReturnsCurrentToolModeSlotValue() {
            double inactiveValue = InteractionServices.TransformSnap.GetActiveSnapValue(EditorViewportToolMode.Translate, false, false);
            double translateControlValue = InteractionServices.TransformSnap.GetActiveSnapValue(EditorViewportToolMode.Translate, true, false);
            double translateShiftValue = InteractionServices.TransformSnap.GetActiveSnapValue(EditorViewportToolMode.Translate, false, true);
            double rotateControlValue = InteractionServices.TransformSnap.GetActiveSnapValue(EditorViewportToolMode.Rotate, true, false);
            double rotateShiftValue = InteractionServices.TransformSnap.GetActiveSnapValue(EditorViewportToolMode.Rotate, false, true);
            double scaleControlValue = InteractionServices.TransformSnap.GetActiveSnapValue(EditorViewportToolMode.Scale, true, false);
            double scaleShiftValue = InteractionServices.TransformSnap.GetActiveSnapValue(EditorViewportToolMode.Scale, false, true);

            Assert.Equal(0.0, inactiveValue);
            Assert.Equal(0.25, translateControlValue);
            Assert.Equal(1.0, translateShiftValue);
            Assert.Equal(5.0, rotateControlValue);
            Assert.Equal(15.0, rotateShiftValue);
            Assert.Equal(0.1, scaleControlValue);
            Assert.Equal(0.25, scaleShiftValue);
        }
    }
}
