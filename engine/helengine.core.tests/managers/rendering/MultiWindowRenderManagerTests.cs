namespace helengine.core.tests {
    /// <summary>
    /// Verifies independent sizing and input routing across one shared renderer.
    /// </summary>
    public sealed class MultiWindowRenderManagerTests {
        /// <summary>
        /// Secondary resize preserves primary layout and primary resize updates its stored size.
        /// </summary>
        [Fact]
        public void ResizeKeepsWindowSizesIndependent() {
            using MultiWindowTestRenderManager renderer = new MultiWindowTestRenderManager();
            renderer.AddWindow(new IntPtr(1), 640, 360);
            renderer.AddWindow(new IntPtr(2), 800, 600);
            renderer.OnWindowResize(new IntPtr(2), 900, 700);
            Assert.Equal(640, renderer.MainWindowSize.X);
            Assert.Equal(900, renderer.GetWindowSize(new IntPtr(2)).X);
            renderer.OnWindowResize(new IntPtr(1), 1024, 768);
            Assert.Equal(1024, renderer.MainWindowSize.X);
            Assert.Equal(700, renderer.GetWindowSize(new IntPtr(2)).Y);
        }

        /// <summary>
        /// Input follows the selected window and returns to primary when that window closes.
        /// </summary>
        [Fact]
        public void InputSelectionSurvivesMinimizeAndRemoval() {
            using MultiWindowTestRenderManager renderer = new MultiWindowTestRenderManager();
            renderer.AddWindow(new IntPtr(1), 640, 360);
            renderer.AddWindow(new IntPtr(2), 800, 600);
            renderer.SelectInputWindow(new IntPtr(2));
            Assert.Equal(800, renderer.InputWindowSize.X);
            renderer.OnWindowResize(new IntPtr(2), 0, 0);
            Assert.Equal(0, renderer.InputWindowSize.X);
            Assert.Equal(640, renderer.MainWindowSize.X);
            renderer.RemoveWindow(new IntPtr(2));
            Assert.Equal(new IntPtr(1), renderer.InputWindowHandle);
            Assert.Equal(640, renderer.InputWindowSize.X);
            Assert.Equal(1, renderer.WindowCount);
        }

        /// <summary>
        /// Minimizing the primary view does not collapse the shared scene layout used by visible secondary views.
        /// </summary>
        [Fact]
        public void PrimaryMinimizeRetainsLayoutReference() {
            using MultiWindowTestRenderManager renderer = new MultiWindowTestRenderManager();
            renderer.AddWindow(new IntPtr(1), 640, 360);
            renderer.AddWindow(new IntPtr(2), 800, 600);
            renderer.OnWindowResize(new IntPtr(1), 0, 0);
            Assert.Equal(0, renderer.GetWindowSize(new IntPtr(1)).X);
            Assert.Equal(640, renderer.MainWindowSize.X);
            renderer.OnWindowResize(new IntPtr(1), 1024, 768);
            Assert.Equal(1024, renderer.MainWindowSize.X);
        }

        /// <summary>
        /// Invalid registrations and unknown targets fail without altering other windows.
        /// </summary>
        [Fact]
        public void InvalidOperationsThrow() {
            using MultiWindowTestRenderManager renderer = new MultiWindowTestRenderManager();
            renderer.AddWindow(new IntPtr(1), 640, 360);
            Assert.Throws<InvalidOperationException>(() => renderer.AddWindow(new IntPtr(1), 800, 600));
            Assert.Throws<ArgumentException>(() => renderer.OnWindowResize(new IntPtr(2), 800, 600));
            Assert.Throws<ArgumentException>(() => renderer.SelectInputWindow(new IntPtr(2)));
            Assert.Throws<InvalidOperationException>(() => renderer.RemoveWindow(new IntPtr(1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => renderer.OnWindowResize(new IntPtr(1), -1, 600));
        }
    }
}
