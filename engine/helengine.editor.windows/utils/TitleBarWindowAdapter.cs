using Nucleus.Platform.Windows.Interop;

namespace helengine.editor.windows {
    /// <summary>
    /// Bridges editor title bar events to WinForms window actions for dragging and window state changes.
    /// </summary>
    public static class TitleBarWindowAdapter {
        const uint WmNcLButtonDown = 0xA1;
        const uint HtCaption = 0x2;

        /// <summary>
        /// Wires title bar events to the provided WinForms host form.
        /// </summary>
        /// <param name="titleBar">Editor title bar instance.</param>
        /// <param name="hostForm">Host window receiving drag and window state requests.</param>
        public static void Attach(EditorTitleBar titleBar, Form hostForm) {
            titleBar.DragRequested += () => StartWindowDrag(hostForm.Handle);
            titleBar.ToggleMaximizeRequested += () => ToggleWindowState(hostForm);
            titleBar.MinimizeRequested += () => hostForm.WindowState = FormWindowState.Minimized;
            titleBar.CloseRequested += hostForm.Close;
        }

        /// <summary>
        /// Starts a Win32 window drag using the host handle.
        /// </summary>
        /// <param name="handle">Handle for the host window.</param>
        static void StartWindowDrag(IntPtr handle) {
            User32Interop.ReleaseCapture();
            User32Interop.SendMessage(handle, WmNcLButtonDown, HtCaption, 0);
        }

        /// <summary>
        /// Toggles between maximized and normal window states.
        /// </summary>
        /// <param name="hostForm">Host window to modify.</param>
        static void ToggleWindowState(Form hostForm) {
            hostForm.WindowState = hostForm.WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
        }
    }
}
