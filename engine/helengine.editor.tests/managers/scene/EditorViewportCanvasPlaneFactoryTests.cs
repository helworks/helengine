using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.scene {
    /// <summary>
    /// Verifies world-space 2D canvas plane creation and coordinate mapping.
    /// </summary>
    public class EditorViewportCanvasPlaneFactoryTests {
        /// <summary>
        /// Ensures the plane factory creates an internal mesh entity on the dedicated canvas-plane layer.
        /// </summary>
        [Fact]
        public void Create_WhenCalled_BuildsInternalCanvasPlaneOnDedicatedLayer() {
            InitializeCore();
            TestRenderManager3D render3D = new TestRenderManager3D();
            var renderTarget = new TestRenderTarget {
                Width = 1280,
                Height = 720
            };

            EditorEntity planeEntity = EditorViewportCanvasPlaneFactory.Create(render3D, renderTarget);
            MeshComponent meshComponent = Assert.IsType<MeshComponent>(Assert.Single(planeEntity.Components, component => component is MeshComponent));
            ModelAsset builtModelAsset = Assert.Single(render3D.BuiltModelAssets);

            Assert.True(planeEntity.InternalEntity);
            Assert.Equal(EditorLayerMasks.SceneCanvasPlane, planeEntity.LayerMask);
            Assert.NotNull(meshComponent.Model);
            Assert.NotNull(Assert.Single(meshComponent.Materials));
            ShaderRuntimeMaterial material = Assert.IsAssignableFrom<ShaderRuntimeMaterial>(Assert.Single(meshComponent.Materials));
            Assert.Same(renderTarget, material.ResolveTexture());
            Assert.Equal(new float2(0f, 1f), builtModelAsset.TexCoords[0]);
            Assert.Equal(new float2(0f, 0f), builtModelAsset.TexCoords[3]);
        }

        /// <summary>
        /// Ensures the default pixel-to-world mapping converts one world hit into the expected canvas pixel coordinates.
        /// </summary>
        [Fact]
        public void MapWorldPointToCanvas_WhenUsingDefaultScale_ReturnsExpectedPixels() {
            EditorSceneCanvasProfileState sceneCanvasProfileState = new EditorSceneCanvasProfileState();
            EditorViewportCanvasPreviewSettings settings = new EditorViewportCanvasPreviewSettings();

            int2 pixel = EditorViewportCanvasPlaneCoordinateMapper.MapWorldToCanvas(new float3(6.4f, 3.6f, 0f), sceneCanvasProfileState, settings);

            Assert.Equal(new int2(640, 360), pixel);
        }

        /// <summary>
        /// Ensures world points on the top edge of the plane map to canvas Y zero so preview texture orientation matches 2D scene coordinates.
        /// </summary>
        [Fact]
        public void MapWorldPointToCanvas_WhenPointIsOnTopEdge_ReturnsZeroCanvasY() {
            EditorSceneCanvasProfileState sceneCanvasProfileState = new EditorSceneCanvasProfileState();
            EditorViewportCanvasPreviewSettings settings = new EditorViewportCanvasPreviewSettings();

            int2 pixel = EditorViewportCanvasPlaneCoordinateMapper.MapWorldToCanvas(new float3(0f, 7.2f, 0f), sceneCanvasProfileState, settings);

            Assert.Equal(new int2(0, 0), pixel);
        }

        /// <summary>
        /// Initializes the core services required by canvas-plane factory tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }
    }
}
