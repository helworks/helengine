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
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Releases the active core instance after each test run.
        /// </summary>
        public void Dispose() {
            EditorWorldSpace2DPreviewRegistry.Clear();
            EditorWorldSpace2DPreviewMeshResources.ResetForTests();
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
        /// Ensures camera-bound viewports expose the resolved viewport size needed by direct 2D-in-3D presentation.
        /// </summary>
        [Fact]
        public void ViewportComponent_WhenViewportBindsToAncestorCamera_ExposesResolvedViewportSize() {
            Entity cameraEntity = new Entity();
            cameraEntity.InitComponents();
            cameraEntity.InitChildren();
            CameraComponent camera = new CameraComponent {
                Viewport = new float4(0f, 0f, 1280f, 720f)
            };
            cameraEntity.AddComponent(camera);

            Entity viewportEntity = new Entity();
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            ViewportComponent viewport = new ViewportComponent {
                BindingMode = ViewportComponent.AncestorCameraBindingMode
            };
            viewportEntity.AddComponent(viewport);
            cameraEntity.AddChild(viewportEntity);

            Core.Instance.Update();

            Assert.Equal(new int2(1280, 720), viewport.ResolvedViewportSize);
        }

        /// <summary>
        /// Ensures camera-bound viewports raise one bounds-change notification when the driving camera viewport changes.
        /// </summary>
        [Fact]
        public void ViewportComponent_WhenBoundCameraViewportChanges_RaisesAnchorBoundsChangedOnce() {
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
            ViewportComponent viewport = new ViewportComponent {
                BindingMode = ViewportComponent.AncestorCameraBindingMode
            };
            viewportEntity.AddComponent(viewport);
            cameraEntity.AddChild(viewportEntity);

            int anchorBoundsChangedCount = 0;
            viewport.AnchorBoundsChanged += () => anchorBoundsChangedCount++;

            Core.Instance.Update();
            anchorBoundsChangedCount = 0;

            camera.Viewport = new float4(0f, 0f, 640f, 360f);

            Assert.Equal(1, anchorBoundsChangedCount);
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
        /// Ensures anchors can resolve against an explicitly assigned camera instead of relying on ancestor camera lookup.
        /// </summary>
        [Fact]
        public void AnchorComponent_WhenViewportBindsToExplicitCamera_UsesTheTargetCameraViewport() {
            Entity leftCameraEntity = new Entity();
            leftCameraEntity.InitComponents();
            leftCameraEntity.InitChildren();
            CameraComponent leftCamera = new CameraComponent {
                Viewport = new float4(0f, 0f, 320f, 180f)
            };
            leftCameraEntity.AddComponent(leftCamera);

            Entity rightCameraEntity = new Entity();
            rightCameraEntity.InitComponents();
            rightCameraEntity.InitChildren();
            CameraComponent rightCamera = new CameraComponent {
                Viewport = new float4(0f, 0f, 640f, 360f)
            };
            rightCameraEntity.AddComponent(rightCamera);

            Entity viewportEntity = new Entity();
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            viewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.ExplicitCameraBindingMode,
                BoundCameraComponent = rightCamera
            });

            Entity contentEntity = new Entity {
                LocalPosition = new float3(520f, 290f, 0f)
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

            Assert.Equal(new float3(520f, 290f, 0f), contentEntity.LocalPosition);
        }

        /// <summary>
        /// Ensures camera-bound viewports resolve normalized camera rectangles into pixel-space bounds before anchored layout runs.
        /// </summary>
        [Fact]
        public void AnchorComponent_WhenViewportBindsToNormalizedAncestorCamera_UsesResolvedPixelBounds() {
            TestRenderManager3D renderManager = (TestRenderManager3D)Core.Instance.RenderManager3D;
            renderManager.OnWindowResize(IntPtr.Zero, 256, 192);

            Entity cameraEntity = new Entity();
            cameraEntity.InitComponents();
            cameraEntity.InitChildren();
            CameraComponent camera = new CameraComponent {
                Viewport = new float4(0f, 1f, 1f, 1f)
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
                LocalPosition = new float3(156f, 142f, 0f)
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

            Assert.Equal(new float3(156f, 142f, 0f), contentEntity.LocalPosition);
        }

        /// <summary>
        /// Ensures camera-bound viewport subtrees only populate the render queue of the camera that owns each subtree.
        /// </summary>
        [Fact]
        public void RenderQueues_WhenViewportBindsToDifferentAncestorCameras_OnlyReceiveTheirOwnSubtrees() {
            Entity topCameraEntity = new Entity();
            topCameraEntity.InitComponents();
            topCameraEntity.InitChildren();
            CameraComponent topCamera = new CameraComponent {
                Viewport = new float4(0f, 0f, 1f, 1f)
            };
            topCameraEntity.AddComponent(topCamera);

            Entity bottomCameraEntity = new Entity();
            bottomCameraEntity.InitComponents();
            bottomCameraEntity.InitChildren();
            CameraComponent bottomCamera = new CameraComponent {
                Viewport = new float4(0f, 1f, 1f, 1f)
            };
            bottomCameraEntity.AddComponent(bottomCamera);

            Entity topViewportEntity = new Entity();
            topViewportEntity.InitComponents();
            topViewportEntity.InitChildren();
            topViewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.AncestorCameraBindingMode
            });
            topCameraEntity.AddChild(topViewportEntity);

            Entity bottomViewportEntity = new Entity();
            bottomViewportEntity.InitComponents();
            bottomViewportEntity.InitChildren();
            bottomViewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.AncestorCameraBindingMode
            });
            bottomCameraEntity.AddChild(bottomViewportEntity);

            Entity topDrawableEntity = new Entity();
            topDrawableEntity.InitComponents();
            topDrawableEntity.InitChildren();
            topDrawableEntity.AddComponent(new RoundedRectComponent {
                Size = new int2(32, 32)
            });
            topViewportEntity.AddChild(topDrawableEntity);

            Entity bottomDrawableEntity = new Entity();
            bottomDrawableEntity.InitComponents();
            bottomDrawableEntity.InitChildren();
            bottomDrawableEntity.AddComponent(new RoundedRectComponent {
                Size = new int2(32, 32)
            });
            bottomViewportEntity.AddChild(bottomDrawableEntity);

            Assert.Equal(1, topCamera.RenderQueue2D.Count);
            Assert.Equal(1, bottomCamera.RenderQueue2D.Count);
        }

        /// <summary>
        /// Ensures viewport-owned reference-canvas scaling reuses the same anchor-space instance across updates instead of replacing it.
        /// </summary>
        [Fact]
        public void ViewportComponent_WhenReferenceCanvasScalingUpdates_ReusesTheExistingAnchorSpaceInstance() {
            TestRenderManager3D renderManager = (TestRenderManager3D)Core.Instance.RenderManager3D;
            renderManager.OnWindowResize(IntPtr.Zero, 1280, 720);

            Entity viewportEntity = new Entity();
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            ViewportComponent viewport = new ViewportComponent {
                BindingMode = ViewportComponent.ScreenBindingMode,
                ScalingMode = ViewportComponent.ReferenceCanvasScalingMode,
                ReferenceWidth = 1280,
                ReferenceHeight = 720
            };
            viewportEntity.AddComponent(viewport);

            AnchorSpace initialAnchorSpace = viewport.AnchorSpace;

            renderManager.OnWindowResize(IntPtr.Zero, 640, 480);
            Core.Instance.Update();

            Assert.Same(initialAnchorSpace, viewport.AnchorSpace);
            Assert.Equal(new int2(640, 360), viewport.AnchorSpace.Size);
        }

        /// <summary>
        /// Ensures ordinary 2D scene entities without one viewport-owner subtree receive world-space preview proxies.
        /// </summary>
        [Fact]
        public void SceneViewPreview_WhenSourceEntityHasNoViewportOwner_UsesRealWorldTransform() {
            Entity sourceEntity = new Entity {
                LocalPosition = new float3(12f, 34f, 56f)
            };
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            sourceEntity.AddComponent(new SpriteComponent {
                Size = new int2(80, 40),
                Texture = TextureUtils.PixelTexture
            });

            EditorEntity syncHostEntity = new EditorEntity();
            EditorWorldSpace2DPreviewSyncComponent syncComponent = new EditorWorldSpace2DPreviewSyncComponent();
            syncHostEntity.AddComponent(syncComponent);

            syncComponent.Update();

            EditorEntity previewEntity = EditorWorldSpace2DPreviewRegistry.ResolvePreviewEntity(sourceEntity);
            Assert.NotNull(previewEntity);
            Assert.False(EditorViewportDirect2DPresentationService.ShouldKeepViewportLockBehavior(sourceEntity));
            Assert.Equal(sourceEntity.Position, previewEntity.Position);
        }

        /// <summary>
        /// Ensures authored 2D entities inside one viewport-owned subtree still receive world-space preview proxies in scene view.
        /// </summary>
        [Fact]
        public void SceneViewPreview_WhenSourceEntityIsInsideViewportSubtree_StillUsesWorldSpacePreview() {
            Entity viewportEntity = new Entity();
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            viewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.ScreenBindingMode
            });

            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();
            sourceEntity.AddComponent(new SpriteComponent {
                Size = new int2(80, 40),
                Texture = TextureUtils.PixelTexture
            });
            viewportEntity.AddChild(sourceEntity);

            EditorEntity syncHostEntity = new EditorEntity();
            EditorWorldSpace2DPreviewSyncComponent syncComponent = new EditorWorldSpace2DPreviewSyncComponent();
            syncHostEntity.AddComponent(syncComponent);

            syncComponent.Update();

            Assert.False(EditorViewportDirect2DPresentationService.ShouldKeepViewportLockBehavior(sourceEntity));
            Assert.NotNull(EditorWorldSpace2DPreviewRegistry.ResolvePreviewEntity(sourceEntity));
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
