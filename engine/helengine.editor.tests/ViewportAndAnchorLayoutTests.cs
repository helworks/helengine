using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies viewport-bound anchor layout behavior for responsive scene authoring.
    /// </summary>
    public sealed class ViewportAndAnchorLayoutTests : IDisposable {
        /// <summary>
        /// Initializes the core services required by the layout tests.
        /// </summary>
        public ViewportAndAnchorLayoutTests() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend());
        }

        /// <summary>
        /// Releases the active core instance after each test run.
        /// </summary>
        public void Dispose() {
            Core.Instance?.Dispose();
        }

        /// <summary>
        /// Ensures anchors follow a viewport component that binds to the current screen size.
        /// </summary>
        [Fact]
        public void AnchorComponent_WhenViewportBindsToScreen_RepositionsWhenTheWindowResizes() {
            TestRenderManager3D renderManager = (TestRenderManager3D)Core.Instance.RenderManager3D;
            renderManager.OnWindowResize(IntPtr.Zero, 640, 480);

            Entity viewportEntity = new Entity();
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            viewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.ScreenBindingMode
            });

            Entity contentEntity = new Entity {
                LocalPosition = new float3(530f, 390f, 0f)
            };
            contentEntity.InitComponents();
            contentEntity.InitChildren();
            contentEntity.AddComponent(new RoundedRectComponent {
                Size = new int2(100, 50)
            });
            contentEntity.AddComponent(new AnchorComponent());
            viewportEntity.AddChild(contentEntity);

            AnchorComponent anchor = Assert.IsType<AnchorComponent>(Assert.Single(contentEntity.Components, component => component is AnchorComponent));
            anchor.EnableAnchoring(right: true, bottom: true);

            Assert.Equal(new float3(530f, 390f, 0f), contentEntity.LocalPosition);

            renderManager.OnWindowResize(IntPtr.Zero, 800, 600);

            Assert.Equal(new float3(690f, 510f, 0f), contentEntity.LocalPosition);
        }

        /// <summary>
        /// Ensures anchors follow a viewport component that binds to an ancestor camera component.
        /// </summary>
        [Fact]
        public void AnchorComponent_WhenViewportBindsToAncestorCamera_RepositionsWhenTheCameraViewportChanges() {
            Entity cameraEntity = new Entity();
            cameraEntity.InitComponents();
            cameraEntity.InitChildren();
            CameraComponent camera = new CameraComponent {
                Viewport = new float4(0f, 0f, 320f, 180f)
            };
            cameraEntity.AddComponent(camera);

            Entity viewportEntity = new Entity();
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            viewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.AncestorCameraBindingMode
            });
            cameraEntity.AddChild(viewportEntity);

            Entity contentEntity = new Entity {
                LocalPosition = new float3(200f, 110f, 0f)
            };
            contentEntity.InitComponents();
            contentEntity.InitChildren();
            contentEntity.AddComponent(new RoundedRectComponent {
                Size = new int2(100, 50)
            });
            contentEntity.AddComponent(new AnchorComponent());
            viewportEntity.AddChild(contentEntity);

            AnchorComponent anchor = Assert.IsType<AnchorComponent>(Assert.Single(contentEntity.Components, component => component is AnchorComponent));
            anchor.EnableAnchoring(right: true, bottom: true);

            Assert.Equal(new float3(200f, 110f, 0f), contentEntity.LocalPosition);

            camera.Viewport = new float4(0f, 0f, 640f, 360f);

            Assert.Equal(new float3(520f, 290f, 0f), contentEntity.LocalPosition);
        }

        /// <summary>
        /// Ensures anchored sizing can be resolved from a text component on the same entity.
        /// </summary>
        [Fact]
        public void AnchorComponent_WhenTextComponentProvidesSize_UsesTheTextBoundsForPlacement() {
            Entity viewportEntity = new Entity();
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            viewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode,
                FixedSize = new int2(640, 480)
            });

            Entity contentEntity = new Entity {
                LocalPosition = new float3(540f, 395f, 0f)
            };
            contentEntity.InitComponents();
            contentEntity.InitChildren();
            TextComponent text = new TextComponent {
                Size = new int2(80, 40)
            };
            contentEntity.AddComponent(text);
            contentEntity.AddComponent(new AnchorComponent());
            viewportEntity.AddChild(contentEntity);

            AnchorComponent anchor = Assert.IsType<AnchorComponent>(Assert.Single(contentEntity.Components, component => component is AnchorComponent));
            anchor.EnableAnchoring(right: true, bottom: true);

            Assert.Equal(new float3(540f, 395f, 0f), contentEntity.LocalPosition);
        }

        /// <summary>
        /// Ensures same-aspect widescreen shrink targets preserve normalized anchored placement when reference-canvas fit is active.
        /// </summary>
        [Fact]
        public void AnchorComponent_WhenReferenceCanvasFitUses853x480_Matches1280x720NormalizedPlacement() {
            TestRenderManager3D renderManager = (TestRenderManager3D)Core.Instance.RenderManager3D;
            renderManager.OnWindowResize(IntPtr.Zero, 1280, 720);

            Entity viewportEntity = new Entity();
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            viewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.ScreenBindingMode
            });
            viewportEntity.AddComponent(new ReferenceCanvasFitComponent {
                ReferenceWidth = 1280,
                ReferenceHeight = 720
            });

            Entity contentEntity = new Entity {
                LocalPosition = new float3(88f, 190f, 0f)
            };
            contentEntity.InitComponents();
            contentEntity.InitChildren();
            contentEntity.AddComponent(new RoundedRectComponent {
                Size = new int2(560, 420)
            });
            contentEntity.AddComponent(new AnchorComponent());
            viewportEntity.AddChild(contentEntity);

            AnchorComponent anchor = Assert.IsType<AnchorComponent>(Assert.Single(contentEntity.Components, component => component is AnchorComponent));
            anchor.SetAnchorDistances(left: 88f, top: 190f);

            renderManager.OnWindowResize(IntPtr.Zero, 853, 480);
            Core.Instance.Update();

            AssertFloat3ApproximatelyEqual(new float3(58.64375f, 126.66667f, 0f), contentEntity.LocalPosition, 0.01f);
        }

        /// <summary>
        /// Asserts that two three-component floating-point vectors are equal within a caller-supplied tolerance.
        /// </summary>
        /// <param name="expected">Expected vector value.</param>
        /// <param name="actual">Actual vector value.</param>
        /// <param name="tolerance">Maximum absolute difference allowed for each component.</param>
        static void AssertFloat3ApproximatelyEqual(float3 expected, float3 actual, float tolerance) {
            AssertApproximatelyEqual(expected.X, actual.X, tolerance);
            AssertApproximatelyEqual(expected.Y, actual.Y, tolerance);
            AssertApproximatelyEqual(expected.Z, actual.Z, tolerance);
        }

        /// <summary>
        /// Asserts that two floating-point values are equal within a caller-supplied tolerance.
        /// </summary>
        /// <param name="expected">Expected floating-point value.</param>
        /// <param name="actual">Actual floating-point value.</param>
        /// <param name="tolerance">Maximum absolute difference allowed between the two values.</param>
        static void AssertApproximatelyEqual(float expected, float actual, float tolerance) {
            float difference = Math.Abs(expected - actual);
            Assert.True(
                difference <= tolerance,
                $"Expected {expected} but received {actual}. Difference {difference} exceeded tolerance {tolerance}.");
        }
    }
}
