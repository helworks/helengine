using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies shared editor UI metrics scale first-pass chrome values consistently.
    /// </summary>
    public sealed class EditorUiMetricsTests {
        /// <summary>
        /// Ensures a larger UI scale produces scaled host and dock chrome values from the shared metrics object.
        /// </summary>
        [Fact]
        public void Constructor_WhenScaleIsOnePointFive_ScalesSharedChromeMetrics() {
            EditorUiMetrics metrics = new EditorUiMetrics(1.5);

            Assert.Equal(18, metrics.UiFontPixelSize);
            Assert.Equal(23, metrics.SnapModifierFontPixelSize);
            Assert.Equal(41, metrics.HostTitleBarHeight);
            Assert.Equal(30, metrics.DockTitleBarHeight);
        }
    }
}
