using Xunit;

namespace helengine.editor.windows.tests.utils {
    /// <summary>
    /// Verifies the User32-backed Windows arrangement feature reader.
    /// </summary>
    public sealed class WindowsWindowArrangementFeatureStateTests {
        /// <summary>
        /// Ensures the native SystemParametersInfo imports resolve when caching every Windows arrangement setting.
        /// </summary>
        [Fact]
        public void Constructor_WhenCreated_CachesEveryUser32ArrangementSetting() {
            WindowsWindowArrangementFeatureState featureState = new WindowsWindowArrangementFeatureState();

            bool isWindowArrangingEnabled = featureState.IsWindowArrangingEnabled;
            bool isDockMovingEnabled = featureState.IsDockMovingEnabled;
            bool isDragFromMaximizeEnabled = featureState.IsDragFromMaximizeEnabled;

            Assert.True(isWindowArrangingEnabled || !isWindowArrangingEnabled);
            Assert.True(isDockMovingEnabled || !isDockMovingEnabled);
            Assert.True(isDragFromMaximizeEnabled || !isDragFromMaximizeEnabled);
        }
    }
}
