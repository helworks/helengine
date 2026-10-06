using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.components.ui {
    /// <summary>
    /// Verifies the viewport navigation cube allocates and releases its private 2D overlay resources.
    /// </summary>
    public sealed class EditorViewportNavigationCubeTests : IDisposable {
        readonly Core CoreValue;
        readonly TestRenderManager2D RenderManager2DValue;
        readonly TestInputBackend InputBackendValue;
        readonly EditorSessionInteractionServices InteractionServicesValue;
        readonly EditorEntity ViewportEntityValue;
        readonly EditorEntity CameraEntityValue;
        readonly EditorViewportCameraComponent CameraValue;
        readonly EditorViewportCameraController CameraControllerValue;
        readonly EditorViewportNavigationController NavigationControllerValue;
        readonly FontAsset FontValue;
        /// <summary>Shader library shared by the test cube's GPU resources.</summary>
        readonly EditorBuiltInShaderAssetLibrary Shaders = TestGeneratedAssetGraph.CreateShaderLibrary();

        /// <summary>The displayed cube comes from a real six-face GPU scene with an isolated camera.</summary>
        [Fact]
        public void Initialize_CreatesPhysicalCubeAndGpuTarget() {
            using EditorViewportNavigationCube cube = CreateCube();
            cube.Initialize();
            EditorEntity root = Assert.Single(CoreValue.ObjectManager.Entities.OfType<EditorEntity>(), entity => entity.Name == "Navigation Cube Root");
            Assert.IsAssignableFrom<RenderTarget>(root.Components.OfType<SpriteComponent>().First().Texture);
            EditorEntity cameraEntity = Assert.Single(CoreValue.ObjectManager.Entities.OfType<EditorEntity>(), entity => entity.Name == "Navigation Cube Camera");
            CameraComponent camera = Assert.Single(cameraEntity.Components.OfType<CameraComponent>());
            EditorEntity unrelatedPreview = new EditorEntity(CoreValue, InteractionServicesValue) {
                InternalEntity = true, LayerMask = EditorLayerMasks.SceneModelPreview
            };
            unrelatedPreview.AddComponent(new MeshComponent());
            Assert.Equal(6, camera.RenderQueue3D.Count);
        }

        /// <summary>The private camera and pointer classifier follow projection changes in both directions.</summary>
        [Fact]
        public void Update_TracksViewportProjection() {
            using EditorViewportNavigationCube cube = CreateCube();
            cube.Initialize();
            EditorEntity cameraEntity = Assert.Single(CoreValue.ObjectManager.Entities.OfType<EditorEntity>(), entity => entity.Name == "Navigation Cube Camera");
            EditorViewportCameraComponent camera = Assert.Single(cameraEntity.Components.OfType<EditorViewportCameraComponent>());
            foreach (CameraProjectionMode mode in new[] { CameraProjectionMode.Perspective, CameraProjectionMode.Orthographic, CameraProjectionMode.Perspective }) {
                CameraValue.ProjectionMode = mode;
                cube.Update();
                Assert.Equal(mode, camera.ProjectionMode);
                Assert.Equal(mode, cube.InteractionController.ProjectionMode);
            }
        }

        /// <summary>Creates one isolated session graph for overlay lifetime tests.</summary>
        public EditorViewportNavigationCubeTests() {
            CoreValue = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            RenderManager2DValue = new TestRenderManager2D();
            InputBackendValue = new TestInputBackend();
            CoreValue.Initialize(new TestRenderManager3D(), RenderManager2DValue, InputBackendValue, new PlatformInfo("test", "test-version"));
            InteractionServicesValue = new EditorSessionInteractionServices();
            ViewportEntityValue = new EditorEntity(CoreValue, InteractionServicesValue) {
                InternalEntity = true,
                Position = new float3(10f, 30f, 0f)
            };
            CameraEntityValue = new EditorEntity(CoreValue, InteractionServicesValue) {
                InternalEntity = true,
                Position = new float3(0f, 0f, 10f),
                Orientation = float4.Identity
            };
            CameraValue = new EditorViewportCameraComponent { Viewport = new float4(10f, 30f, 800f, 600f) };
            CameraEntityValue.AddComponent(CameraValue);
            CameraControllerValue = new EditorViewportCameraController(CameraValue, CoreValue.Input);
            CameraEntityValue.AddComponent(CameraControllerValue);
            CameraControllerValue.SetViewPose(float3.Zero, float4.Identity, 10.0);
            NavigationControllerValue = new EditorViewportNavigationController(CameraControllerValue);
            FontValue = CreateFont();
        }

        /// <summary>Releases test interaction state and renderer services.</summary>
        public void Dispose() {
            Shaders.Dispose();
            InteractionServicesValue.Dispose();
            CoreValue.Dispose();
            FontValue.Dispose();
        }

        /// <summary>
        /// Ensures initialization allocates one isolated cube texture and disposal releases it and all input blockers.
        /// </summary>
        [Fact]
        public void InitializeAndDispose_CubeOverlay_AllocatesAndReleasesPrivateTextureAndCapture() {
            int buildCountBefore = RenderManager2DValue.BuildTextureFromRawCallCount;
            int releaseCountBefore = RenderManager2DValue.ReleasedTextures.Count;
            EditorViewportNavigationCube cube = CreateCube();
            cube.Initialize();

            Assert.Equal(buildCountBefore + 7, RenderManager2DValue.BuildTextureFromRawCallCount);

            cube.Dispose();

            Assert.Equal(releaseCountBefore + 7, RenderManager2DValue.ReleasedTextures.Count);
            Assert.Single(RenderManager2DValue.ReleasedTextures, texture => texture.Width == 96 && texture.Height == 96);
            Assert.DoesNotContain(RenderManager2DValue.ReleasedTextures, texture => !texture.IsDisposed);
            Assert.False(InteractionServicesValue.InputCapture.IsPointerBlocked(new int2(780, 40)));
            Assert.DoesNotContain(CoreValue.ObjectManager.Entities.OfType<EditorEntity>(), entity =>
                entity.Name == "Navigation Cube Face" || entity.Name == "Navigation Cube Camera");
        }

        /// <summary>
        /// Ensures the updater opts into editor execution so cube interaction survives default component suppression.
        /// </summary>
        [Fact]
        public void Updater_HasExplicitRunInEditorExecutionOptIn() {
            RunInEditorAttribute optIn = typeof(EditorViewportNavigationCubeUpdateComponent)
                .GetCustomAttribute<RunInEditorAttribute>();

            Assert.NotNull(optIn);
        }

        /// <summary>
        /// Ensures the face-on front/top edge produces an obvious destination preview at both supported test scales.
        /// </summary>
        /// <param name="uiScale">UI scale applied to the cube overlay and hit bounds.</param>
        [Theory]
        [InlineData(1f)]
        [InlineData(2f)]
        public void Update_HoveringFrontEdge_ShowsFilledFeedbackAndDestinationWithoutMovingCamera(float uiScale) {
            EditorViewportNavigationCube cube = CreateCube();
            cube.Initialize();
            cube.ApplyUiMetrics(FontValue, new EditorUiMetrics(uiScale));
            byte[] idlePixels = RenderAtPointer(cube, new int2(0, 0));
            float3 initialPosition = CameraEntityValue.Position;
            float4 initialOrientation = CameraEntityValue.Orientation;
            float3 initialPivot = CameraControllerValue.GetOrbitTarget();

            int2 edgePointer = GetScreenPointer(cube, new float2(48f, 12f), uiScale);
            byte[] edgePixels = RenderAtPointer(cube, edgePointer);

            Assert.Equal(new EditorViewportNavigationTarget(0, 1, 1), cube.InteractionController.HoveredTarget);
            Assert.True(CountChangedPixels(idlePixels, edgePixels) >= 300);
            EditorEntity previewRoot = GetDestinationPreviewRoot();
            TextComponent previewText = GetDestinationPreviewText(previewRoot);
            Assert.True(previewRoot.Enabled);
            Assert.Equal("Front / Top", previewText.Text);
            AssertPreviewFitsViewport(previewRoot);
            AssertPreviewBlocksScene(previewRoot);
            Assert.True(InteractionServicesValue.InputCapture.IsPointerBlocked(edgePointer));
            Assert.Equal(initialPosition, CameraEntityValue.Position);
            Assert.Equal(initialOrientation, CameraEntityValue.Orientation);
            Assert.Equal(initialPivot, CameraControllerValue.GetOrbitTarget());
            Assert.Equal(CameraProjectionMode.Perspective, CameraValue.ProjectionMode);

            byte[] leavePixels = RenderAtPointer(cube, new int2(0, 0));

            Assert.Null(cube.InteractionController.HoveredTarget);
            Assert.False(previewRoot.Enabled);
            Assert.Empty(previewText.Text);
            Assert.Equal(idlePixels, leavePixels);
            cube.Dispose();
        }

        /// <summary>
        /// Ensures the diagonal corner preview names all destination faces and highlights its filled hit patch.
        /// </summary>
        /// <param name="uiScale">UI scale applied to the cube overlay and hit bounds.</param>
        [Theory]
        [InlineData(1f)]
        [InlineData(2f)]
        public void Update_HoveringDiagonalCorner_ShowsFilledFeedbackAndThreeFaceDestination(float uiScale) {
            NavigationControllerValue.SelectTarget(new EditorViewportNavigationTarget(1, 1, 1));
            NavigationControllerValue.Advance(0.2);
            EditorViewportNavigationCube cube = CreateCube();
            cube.Initialize();
            cube.ApplyUiMetrics(FontValue, new EditorUiMetrics(uiScale));
            byte[] idlePixels = RenderAtPointer(cube, new int2(0, 0));
            float3 initialPosition = CameraEntityValue.Position;
            float4 initialOrientation = CameraEntityValue.Orientation;
            float3 initialPivot = CameraControllerValue.GetOrbitTarget();
            EditorViewportNavigationCubeGeometry geometry = new EditorViewportNavigationCubeGeometry();
            IReadOnlyList<float2> vertices = geometry.GetProjectedVertices(initialOrientation, EditorViewportNavigationCubeInteractionController.LogicalCubeSize);
            int cornerIndex = FindVisibleCornerOutsideCenter(geometry, initialOrientation);
            Assert.True(cornerIndex >= 0);
            float2 visibleCorner = vertices[cornerIndex];
            Assert.True(geometry.TryHit(visibleCorner, initialOrientation,
                EditorViewportNavigationCubeInteractionController.LogicalCubeSize, out EditorViewportNavigationCubeHit expectedHit));
            Assert.True(expectedHit.Target.IsCorner);

            int2 cornerPointer = GetScreenPointer(cube, visibleCorner, uiScale);
            byte[] cornerPixels = RenderAtPointer(cube, cornerPointer);

            Assert.Equal(expectedHit.Target, cube.InteractionController.HoveredTarget);
            Assert.True(CountChangedPixels(idlePixels, cornerPixels) >= 100);
            EditorEntity previewRoot = GetDestinationPreviewRoot();
            TextComponent previewText = GetDestinationPreviewText(previewRoot);
            Assert.True(previewRoot.Enabled);
            Assert.Equal(BuildDestinationLabel(expectedHit.Target), previewText.Text);
            AssertPreviewFitsViewport(previewRoot);
            AssertPreviewBlocksScene(previewRoot);
            Assert.True(InteractionServicesValue.InputCapture.IsPointerBlocked(cornerPointer));
            Assert.Equal(initialPosition, CameraEntityValue.Position);
            Assert.Equal(initialOrientation, CameraEntityValue.Orientation);
            Assert.Equal(initialPivot, CameraControllerValue.GetOrbitTarget());

            byte[] leavePixels = RenderAtPointer(cube, new int2(0, 0));

            Assert.False(previewRoot.Enabled);
            Assert.Empty(previewText.Text);
            Assert.Equal(idlePixels, leavePixels);
            cube.Dispose();
        }

        /// <summary>Ensures center hover previews the opposite projection mode and draws a distinct center badge.</summary>
        /// <param name="uiScale">UI scale applied to the cube overlay and hit bounds.</param>
        /// <param name="isDiagonal">True when the represented camera orientation is diagonal.</param>
        [Theory]
        [InlineData(1f, false)]
        [InlineData(2f, false)]
        [InlineData(1f, true)]
        [InlineData(2f, true)]
        public void Update_HoveringCenterToggle_ShowsProjectionDestinationAndCenterFeedback(float uiScale, bool isDiagonal) {
            if (isDiagonal) {
                NavigationControllerValue.SelectTarget(new EditorViewportNavigationTarget(1, 1, 1));
                NavigationControllerValue.Advance(0.2);
            }

            EditorViewportNavigationCube cube = CreateCube();
            cube.Initialize();
            cube.ApplyUiMetrics(FontValue, new EditorUiMetrics(uiScale));
            byte[] idlePixels = RenderAtPointer(cube, new int2(0, 0));
            float3 initialPosition = CameraEntityValue.Position;
            float4 initialOrientation = CameraEntityValue.Orientation;
            float3 initialPivot = CameraControllerValue.GetOrbitTarget();
            int2 centerPointer = GetScreenPointer(cube, new float2(48f, 48f), uiScale);

            byte[] hoveredPixels = RenderAtPointer(cube, centerPointer);

            Assert.True(cube.InteractionController.IsCenterToggleHovered);
            Assert.Null(cube.InteractionController.HoveredTarget);
            Assert.Equal("Orthographic", GetDestinationPreviewText(GetDestinationPreviewRoot()).Text);
            Assert.True(GetDestinationPreviewRoot().Enabled);
            Assert.True(CountChangedPixels(idlePixels, hoveredPixels) >= 100);
            int centerPixel = ((48 * EditorViewportNavigationCubeInteractionController.LogicalCubeSize) + 48) * 4;
            Assert.True(hoveredPixels[centerPixel] > 180);
            Assert.True(hoveredPixels[centerPixel + 2] < 220);
            AssertPreviewBlocksScene(GetDestinationPreviewRoot());
            Assert.True(InteractionServicesValue.InputCapture.IsPointerBlocked(centerPointer));
            Assert.Equal(CameraProjectionMode.Perspective, CameraValue.ProjectionMode);
            Assert.Equal(initialPosition, CameraEntityValue.Position);
            Assert.Equal(initialOrientation, CameraEntityValue.Orientation);
            Assert.Equal(initialPivot, CameraControllerValue.GetOrbitTarget());

            byte[] leavePixels = RenderAtPointer(cube, new int2(0, 0));

            Assert.False(cube.InteractionController.IsCenterToggleHovered);
            Assert.False(GetDestinationPreviewRoot().Enabled);
            Assert.Empty(GetDestinationPreviewText(GetDestinationPreviewRoot()).Text);
            Assert.Equal(idlePixels, leavePixels);
            cube.Dispose();
        }

        /// <summary>
        /// Ensures a long diagonal destination wraps inside the cube bounds when the viewport has no space to its left.
        /// </summary>
        [Fact]
        public void Update_NarrowViewportWrapsDestinationInsideUsableBounds() {
            NavigationControllerValue.SelectTarget(new EditorViewportNavigationTarget(1, 1, 1));
            NavigationControllerValue.Advance(0.2);
            EditorViewportNavigationCube cube = CreateCube();
            cube.Initialize();
            CameraValue.Viewport = new float4(10f, 30f, 112f, 180f);
            cube.Resize(new int2(112, 180), 1f);
            byte[] idlePixels = RenderAtPointer(cube, new int2(0, 0));
            EditorViewportNavigationCubeGeometry geometry = new EditorViewportNavigationCubeGeometry();
            IReadOnlyList<float2> vertices = geometry.GetProjectedVertices(CameraEntityValue.Orientation,
                EditorViewportNavigationCubeInteractionController.LogicalCubeSize);
            int cornerIndex = FindVisibleCornerOutsideCenter(geometry, CameraEntityValue.Orientation);
            Assert.True(cornerIndex >= 0);
            float2 corner = vertices[cornerIndex];
            Assert.True(geometry.TryHit(corner, CameraEntityValue.Orientation,
                EditorViewportNavigationCubeInteractionController.LogicalCubeSize, out EditorViewportNavigationCubeHit expectedHit));
            int2 pointer = GetScreenPointer(cube, corner, 1f);

            RenderAtPointer(cube, pointer);

            EditorEntity previewRoot = GetDestinationPreviewRoot();
            RoundedRectComponent previewBackground = Assert.Single(previewRoot.Components.OfType<RoundedRectComponent>());
            TextComponent previewText = GetDestinationPreviewText(previewRoot);
            Assert.Equal(BuildDestinationLabel(expectedHit.Target), previewText.Text);
            Assert.True(previewText.WrapText);
            Assert.True(previewBackground.Size.X <= 96);
            AssertPreviewFitsViewport(previewRoot);
            AssertPreviewBlocksScene(previewRoot);
            Assert.True(CountChangedPixels(idlePixels, RenderManager2DValue.LastUpdatedTexturePixels) >= 100);
            cube.Dispose();
        }

        /// <summary>Builds a cube view with resources from this fixture.</summary>
        /// <returns>Uninitialized viewport cube.</returns>
        EditorViewportNavigationCube CreateCube() {
            return new EditorViewportNavigationCube(
                ViewportEntityValue,
                CameraValue,
                NavigationControllerValue,
                RenderManager2DValue,
                CoreValue.Input,
                FontValue,
                new EditorUiMetrics(1d),
                InteractionServicesValue,
                () => 0.016,
                Shaders);
        }

        /// <summary>Advances the real input backend and overlay with one released-button pointer sample.</summary>
        /// <param name="cube">Initialized navigation view to update.</param>
        /// <param name="pointer">Window-space pointer position.</param>
        /// <returns>RGBA buffer submitted by the production cube rasterizer.</returns>
        byte[] RenderAtPointer(EditorViewportNavigationCube cube, int2 pointer) {
            InputBackendValue.SetMouseState(new MouseState(pointer.X, pointer.Y, 0,
                ButtonState.Released, ButtonState.Released, ButtonState.Released,
                ButtonState.Released, ButtonState.Released));
            InputBackendValue.EarlyUpdate();
            InputBackendValue.Update();
            cube.Update();
            return RenderManager2DValue.LastUpdatedTexturePixels;
        }

        /// <summary>Converts one logical cube point to the current screen-space hit location.</summary>
        /// <param name="cube">Initialized navigation view that owns the current cube bounds.</param>
        /// <param name="logicalPoint">Logical raster position inside the 96-pixel cube.</param>
        /// <param name="uiScale">Current multiplier between logical and screen pixels.</param>
        /// <returns>Rounded window-space pointer coordinate.</returns>
        static int2 GetScreenPointer(EditorViewportNavigationCube cube, float2 logicalPoint, float uiScale) {
            return new int2(
                cube.InteractionController.CubeScreenPosition.X + (int)Math.Round(logicalPoint.X * uiScale),
                cube.InteractionController.CubeScreenPosition.Y + (int)Math.Round(logicalPoint.Y * uiScale));
        }

        /// <summary>Finds a visible projected corner outside the center-toggle hit radius.</summary>
        /// <param name="geometry">Shared navigation-cube projection and hit geometry.</param>
        /// <param name="orientation">Current represented camera orientation.</param>
        /// <returns>Visible vertex index separated from the center toggle, or -1 if none is available.</returns>
        static int FindVisibleCornerOutsideCenter(EditorViewportNavigationCubeGeometry geometry, float4 orientation) {
            IReadOnlyList<float2> vertices = geometry.GetProjectedVertices(
                orientation,
                EditorViewportNavigationCubeInteractionController.LogicalCubeSize);
            IReadOnlyList<int> visibleVertices = geometry.GetVisibleVertexIndices(
                orientation,
                EditorViewportNavigationCubeInteractionController.LogicalCubeSize);
            double minimumDistance = EditorViewportNavigationCubeInteractionController.LogicalCenterToggleRadius +
                (EditorViewportNavigationCubeInteractionController.LogicalCubeSize * EditorViewportNavigationCubeGeometry.CornerHitRadiusFraction);
            double minimumDistanceSquared = minimumDistance * minimumDistance;
            for (int visibleIndex = 0; visibleIndex < visibleVertices.Count; visibleIndex++) {
                int vertexIndex = visibleVertices[visibleIndex];
                double deltaX = vertices[vertexIndex].X - 48.0;
                double deltaY = vertices[vertexIndex].Y - 48.0;
                if ((deltaX * deltaX) + (deltaY * deltaY) > minimumDistanceSquared) {
                    return vertexIndex;
                }
            }

            return -1;
        }

        /// <summary>Builds the face-name label expected for a signed test target.</summary>
        /// <param name="target">Expected target returned by shared hit geometry.</param>
        /// <returns>Destination label in the overlay's front, vertical, horizontal order.</returns>
        static string BuildDestinationLabel(EditorViewportNavigationTarget target) {
            List<string> faceNames = new List<string>(3);
            if (target.Z != 0) {
                faceNames.Add(target.Z > 0 ? "Front" : "Back");
            }
            if (target.Y != 0) {
                faceNames.Add(target.Y > 0 ? "Top" : "Bottom");
            }
            if (target.X != 0) {
                faceNames.Add(target.X > 0 ? "Right" : "Left");
            }

            return string.Join(" / ", faceNames);
        }

        /// <summary>Finds the rendered destination label attached to the viewport overlay hierarchy.</summary>
        /// <returns>Enabled or hidden destination-preview root entity.</returns>
        EditorEntity GetDestinationPreviewRoot() {
            return Assert.Single(ViewportEntityValue.Children.OfType<EditorEntity>(),
                child => child.Name == "Navigation Cube Destination Preview");
        }

        /// <summary>Finds the dynamic text drawable inside the destination preview pill.</summary>
        /// <param name="previewRoot">Root of the destination preview hierarchy.</param>
        /// <returns>Preview text whose contents show the selected direction.</returns>
        static TextComponent GetDestinationPreviewText(EditorEntity previewRoot) {
            EditorEntity textRoot = Assert.Single(previewRoot.Children.OfType<EditorEntity>(),
                child => child.Name == "Navigation Cube Destination Label");
            return Assert.Single(textRoot.Components.OfType<TextComponent>());
        }

        /// <summary>Ensures the complete destination pill stays inside the scene-camera viewport.</summary>
        /// <param name="previewRoot">Destination pill whose world bounds are checked.</param>
        void AssertPreviewFitsViewport(EditorEntity previewRoot) {
            RoundedRectComponent background = Assert.Single(previewRoot.Components.OfType<RoundedRectComponent>());
            Assert.True(previewRoot.Position.X >= CameraValue.Viewport.X);
            Assert.True(previewRoot.Position.Y >= CameraValue.Viewport.Y);
            Assert.True(previewRoot.Position.X + background.Size.X <= CameraValue.Viewport.X + CameraValue.Viewport.Z);
            Assert.True(previewRoot.Position.Y + background.Size.Y <= CameraValue.Viewport.Y + CameraValue.Viewport.W);
        }

        /// <summary>Ensures the non-interactive label blocks scene and gizmo input in its own screen rectangle.</summary>
        /// <param name="previewRoot">Visible destination preview root.</param>
        void AssertPreviewBlocksScene(EditorEntity previewRoot) {
            RoundedRectComponent background = Assert.Single(previewRoot.Components.OfType<RoundedRectComponent>());
            int2 center = new int2(
                (int)Math.Round(previewRoot.Position.X + (background.Size.X * 0.5)),
                (int)Math.Round(previewRoot.Position.Y + (background.Size.Y * 0.5)));
            Assert.True(InteractionServicesValue.InputCapture.IsPointerBlocked(center));
        }

        /// <summary>Counts raster pixels whose RGBA values changed between idle and hovered images.</summary>
        /// <param name="first">Idle cube raster.</param>
        /// <param name="second">Hovered cube raster.</param>
        /// <returns>Number of changed logical cube pixels.</returns>
        static int CountChangedPixels(byte[] first, byte[] second) {
            Assert.Equal(first.Length, second.Length);
            int changedPixels = 0;
            for (int pixelIndex = 0; pixelIndex < first.Length; pixelIndex += 4) {
                if (first[pixelIndex] != second[pixelIndex] ||
                    first[pixelIndex + 1] != second[pixelIndex + 1] ||
                    first[pixelIndex + 2] != second[pixelIndex + 2] ||
                    first[pixelIndex + 3] != second[pixelIndex + 3]) {
                    changedPixels++;
                }
            }

            return changedPixels;
        }

        /// <summary>Creates a font atlas with every character used by cube face and projection labels.</summary>
        /// <returns>Minimal font asset for overlay label layout.</returns>
        static FontAsset CreateFont() {
            Dictionary<char, FontChar> glyphs = new Dictionary<char, FontChar>();
            const string characters = "LeftRightBottomTopBackFrontPerspOrthoPerspectiveOrthographic /";
            for (int i = 0; i < characters.Length; i++) {
                glyphs[characters[i]] = new FontChar(new float4(0f, 0f, 0.1f, 0.1f), 0f, 8f, 0f, 0f);
            }
            return new FontAsset(
                new FontInfo("NavigationCubeTestFont", 12, 4f),
                new TestRuntimeTexture { Width = 16, Height = 16 },
                glyphs,
                16f,
                16,
                16);
        }
    }
}
