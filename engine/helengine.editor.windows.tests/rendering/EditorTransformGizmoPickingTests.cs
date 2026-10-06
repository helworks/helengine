using System.Drawing;
using System.Runtime.Versioning;
using helengine.directx11;
using helengine.editor;

namespace helengine.editor.windows.tests.rendering {
    /// <summary>Checks generated gizmo geometry remains pickable after custom passes and ordinary cameras finish a complete GPU frame.</summary>
    [SupportedOSPlatform("windows")]
    public sealed class EditorTransformGizmoPickingTests {
        /// <summary>Renders each transform tool's tip or ring with its production shader and reads the isolated picker target afterward.</summary>
        [Theory]
        [InlineData(EditorViewportToolMode.Translate)]
        [InlineData(EditorViewportToolMode.Rotate)]
        [InlineData(EditorViewportToolMode.Scale)]
        public void Draw_PreservesOpaqueGizmoPickId(EditorViewportToolMode tool) {
            Exception failure = null;
            Thread thread = new Thread(() => {
                try {
                    AssertGizmoPick(tool);
                } catch (Exception exception) {
                    failure = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        /// <summary>Creates a synthetic window surface and reads only the GPU picker target, without capturing desktop pixels.</summary>
        /// <param name="tool">Transform tool whose generated geometry is rendered.</param>
        static void AssertGizmoPick(EditorViewportToolMode tool) {
            using DirectX11Renderer3D renderer = new DirectX11Renderer3D();
            using Form window = new Form {
                ClientSize = new Size(64, 64), ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000)
            };
            window.Show();
            Application.DoEvents();
            renderer.AddWindow(window.Handle, 64, 64);
            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(renderer, renderer.Render2D, null, new PlatformInfo("windows", "test"));
            ShaderBackendRegistry backends = new ShaderBackendRegistry();
            backends.Register(new DirectX11ShaderBackend());
            using EditorBuiltInShaderAssetLibrary shaders = new EditorBuiltInShaderAssetLibrary(backends);
            ShaderAsset shader = shaders.LoadShaderAsset(renderer, "EditorTransformGizmo.hlsl");
            RuntimeMaterial material = renderer.BuildMaterialFromRaw(new ShaderMaterialAsset {
                Id = "EditorTransformGizmo.material", ShaderAssetId = shader.Id,
                VertexProgram = "EditorTransformGizmo.vs", PixelProgram = "EditorTransformGizmo.ps", Variant = "default"
            }, shader);
            ModelAsset model = tool == EditorViewportToolMode.Translate ? TransformGizmoMeshFactory.CreateCone(0.5f, 1f, 18)
                : tool == EditorViewportToolMode.Rotate ? TransformGizmoMeshFactory.CreateTubeRing(0.6f, 1f, 0.12f, 48)
                : TransformGizmoMeshFactory.CreateBox(1f, 1f, 1f);
            Entity handle = CreateEntity(core, true);
            if (tool == EditorViewportToolMode.Rotate) {
                float4.CreateFromAxisAngle(new float3(1, 0, 0), (float)(Math.PI / 2), out float4 orientation);
                handle.LocalOrientation = orientation;
            }
            MeshComponent mesh = new MeshComponent { Model = renderer.BuildModelFromRaw(model), Materials = [material] };
            handle.AddComponent(mesh);
            using DirectX11RenderTargetResource sceneTarget = Assert.IsType<DirectX11RenderTargetResource>(renderer.CreateRenderTarget(64, 64));
            Entity sceneCameraOwner = CreateEntity(core, true);
            sceneCameraOwner.LocalPosition = new float3(0, 0, 10);
            EditorViewportCameraComponent sceneCamera = CreateCamera();
            sceneCamera.RenderTarget = sceneTarget;
            sceneCameraOwner.AddComponent(sceneCamera);
            sceneCamera.RenderQueue3D.Add(mesh);
            Entity pickerOwner = CreateEntity(core, false);
            pickerOwner.LocalPosition = sceneCameraOwner.Position;
            EditorViewportCameraComponent pickerCamera = CreateCamera();
            pickerOwner.AddComponent(pickerCamera);
            pickerCamera.RenderQueue3D.Add(mesh);
            Assert.DoesNotContain(pickerCamera, core.ObjectManager.Cameras);
            byte4 id = new byte4(17, 31, 53, 255);
            using DirectX11EditorPickingBackend picker = new DirectX11EditorPickingBackend(renderer, pickerCamera);
            picker.Render(pickerCamera, new Dictionary<IDrawable3D, byte4> { [mesh] = id });
            renderer.Draw();
            int2 pixel = tool == EditorViewportToolMode.Rotate ? new int2(45, 32) : new int2(32, 24);
            Assert.True(picker.TryReadPixel(pixel, out byte4 actual));
            Assert.Equal(id, actual);
            renderer.ReleaseMaterial(material);
        }

        /// <summary>Creates a matching orthographic viewport for visible and ID passes.</summary>
        /// <returns>Camera that frames the generated one-unit handle geometry.</returns>
        static EditorViewportCameraComponent CreateCamera() {
            return new EditorViewportCameraComponent {
                LayerMask = EditorLayerMasks.SceneGizmo, Viewport = new float4(0, 0, 64, 64),
                ProjectionMode = CameraProjectionMode.Orthographic, OrthographicVerticalSpan = 4, FarPlaneDistance = 100f
            };
        }

        /// <summary>Registers an entity with the same visibility and layer rules as the viewport's gizmo and hidden picker owners.</summary>
        /// <param name="core">Core that owns the synthetic scene.</param>
        /// <param name="enabled">Whether ordinary rendering registers this entity's camera or drawables.</param>
        /// <returns>Initialized owner ready to receive components.</returns>
        static Entity CreateEntity(Core core, bool enabled) {
            Entity entity = new Entity(core) { Enabled = enabled, LayerMask = EditorLayerMasks.SceneGizmo };
            entity.InitComponents();
            entity.InitChildren();
            return entity;
        }
    }
}
