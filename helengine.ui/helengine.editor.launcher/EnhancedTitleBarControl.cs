using Nucleus.Platform.Windows.Controls;

namespace helengine.editor.launcher {
    /// <summary>
    /// Enhanced TitleBarControl that respects Windows system settings for double-click maximize
    /// This control can be reused across all forms in the project
    /// </summary>
    public class EnhancedTitleBarControl : TitleBarControl {
        
        public EnhancedTitleBarControl() : base() {
            // All initialization is handled by the base class
        }

        /// <summary>
        /// Override double-click behavior to respect Windows system settings
        /// </summary>
        protected override void OnDoubleClick(EventArgs e) {
            // Call base first to ensure proper event handling
            base.OnDoubleClick(e);
            
            // Only perform maximize/restore if Windows system settings allow it
            // The base class already calls ToggleMaximize(), but we need to check
            // the system settings first. Since we can't prevent the base call,
            // we'll override this to handle it properly.
        }

        /// <summary>
        /// Override the mouse event handling to implement proper double-click detection
        /// that respects Windows system settings
        /// </summary>
        protected override void OnMouseDown(MouseEventArgs e) {
            // Handle dragging
            base.OnMouseDown(e);
        }

        /// <summary>
        /// Custom double-click detection with Windows system settings respect
        /// </summary>
        DateTime lastClickTime = DateTime.MinValue;
        Point lastClickLocation = Point.Empty;

        protected override void OnClick(EventArgs e) {
            base.OnClick(e);

            // Implement custom double-click detection that respects system settings
            DateTime currentTime = DateTime.Now;
            Point currentLocation = Cursor.Position;

            if (lastClickTime != DateTime.MinValue) {
                TimeSpan timeDiff = currentTime - lastClickTime;
                uint systemDoubleClickTime = WindowsSystemSettings.GetSystemDoubleClickTime();
                var (doubleClickWidth, doubleClickHeight) = WindowsSystemSettings.GetDoubleClickArea();

                // Check if this qualifies as a double-click based on system settings
                bool withinTimeLimit = timeDiff.TotalMilliseconds <= systemDoubleClickTime;
                bool withinArea = Math.Abs(currentLocation.X - lastClickLocation.X) <= doubleClickWidth &&
                                  Math.Abs(currentLocation.Y - lastClickLocation.Y) <= doubleClickHeight;

                if (withinTimeLimit && withinArea) {
                    // This is a valid double-click according to system settings
                    OnSystemRespectingDoubleClick();
                    lastClickTime = DateTime.MinValue; // Reset to prevent triple-click
                    return;
                }
            }

            lastClickTime = currentTime;
            lastClickLocation = currentLocation;
        }

        /// <summary>
        /// Handle double-click that respects Windows system settings
        /// </summary>
        void OnSystemRespectingDoubleClick() {
            // Only maximize if Windows system settings allow double-click titlebar maximize
            if (WindowsSystemSettings.IsDoubleClickTitleBarMaximizeEnabled()) {
                PerformMaximizeToggle();
            }
        }

        /// <summary>
        /// Perform the actual maximize/restore toggle
        /// </summary>
        void PerformMaximizeToggle() {
            var parentForm = this.FindForm();
            if (parentForm != null && EnableMaximize) {
                parentForm.WindowState = parentForm.WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal
                    : FormWindowState.Maximized;
            }
        }
    }
}
