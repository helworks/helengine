using System;
using System.Collections.Generic;
using System.Diagnostics;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using Buffer = SharpDX.Direct3D11.Buffer;
using D3DDevice = SharpDX.Direct3D11.Device;
using DxgiFactory1 = SharpDX.DXGI.Factory1;

namespace helengine.sharpdx {
    /// <summary>
    /// SharpDX-backed renderer responsible for 3D rendering and swap chain management.
    /// </summary>
    public class SharpDXRenderer3D : RenderManager3D {
        const int SwapChainBufferCount = 2;
        /// <summary>
        /// Default forward axis for cameras before rotation.
        /// </summary>
        static readonly float3 DefaultForward = new float3(0f, 0f, -1f);
        /// <summary>
        /// Default up axis for cameras before rotation.
        /// </summary>
        static readonly float3 DefaultUp = new float3(0f, 1f, 0f);

        Stopwatch frameStopwatch = Stopwatch.StartNew();
        int drawCallsThisFrame;
        int lastDrawCalls;
        double lastFps;
        double lastFrameTimeMs;
        List<SharpDXSwapChainSurface> surfaces;
        Dictionary<IntPtr, SharpDXSwapChainSurface> surfacesByHandle;
        InputLayout inputLayout;
        Buffer constantBuffer;
        VertexShader vertexShader;
        PixelShader pixelShader;
        BlendState blendState;
        SharpDXRenderer2D renderer2D;
        RasterizerState rasterizerState3D;
        DepthStencilState depthStencilState3D;

        /// <summary>
        /// Initializes the SharpDX device and default pipelines.
        /// </summary>
        public SharpDXRenderer3D() {
            surfaces = new List<SharpDXSwapChainSurface>();
            surfacesByHandle = new Dictionary<IntPtr, SharpDXSwapChainSurface>();

            WindowResized += OnWindowResized;

            using (var factory = new DxgiFactory1()) {
                Adapter = factory.GetAdapter1(0);
            }

            Device = new D3DDevice(Adapter, DeviceCreationFlags.None, new[] {
                FeatureLevel.Level_12_1,
                FeatureLevel.Level_12_0,
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0,
                FeatureLevel.Level_10_0,
                FeatureLevel.Level_9_3,
                FeatureLevel.Level_9_2,
                FeatureLevel.Level_9_1,
            });

            using (var vertexShaderByteCode = ShaderBytecode.CompileFromFile("shaders\\MiniCube.fx", "VS", "vs_4_0")) {
                vertexShader = new VertexShader(Device, vertexShaderByteCode);
                var signature = ShaderSignature.GetInputSignature(vertexShaderByteCode);
                inputLayout = new InputLayout(Device, signature, VertexPositionNormalUV.Elements);
            }

            using (var pixelShaderByteCode = ShaderBytecode.CompileFromFile("shaders\\MiniCube.fx", "PS", "ps_4_0")) {
                pixelShader = new PixelShader(Device, pixelShaderByteCode);
            }

            constantBuffer = new Buffer(Device, Utilities.SizeOf<float4x4>(), ResourceUsage.Default,
                BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);

            blendState = CreateBlendState(BlendOption.SourceAlpha, BlendOption.InverseSourceAlpha, BlendOperation.Add);

            renderer2D = new SharpDXRenderer2D(this);
            DebugInfoRegistry.Register(new SharpDXRenderer3DDebugInfoProvider(this));

            var rasterizerDesc3D = new RasterizerStateDescription {
                CullMode = CullMode.Back,
                FillMode = FillMode.Solid,
                IsDepthClipEnabled = true
            };
            rasterizerState3D = new RasterizerState(Device, rasterizerDesc3D);

            var depthStencilDesc3D = new DepthStencilStateDescription {
                IsDepthEnabled = true,
                DepthWriteMask = DepthWriteMask.All,
                DepthComparison = Comparison.Less
            };
            depthStencilState3D = new DepthStencilState(Device, depthStencilDesc3D);
        }

        /// <summary>
        /// Gets the Direct3D device.
        /// </summary>
        public D3DDevice Device { get; }

        /// <summary>
        /// Gets the DXGI adapter used by the device.
        /// </summary>
        public Adapter1 Adapter { get; }

        /// <summary>
        /// Gets the 2D renderer used for overlay/UI rendering.
        /// </summary>
        public RenderManager2D Render2D => renderer2D;

        /// <summary>
        /// Gets the last recorded frames-per-second value.
        /// </summary>
        internal double LastFps => lastFps;

        /// <summary>
        /// Gets the draw call count from the previous frame.
        /// </summary>
        internal int LastDrawCalls => lastDrawCalls;

        /// <summary>
        /// Gets the last frame time in milliseconds.
        /// </summary>
        internal double LastFrameTimeMs => lastFrameTimeMs;

        /// <summary>
        /// Releases GPU resources and detaches from window events.
        /// </summary>
        public override void Dispose() {
            WindowResized -= OnWindowResized;

            renderer2D.Dispose();

            for (int i = 0; i < surfaces.Count; i++) {
                surfaces[i].Dispose();
            }
            surfaces.Clear();
            surfacesByHandle.Clear();

            depthStencilState3D?.Dispose();
            rasterizerState3D?.Dispose();
            blendState?.Dispose();
            constantBuffer?.Dispose();
            inputLayout?.Dispose();
            pixelShader?.Dispose();
            vertexShader?.Dispose();
            Device?.Dispose();
            Adapter?.Dispose();

            base.Dispose();
        }

        /// <summary>
        /// Adds a window and builds the swap chain and render targets.
        /// </summary>
        /// <param name="handle">Native window handle.</param>
        /// <param name="width">Window width.</param>
        /// <param name="height">Window height.</param>
        public override void AddWindow(IntPtr handle, int width, int height) {
            base.AddWindow(handle, width, height);

            using (var factory = Adapter.GetParent<Factory>()) {
                var surface = new SharpDXSwapChainSurface();
                surfaces.Add(surface);
                surfacesByHandle.Add(handle, surface);

                surface.Width = width;
                surface.Height = height;

                var desc = new SwapChainDescription {
                    BufferCount = SwapChainBufferCount,
                    ModeDescription = new ModeDescription(width, height, new Rational(60, 1), Format.B8G8R8A8_UNorm),
                    IsWindowed = true,
                    OutputHandle = handle,
                    SampleDescription = new SampleDescription(1, 0),
                    SwapEffect = SwapEffect.FlipDiscard,
                    Usage = Usage.RenderTargetOutput,
                    Flags = SwapChainFlags.AllowModeSwitch
                };

                surface.SwapChain = new SwapChain(factory, Device, desc);

                using (var backBuffer = surface.SwapChain.GetBackBuffer<Texture2D>(0)) {
                    surface.RenderTargetView = new RenderTargetView(Device, backBuffer);
                }

                surface.DepthBuffer = new Texture2D(Device, new Texture2DDescription {
                    Format = Format.D32_Float_S8X24_UInt,
                    ArraySize = 1,
                    MipLevels = 1,
                    Width = width,
                    Height = height,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Default,
                    BindFlags = BindFlags.DepthStencil,
                    CpuAccessFlags = CpuAccessFlags.None,
                    OptionFlags = ResourceOptionFlags.None
                });

                surface.DepthStencilView = new DepthStencilView(Device, surface.DepthBuffer);

                factory.MakeWindowAssociation(handle, WindowAssociationFlags.IgnoreAll);
            }
        }

        /// <summary>
        /// Handles window resize by recreating swap chain buffers and depth resources.
        /// </summary>
        /// <param name="handle">Native window handle.</param>
        /// <param name="newWidth">New width.</param>
        /// <param name="newHeight">New height.</param>
        void OnWindowResized(IntPtr handle, int newWidth, int newHeight) {
            if (!surfacesByHandle.TryGetValue(handle, out var surface)) {
                return;
            }

            surface.RenderTargetView?.Dispose();
            surface.DepthStencilView?.Dispose();
            surface.DepthBuffer?.Dispose();

            surface.Width = newWidth;
            surface.Height = newHeight;

            surface.SwapChain.ResizeBuffers(SwapChainBufferCount, newWidth, newHeight, Format.B8G8R8A8_UNorm, SwapChainFlags.AllowModeSwitch);

            using (var backBuffer = surface.SwapChain.GetBackBuffer<Texture2D>(0)) {
                surface.RenderTargetView = new RenderTargetView(Device, backBuffer);
            }

            surface.DepthBuffer = new Texture2D(Device, new Texture2DDescription {
                Format = Format.D32_Float_S8X24_UInt,
                ArraySize = 1,
                MipLevels = 1,
                Width = newWidth,
                Height = newHeight,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });

            surface.DepthStencilView = new DepthStencilView(Device, surface.DepthBuffer);
        }

        /// <summary>
        /// Builds a runtime model from raw mesh data.
        /// </summary>
        /// <param name="data">Raw model asset data.</param>
        /// <returns>GPU-ready model resource.</returns>
        public override RuntimeModel BuildModelFromRaw(ModelAsset data) {
            var model = new SharpDXModelResource();
            var vertices = new VertexPositionNormalUV[data.Positions.Length];

            for (int i = 0; i < data.Positions.Length; i++) {
                float3 pos = data.Positions[i];
                float3 normal = data.Normals[i];
                float2 tex = data.TexCoords[i];
                vertices[i] = new VertexPositionNormalUV(pos, normal, tex);
            }

            model.VertexBuffer = Buffer.Create(Device, BindFlags.VertexBuffer, vertices);
            model.VertexCount = vertices.Length;

            if (data.Indices16 != null && data.Indices16.Length > 0) {
                model.IndexCount = data.Indices16.Length;
                model.IndexBuffer = Buffer.Create(Device, BindFlags.IndexBuffer, data.Indices16);
            }

            return model;
        }

        /// <summary>
        /// Renders a camera's 3D pass and then its 2D overlay.
        /// </summary>
        /// <param name="surface">Render surface for the window.</param>
        /// <param name="camera">Camera to render.</param>
        void RenderCamera(SharpDXSwapChainSurface surface, ICamera camera) {
            Device.ImmediateContext.ClearDepthStencilView(surface.DepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);

            var context = Device.ImmediateContext;
            context.OutputMerger.SetBlendState(blendState);
            context.Rasterizer.State = rasterizerState3D;
            context.OutputMerger.SetDepthStencilState(depthStencilState3D, 0);

            float4x4 view;
            float3 cameraPos = camera.Parent.Position;
            float4 cameraOrientation = camera.Parent.Orientation;
            float3 cameraForward = float4.RotateVector(DefaultForward, cameraOrientation);
            float3 cameraUp = float4.RotateVector(DefaultUp, cameraOrientation);
            float3 cameraTarget = cameraPos + cameraForward;
            float4x4.CreateLookAt(ref cameraPos, ref cameraTarget, ref cameraUp, out view);

            float4 viewport = camera.Viewport;
            context.Rasterizer.SetViewport(viewport.X, viewport.Y, viewport.Z, viewport.W);

            float4x4 projection;
            float4x4.CreatePerspectiveFieldOfView((float)Math.PI / 4.0f, (viewport.Z / viewport.W), 0.1f, 100f, out projection);

            float4x4 viewProj;
            float4x4.Multiply(ref view, ref projection, out viewProj);

            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.VertexShader.Set(vertexShader);
            context.PixelShader.Set(pixelShader);
            context.VertexShader.SetConstantBuffer(0, constantBuffer);

            var buckets3D = camera.RenderBuckets3D;
            for (int variant = 0; variant < buckets3D.Length; variant++) {
                var variantBuckets = buckets3D[variant];
                for (int bucket = 0; bucket < variantBuckets.Length; bucket++) {
                    var binBuckets = variantBuckets[bucket];
                    for (int bin = 0; bin < binBuckets.Length; bin++) {
                        var rb = binBuckets[bin];
                        for (int i = 0; i < rb.Count; i++) {
                            IDrawable3D drawable = rb.Items[i];
                            if (drawable?.Parent == null || !drawable.Parent.Enabled) {
                                continue;
                            }

                            Entity parent = drawable.Parent;
                            var data = (SharpDXModelResource)drawable.Model;

                            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(data.VertexBuffer, Utilities.SizeOf<VertexPositionNormalUV>(), 0));
                            if (data.IndexBuffer != null && data.IndexCount > 0) {
                                context.InputAssembler.SetIndexBuffer(data.IndexBuffer, Format.R16_UInt, 0);
                            }

                            float4 orientation = parent.Orientation;
                            float4x4 rotation;
                            float4x4.CreateFromQuaternion(ref orientation, out rotation);

                            float3 scale = parent.Scale;
                            float4x4 size;
                            float4x4.CreateScale(scale.X, scale.Y, scale.Z, out size);

                            float4x4 world;
                            float4x4.Multiply(ref rotation, ref size, out world);

                            float4x4 worldViewProj;
                            float4x4.Multiply(ref world, ref viewProj, out worldViewProj);

                            float4x4 worldViewProjTransposed;
                            float4x4.Transpose(ref worldViewProj, out worldViewProjTransposed);

                            context.UpdateSubresource(ref worldViewProjTransposed, constantBuffer);

                            if (data.IndexBuffer != null && data.IndexCount > 0) {
                                context.DrawIndexed(data.IndexCount, 0, 0);
                            } else {
                                context.Draw(data.VertexCount, 0);
                            }
                        }
                    }
                }
            }

            renderer2D.RenderCamera(camera);
        }


        /// <summary>
        /// Sets the rounded-rectangle rendering backend for UI shapes.
        /// </summary>
        /// <param name="backend">Backend to use for rounded rectangles.</param>
        public void SetRoundedRectBackend(RoundedRectBackend backend) {
            renderer2D.SetRoundedRectBackend(backend);
        }

        /// <summary>
        /// Increments the per-frame draw call counter.
        /// </summary>
        /// <param name="count">Number of draw calls to add.</param>
        internal void IncrementDrawCalls(int count) {
            drawCallsThisFrame += count;
        }

        /// <summary>
        /// Executes the full render pass for all windows and cameras.
        /// </summary>
        public override void Draw() {
            base.Draw();

            if (surfaces.Count == 0) {
                return;
            }

            UpdateFrameStats();

            var context = Device.ImmediateContext;
            context.InputAssembler.InputLayout = inputLayout;

            var cameraBuckets = Core.Instance.ObjectManager.Cameras;

            for (int i = 0; i < surfaces.Count; i++) {
                var surface = surfaces[i];

                context.OutputMerger.SetTargets(surface.DepthStencilView, surface.RenderTargetView);
                context.ClearRenderTargetView(surface.RenderTargetView, new RawColor4(1f, 0.5f, 0, 1.0f));

                for (int k = 0; k < cameraBuckets.Length; k++) {
                    var cameras = cameraBuckets[k];
                    for (int j = 0; j < cameras.Count; j++) {
                        var camera = cameras[j];
                        RenderCamera(surface, camera);
                    }
                }

                surface.SwapChain.Present(0, PresentFlags.None);
            }
        }

        /// <summary>
        /// Updates FPS and draw call statistics for the current frame.
        /// </summary>
        void UpdateFrameStats() {
            lastDrawCalls = drawCallsThisFrame;
            drawCallsThisFrame = 0;

            double ms = frameStopwatch.Elapsed.TotalMilliseconds;
            lastFrameTimeMs = ms;
            lastFps = ms > 0 ? 1000.0 / ms : 0;
            frameStopwatch.Restart();
        }

        /// <summary>
        /// Creates a blend state configured for alpha blending.
        /// </summary>
        /// <param name="srcBlend">Source blend factor.</param>
        /// <param name="destBlend">Destination blend factor.</param>
        /// <param name="blendOp">Blend operation.</param>
        /// <returns>Blend state instance.</returns>
        BlendState CreateBlendState(BlendOption srcBlend, BlendOption destBlend, BlendOperation blendOp) {
            var blendStateDesc = new BlendStateDescription {
                AlphaToCoverageEnable = false,
                IndependentBlendEnable = false
            };

            blendStateDesc.RenderTarget[0] = new RenderTargetBlendDescription {
                IsBlendEnabled = true,
                SourceBlend = srcBlend,
                DestinationBlend = destBlend,
                BlendOperation = blendOp,
                SourceAlphaBlend = BlendOption.One,
                DestinationAlphaBlend = BlendOption.Zero,
                AlphaBlendOperation = BlendOperation.Add,
                RenderTargetWriteMask = ColorWriteMaskFlags.All
            };

            return new BlendState(Device, blendStateDesc);
        }
    }
}
