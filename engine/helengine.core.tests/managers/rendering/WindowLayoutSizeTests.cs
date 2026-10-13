namespace helengine.core.tests {
    /// <summary>Verifies named layout dimensions remain independent of render/input selection and window visibility.</summary>
    public sealed class WindowLayoutSizeTests {
        /// <summary>Host DPI reports publish layout changes while keeping physical rendering and pointer coordinates intact.</summary>
        [Fact]
        public void DisplayDensityIsIndependentOfRenderSize() {
            using MultiWindowTestRenderManager renderer = CreateRenderer();
            renderer.OnWindowResize(new IntPtr(1), 1920, 1080);
            List<string> changes = new List<string>();
            renderer.WindowLayoutChanged += changes.Add;
            renderer.SetWindowDensityDpi(new IntPtr(1), 420);
            renderer.SetWindowDensityDpi(new IntPtr(1), 420);
            renderer.SetWindowDensityDpi(new IntPtr(2), 320);
            Assert.Equal(420, renderer.GetWindowDensityDpi("MAIN"));
            Assert.Equal(320, renderer.GetWindowDensityDpi("glass"));
            Assert.Equal(new[] { "main", "glass" }, changes);
            Assert.Equal(new int2(1920, 1080), renderer.GetWindowSize(new IntPtr(1)));
            Assert.Equal(new int2(1920, 1080), renderer.MainWindowSize);
            Assert.Equal(new int2(1920, 1080), renderer.InputWindowSize);
            Assert.Equal(new int2(1920, 1080), renderer.GetCameraLayoutSize(new CameraComponent()));
            renderer.SelectInputWindow(new IntPtr(2));
            Assert.Equal(new int2(640, 360), renderer.InputWindowSize);
        }

        /// <summary>Density scaling requires an explicit positive host report and rejects unregistered windows.</summary>
        [Fact]
        public void DisplayDensityRejectsMissingOrInvalidReports() {
            using MultiWindowTestRenderManager renderer = CreateRenderer();
            Assert.Throws<InvalidOperationException>(() => renderer.GetWindowDensityDpi("main"));
            Assert.Throws<ArgumentOutOfRangeException>(() => renderer.SetWindowDensityDpi(new IntPtr(1), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => renderer.SetWindowDensityDpi(new IntPtr(1), -1));
            Assert.Throws<ArgumentException>(() => renderer.SetWindowDensityDpi(new IntPtr(99), 160));
            Assert.Equal(new int2(1280, 720), renderer.MainWindowSize);
        }
        /// <summary>Named cameras use their own dimensions while shared cameras retain the primary layout reference.</summary>
        [Fact]
        public void CameraLayoutUsesItsNamedView() {
            using MultiWindowTestRenderManager renderer = CreateRenderer();
            CameraComponent camera = new CameraComponent { OutputWindowTag = "GLASS" };
            renderer.SelectInputWindow(new IntPtr(2));
            renderer.SelectRenderWindow(new IntPtr(2));
            Assert.Equal(new int2(640, 360), renderer.GetCameraLayoutSize(camera));
            Assert.Equal(new int2(1280, 720), renderer.GetCameraLayoutSize(new CameraComponent()));
            renderer.OnWindowResize(new IntPtr(1), 1920, 1080);
            Assert.Equal(new int2(640, 360), renderer.GetCameraLayoutSize(camera));
        }

        /// <summary>Minimization and closure preserve the last usable layout, and a newly registered replacement updates that identity.</summary>
        [Fact]
        public void LayoutSurvivesMinimizeCloseAndTagReuse() {
            using MultiWindowTestRenderManager renderer = CreateRenderer();
            renderer.OnWindowResize(new IntPtr(2), 800, 600);
            renderer.OnWindowResize(new IntPtr(2), 0, 0);
            Assert.Equal(new int2(0, 0), renderer.GetWindowSize(new IntPtr(2)));
            Assert.Equal(new int2(800, 600), renderer.GetWindowLayoutSize("glass"));
            renderer.RemoveWindow(new IntPtr(2));
            Assert.Equal(new int2(800, 600), renderer.GetWindowLayoutSize("glass"));
            renderer.AddWindow(new IntPtr(3), 320, 240);
            renderer.SetWindowTag(new IntPtr(3), "GLASS");
            Assert.Equal(new int2(320, 240), renderer.GetWindowLayoutSize("glass"));
        }

        /// <summary>Tagging a minimized view uses its own retained size rather than its zero-sized client rectangle.</summary>
        [Fact]
        public void NamingMinimizedWindowPreservesItsUsableSize() {
            using MultiWindowTestRenderManager renderer = CreateRenderer();
            renderer.AddWindow(new IntPtr(3), 900, 700);
            renderer.OnWindowResize(new IntPtr(3), 0, 0);
            renderer.SetWindowTag(new IntPtr(3), "preview");
            Assert.Equal(new int2(900, 700), renderer.GetWindowLayoutSize("preview"));
        }

        /// <summary>Only usable dimension changes publish layout notifications; minimization cannot collapse scene UI.</summary>
        [Fact]
        public void LayoutNotificationsIdentifyTheView() {
            using MultiWindowTestRenderManager renderer = CreateRenderer();
            List<string> tags = new List<string>();
            renderer.WindowLayoutChanged += tag => tags.Add(tag);
            renderer.OnWindowResize(new IntPtr(2), 800, 600);
            renderer.OnWindowResize(new IntPtr(2), 0, 0);
            renderer.OnWindowResize(new IntPtr(1), 1920, 1080);
            Assert.Equal(new[] { "glass", "main" }, tags);
        }

        /// <summary>Missing named references and disposed registries fail explicitly rather than borrowing another view's size.</summary>
        [Fact]
        public void UnknownLayoutReferencesAreRejected() {
            using MultiWindowTestRenderManager renderer = CreateRenderer();
            Assert.Throws<InvalidOperationException>(() => renderer.GetWindowLayoutSize("missing"));
            Assert.Throws<ArgumentNullException>(() => renderer.GetCameraLayoutSize(null));
            renderer.Dispose();
            Assert.Throws<InvalidOperationException>(() => renderer.GetWindowLayoutSize("glass"));
        }

        /// <summary>Registers a primary desktop-sized reference and an independently sized overlay.</summary>
        /// <returns>A disposable renderer with declared layout identities.</returns>
        static MultiWindowTestRenderManager CreateRenderer() {
            MultiWindowTestRenderManager renderer = new MultiWindowTestRenderManager();
            renderer.AddWindow(new IntPtr(1), 1280, 720);
            renderer.AddWindow(new IntPtr(2), 640, 360);
            renderer.SetWindowTag(new IntPtr(2), "glass");
            return renderer;
        }
    }
}
