namespace helengine.core.tests.components {
    /// <summary>Checks layout ownership transfers and provider lifetimes at cold component mutations.</summary>
    public sealed class ViewportLayoutMutationTests {
        /// <summary>A newly introduced nested owner receives authored values and stays independent of outer resizes.</summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void NewScalingOwner_TransfersOuterParticipants(bool legacyFit, bool enableAfterAttach) {
            using ViewportLayoutTestScene scene = new ViewportLayoutTestScene();
            Entity root = scene.CreateEntity();
            ViewportComponent outer = scene.AddViewport(root, ViewportLayoutProcessingMode.Dynamic);
            Entity nested = scene.CreateEntity();
            RoundedRectComponent rectangle = new RoundedRectComponent { Size = new int2(100, 40) };
            nested.AddComponent(rectangle);
            root.AddChild(nested);
            outer.Update();
            Assert.Equal(new int2(50, 20), rectangle.Size);
            if (legacyFit) {
                ReferenceCanvasFitComponent fit = new ReferenceCanvasFitComponent { ReferenceWidth = 1280, ReferenceHeight = 960 };
                nested.AddComponent(fit);
                fit.Update();
            } else {
                ViewportComponent inner = new ViewportComponent {
                    ScalingMode = enableAfterAttach ? ViewportComponent.NoScalingMode : ViewportComponent.ReferenceCanvasStretchScalingMode,
                    ReferenceWidth = 1280, ReferenceHeight = 960, LayoutProcessingMode = ViewportLayoutProcessingMode.OnLoad
                };
                nested.AddComponent(inner);
                inner.ScalingMode = ViewportComponent.ReferenceCanvasStretchScalingMode;
                inner.Update();
            }
            Assert.Equal(new int2(25, 10), rectangle.Size);
            outer.Update();
            Assert.Equal(1, outer.RetainedLayoutPositionCount);
            Assert.Equal(new int2(25, 10), rectangle.Size);
            scene.Meshes.OnWindowResize(IntPtr.Zero, 640, 480);
            outer.Update();
            Assert.Equal(new int2(25, 10), rectangle.Size);
        }

        /// <summary>Removing a viewport releases inherited policy and anchor aliases before the provider is destroyed.</summary>
        [Fact]
        public void RemovedViewport_RebindsFitAndAnchorsBeforeDisposal() {
            using ViewportLayoutTestScene scene = new ViewportLayoutTestScene();
            Entity root = scene.CreateEntity();
            ViewportComponent viewport = new ViewportComponent { LayoutProcessingMode = ViewportLayoutProcessingMode.OnLoad };
            root.AddComponent(viewport);
            Entity child = scene.CreateEntity();
            root.AddChild(child);
            ReferenceCanvasFitComponent fit = new ReferenceCanvasFitComponent { ReferenceWidth = 640, ReferenceHeight = 480 };
            child.AddComponent(fit);
            Entity anchored = scene.CreateEntity();
            root.AddChild(anchored);
            anchored.AddComponent(new RoundedRectComponent { Size = new int2(100, 40) });
            LayoutComponent layout = new LayoutComponent();
            anchored.AddComponent(layout);
            layout.SetAnchorDistances(right: 0);
            viewport.Update();
            fit.Update();
            root.RemoveComponent(viewport);
            Assert.Equal(ViewportLayoutProcessingMode.Dynamic, fit.EffectiveLayoutProcessingMode);
            viewport.Dispose();
            layout.RefreshAnchoring();
            fit.Update();
        }

        /// <summary>Camera attachment and removal invalidate prepared Dynamic descendants without waiting for a resize.</summary>
        [Fact]
        public void CameraMutation_RebindsAncestorViewport() {
            using ViewportLayoutTestScene scene = new ViewportLayoutTestScene();
            Entity root = scene.CreateEntity();
            Entity child = scene.CreateEntity();
            root.AddChild(child);
            ViewportComponent viewport = scene.AddViewport(child, ViewportLayoutProcessingMode.Dynamic);
            viewport.BindingMode = ViewportComponent.AncestorCameraBindingMode;
            viewport.FixedSize = new int2(320, 240);
            RoundedRectComponent rectangle = new RoundedRectComponent { Size = new int2(100, 40) };
            child.AddComponent(rectangle);
            viewport.Update();
            Assert.Equal(new int2(50, 20), rectangle.Size);
            CameraComponent camera = new CameraComponent { Viewport = new float4(0, 0, 160, 120) };
            root.AddComponent(camera);
            viewport.Update();
            Assert.Equal(new int2(25, 10), rectangle.Size);
            root.RemoveComponent(camera);
            camera.Dispose();
            viewport.Update();
            Assert.Equal(new int2(50, 20), rectangle.Size);
        }

        /// <summary>Explicit camera references in a different registered root are cleared before native deletion.</summary>
        [Fact]
        public void RemovedCamera_ClearsExplicitReferenceAcrossRoots() {
            using ViewportLayoutTestScene scene = new ViewportLayoutTestScene();
            Entity cameraRoot = scene.CreateEntity();
            CameraComponent camera = new CameraComponent();
            cameraRoot.AddComponent(camera);
            Entity uiRoot = scene.CreateEntity();
            ViewportComponent viewport = new ViewportComponent {
                BindingMode = ViewportComponent.ExplicitCameraBindingMode, BoundCameraComponent = camera
            };
            uiRoot.AddComponent(viewport);
            cameraRoot.RemoveComponent(camera);
            Assert.Null(viewport.BoundCameraComponent);
            camera.Dispose();
            viewport.Update();
        }
    }
}
