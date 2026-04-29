using helengine.editor.windows.tests.testing;
using Xunit;

namespace helengine.editor.windows.tests.utils {
    /// <summary>
    /// Verifies that the borderless-window controller honors Windows arrangement settings before applying custom drag behavior.
    /// </summary>
    public sealed class BorderlessWindowStateControllerTests {
        /// <summary>
        /// Ensures a maximized window stays maximized when the Windows drag-from-maximize behavior is disabled.
        /// </summary>
        [Fact]
        public void PrepareForTitleBarDrag_WhenDragFromMaximizeIsDisabled_KeepsCustomMaximizedBounds() {
            using TestResizeBorderStateForm form = new TestResizeBorderStateForm();
            Rectangle initialBounds = new Rectangle(160, 120, 1200, 800);
            form.Bounds = initialBounds;

            TestWindowArrangementFeatureState featureState = new TestWindowArrangementFeatureState {
                IsWindowArrangingEnabled = true,
                IsDockMovingEnabled = true,
                IsDragFromMaximizeEnabled = false
            };
            BorderlessWindowStateController controller = new BorderlessWindowStateController(featureState);

            controller.ToggleMaximize(form);
            Rectangle maximizedBounds = form.Bounds;
            Point cursorScreenPosition = new Point(maximizedBounds.Left + (maximizedBounds.Width / 2), maximizedBounds.Top + 12);

            controller.PrepareForTitleBarDrag(form, cursorScreenPosition);

            Assert.Equal(maximizedBounds, form.Bounds);
            Assert.False(controller.IsResizeBorderEnabled);
        }

        /// <summary>
        /// Ensures a drag ending at the top edge does not maximize when Windows dock-moving behavior is disabled.
        /// </summary>
        [Fact]
        public void CompleteTitleBarDrag_WhenDockMovingIsDisabled_DoesNotApplyCustomMaximize() {
            using TestResizeBorderStateForm form = new TestResizeBorderStateForm();
            Rectangle initialBounds = form.Bounds;
            Rectangle workingArea = Screen.FromControl(form).WorkingArea;

            TestWindowArrangementFeatureState featureState = new TestWindowArrangementFeatureState {
                IsWindowArrangingEnabled = true,
                IsDockMovingEnabled = false,
                IsDragFromMaximizeEnabled = true
            };
            BorderlessWindowStateController controller = new BorderlessWindowStateController(featureState);

            controller.PrepareForTitleBarDrag(form, new Point(initialBounds.Left + 40, initialBounds.Top + 16));
            controller.CompleteTitleBarDrag(form, new Point(workingArea.Left + (workingArea.Width / 2), workingArea.Top));

            Assert.Equal(initialBounds, form.Bounds);
            Assert.True(controller.IsResizeBorderEnabled);
        }

        /// <summary>
        /// Ensures top-edge maximize is also suppressed when Windows window arrangement is disabled globally.
        /// </summary>
        [Fact]
        public void CompleteTitleBarDrag_WhenWindowArrangingIsDisabled_DoesNotApplyCustomMaximize() {
            using TestResizeBorderStateForm form = new TestResizeBorderStateForm();
            Rectangle initialBounds = form.Bounds;
            Rectangle workingArea = Screen.FromControl(form).WorkingArea;

            TestWindowArrangementFeatureState featureState = new TestWindowArrangementFeatureState {
                IsWindowArrangingEnabled = false,
                IsDockMovingEnabled = true,
                IsDragFromMaximizeEnabled = true
            };
            BorderlessWindowStateController controller = new BorderlessWindowStateController(featureState);

            controller.PrepareForTitleBarDrag(form, new Point(initialBounds.Left + 40, initialBounds.Top + 16));
            controller.CompleteTitleBarDrag(form, new Point(workingArea.Left + (workingArea.Width / 2), workingArea.Top));

            Assert.Equal(initialBounds, form.Bounds);
            Assert.True(controller.IsResizeBorderEnabled);
        }
    }
}
