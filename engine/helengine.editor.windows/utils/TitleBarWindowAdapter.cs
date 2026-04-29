using Nucleus.Platform.Windows.Interop;
using System;
using System.Windows.Forms;

namespace helengine.editor.windows {
    /// <summary>
    /// Bridges editor title bar events to WinForms window actions for dragging and window state changes.
    /// </summary>
    public static class TitleBarWindowAdapter {
        /// <summary>
        /// Windows message used to begin a non-client left-button drag on the caption.
        /// </summary>
        const uint WmNcLButtonDown = 0xA1;
        /// <summary>
        /// Hit-test value that tells Windows to treat the drag as a title-bar drag.
        /// </summary>
        const uint HtCaption = 0x2;

        /// <summary>
        /// Wires title bar events to the provided WinForms host form.
        /// </summary>
        /// <param name="titleBar">Editor title bar instance.</param>
        /// <param name="hostForm">Host window receiving drag and window state requests.</param>
        /// <param name="toggleMaximize">Callback invoked when maximize/restore is requested.</param>
        public static void Attach(EditorTitleBar titleBar, Form hostForm, Action toggleMaximize) {
            if (titleBar == null) {
                throw new ArgumentNullException(nameof(titleBar));
            }
            if (hostForm == null) {
                throw new ArgumentNullException(nameof(hostForm));
            }
            if (toggleMaximize == null) {
                throw new ArgumentNullException(nameof(toggleMaximize));
            }

            titleBar.DragRequested += () => StartWindowDrag(hostForm);
            titleBar.ToggleMaximizeRequested += toggleMaximize;
            titleBar.MinimizeRequested += () => hostForm.WindowState = FormWindowState.Minimized;
            titleBar.CloseRequested += hostForm.Close;
        }

        /// <summary>
        /// Starts a Win32 window drag using the host handle.
        /// </summary>
        /// <param name="hostForm">Host window that should begin moving.</param>
        static void StartWindowDrag(Form hostForm) {
            if (hostForm == null) {
                throw new ArgumentNullException(nameof(hostForm));
            }

            if (hostForm is ITitleBarDragRestoreState dragRestoreState) {
                dragRestoreState.PrepareForTitleBarDrag(Cursor.Position);
            }

            User32Interop.ReleaseCapture();
            User32Interop.SendMessage(hostForm.Handle, WmNcLButtonDown, HtCaption, 0);
        }
    }
}
