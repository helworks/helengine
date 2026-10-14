namespace helengine.core.tests {
    /// <summary>
    /// Verifies the per-window display pixel aspect: windows are square until their host reports otherwise, named views
    /// keep their own value, render-target cameras stay square, and invalid reports are refused.
    /// </summary>
    public sealed class WindowPixelAspectTests {
        /// <summary>Windows and cameras have square pixels until a host reports a pixel aspect.</summary>
        [Fact]
        public void UnreportedWindowsAndCamerasAreSquare() {
            using MultiWindowTestRenderManager renderer = CreateRenderer();

            Assert.Equal(RenderManager3D.SquarePixelAspect, renderer.MainWindowPixelAspect);
            Assert.Equal(1d, renderer.GetWindowPixelAspect(new IntPtr(1)));
            Assert.Equal(1d, renderer.GetWindowPixelAspect(new IntPtr(2)));
            Assert.Equal(1d, renderer.GetCameraPixelAspect(new CameraComponent()));
            Assert.Equal(1d, renderer.GetCameraPixelAspect(new CameraComponent { OutputWindowTag = "glass" }));
        }

        /// <summary>
        /// A reported pixel aspect reaches the window, the primary reference and the cameras that draw into that view,
        /// and each change notifies the view's layout once.
        /// </summary>
        [Fact]
        public void ReportedPixelAspectReachesItsWindowAndCameras() {
            using MultiWindowTestRenderManager renderer = CreateRenderer();
            List<string> changes = new List<string>();
            renderer.WindowLayoutChanged += changes.Add;

            renderer.SetWindowPixelAspect(new IntPtr(1), 0.5d);
            renderer.SetWindowPixelAspect(new IntPtr(1), 0.5d);
            renderer.SetWindowPixelAspect(new IntPtr(2), 2d);

            Assert.Equal(0.5d, renderer.MainWindowPixelAspect);
            Assert.Equal(0.5d, renderer.GetWindowPixelAspect(new IntPtr(1)));
            Assert.Equal(0.5d, renderer.GetCameraPixelAspect(new CameraComponent()));
            Assert.Equal(2d, renderer.GetCameraPixelAspect(new CameraComponent { OutputWindowTag = "GLASS" }));
            Assert.Equal(new[] { "main", "glass" }, changes);
            Assert.Equal(new int2(1280, 720), renderer.MainWindowSize);
        }

        /// <summary>A camera rendering into a render target projects with square pixels whatever the window reports.</summary>
        [Fact]
        public void RenderTargetCameraIsAlwaysSquare() {
            using MultiWindowTestRenderManager renderer = CreateRenderer();
            renderer.SetWindowPixelAspect(new IntPtr(1), 0.5d);
            using PixelAspectTestRenderTarget target = new PixelAspectTestRenderTarget();

            Assert.Equal(1d, renderer.GetCameraPixelAspect(new CameraComponent { RenderTarget = target }));
        }

        /// <summary>A named view keeps its pixel aspect when the window is tagged after the report.</summary>
        [Fact]
        public void TaggingAWindowCarriesItsPixelAspect() {
            using MultiWindowTestRenderManager renderer = CreateRenderer();
            renderer.AddWindow(new IntPtr(3), 320, 240);
            renderer.SetWindowPixelAspect(new IntPtr(3), 0.75d);

            renderer.SetWindowTag(new IntPtr(3), "preview");

            Assert.Equal(0.75d, renderer.GetCameraPixelAspect(new CameraComponent { OutputWindowTag = "preview" }));
        }

        /// <summary>
        /// Removing a window forgets its pixel aspect, so a later window registered with the same handle (hosts reuse
        /// handle values) starts with square pixels.
        /// </summary>
        [Fact]
        public void RemovedWindowForgetsItsPixelAspect() {
            using MultiWindowTestRenderManager renderer = CreateRenderer();
            renderer.AddWindow(new IntPtr(3), 320, 240);
            renderer.SetWindowPixelAspect(new IntPtr(3), 0.75d);

            renderer.RemoveWindow(new IntPtr(3));
            renderer.AddWindow(new IntPtr(3), 320, 240);

            Assert.Equal(1d, renderer.GetWindowPixelAspect(new IntPtr(3)));
        }

        /// <summary>
        /// Tagging a window that never reported a pixel aspect clears the value a previous owner of that view reported,
        /// so the view is square again.
        /// </summary>
        [Fact]
        public void RetaggingAnUnreportedWindowClearsTheViewsStalePixelAspect() {
            using MultiWindowTestRenderManager renderer = CreateRenderer();
            renderer.AddWindow(new IntPtr(3), 320, 240);
            renderer.SetWindowTag(new IntPtr(3), "preview");
            renderer.SetWindowPixelAspect(new IntPtr(3), 0.75d);
            renderer.RemoveWindow(new IntPtr(3));
            renderer.AddWindow(new IntPtr(4), 320, 240);

            renderer.SetWindowTag(new IntPtr(4), "preview");

            Assert.Equal(1d, renderer.GetCameraPixelAspect(new CameraComponent { OutputWindowTag = "preview" }));
        }

        /// <summary>Non-finite or non-positive values, unknown windows and unknown views are refused.</summary>
        [Fact]
        public void InvalidReportsAndUnknownViewsAreRefused() {
            using MultiWindowTestRenderManager renderer = CreateRenderer();

            Assert.Throws<ArgumentOutOfRangeException>(() => renderer.SetWindowPixelAspect(new IntPtr(1), 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => renderer.SetWindowPixelAspect(new IntPtr(1), -1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => renderer.SetWindowPixelAspect(new IntPtr(1), double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => renderer.SetWindowPixelAspect(new IntPtr(1), double.PositiveInfinity));
            Assert.Throws<ArgumentException>(() => renderer.SetWindowPixelAspect(new IntPtr(99), 0.5d));
            Assert.Throws<ArgumentException>(() => renderer.GetWindowPixelAspect(new IntPtr(99)));
            Assert.Throws<InvalidOperationException>(() => renderer.GetCameraPixelAspect(new CameraComponent { OutputWindowTag = "nowhere" }));
            Assert.Equal(1d, renderer.MainWindowPixelAspect);
        }

        /// <summary>Disposing the renderer returns the primary reference to square pixels.</summary>
        [Fact]
        public void DisposeResetsThePrimaryPixelAspect() {
            MultiWindowTestRenderManager renderer = CreateRenderer();
            renderer.SetWindowPixelAspect(new IntPtr(1), 0.5d);

            renderer.Dispose();

            Assert.Equal(1d, renderer.MainWindowPixelAspect);
        }

        /// <summary>Registers a primary 1280x720 window and a 640x360 window tagged <c>glass</c>.</summary>
        /// <returns>A disposable renderer with two views.</returns>
        static MultiWindowTestRenderManager CreateRenderer() {
            MultiWindowTestRenderManager renderer = new MultiWindowTestRenderManager();
            renderer.AddWindow(new IntPtr(1), 1280, 720);
            renderer.AddWindow(new IntPtr(2), 640, 360);
            renderer.SetWindowTag(new IntPtr(2), "glass");
            return renderer;
        }
    }
}
