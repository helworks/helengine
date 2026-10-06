using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies editor-only viewport selection framing behavior for scene-view focus operations.
    /// </summary>
    public sealed class EditorViewportSelectionFramingServiceTests : IDisposable {
        /// <summary>Perspective F applies successive 35 and 30 percent distance reductions to 2D content and retains the original distance for mixed mesh selections.</summary>
        /// <param name="kind">Supported component or hierarchy whose framing distance is measured.</param>
        [Theory]
        [InlineData("sprite")]
        [InlineData("text")]
        [InlineData("panel")]
        [InlineData("container")]
        [InlineData("viewport")]
        [InlineData("mixed")]
        public void FocusSelection_Perspective2DUsesCloserDistance(string kind) {
            CameraComponent camera = CreateSceneCamera();
            EditorViewportCameraController controller = CreateCameraController(camera, out EditorEntity cameraEntity);
            Entity selected = kind == "viewport" ? CreateViewportEntity(new int2(1280, 720)) : new EditorEntity(Core.Instance, InteractionServices);
            if (kind == "sprite") {
                selected.AddComponent(new SpriteComponent { Size = new int2(1280, 720) });
            } else if (kind == "text") {
                selected.AddComponent(new TextComponent { Size = new int2(1280, 720) });
            } else if (kind == "panel" || kind == "mixed") {
                selected.AddComponent(new RoundedRectComponent { Size = new int2(1280, 720) });
                if (kind == "mixed") {
                    TestRuntimeModel model = new TestRuntimeModel();
                    model.SetBounds(float3.Zero, new float3(1280, 720, 0));
                    selected.AddComponent(new MeshComponent { Model = model });
                }
            } else if (kind == "container") {
                Entity child = new EditorEntity(Core.Instance, InteractionServices);
                selected.AddChild(child);
                child.AddComponent(new RoundedRectComponent { Size = new int2(1280, 720) });
            }
            double radius = Math.Sqrt(640.0 * 640.0 + 360.0 * 360.0);
            double originalDistance = radius * 1.1 / Math.Sin(camera.FieldOfView * 0.5);

            new EditorViewportSelectionFramingService().FocusSelection(camera, controller, selected);

            double distance = float3.Distance(cameraEntity.Position, controller.GetOrbitTarget());
            double expectedFactor = kind == "mixed" ? 1.0 : 0.455;
            Assert.InRange(distance / originalDistance, expectedFactor - 0.00001, expectedFactor + 0.00001);
            AssertPointIsVisible(cameraEntity, camera, float3.Zero);
            AssertPointIsVisible(cameraEntity, camera, new float3(1280, kind == "viewport" ? -720 : 720, 0));
        }

        /// <summary>F on a menu container includes visible 2D descendants and excludes inactive panels.</summary>
        [Theory]
        [InlineData(CameraProjectionMode.Perspective)]
        [InlineData(CameraProjectionMode.Orthographic)]
        public void FocusSelection_MenuRootFramesVisibleDescendants(CameraProjectionMode mode) {
            EditorViewportCameraComponent camera = new EditorViewportCameraComponent {
                Viewport = new float4(0, 0, 400, 800), ProjectionMode = mode
            };
            EditorViewportCameraController controller = CreateCameraController(camera, out EditorEntity cameraEntity);
            EditorEntity root = new EditorEntity(Core.Instance, InteractionServices);
            EditorEntity panel = new EditorEntity(Core.Instance, InteractionServices) { LocalPosition = new float3(100, 200, 30) };
            root.AddChild(panel);
            panel.AddComponent(new RoundedRectComponent { Size = new int2(1200, 700) });
            EditorEntity label = new EditorEntity(Core.Instance, InteractionServices) { LocalPosition = new float3(1400, 900, 52) };
            root.AddChild(label);
            label.AddComponent(new TextComponent { Size = new int2(200, 100), Text = "Menu" });
            EditorEntity hidden = new EditorEntity(Core.Instance, InteractionServices) { Enabled = false, LocalPosition = new float3(100000, 0, 0) };
            root.AddChild(hidden);
            hidden.AddComponent(new SpriteComponent { Size = new int2(100, 100) });
            new EditorViewportSelectionFramingService().FocusSelection(camera, controller, root);
            Assert.Equal(new float3(850, 600, 41), controller.GetOrbitTarget());
            foreach (float3 point in new[] { new float3(100, 200, 30), new float3(1300, 900, 30), new float3(1600, 1000, 52) }) {
                if (mode == CameraProjectionMode.Orthographic) {
                    AssertPointIsVisibleOrthographic(cameraEntity, camera, point);
                } else {
                    AssertPointIsVisible(cameraEntity, camera, point);
                }
            }
        }

        /// <summary>Nested mesh bounds include inherited scale when framing their empty root.</summary>
        [Fact]
        public void FocusSelection_ContainerIncludesScaledMeshChild() {
            CameraComponent camera = CreateSceneCamera();
            EditorViewportCameraController controller = CreateCameraController(camera, out EditorEntity cameraEntity);
            EditorEntity root = new EditorEntity(Core.Instance, InteractionServices) { LocalScale = new float3(2, 3, 4) };
            Entity child = CreateMeshEntity();
            root.AddChild(child);
            child.LocalScale = new float3(2, 2, 2);
            new EditorViewportSelectionFramingService().FocusSelection(camera, controller, root);
            Assert.Equal(new float3(84, 252, 504), controller.GetOrbitTarget());
            AssertPointIsVisible(cameraEntity, camera, new float3(88, 264, 528));
            AssertPointIsVisible(cameraEntity, camera, new float3(80, 240, 480));
        }

        /// <summary>Checks that averaging large finite bounds does not overflow the focus center.</summary>
        [Fact]
        public void FocusSelection_WithLargeFiniteCenter_PreservesFiniteCameraAndPivot() {
            CameraComponent camera = CreateSceneCamera();
            EditorViewportCameraController controller = CreateCameraController(camera, out EditorEntity cameraEntity);
            EditorEntity selected = new EditorEntity(Core.Instance, InteractionServices) {
                Position = new float3(3e38f, 3e38f, 0f)
            };
            selected.AddComponent(new SpriteComponent { Size = new int2(64, 96) });

            new EditorViewportSelectionFramingService().FocusSelection(camera, controller, selected);

            Assert.Equal(selected.Position, controller.GetOrbitTarget());
            Assert.True(float.IsFinite(cameraEntity.Position.X));
            Assert.True(float.IsFinite(cameraEntity.Position.Y));
            Assert.True(float.IsFinite(cameraEntity.Position.Z));
        }

        /// <summary>Unrepresentable focus bounds leave the existing view and clipping range intact.</summary>
        [Theory]
        [InlineData(CameraProjectionMode.Perspective)]
        [InlineData(CameraProjectionMode.Orthographic)]
        public void FocusSelection_WithUnrepresentableBounds_PreservesCameraState(CameraProjectionMode mode) {
            EditorViewportCameraComponent camera = new EditorViewportCameraComponent {
                Viewport = new float4(0, 0, 1280, 720), ProjectionMode = mode,
                OrthographicVerticalSpan = 20, FarPlaneDistance = 5000
            };
            EditorViewportCameraController controller = CreateCameraController(camera, out EditorEntity cameraEntity);
            controller.SetViewPose(new float3(1, 2, 3), float4.Identity, 10);
            float3 previousPosition = cameraEntity.Position;
            float3 previousPivot = controller.GetOrbitTarget();
            EditorEntity root = new EditorEntity(Core.Instance, InteractionServices);
            foreach (float coordinate in new[] { -3e38f, 3e38f }) {
                EditorEntity child = new EditorEntity(Core.Instance, InteractionServices) {
                    LocalPosition = new float3(coordinate, 0, 0)
                };
                root.AddChild(child);
                child.AddComponent(new SpriteComponent { Size = new int2(64, 96) });
            }

            new EditorViewportSelectionFramingService().FocusSelection(camera, controller, root);

            Assert.Equal(previousPosition, cameraEntity.Position);
            Assert.Equal(previousPivot, controller.GetOrbitTarget());
            Assert.Equal(20f, camera.OrthographicVerticalSpan);
            Assert.Equal(5000f, camera.FarPlaneDistance);
        }

        readonly helengine.editor.EditorSessionInteractionServices InteractionServices = new helengine.editor.EditorSessionInteractionServices();
        /// <summary>
        /// Initializes the core services required by editor viewport framing tests.
        /// </summary>
        public EditorViewportSelectionFramingServiceTests() {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Disposes the active core instance after each framing test.
        /// </summary>
        public void Dispose() {
            InteractionServices.Selection.ClearSelection();
            Core.Instance?.Dispose();
        }

        /// <summary>
        /// Ensures focusing a selected viewport frames all authored viewport corners inside the active scene camera.
        /// </summary>
        [Fact]
        public void FocusSelection_WhenViewportEntityIsSelected_FramesEntireViewport() {
            CameraComponent camera = CreateSceneCamera();
            EditorViewportCameraController controller = CreateCameraController(camera, out EditorEntity cameraEntity);
            Entity viewportEntity = CreateViewportEntity(new int2(1280, 720));
            EditorViewportSelectionFramingService service = new EditorViewportSelectionFramingService();

            service.FocusSelection(camera, controller, viewportEntity);

            Assert.Equal(new float3(640f, -360f, 0f), controller.GetOrbitTarget());
            AssertPointIsVisible(cameraEntity, camera, new float3(0f, 0f, 0f));
            AssertPointIsVisible(cameraEntity, camera, new float3(1280f, 0f, 0f));
            AssertPointIsVisible(cameraEntity, camera, new float3(0f, -720f, 0f));
            AssertPointIsVisible(cameraEntity, camera, new float3(1280f, -720f, 0f));
        }

        /// <summary>
        /// Ensures focusing a very large selected viewport expands the far clip plane when the default editor distance is insufficient.
        /// </summary>
        [Fact]
        public void FocusSelection_WhenViewportIsHuge_ExpandsFarPlaneToFitSelection() {
            CameraComponent camera = CreateSceneCamera();
            EditorViewportCameraController controller = CreateCameraController(camera, out EditorEntity cameraEntity);
            Entity viewportEntity = CreateViewportEntity(new int2(40000, 20000));
            EditorViewportSelectionFramingService service = new EditorViewportSelectionFramingService();

            service.FocusSelection(camera, controller, viewportEntity);

            Assert.True(camera.FarPlaneDistance > 5000f);
            AssertPointIsVisible(cameraEntity, camera, new float3(0f, 0f, 0f));
            AssertPointIsVisible(cameraEntity, camera, new float3(40000f, -20000f, 0f));
        }

        /// <summary>
        /// Ensures focusing a selected mesh uses the mesh bounds center as the orbit target.
        /// </summary>
        [Fact]
        public void FocusSelection_WhenMeshEntityIsSelected_UsesMeshBoundsCenterAsOrbitTarget() {
            CameraComponent camera = CreateSceneCamera();
            EditorViewportCameraController controller = CreateCameraController(camera, out _);
            Entity meshEntity = CreateMeshEntity();
            EditorViewportSelectionFramingService service = new EditorViewportSelectionFramingService();

            service.FocusSelection(camera, controller, meshEntity);

            Assert.Equal(new float3(21f, 42f, 63f), controller.GetOrbitTarget());
        }

        /// <summary>
        /// Ensures orthographic selection framing fits tiny off-origin bounds for portrait and wide viewport shapes.
        /// </summary>
        /// <param name="viewportWidth">Viewport width in pixels.</param>
        /// <param name="viewportHeight">Viewport height in pixels.</param>
        [Theory]
        [InlineData(400, 800)]
        [InlineData(1200, 600)]
        public void FocusSelection_WhenOrthographicAndSelectionIsTiny_UsesAspectCompensatedSpan(int viewportWidth, int viewportHeight) {
            EditorViewportCameraComponent camera = new EditorViewportCameraComponent {
                Viewport = new float4(0f, 0f, viewportWidth, viewportHeight),
                ProjectionMode = CameraProjectionMode.Orthographic,
                OrthographicVerticalSpan = 1f,
                FarPlaneDistance = 5000f
            };
            EditorViewportCameraController controller = CreateCameraController(camera, out EditorEntity cameraEntity);
            cameraEntity.Position = new float3(0f, 0f, 100f);
            controller.SetOrbitTarget(float3.Zero);
            Entity selectedEntity = CreateViewportEntity(new int2(8, 4));
            selectedEntity.LocalPosition = new float3(1000f, 500f, 0f);
            EditorViewportSelectionFramingService service = new EditorViewportSelectionFramingService();
            double expectedRadius = Math.Sqrt(20.0);
            double aspectRatio = (double)viewportWidth / viewportHeight;
            double minimumFittingSpan = 2.0 * expectedRadius * Math.Max(1.0, 1.0 / aspectRatio);

            service.FocusSelection(camera, controller, selectedEntity);

            Assert.Equal(new float3(1004f, 498f, 0f), controller.GetOrbitTarget());
            Assert.True(camera.OrthographicVerticalSpan >= minimumFittingSpan);
            AssertPointIsVisibleOrthographic(cameraEntity, camera, new float3(1000f, 500f, 0f));
            AssertPointIsVisibleOrthographic(cameraEntity, camera, new float3(1008f, 500f, 0f));
            AssertPointIsVisibleOrthographic(cameraEntity, camera, new float3(1000f, 496f, 0f));
            AssertPointIsVisibleOrthographic(cameraEntity, camera, new float3(1008f, 496f, 0f));
        }

        /// <summary>
        /// Ensures a zero-sized viewport cannot move a camera or change its orthographic frame during focus.
        /// </summary>
        [Fact]
        public void FocusSelection_WhenViewportHasZeroDimension_DoesNotChangeCameraOrSpan() {
            EditorViewportCameraComponent camera = new EditorViewportCameraComponent {
                Viewport = new float4(0f, 0f, 400f, 0f),
                ProjectionMode = CameraProjectionMode.Orthographic,
                OrthographicVerticalSpan = 10f
            };
            EditorViewportCameraController controller = CreateCameraController(camera, out EditorEntity cameraEntity);
            cameraEntity.Position = new float3(5f, 6f, 7f);
            float3 initialPosition = cameraEntity.Position;
            Entity selectedEntity = CreateViewportEntity(new int2(8, 4));
            EditorViewportSelectionFramingService service = new EditorViewportSelectionFramingService();

            service.FocusSelection(camera, controller, selectedEntity);

            Assert.Equal(initialPosition, cameraEntity.Position);
            Assert.Equal(10f, camera.OrthographicVerticalSpan);
        }

        /// <summary>
        /// Ensures viewport selection extent resolves from the full fixed viewport size.
        /// </summary>
        [Fact]
        public void ResolveSelectionExtent_WhenViewportEntityIsSelected_UsesResolvedViewportSize() {
            Entity viewportEntity = CreateViewportEntity(new int2(1280, 720));
            EditorViewportSelectionFramingService service = new EditorViewportSelectionFramingService();

            double selectionExtent = service.ResolveSelectionExtentForTest(viewportEntity);

            Assert.Equal(1280.0, selectionExtent);
        }

        /// <summary>
        /// Ensures mesh selection extent resolves from the largest scaled model dimension.
        /// </summary>
        [Fact]
        public void ResolveSelectionExtent_WhenMeshEntityIsSelected_UsesLargestScaledModelDimension() {
            TestRuntimeModel runtimeModel = new TestRuntimeModel();
            runtimeModel.SetBounds(new float3(-1f, -2f, -3f), new float3(3f, 4f, 5f));
            Entity meshEntity = new Entity(Core.Instance);
            meshEntity.InitComponents();
            meshEntity.InitChildren();
            meshEntity.LocalScale = new float3(2f, 3f, 4f);
            meshEntity.AddComponent(new MeshComponent {
                Model = runtimeModel
            });
            EditorViewportSelectionFramingService service = new EditorViewportSelectionFramingService();

            double selectionExtent = service.ResolveSelectionExtentForTest(meshEntity);

            Assert.Equal(32.0, selectionExtent);
        }

        /// <summary>
        /// Ensures sprite selection extent resolves from the largest sprite dimension.
        /// </summary>
        [Fact]
        public void ResolveSelectionExtent_WhenSpriteEntityIsSelected_UsesLargestSpriteDimension() {
            Entity spriteEntity = new Entity(Core.Instance);
            spriteEntity.InitComponents();
            spriteEntity.InitChildren();
            spriteEntity.AddComponent(new SpriteComponent {
                Size = new int2(64, 96)
            });
            EditorViewportSelectionFramingService service = new EditorViewportSelectionFramingService();

            double selectionExtent = service.ResolveSelectionExtentForTest(spriteEntity);

            Assert.Equal(96.0, selectionExtent);
        }

        /// <summary>
        /// Ensures unsupported selections report zero extent so callers can fall back cleanly.
        /// </summary>
        [Fact]
        public void ResolveSelectionExtent_WhenEntityHasNoSupportedBounds_ReturnsZero() {
            Entity entity = new Entity(Core.Instance);
            entity.InitComponents();
            entity.InitChildren();
            EditorViewportSelectionFramingService service = new EditorViewportSelectionFramingService();

            double selectionExtent = service.ResolveSelectionExtentForTest(entity);

            Assert.Equal(0.0, selectionExtent);
        }

        /// <summary>
        /// Creates one standard scene camera used by framing tests.
        /// </summary>
        /// <returns>Configured camera component.</returns>
        CameraComponent CreateSceneCamera() {
            CameraComponent camera = new CameraComponent();
            camera.Viewport = new float4(0f, 0f, 1280f, 720f);
            camera.FarPlaneDistance = 5000f;
            return camera;
        }

        /// <summary>
        /// Creates one scene camera controller hosted on an editor entity.
        /// </summary>
        /// <param name="camera">Scene camera rendered by the controller host.</param>
        /// <param name="cameraEntity">Receives the created camera host entity.</param>
        /// <returns>Configured viewport camera controller.</returns>
        EditorViewportCameraController CreateCameraController(CameraComponent camera, out EditorEntity cameraEntity) {
            cameraEntity = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices());
            cameraEntity.AddComponent(camera);

            EditorViewportCameraController controller = new EditorViewportCameraController(camera, Core.Instance.Input);
            cameraEntity.AddComponent(controller);
            return controller;
        }

        /// <summary>
        /// Creates one authored viewport entity with a fixed viewport size.
        /// </summary>
        /// <param name="viewportSize">Authored viewport size in pixels.</param>
        /// <returns>Configured authored viewport entity.</returns>
        Entity CreateViewportEntity(int2 viewportSize) {
            Entity viewportEntity = new Entity(Core.Instance);
            viewportEntity.InitComponents();
            viewportEntity.InitChildren();
            viewportEntity.AddComponent(new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode,
                FixedSize = viewportSize
            });
            return viewportEntity;
        }

        /// <summary>
        /// Creates one mesh entity with deterministic model bounds.
        /// </summary>
        /// <returns>Configured mesh entity.</returns>
        Entity CreateMeshEntity() {
            TestRuntimeModel runtimeModel = new TestRuntimeModel();
            runtimeModel.SetBounds(new float3(20f, 40f, 60f), new float3(22f, 44f, 66f));

            Entity meshEntity = new Entity(Core.Instance);
            meshEntity.InitComponents();
            meshEntity.InitChildren();
            meshEntity.AddComponent(new MeshComponent {
                Model = runtimeModel
            });
            return meshEntity;
        }

        /// <summary>
        /// Asserts that one world-space point projects inside the active scene camera viewport.
        /// </summary>
        /// <param name="cameraEntity">Entity that owns the scene camera transform.</param>
        /// <param name="camera">Scene camera used to project the point.</param>
        /// <param name="worldPoint">World-space point that should remain visible.</param>
        void AssertPointIsVisible(EditorEntity cameraEntity, CameraComponent camera, float3 worldPoint) {
            double verticalFieldOfView = Math.PI / 4.0;
            float4 viewport = camera.Viewport;
            double aspectRatio = viewport.Z / viewport.W;
            double horizontalFieldOfView = 2.0 * Math.Atan(Math.Tan(verticalFieldOfView * 0.5) * aspectRatio);
            float3 relativePoint = worldPoint - cameraEntity.Position;
            float4 inverseOrientation = float4.Inverse(cameraEntity.Orientation);
            float3 viewPoint = float4.RotateVector(relativePoint, inverseOrientation);
            double depth = Math.Max(-(double)viewPoint.Z, 0.0001);
            double halfHorizontal = Math.Abs(viewPoint.X) / depth;
            double halfVertical = Math.Abs(viewPoint.Y) / depth;

            Assert.True(viewPoint.Z < 0f);
            Assert.True(halfHorizontal <= Math.Tan(horizontalFieldOfView * 0.5) + 0.0001);
            Assert.True(halfVertical <= Math.Tan(verticalFieldOfView * 0.5) + 0.0001);
        }

        /// <summary>
        /// Asserts that a point lies within the active orthographic horizontal and vertical extents.
        /// </summary>
        /// <param name="cameraEntity">Entity that owns the scene camera transform.</param>
        /// <param name="camera">Camera whose orthographic span defines the view.</param>
        /// <param name="worldPoint">World-space point that should remain visible.</param>
        void AssertPointIsVisibleOrthographic(EditorEntity cameraEntity, EditorViewportCameraComponent camera, float3 worldPoint) {
            float4 viewport = camera.Viewport;
            double aspectRatio = viewport.Z / viewport.W;
            float3 relativePoint = worldPoint - cameraEntity.Position;
            float4 inverseOrientation = float4.Inverse(cameraEntity.Orientation);
            float3 viewPoint = float4.RotateVector(relativePoint, inverseOrientation);
            double halfVerticalSpan = camera.OrthographicVerticalSpan * 0.5;
            double halfHorizontalSpan = halfVerticalSpan * aspectRatio;

            Assert.True(viewPoint.Z < 0f);
            Assert.True(Math.Abs(viewPoint.X) <= halfHorizontalSpan + 0.0001);
            Assert.True(Math.Abs(viewPoint.Y) <= halfVerticalSpan + 0.0001);
        }
    }
}
