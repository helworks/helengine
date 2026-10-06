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
        /// <summary>Camera previews retain scaled glyph overflow beyond small layout boxes and update actual pixels after edits and camera movement.</summary>
        /// <param name="layoutHeight">Authored height, including a layout exposing only the top quarter of the glyph before the overflow fix.</param>
        [Theory]
        [InlineData(64)]
        [InlineData(16)]
        public void CameraPreview_WhenCameraMoves_RendersNewScenePixels(int layoutHeight) {
            Exception failure = null;
            Thread thread = new Thread(() => {
                try { AssertLiveCameraPreview(layoutHeight); } catch (Exception exception) { failure = exception; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        /// <summary>Exercises the real camera render loop using only a synthetic offscreen target for pixel readback.</summary>
        /// <param name="layoutHeight">Text layout height used independently of the 64-pixel glyph height.</param>
        static void AssertLiveCameraPreview(int layoutHeight) {
            using DirectX11Renderer3D renderer = new DirectX11Renderer3D();
            using Form window = new Form {
                ClientSize = new Size(64, 64), ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000)
            };
            window.Show();
            Application.DoEvents();
            renderer.AddWindow(window.Handle, 64, 64);
            using EditorCore core = new EditorCore(new helengine.ui.Project { Name = "Live preview", Path = AppContext.BaseDirectory });
            core.Initialize(renderer, renderer.Render2D, null, new PlatformInfo("windows", "test"));
            using EditorSessionInteractionServices interactions = new EditorSessionInteractionServices();
            using EditorCoreInteractionGraphBinding binding = new EditorCoreInteractionGraphBinding(core, interactions);
            ShaderBackendRegistry backends = new ShaderBackendRegistry();
            backends.Register(new DirectX11ShaderBackend());
            using EditorBuiltInShaderAssetLibrary shaders = new EditorBuiltInShaderAssetLibrary(backends);
            using EditorSessionRendererResources resources = new EditorSessionRendererResources(renderer, renderer.Render2D,
                core.ObjectManager, core.EntityFactory, new EditorSceneEntityIdAllocator(), core.Input,
                () => core.FrameDeltaSeconds, null, interactions);
            RuntimeTexture coverage = renderer.Render2D.BuildTextureFromRaw(new TextureAsset {
                Width = 1, Height = 1, Colors = [255, 255, 255, 255]
            });
            FontAsset font = new FontAsset(new FontInfo("Preview", 1, 1), coverage,
                new Dictionary<char, FontChar> { ['H'] = new FontChar(new float4(0, 0, 1, 1), 0, 1, 0, 0) }, 1, 1, 1);
            EditorEntity label = new EditorEntity(core, interactions) { LayerMask = EditorLayerMasks.SceneObjects };
            TextComponent text = new TextComponent { Font = font, Text = "H", FontScale = 64, Size = new int2(64, layoutHeight) };
            label.AddComponent(text);
            EditorEntity syncHost = new EditorEntity(core, interactions) { InternalEntity = true };
            EditorWorldSpace2DPreviewSyncComponent sync = new EditorWorldSpace2DPreviewSyncComponent(shaders, resources);
            syncHost.AddComponent(sync);
            syncHost.InitializeHierarchy();
            EditorEntity cameraEntity = new EditorEntity(core, interactions) {
                LayerMask = EditorLayerMasks.SceneObjects, Position = new float3(32, 32, 100)
            };
            CameraComponent camera = new CameraComponent {
                CameraDrawOrder = 2, LayerMask = EditorLayerMasks.SceneObjects, Viewport = new float4(0, 0, 64, 64), FarPlaneDistance = 1000,
                ClearSettings = new CameraClearSettings(true, new float4(0, 0, 0, 1), true, 1, false, 0)
            };
            cameraEntity.AddComponent(camera);
            EditorSceneCameraSuppressionService.AttachAndSuppress(cameraEntity, core.ObjectManager);
            using CameraPreviewSource preview = new CameraPreviewSource(cameraEntity, camera, renderer, resources);
            using PreviewPanel panel = new PreviewPanel(core, interactions, font) { Size = new int2(32, 32) };
            panel.SetPreviewSource(preview);
            using RenderTarget panelTarget = renderer.CreateRenderTarget(64, 64);
            EditorEntity panelCameraEntity = new EditorEntity(core, interactions) { InternalEntity = true };
            panelCameraEntity.AddComponent(new CameraComponent {
                CameraDrawOrder = EditorUiCameraDrawOrders.SharedUi, LayerMask = panel.LayerMask,
                Viewport = new float4(0, 0, 64, 64), RenderTarget = panelTarget,
                ClearSettings = new CameraClearSettings(true, new float4(0, 0, 0, 1), true, 1, false, 0)
            });
            core.Update(1d / 60d);
            renderer.Draw();
            Assert.Equal(1, preview.PreviewCamera.RenderQueue3D.Count);
            EditorEntity proxy = interactions.WorldSpace2DPreviewRegistry.ResolvePreviewEntity(label);
            Assert.Equal(new float3(64, 64, 1), proxy.Scale);
            EditorTextWorldPreviewComponent proxyComponent = Assert.IsType<EditorTextWorldPreviewComponent>(Assert.Single(proxy.Components, component => component is EditorTextWorldPreviewComponent));
            EditorExact2DPreviewCaptureService capture = (EditorExact2DPreviewCaptureService)typeof(EditorExact2DWorldPreviewComponentBase)
                .GetField("CaptureServiceValue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(proxyComponent);
            Assert.Equal(new int2(64, layoutHeight), capture.PreviewTextComponent.Size);
            Assert.Equal(64, capture.PreviewRenderTarget.Height);
            Assert.Equal(byte.MaxValue, ReadPixel(capture.PreviewRenderTarget, renderer.Device, 60, 32).W);
            Assert.Equal(byte.MaxValue, ReadPixel(preview.RenderTarget, renderer.Device, 16, 16).X);
            Assert.Equal(byte.MaxValue, ReadPixel(panelTarget, renderer.Device, 36, 16).X);

            cameraEntity.LocalPosition = new float3(512, 32, 100);
            core.Update(1d / 60d);
            renderer.Draw();
            Assert.Equal(0, ReadPixel(preview.RenderTarget, renderer.Device, 16, 16).X);
            Assert.Equal(0, ReadPixel(panelTarget, renderer.Device, 36, 16).X);

            cameraEntity.LocalPosition = new float3(32, 32, 100);
            text.Color = new byte4(128, 128, 128, 255);
            panel.Size = new int2(48, 48);
            core.Update(1d / 60d);
            renderer.Draw();
            Assert.InRange((int)ReadPixel(preview.RenderTarget, renderer.Device, 24, 24).X, 127, 129);
            Assert.InRange((int)ReadPixel(panelTarget, renderer.Device, 44, 24).X, 127, 129);
            text.Text = string.Empty;
            core.Update(1d / 60d);
            renderer.Draw();
            Assert.Equal(0, ReadPixel(preview.RenderTarget, renderer.Device, 24, 24).X);
            Assert.Equal(0, ReadPixel(panelTarget, renderer.Device, 44, 24).X);
        }

        /// <summary>The navigation cube renders opposing physical faces through its isolated GPU camera.</summary>
        [Fact]
        public void NavigationCube_RendersPhysicalFacesWithDepthBuffer() {
            Exception failure = null;
            Thread thread = new Thread(() => {
                try { AssertNavigationCube(); } catch (Exception exception) { failure = exception; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        /// <summary>Reads only an offscreen render target after rotating the real navigation-cube camera.</summary>
        static void AssertNavigationCube() {
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
            using EditorSessionInteractionServices interactions = new EditorSessionInteractionServices();
            EditorEntity owner = new EditorEntity(core, interactions) { InternalEntity = true };
            byte4 red = new byte4(255, 0, 0, 255);
            byte4 blue = new byte4(0, 0, 255, 255);
            using EditorNavigationCubeRenderScene cube = new EditorNavigationCubeRenderScene(owner, shaders,
                [red, red, red, red, red, blue], 64);
            renderer.Draw();
            Assert.Equal(blue, ReadPixel(cube.Target, renderer.Device, 32));
            cube.Synchronize(new float4(0, 1, 0, 0), true);
            renderer.Draw();
            Assert.Equal(red, ReadPixel(cube.Target, renderer.Device, 32));
            float4.CreateFromYawPitchRoll(0.65f, -0.4f, 0f, out float4 orientation);
            foreach (CameraProjectionMode mode in new[] { CameraProjectionMode.Perspective, CameraProjectionMode.Orthographic }) {
                cube.Synchronize(orientation, true, mode);
                Assert.Equal(mode, cube.Camera.ProjectionMode);
                renderer.Draw();
                EditorViewportNavigationCubeGeometry geometry = new EditorViewportNavigationCubeGeometry { ProjectionMode = mode };
                foreach (EditorViewportNavigationTarget face in geometry.GetVisibleFaceTargets(orientation)) {
                    IReadOnlyList<float2> vertices = geometry.GetFaceVertices(face, orientation, 64);
                    int x = (int)vertices.Average(vertex => vertex.X);
                    int y = (int)vertices.Average(vertex => vertex.Y);
                    Assert.True(geometry.TryHit(new float2(x, y), orientation, 64, out EditorViewportNavigationCubeHit hit));
                    Assert.Equal(face, hit.Target);
                    Assert.Equal(face.Z == 1 ? blue : red, ReadPixel(cube.Target, renderer.Device, y, x));
                }
            }
        }

        /// <summary>Verifies that oblique camera views compose physically separated planes from either side.</summary>
        [Fact]
        public void WorldPlanes_KeepPhysicalDepthWhenCameraOrbits() {
            Exception failure = null;
            Thread thread = new Thread(() => {
                try {
                    AssertPlanarComposition();
                } catch (Exception exception) {
                    failure = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        /// <summary>Renders overlapping red and blue planes with displaced origins into an isolated GPU target.</summary>
        static void AssertPlanarComposition() {
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
            using EditorWorldSpace2DPreviewMeshResources meshes = new EditorWorldSpace2DPreviewMeshResources(renderer);
            using DirectX11RenderTargetResource target = Assert.IsType<DirectX11RenderTargetResource>(renderer.CreateRenderTarget(64, 64));
            RuntimeTexture red = renderer.Render2D.BuildTextureFromRaw(new TextureAsset { Width = 1, Height = 1, Colors = [255, 0, 0, 255] });
            RuntimeTexture blue = renderer.Render2D.BuildTextureFromRaw(new TextureAsset { Width = 1, Height = 1, Colors = [0, 0, 255, 255] });
            Entity background = CreateEntity(core, 1);
            background.LocalPosition = new float3(0, 0, 12);
            background.LocalScale = new float3(128, 128, 1);
            background.AddComponent(new PlanarPreviewTestMeshComponent {
                Model = meshes.GetRuntimeModel(), Materials = [EditorWorldSpaceSpritePreviewMaterialFactory.Create(renderer, red, shaders)]
            });
            Entity foreground = CreateEntity(core, 1);
            foreground.LocalPosition = new float3(32, 0, 13);
            foreground.LocalScale = new float3(64, 128, 1);
            foreground.AddComponent(new PlanarPreviewTestMeshComponent {
                Model = meshes.GetRuntimeModel(), Materials = [EditorWorldSpaceSpritePreviewMaterialFactory.Create(renderer, blue, shaders)]
            });
            Entity cameraEntity = CreateEntity(core, 1);
            cameraEntity.AddComponent(new EditorViewportCameraComponent {
                LayerMask = 1, RenderTarget = target, Viewport = new float4(0, 0, 64, 64),
                ProjectionMode = CameraProjectionMode.Orthographic, OrthographicVerticalSpan = 64, FarPlaneDistance = 1000f
            });
            float4.CreateFromAxisAngle(new float3(0, 1, 0), (float)(-Math.PI / 4), out float4 front);
            cameraEntity.LocalOrientation = front;
            cameraEntity.LocalPosition = new float3(48, 48, 13) - float4.RotateVector(new float3(0, 0, -100), front);
            renderer.Draw();
            Assert.Equal(new byte4(0, 0, 255, 255), ReadPixel(target, renderer.Device, 32));

            float4.CreateFromAxisAngle(new float3(0, 1, 0), (float)(3 * Math.PI / 4), out float4 back);
            cameraEntity.LocalOrientation = back;
            cameraEntity.LocalPosition = new float3(48, 48, 13) - float4.RotateVector(new float3(0, 0, -100), back);
            renderer.Draw();
            Assert.Equal(new byte4(255, 0, 0, 255), ReadPixel(target, renderer.Device, 32));
        }

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
        /// <param name="row">Row to sample.</param>
        /// <param name="column">Column to sample, defaulting to the target center.</param>
        /// <returns>The composited pixel color.</returns>
        static byte4 ReadPixel(RenderTarget target, Device device, int row, int column = 32) {
            DirectX11RenderTargetResource resource = Assert.IsType<DirectX11RenderTargetResource>(target);
            Texture2DDescription description = resource.ColorTexture.Description;
            Assert.InRange(row, 0, description.Height - 1);
            Assert.InRange(column, 0, description.Width - 1);
            description.BindFlags = BindFlags.None;
            description.Usage = ResourceUsage.Staging;
            description.CpuAccessFlags = CpuAccessFlags.Read;
            description.OptionFlags = ResourceOptionFlags.None;
            using Texture2D staging = new Texture2D(device, description);
            device.ImmediateContext.CopyResource(resource.ColorTexture, staging);
            SharpDX.DataBox mapped = device.ImmediateContext.MapSubresource(staging, 0, MapMode.Read, MapFlags.None);
            try {
                int offset = row * mapped.RowPitch + column * 4;
                return new byte4(Marshal.ReadByte(mapped.DataPointer, offset), Marshal.ReadByte(mapped.DataPointer, offset + 1),
                    Marshal.ReadByte(mapped.DataPointer, offset + 2), Marshal.ReadByte(mapped.DataPointer, offset + 3));
            } finally {
                device.ImmediateContext.UnmapSubresource(staging, 0);
            }
        }
    }
}
