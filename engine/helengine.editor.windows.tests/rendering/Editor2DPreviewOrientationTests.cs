using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using helengine.directx11;
using helengine.editor;
using SharpDX.Direct3D11;

namespace helengine.editor.windows.tests.rendering {
    /// <summary>Checks preview orientation using pixels rendered into synthetic offscreen targets.</summary>
    [SupportedOSPlatform("windows")]
    public sealed class Editor2DPreviewOrientationTests {
        /// <summary>Verifies a colored band at the top of a capture stays at the top of its world-space preview.</summary>
        [Fact]
        public void RenderTargetPreview_PreservesTopAndBottom() {
            Exception failure = null;
            Thread thread = new Thread(() => {
                try {
                    AssertPreviewOrientation();
                } catch (Exception exception) {
                    failure = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        /// <summary>Checks actual 2D output changes when only entity depth changes, without unregistering either sprite.</summary>
        [Fact]
        public void Render2D_DepthChangesWhichSpriteIsVisible() {
            Exception failure = null;
            Thread thread = new Thread(() => {
                try {
                    AssertDepthComposition();
                } catch (Exception exception) {
                    failure = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        /// <summary>Composites two opaque colored sprites into a synthetic target and reverses their depths.</summary>
        static void AssertDepthComposition() {
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
            using DirectX11RenderTargetResource target = Assert.IsType<DirectX11RenderTargetResource>(renderer.CreateRenderTarget(64, 64));
            Entity cameraEntity = CreateEntity(core, 1);
            cameraEntity.AddComponent(new CameraComponent {
                LayerMask = 1, RenderTarget = target, Viewport = new float4(0, 0, 64, 64)
            });
            Entity red = CreateEntity(core, 1);
            red.LocalPosition = new float3(0, 0, 1);
            red.AddComponent(new SpriteComponent {
                Texture = renderer.Render2D.PixelTexture, Size = new int2(64, 64), Color = new byte4(255, 0, 0, 255)
            });
            Entity blue = CreateEntity(core, 1);
            blue.AddComponent(new SpriteComponent {
                Texture = renderer.Render2D.PixelTexture, Size = new int2(64, 64), Color = new byte4(0, 0, 255, 255)
            });
            renderer.Draw();
            Assert.Equal(new byte4(255, 0, 0, 255), ReadPixel(target, renderer.Device, 32));
            red.LocalPosition = new float3(0, 0, -1);
            renderer.Draw();
            Assert.Equal(new byte4(0, 0, 255, 255), ReadPixel(target, renderer.Device, 32));
        }

        /// <summary>Renders an asymmetric 2D capture and its production preview mesh without reading any desktop pixels.</summary>
        static void AssertPreviewOrientation() {
            using DirectX11Renderer3D renderer = new DirectX11Renderer3D();
            using Form window = new Form {
                ClientSize = new Size(64, 64),
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-32000, -32000)
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
            using EditorWorldSpace2DPreviewMeshResources meshes = new EditorWorldSpace2DPreviewMeshResources(renderer);
            using DirectX11RenderTargetResource capture = Assert.IsType<DirectX11RenderTargetResource>(renderer.CreateRenderTarget(64, 64));
            using DirectX11RenderTargetResource result = Assert.IsType<DirectX11RenderTargetResource>(renderer.CreateRenderTarget(64, 64));
            Entity captureCameraEntity = CreateEntity(core, 1);
            captureCameraEntity.AddComponent(new CameraComponent {
                LayerMask = 1, RenderTarget = capture, Viewport = new float4(0, 0, 64, 64)
            });
            Entity band = CreateEntity(core, 1);
            band.AddComponent(new SpriteComponent {
                Texture = renderer.Render2D.PixelTexture,
                Color = new byte4(255, 0, 0, 255),
                Size = new int2(64, 16)
            });
            Entity sceneCameraEntity = CreateEntity(core, 2);
            sceneCameraEntity.LocalPosition = new float3(32, -32, 10);
            sceneCameraEntity.AddComponent(new EditorViewportCameraComponent {
                LayerMask = 2, CameraDrawOrder = 1, RenderTarget = result,
                Viewport = new float4(0, 0, 64, 64),
                ProjectionMode = CameraProjectionMode.Orthographic, OrthographicVerticalSpan = 64
            });
            RuntimeMaterial material = EditorExact2DPreviewMaterialFactory.Create(renderer, capture, shaders);
            Entity plane = CreateEntity(core, 2);
            plane.LocalScale = new float3(64, 64, 1);
            plane.AddComponent(new MeshComponent {
                Model = meshes.GetViewportRenderTargetRuntimeModel(), Materials = new[] { material }
            });
            renderer.Draw();
            Assert.True(ReadAlpha(capture, renderer.Device, 8) > 240, "Capture top must contain the source band.");
            Assert.Equal(0, ReadAlpha(capture, renderer.Device, 56));
            Assert.True(ReadAlpha(result, renderer.Device, 8) > 240, "Preview top must preserve the captured band.");
            Assert.Equal(0, ReadAlpha(result, renderer.Device, 56));
        }

        /// <summary>Creates an initialized runtime entity on a layer isolated from the other rendering pass.</summary>
        static Entity CreateEntity(Core core, ushort layer) {
            Entity entity = new Entity(core) { LayerMask = layer };
            entity.InitComponents();
            entity.InitChildren();
            return entity;
        }

        /// <summary>Reads alpha from a synthetic render target at its horizontal center and requested row.</summary>
        static byte ReadAlpha(RenderTarget target, Device device, int row) {
            return ReadPixel(target, device, row).W;
        }

        /// <summary>Reads RGBA from a synthetic target without accessing desktop or window pixels.</summary>
        /// <param name="target">Offscreen target produced by the test.</param>
        /// <param name="device">Device owning the target.</param>
        /// <param name="row">Row sampled at the horizontal center.</param>
        /// <returns>The composited pixel color.</returns>
        static byte4 ReadPixel(RenderTarget target, Device device, int row) {
            DirectX11RenderTargetResource resource = Assert.IsType<DirectX11RenderTargetResource>(target);
            Texture2DDescription description = resource.ColorTexture.Description;
            description.BindFlags = BindFlags.None;
            description.Usage = ResourceUsage.Staging;
            description.CpuAccessFlags = CpuAccessFlags.Read;
            description.OptionFlags = ResourceOptionFlags.None;
            using Texture2D staging = new Texture2D(device, description);
            device.ImmediateContext.CopyResource(resource.ColorTexture, staging);
            SharpDX.DataBox mapped = device.ImmediateContext.MapSubresource(staging, 0, MapMode.Read, MapFlags.None);
            try {
                int offset = row * mapped.RowPitch + 32 * 4;
                return new byte4(Marshal.ReadByte(mapped.DataPointer, offset), Marshal.ReadByte(mapped.DataPointer, offset + 1),
                    Marshal.ReadByte(mapped.DataPointer, offset + 2), Marshal.ReadByte(mapped.DataPointer, offset + 3));
            } finally {
                device.ImmediateContext.UnmapSubresource(staging, 0);
            }
        }
    }
}
