using System.Reflection;
using System.Runtime.Versioning;
using helengine.directx11;
using helengine.editor;
using SharpDX.Direct3D11;

namespace helengine.editor.windows.tests.rendering {
    /// <summary>Exercises the production GPU picker against textured and render-target-backed 2D planes without desktop capture.</summary>
    [SupportedOSPlatform("windows")]
    public sealed class EditorWorldPreviewPickingTests {
        /// <summary>Checks alpha holes reveal the deeper pick ID from either side, independently of submission order and texture storage.</summary>
        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, false, true)]
        [InlineData(false, true, false)]
        [InlineData(false, true, true)]
        [InlineData(true, false, false)]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        [InlineData(true, true, true)]
        public void Render_AlphaTestsWorldPlanesAndPreservesPickIds(bool renderTargetMask, bool backView, bool foregroundFirst) {
            using DirectX11Renderer3D renderer = new DirectX11Renderer3D();
            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(renderer, renderer.Render2D, null, new PlatformInfo("windows", "test"));
            ShaderBackendRegistry backends = new ShaderBackendRegistry();
            backends.Register(new DirectX11ShaderBackend());
            using EditorBuiltInShaderAssetLibrary shaders = new EditorBuiltInShaderAssetLibrary(backends);
            using EditorWorldSpace2DPreviewMeshResources meshes = new EditorWorldSpace2DPreviewMeshResources(renderer);
            RuntimeTexture mask = renderer.Render2D.BuildTextureFromRaw(new TextureAsset {
                Width = 2, Height = 1, Colors = [255, 255, 255, 0, 255, 255, 255, 128]
            });
            using DirectX11RenderTargetResource capture = Assert.IsType<DirectX11RenderTargetResource>(renderer.CreateRenderTarget(2, 1));
            if (renderTargetMask) {
                using SharpDX.Direct3D11.Resource source = Assert.IsType<DirectX11TextureResource>(mask).Resource.Resource;
                renderer.Device.ImmediateContext.CopyResource(source, capture.ColorTexture);
            }
            RuntimeMaterial foregroundMaterial = EditorWorldSpaceSpritePreviewMaterialFactory.Create(renderer, renderTargetMask ? capture : mask, shaders);
            RuntimeMaterial backgroundMaterial = EditorWorldSpaceSpritePreviewMaterialFactory.Create(renderer, renderer.Render2D.PixelTexture, shaders);
            Entity foreground = CreateEntity(core);
            foreground.LocalPosition = new float3(-32, -32, 0);
            foreground.LocalScale = new float3(64, 64, 1);
            MeshComponent foregroundMesh = new MeshComponent { Model = meshes.GetRuntimeModel(), Materials = [foregroundMaterial] };
            foreground.AddComponent(foregroundMesh);
            Entity background = CreateEntity(core);
            background.LocalPosition = new float3(-32, -32, backView ? 1 : -1);
            background.LocalScale = new float3(64, 64, 1);
            MeshComponent backgroundMesh = new MeshComponent { Model = meshes.GetRuntimeModel(), Materials = [backgroundMaterial] };
            background.AddComponent(backgroundMesh);
            Entity cameraEntity = CreateEntity(core);
            cameraEntity.LocalPosition = new float3(0, 0, backView ? -100 : 100);
            if (backView) {
                float4.CreateFromAxisAngle(new float3(0, 1, 0), (float)Math.PI, out float4 orientation);
                cameraEntity.LocalOrientation = orientation;
            }
            EditorViewportCameraComponent camera = new EditorViewportCameraComponent {
                LayerMask = 1, Viewport = new float4(0, 0, 64, 64),
                ProjectionMode = CameraProjectionMode.Orthographic, OrthographicVerticalSpan = 64, FarPlaneDistance = 1000f
            };
            cameraEntity.AddComponent(camera);
            camera.RenderQueue3D.Clear();
            camera.RenderQueue3D.Add(foregroundFirst ? foregroundMesh : backgroundMesh);
            camera.RenderQueue3D.Add(foregroundFirst ? backgroundMesh : foregroundMesh);
            byte4 foregroundId = new byte4(19, 27, 38, 255);
            byte4 backgroundId = new byte4(41, 53, 67, 255);
            using DirectX11EditorPickingBackend picker = new DirectX11EditorPickingBackend(renderer, camera);
            picker.Render(camera, new Dictionary<IDrawable3D, byte4> {
                [foregroundMesh] = foregroundId, [backgroundMesh] = backgroundId
            });
            typeof(DirectX11Renderer3D).GetMethod("RenderCustomPasses", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(renderer, null);
            Assert.True(picker.TryReadPixel(new int2(backView ? 48 : 16, 32), out byte4 transparent));
            Assert.Equal(backgroundId, transparent);
            Assert.True(picker.TryReadPixel(new int2(backView ? 16 : 48, 32), out byte4 visible));
            Assert.Equal(foregroundId, visible);
            renderer.ReleaseMaterial(foregroundMaterial);
            renderer.ReleaseMaterial(backgroundMaterial);
        }

        /// <summary>Creates a scene entity registered with the test core and isolated scene layer.</summary>
        /// <param name="core">Core that owns the synthetic scene.</param>
        /// <returns>Entity ready for transforms and mesh/camera components.</returns>
        static Entity CreateEntity(Core core) {
            Entity entity = new Entity(core) { LayerMask = 1 };
            entity.InitComponents();
            entity.InitChildren();
            return entity;
        }
    }
}
