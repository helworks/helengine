using System.Reflection;
using helengine.core.tests.components;

namespace helengine.core.tests {
    /// <summary>Exercises the actual Cook-only source implementation, with all dynamic cache types excluded.</summary>
    public sealed class CookedViewportTests {
        /// <summary>Gameplay initialization can convert authored coordinates before the presentation pass publishes metrics.</summary>
        [Fact]
        public void Fit_CalculatesCoordinatesFromMetadataBeforePreparation() {
            using ViewportLayoutTestScene scene = new ViewportLayoutTestScene();
            Entity root = scene.CreateEntity();
            ReferenceCanvasFitComponent fit = new ReferenceCanvasFitComponent {
                ReferenceWidth = 640, ReferenceHeight = 480, CookedLayout = CreateMetadata()
            };
            root.AddComponent(fit);
            Assert.Equal(new float2(0.5f, 0.5f), fit.CalculateScale());
            Assert.Equal(new float3(100, 40, 2), fit.CalculatePosition(new float3(200, 80, 2)));
        }

        /// <summary>Children attached by initialization callbacks belong to the outer attachment's single preparation pass.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void InitializationCreatedDescendant_IsScaledExactlyOnce(bool fitOwner) {
            using ViewportLayoutTestScene scene = new ViewportLayoutTestScene();
            Entity root = scene.CreateEntity();
            if (fitOwner) { root.AddComponent(new ReferenceCanvasFitComponent { CookedLayout = CreateMetadata() }); }
            else {
                ViewportComponent viewport = scene.AddViewport(root, ViewportLayoutProcessingMode.Cook);
                viewport.CookedLayout = CreateMetadata();
            }
            root.InitializeHierarchy();
            foreach (Component component in root.Components) {
                if (component is ViewportComponent viewport) { viewport.Update(); }
                else if (component is ReferenceCanvasFitComponent fit) { fit.Update(); }
            }
            Entity child = scene.CreateEntity();
            CookedLayoutChildSpawner spawner = new CookedLayoutChildSpawner();
            child.AddComponent(spawner);
            root.AddChild(child);
            Assert.Equal(new int2(100, 40), spawner.CreatedRectangle.Size);
        }
        /// <summary>No original-value record or dynamic cache type may remain in a Cook-only assembly.</summary>
        [Fact]
        public void CookedAssembly_ExcludesOriginalValueRecords() {
            Assembly assembly = typeof(ViewportComponent).Assembly;
            Assert.Null(assembly.GetType("helengine.ViewportLayoutCache"));
            Assert.Null(assembly.GetType("helengine.ViewportPositionState"));
            Assert.Null(assembly.GetType("helengine.ViewportTextState"));
        }
        /// <summary>Resize notifications cannot change baked geometry, factors or trigger anchor notifications.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CookedLayout_RemainsFixedWithoutResizeCallbacks(bool fitOwner) {
            using ViewportLayoutTestScene scene = new ViewportLayoutTestScene();
            Entity root = scene.CreateEntity();
            RoundedRectComponent rectangle = new RoundedRectComponent { Size = new int2(50, 20) };
            root.AddComponent(rectangle);
            ViewportCookedLayout layout = CreateMetadata();
            int notifications = 0;
            if (fitOwner) {
                ReferenceCanvasFitComponent fit = new ReferenceCanvasFitComponent { CookedLayout = layout };
                root.AddComponent(fit);
                fit.Update();
                fit.AnchorBoundsChanged += () => notifications++;
                scene.Meshes.OnWindowResize(IntPtr.Zero, 640, 480);
                fit.Update();
                Assert.Equal(0, fit.RetainedLayoutPositionCount);
                Assert.Equal(320, fit.AnchorSpace.Size.X);
            } else {
                ViewportComponent viewport = scene.AddViewport(root, ViewportLayoutProcessingMode.Cook);
                viewport.CookedLayout = layout;
                viewport.Update();
                viewport.AnchorBoundsChanged += () => notifications++;
                scene.Meshes.OnWindowResize(IntPtr.Zero, 640, 480);
                viewport.Update();
                Assert.Equal(new int2(320, 240), viewport.ResolvedViewportSize);
                Assert.Equal(0, viewport.RetainedLayoutPositionCount);
                Assert.Throws<InvalidOperationException>(() => viewport.LayoutProcessingMode = ViewportLayoutProcessingMode.Dynamic);
                Assert.Throws<InvalidOperationException>(() => viewport.ReferenceWidth = 800);
            }
            Assert.Equal(0, notifications);
            Assert.Equal(new int2(50, 20), rectangle.Size);
        }
        /// <summary>Incorrect versions and video modes fail instead of enabling a runtime scale pass.</summary>
        [Theory]
        [InlineData(false, 0)]
        [InlineData(false, 1)]
        [InlineData(false, 2)]
        [InlineData(true, 0)]
        [InlineData(true, 1)]
        [InlineData(true, 2)]
        public void CookedLayout_RejectsMissingOrMismatchedMetadata(bool fitOwner, int mismatch) {
            using ViewportLayoutTestScene scene = new ViewportLayoutTestScene();
            Entity root = scene.CreateEntity();
            ViewportCookedLayout layout = mismatch == 0 ? null : CreateMetadata();
            if (mismatch == 1) { layout.AlgorithmVersion = -1; }
            if (mismatch == 2) { layout.TargetWidth = 640; }
            if (fitOwner) {
                ReferenceCanvasFitComponent fit = new ReferenceCanvasFitComponent { CookedLayout = layout };
                root.AddComponent(fit);
                Assert.Throws<InvalidOperationException>(() => fit.Update());
            } else {
                ViewportComponent viewport = scene.AddViewport(root, ViewportLayoutProcessingMode.Cook);
                viewport.CookedLayout = layout;
                Assert.Throws<InvalidOperationException>(() => viewport.Update());
            }
        }
        /// <summary>Supplies one known fixed surface with already cooked scale factors.</summary>
        static ViewportCookedLayout CreateMetadata() {
            return new ViewportCookedLayout { AlgorithmVersion = ViewportCookedLayout.CurrentAlgorithmVersion,
                TargetWidth = 320, TargetHeight = 240, PixelAspect = 1, ScaleX = 0.5, ScaleY = 0.5,
                GlyphScale = 0.5, AnchorSize = new int2(320, 240) };
        }
    }
}
