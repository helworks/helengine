namespace helengine.editor.windows.tests.testing {
    /// <summary>
    /// Provides a lightweight borderless host form with controllable resize-border state for adapter tests.
    /// </summary>
    public sealed class TestResizeBorderStateForm : Form, IResizeBorderState {
        /// <summary>
        /// Initializes a form with deterministic bounds for resize-adapter tests.
        /// </summary>
        public TestResizeBorderStateForm() {
            StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(100, 100, 200, 150);
            FormBorderStyle = FormBorderStyle.None;
        }

        /// <summary>
        /// Gets or sets a value indicating whether border-resize behavior should remain enabled.
        /// </summary>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool IsResizeBorderEnabled { get; set; }
    }
}
