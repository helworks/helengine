namespace helengine.core.tests {
    /// <summary>Exercises view routing independently of input, resize and primary lifetime.</summary>
    public sealed class CameraWindowRoutingTests {
        /// <summary>Shared cameras render in all views while tagged cameras appear only in the matching view.</summary>
        [Fact]
        public void CameraAssignmentsSeparateViews() {
            using MultiWindowTestRenderManager renderer = CreateRenderer();
            CameraComponent shared = new CameraComponent();
            CameraComponent main = new CameraComponent { OutputWindowTag = "main" };
            CameraComponent glass = new CameraComponent { OutputWindowTag = "GLASS" };
            Assert.True(renderer.IsCameraVisibleInWindow(shared, new IntPtr(1)));
            Assert.True(renderer.IsCameraVisibleInWindow(shared, new IntPtr(2)));
            Assert.True(renderer.IsCameraVisibleInWindow(main, new IntPtr(1)));
            Assert.False(renderer.IsCameraVisibleInWindow(main, new IntPtr(2)));
            Assert.True(renderer.IsCameraVisibleInWindow(glass, new IntPtr(2)));
            Assert.False(renderer.IsCameraVisibleInWindow(glass, new IntPtr(1)));
        }

        /// <summary>Output selection is independent of input, primary layout and secondary resize.</summary>
        [Fact]
        public void DrawAndInputSelectionRemainIndependent() {
            using MultiWindowTestRenderManager renderer = CreateRenderer();
            renderer.SelectRenderWindow(new IntPtr(2));
            renderer.OnWindowResize(new IntPtr(2), 900, 700);
            Assert.Equal(new IntPtr(1), renderer.InputWindowHandle);
            Assert.Equal(new IntPtr(2), renderer.RenderWindowHandle);
            Assert.Equal(640, renderer.MainWindowSize.X);
            renderer.SelectInputWindow(new IntPtr(2));
            renderer.SelectRenderWindow(new IntPtr(1));
            Assert.Equal(new IntPtr(2), renderer.InputWindowHandle);
        }

        /// <summary>Removing a view releases its tag and restores output and input ownership to main.</summary>
        [Fact]
        public void RemovalReleasesTheTagAndSelections() {
            using MultiWindowTestRenderManager renderer = CreateRenderer();
            renderer.SelectInputWindow(new IntPtr(2));
            renderer.SelectRenderWindow(new IntPtr(2));
            renderer.RemoveWindow(new IntPtr(2));
            Assert.Equal(new IntPtr(1), renderer.RenderWindowHandle);
            Assert.Equal(new IntPtr(1), renderer.InputWindowHandle);
            renderer.AddWindow(new IntPtr(3), 320, 240);
            renderer.SetWindowTag(new IntPtr(3), "glass");
            Assert.True(renderer.IsCameraVisibleInWindow(new CameraComponent { OutputWindowTag = "glass" }, new IntPtr(3)));
        }

        /// <summary>Duplicates, missing registrations and primary renames preserve established routing state.</summary>
        [Fact]
        public void InvalidAssignmentsLeaveValidStateIntact() {
            using MultiWindowTestRenderManager renderer = CreateRenderer();
            Assert.Throws<InvalidOperationException>(() => renderer.SetWindowTag(new IntPtr(2), "main"));
            Assert.Throws<ArgumentException>(() => renderer.SetWindowTag(new IntPtr(1), "other"));
            Assert.Throws<ArgumentException>(() => renderer.SelectRenderWindow(new IntPtr(99)));
            renderer.AddWindow(new IntPtr(3), 320, 240);
            Assert.Throws<InvalidOperationException>(() => renderer.SetWindowTag(new IntPtr(3), "GLASS"));
            Assert.True(renderer.IsCameraVisibleInWindow(new CameraComponent { OutputWindowTag = "glass" }, new IntPtr(2)));
            renderer.Dispose();
            Assert.Equal(IntPtr.Zero, renderer.RenderWindowHandle);
            Assert.Throws<ArgumentException>(() => renderer.IsCameraVisibleInWindow(new CameraComponent(), new IntPtr(1)));
        }

        /// <summary>Malformed authored targets cannot replace the camera's last valid target.</summary>
        /// <param name="tag">A tag rejected by the native host's identifier grammar.</param>
        [Theory]
        [InlineData("two words")]
        [InlineData("glass,overlay")]
        [InlineData("tab/path")]
        [InlineData("glass.window")]
        public void InvalidCameraTagPreservesTheTarget(string tag) {
            CameraComponent camera = new CameraComponent { OutputWindowTag = "glass" };
            Assert.Throws<ArgumentException>(() => camera.OutputWindowTag = tag);
            Assert.Equal("glass", camera.OutputWindowTag);
        }

        /// <summary>Empty means sharing, null remains invalid and the native length boundary is enforced.</summary>
        [Fact]
        public void CameraTagLengthAndSharingAreExplicit() {
            CameraComponent camera = new CameraComponent();
            camera.OutputWindowTag = new string('a', 64);
            Assert.Throws<ArgumentException>(() => camera.OutputWindowTag = new string('a', 65));
            Assert.Throws<ArgumentNullException>(() => camera.OutputWindowTag = null);
            Assert.Equal(64, camera.OutputWindowTag.Length);
            camera.OutputWindowTag = string.Empty;
            Assert.Equal(string.Empty, camera.OutputWindowTag);
        }

        /// <summary>Registers a primary and a tagged secondary with independent dimensions.</summary>
        /// <returns>A disposable headless window registry.</returns>
        static MultiWindowTestRenderManager CreateRenderer() {
            MultiWindowTestRenderManager renderer = new MultiWindowTestRenderManager();
            renderer.AddWindow(new IntPtr(1), 640, 360);
            renderer.AddWindow(new IntPtr(2), 800, 600);
            renderer.SetWindowTag(new IntPtr(2), "glass");
            return renderer;
        }
    }
}
