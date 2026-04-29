namespace helengine.render.validation {
    /// <summary>
    /// Provides a host window for renderer swapchain output during validation.
    /// </summary>
    public class RenderValidationWindow : Form {
        /// <summary>
        /// Initializes a validation window with fixed client dimensions.
        /// </summary>
        /// <param name="width">Client width in pixels.</param>
        /// <param name="height">Client height in pixels.</param>
        /// <param name="backend">Backend label shown in the title.</param>
        public RenderValidationWindow(int width, int height, RenderBackend backend) {
            if (width <= 0) {
                throw new ArgumentOutOfRangeException(nameof(width), "Window width must be greater than zero.");
            }

            if (height <= 0) {
                throw new ArgumentOutOfRangeException(nameof(height), "Window height must be greater than zero.");
            }

            ClientSize = new Size(width, height);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(32, 32);
            ShowInTaskbar = false;
            TopMost = true;
            Text = $"Render Validation - {backend}";
        }
    }
}
