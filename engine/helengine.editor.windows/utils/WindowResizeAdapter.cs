namespace helengine.editor.windows {
    /// <summary>
    /// Provides borderless window resize hit testing and cursor helpers.
    /// </summary>
    public static class WindowResizeAdapter {
        /// <summary>
        /// Windows message identifier for hit testing.
        /// </summary>
        const int WmNcHitTest = 0x84;
        /// <summary>
        /// Hit test result for client area.
        /// </summary>
        const int HtClient = 1;
        /// <summary>
        /// Hit test result for left border.
        /// </summary>
        const int HtLeft = 10;
        /// <summary>
        /// Hit test result for right border.
        /// </summary>
        const int HtRight = 11;
        /// <summary>
        /// Hit test result for top border.
        /// </summary>
        const int HtTop = 12;
        /// <summary>
        /// Hit test result for top-left corner.
        /// </summary>
        const int HtTopLeft = 13;
        /// <summary>
        /// Hit test result for top-right corner.
        /// </summary>
        const int HtTopRight = 14;
        /// <summary>
        /// Hit test result for bottom border.
        /// </summary>
        const int HtBottom = 15;
        /// <summary>
        /// Hit test result for bottom-left corner.
        /// </summary>
        const int HtBottomLeft = 16;
        /// <summary>
        /// Hit test result for bottom-right corner.
        /// </summary>
        const int HtBottomRight = 17;

        /// <summary>
        /// Default thickness in pixels for resizing the borderless window.
        /// </summary>
        public const int DefaultResizeBorderThickness = 6;

        /// <summary>
        /// Applies window resize hit testing to a WinForms message.
        /// </summary>
        /// <param name="hostForm">Host form receiving the message.</param>
        /// <param name="m">Windows message payload.</param>
        /// <param name="resizeBorderThickness">Thickness in pixels for resize hit testing.</param>
        /// <returns>True when the message result was updated.</returns>
        public static bool ApplyResizeHitTest(Form hostForm, ref Message m, int resizeBorderThickness) {
            if (m.Msg != WmNcHitTest) {
                return false;
            }

            if (!IsResizeBorderEnabled(hostForm)) {
                return false;
            }

            if ((int)m.Result != HtClient) {
                return false;
            }

            Point clientPoint = GetHitTestPoint(hostForm, m.LParam);
            int hitTest = GetResizeHitTest(hostForm, clientPoint, resizeBorderThickness);
            if (hitTest != HtClient) {
                m.Result = (IntPtr)hitTest;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether the cursor should display a window resize indicator.
        /// </summary>
        /// <param name="hostForm">Host form to inspect.</param>
        /// <param name="clientPoint">Point in client coordinates.</param>
        /// <param name="resizeBorderThickness">Thickness in pixels for resize hit testing.</param>
        /// <param name="cursor">Resolved cursor for the resize handle.</param>
        /// <returns>True when a resize cursor should be shown.</returns>
        public static bool TryGetResizeCursor(Form hostForm, Point clientPoint, int resizeBorderThickness, out Cursor cursor) {
            cursor = Cursors.Default;
            if (!IsResizeBorderEnabled(hostForm)) {
                return false;
            }

            int hitTest = GetResizeHitTest(hostForm, clientPoint, resizeBorderThickness);
            cursor = GetResizeCursor(hitTest);
            return hitTest != HtClient;
        }

        /// <summary>
        /// Determines whether the current host should expose border-resize behavior.
        /// </summary>
        /// <param name="hostForm">Host form to inspect.</param>
        /// <returns>True when resize cursors and hit testing should remain enabled.</returns>
        static bool IsResizeBorderEnabled(Form hostForm) {
            if (hostForm == null) {
                throw new ArgumentNullException(nameof(hostForm));
            }

            if (hostForm is IResizeBorderState resizeBorderState) {
                return resizeBorderState.IsResizeBorderEnabled;
            }

            return hostForm.WindowState != FormWindowState.Maximized;
        }

        /// <summary>
        /// Converts a hit test message lParam into client coordinates.
        /// </summary>
        /// <param name="hostForm">Host form to convert coordinates.</param>
        /// <param name="lParam">Raw lParam from the hit test message.</param>
        /// <returns>Point in client coordinates.</returns>
        static Point GetHitTestPoint(Form hostForm, IntPtr lParam) {
            int value = lParam.ToInt32();
            int x = (short)(value & 0xFFFF);
            int y = (short)((value >> 16) & 0xFFFF);
            return hostForm.PointToClient(new Point(x, y));
        }

        /// <summary>
        /// Determines the resize hit test result for a client point.
        /// </summary>
        /// <param name="hostForm">Host form to inspect.</param>
        /// <param name="clientPoint">Point in client coordinates.</param>
        /// <param name="resizeBorderThickness">Thickness in pixels for resize hit testing.</param>
        /// <returns>Hit test result for resizing, or client when not on an edge.</returns>
        static int GetResizeHitTest(Form hostForm, Point clientPoint, int resizeBorderThickness) {
            int width = hostForm.ClientSize.Width;
            int height = hostForm.ClientSize.Height;

            bool left = clientPoint.X <= resizeBorderThickness;
            bool right = clientPoint.X >= width - resizeBorderThickness;
            bool top = clientPoint.Y <= resizeBorderThickness;
            bool bottom = clientPoint.Y >= height - resizeBorderThickness;

            if (left && top) {
                return HtTopLeft;
            }
            if (right && top) {
                return HtTopRight;
            }
            if (left && bottom) {
                return HtBottomLeft;
            }
            if (right && bottom) {
                return HtBottomRight;
            }
            if (left) {
                return HtLeft;
            }
            if (right) {
                return HtRight;
            }
            if (top) {
                return HtTop;
            }
            if (bottom) {
                return HtBottom;
            }

            return HtClient;
        }

        /// <summary>
        /// Maps hit test results to Windows resize cursors.
        /// </summary>
        /// <param name="hitTest">Hit test value.</param>
        /// <returns>Cursor that represents the resize direction.</returns>
        static Cursor GetResizeCursor(int hitTest) {
            switch (hitTest) {
                case HtLeft:
                case HtRight:
                    return Cursors.SizeWE;
                case HtTop:
                case HtBottom:
                    return Cursors.SizeNS;
                case HtTopLeft:
                case HtBottomRight:
                    return Cursors.SizeNWSE;
                case HtTopRight:
                case HtBottomLeft:
                    return Cursors.SizeNESW;
                default:
                    return Cursors.Default;
            }
        }
    }
}
