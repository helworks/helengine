using helengine.editor.tests.testing;

namespace helengine.editor.tests {
    /// <summary>Exercises per-view viewport sizing, anchor positions and reference-canvas fitting with one shared core.</summary>
    public sealed class WindowScopedLayoutTests : IDisposable {
        /// <summary>Owns the initialized test engine and its registered view identities.</summary>
        readonly Core CoreValue;

        /// <summary>Releases manually authored test hierarchies before disposing their renderer.</summary>
        readonly List<Entity> Roots = new List<Entity>();

        /// <summary>Registers primary, overlay and preview views before constructing camera-bound UI.</summary>
        public WindowScopedLayoutTests() {
            TestRenderManager3D renderer = new TestRenderManager3D();
            renderer.OnWindowResize(IntPtr.Zero, 1280, 720);
            renderer.AddWindow(new IntPtr(2), 640, 360);
            renderer.SetWindowTag(new IntPtr(2), "glass");
            renderer.AddWindow(new IntPtr(3), 1024, 768);
            renderer.SetWindowTag(new IntPtr(3), "preview");
            CoreValue = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            CoreValue.Initialize(renderer, new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>Disposes hierarchy callbacks before the shared renderer's window records are released.</summary>
        public void Dispose() {
            foreach (Entity root in Roots) {
                root.Dispose();
            }
            CoreValue.Dispose();
        }

        /// <summary>An overlay's normalized camera viewport and right/bottom anchors follow only its own client dimensions.</summary>
        [Fact]
        public void ViewportAndAnchorsIgnoreOtherWindowResizes() {
            Entity root = CreateCameraRoot(true);
            ViewportComponent viewport = GetViewport(root);
            Entity control = CreateAnchoredControl(root);
            Assert.Equal(new int2(640, 360), viewport.ResolvedViewportSize);
            Assert.Equal(new float3(520f, 290f, 0f), control.LocalPosition);
            CoreValue.RenderManager3D.OnWindowResize(IntPtr.Zero, 1920, 1080);
            CoreValue.RenderManager3D.OnWindowResize(new IntPtr(3), 1200, 900);
            Assert.Equal(new float3(520f, 290f, 0f), control.LocalPosition);
            CoreValue.RenderManager3D.OnWindowResize(new IntPtr(2), 800, 600);
            Assert.Equal(new int2(800, 600), viewport.ResolvedViewportSize);
            Assert.Equal(new float3(680f, 530f, 0f), control.LocalPosition);
        }

        /// <summary>Minimizing or closing the overlay keeps its UI stable while the remaining scene continues updating.</summary>
        [Fact]
        public void ViewportRetainsLayoutAfterMinimizeAndClosure() {
            Entity root = CreateCameraRoot(true);
            Entity control = CreateAnchoredControl(root);
            CoreValue.RenderManager3D.OnWindowResize(new IntPtr(2), 0, 0);
            Assert.Equal(new float3(520f, 290f, 0f), control.LocalPosition);
            CoreValue.RenderManager3D.RemoveWindow(new IntPtr(2));
            root.InitializeHierarchy();
            CoreValue.Update(0.016);
            Assert.Equal(new int2(640, 360), GetViewport(root).ResolvedViewportSize);
            Assert.Equal(new float3(520f, 290f, 0f), control.LocalPosition);
        }

        /// <summary>Changing the camera target immediately repositions camera-bound controls into the new view.</summary>
        [Fact]
        public void RetargetingCameraRebindsTheLayout() {
            Entity root = CreateCameraRoot(true);
            Entity control = CreateAnchoredControl(root);
            GetCamera(root).OutputWindowTag = "preview";
            Assert.Equal(new int2(1024, 768), GetViewport(root).ResolvedViewportSize);
            Assert.Equal(new float3(904f, 698f, 0f), control.LocalPosition);
        }

        /// <summary>Anchors without an explicit viewport inherit their nearest camera's window instead of the primary window.</summary>
        [Fact]
        public void FallbackAnchoringUsesTheNearestCamera() {
            Entity root = CreateCameraRoot(false);
            Entity control = CreateAnchoredControl(root);
            Assert.Equal(new float3(520f, 290f, 0f), control.LocalPosition);
            CoreValue.RenderManager3D.OnWindowResize(new IntPtr(2), 800, 600);
            Assert.Equal(new float3(680f, 530f, 0f), control.LocalPosition);
            GetCamera(root).OutputWindowTag = "preview";
            Assert.Equal(new float3(904f, 698f, 0f), control.LocalPosition);
        }

        /// <summary>Reference-canvas fitting scales the immutable authored subtree against its own viewport and camera target.</summary>
        [Fact]
        public void ReferenceCanvasFollowsTheCameraSurface() {
            Entity root = CreateCameraRoot(true);
            Entity fitRoot = CreateEntity();
            root.AddChild(fitRoot);
            Entity control = CreateEntity();
            control.LocalPosition = new float3(100f, 40f, 0f);
            RoundedRectComponent rectangle = new RoundedRectComponent { Size = new int2(200, 100) };
            control.AddComponent(rectangle);
            fitRoot.AddChild(control);
            ReferenceCanvasFitComponent fit = new ReferenceCanvasFitComponent { ReferenceWidth = 1280, ReferenceHeight = 720 };
            fitRoot.AddComponent(fit);
            root.InitializeHierarchy();
            CoreValue.Update(0.016);
            Assert.Equal(new int2(100, 50), rectangle.Size);
            Assert.Equal(new float3(50f, 20f, 0f), control.LocalPosition);
            CoreValue.RenderManager3D.OnWindowResize(IntPtr.Zero, 1920, 1080);
            CoreValue.Update(0.016);
            Assert.Equal(new int2(100, 50), rectangle.Size);
            GetCamera(root).OutputWindowTag = "preview";
            CoreValue.Update(0.016);
            Assert.Equal(new int2(160, 80), rectangle.Size);
            Assert.Equal(new int2(1024, 576), fit.AnchorSpace.Size);
            Assert.Equal(new float3(0f, 96f, 0f), fitRoot.LocalPosition);
        }

        /// <summary>Explicit camera object initializers acquire their host subscriptions when the viewport attaches to its owning core.</summary>
        [Fact]
        public void ExplicitCameraBindingResizesAfterObjectInitializerAttachment() {
            Entity cameraRoot = CreateCameraRoot(false);
            Entity uiRoot = CreateEntity();
            Roots.Add(uiRoot);
            ViewportComponent viewport = new ViewportComponent {
                BindingMode = ViewportComponent.ExplicitCameraBindingMode,
                BoundCameraComponent = GetCamera(cameraRoot)
            };
            uiRoot.AddComponent(viewport);
            Entity control = CreateAnchoredControl(uiRoot);
            Assert.Equal(new float3(520f, 290f, 0f), control.LocalPosition);
            CoreValue.RenderManager3D.OnWindowResize(new IntPtr(2), 800, 600);
            Assert.Equal(new float3(680f, 530f, 0f), control.LocalPosition);
        }

        /// <summary>A shared camera deliberately keeps the primary UI reference while secondary clients change independently.</summary>
        [Fact]
        public void SharedCameraPreservesPrimaryLayoutPolicy() {
            Entity root = CreateCameraRoot(true);
            Entity control = CreateAnchoredControl(root);
            GetCamera(root).OutputWindowTag = string.Empty;
            Assert.Equal(new float3(1160f, 650f, 0f), control.LocalPosition);
            CoreValue.RenderManager3D.OnWindowResize(new IntPtr(2), 800, 600);
            Assert.Equal(new float3(1160f, 650f, 0f), control.LocalPosition);
            CoreValue.RenderManager3D.OnWindowResize(IntPtr.Zero, 900, 600);
            Assert.Equal(new float3(780f, 530f, 0f), control.LocalPosition);
        }

        /// <summary>A camera target with no declared layout window cannot silently borrow primary dimensions.</summary>
        [Fact]
        public void UndeclaredLayoutWindowFailsExplicitly() {
            Entity root = CreateCameraRoot(false);
            GetCamera(root).OutputWindowTag = "missing";
            ViewportComponent viewport = new ViewportComponent { BindingMode = ViewportComponent.AncestorCameraBindingMode };
            root.AddComponent(viewport);
            Assert.Throws<InvalidOperationException>(() => viewport.ResolvedViewportSize);
        }

        /// <summary>Retrieves the unique camera explicitly authored by this fixture.</summary>
        /// <param name="root">The camera hierarchy being checked.</param>
        /// <returns>The existing camera; an incomplete fixture fails the assertion.</returns>
        static CameraComponent GetCamera(Entity root) {
            return Assert.Single(root.Components.OfType<CameraComponent>());
        }

        /// <summary>Retrieves the unique viewport explicitly authored by this fixture.</summary>
        /// <param name="root">The hierarchy whose viewport is required.</param>
        /// <returns>The existing viewport; an incomplete fixture fails the assertion.</returns>
        static ViewportComponent GetViewport(Entity root) {
            return Assert.Single(root.Components.OfType<ViewportComponent>());
        }

        /// <summary>Creates a registered overlay camera with an optional normalized camera-bound viewport.</summary>
        /// <param name="includeViewport">Whether the layout has an explicit viewport provider.</param>
        /// <returns>A hierarchy owned by this test instance.</returns>
        Entity CreateCameraRoot(bool includeViewport) {
            Entity root = CreateEntity();
            Roots.Add(root);
            root.AddComponent(new CameraComponent { OutputWindowTag = "glass" });
            if (includeViewport) {
                root.AddComponent(new ViewportComponent { BindingMode = ViewportComponent.AncestorCameraBindingMode });
            }
            return root;
        }

        /// <summary>Authors a 100x50 control with explicit right and bottom distances in its selected layout space.</summary>
        /// <param name="root">Camera hierarchy that owns the control.</param>
        /// <returns>The owned control entity whose resolved local position can be checked.</returns>
        Entity CreateAnchoredControl(Entity root) {
            Entity control = CreateEntity();
            control.AddComponent(new RoundedRectComponent { Size = new int2(100, 50) });
            LayoutComponent layout = new LayoutComponent();
            control.AddComponent(layout);
            root.AddChild(control);
            layout.SetAnchorDistances(right: 20f, bottom: 20f);
            return control;
        }

        /// <summary>Initializes child and component storage before an entity participates in layout.</summary>
        /// <returns>A test-owned entity ready for hierarchy attachment.</returns>
        Entity CreateEntity() {
            Entity entity = new Entity(CoreValue);
            entity.InitComponents();
            entity.InitChildren();
            return entity;
        }
    }
}
