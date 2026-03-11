using helengine.editor;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies per-tool transform-gizmo snap configuration and modifier-slot selection.
    /// </summary>
    public class TransformGizmoSnapSettingsServiceTests {
        /// <summary>
        /// Restores default snap values before each test.
        /// </summary>
        public TransformGizmoSnapSettingsServiceTests() {
            TransformGizmoSnapSettingsService.ResetDefaults();
        }

        /// <summary>
        /// Ensures each tool mode starts with its intended default snap values.
        /// </summary>
        [Fact]
        public void GetSnapValue_ReturnsPerToolDefaults() {
            Assert.Equal(0.25, TransformGizmoSnapSettingsService.GetSnapValue(EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap1));
            Assert.Equal(1.0, TransformGizmoSnapSettingsService.GetSnapValue(EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap2));
            Assert.Equal(5.0, TransformGizmoSnapSettingsService.GetSnapValue(EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap1));
            Assert.Equal(15.0, TransformGizmoSnapSettingsService.GetSnapValue(EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap2));
            Assert.Equal(0.1, TransformGizmoSnapSettingsService.GetSnapValue(EditorViewportToolMode.Scale, TransformGizmoSnapSlot.Snap1));
            Assert.Equal(0.25, TransformGizmoSnapSettingsService.GetSnapValue(EditorViewportToolMode.Scale, TransformGizmoSnapSlot.Snap2));
        }

        /// <summary>
        /// Ensures increasing and decreasing a snap slot affects only the requested tool-mode slot.
        /// </summary>
        [Fact]
        public void AdjustSnapValue_ChangesOnlyRequestedToolSlot() {
            TransformGizmoSnapSettingsService.IncreaseSnapValue(EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap1);
            TransformGizmoSnapSettingsService.DecreaseSnapValue(EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap2);

            Assert.Equal(0.5, TransformGizmoSnapSettingsService.GetSnapValue(EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap1));
            Assert.Equal(1.0, TransformGizmoSnapSettingsService.GetSnapValue(EditorViewportToolMode.Translate, TransformGizmoSnapSlot.Snap2));
            Assert.Equal(5.0, TransformGizmoSnapSettingsService.GetSnapValue(EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap1));
            Assert.Equal(7.5, TransformGizmoSnapSettingsService.GetSnapValue(EditorViewportToolMode.Rotate, TransformGizmoSnapSlot.Snap2));
        }

        /// <summary>
        /// Ensures snap-slot resolution prefers shift when both snap modifiers are held.
        /// </summary>
        [Fact]
        public void ResolveActiveSnapSlot_PrefersShiftOverControl() {
            TransformGizmoSnapSlot noSlot = TransformGizmoSnapSettingsService.ResolveActiveSnapSlot(false, false);
            TransformGizmoSnapSlot snap1Slot = TransformGizmoSnapSettingsService.ResolveActiveSnapSlot(true, false);
            TransformGizmoSnapSlot snap2Slot = TransformGizmoSnapSettingsService.ResolveActiveSnapSlot(false, true);
            TransformGizmoSnapSlot preferredSlot = TransformGizmoSnapSettingsService.ResolveActiveSnapSlot(true, true);

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
            double inactiveValue = TransformGizmoSnapSettingsService.GetActiveSnapValue(EditorViewportToolMode.Translate, false, false);
            double controlValue = TransformGizmoSnapSettingsService.GetActiveSnapValue(EditorViewportToolMode.Translate, true, false);
            double shiftValue = TransformGizmoSnapSettingsService.GetActiveSnapValue(EditorViewportToolMode.Translate, false, true);

            Assert.Equal(0.0, inactiveValue);
            Assert.Equal(0.25, controlValue);
            Assert.Equal(1.0, shiftValue);
        }
    }
}
