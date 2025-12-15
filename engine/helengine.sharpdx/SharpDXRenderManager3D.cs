using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using System.Runtime.InteropServices;
using Buffer = SharpDX.Direct3D11.Buffer;
using D3DDevice = SharpDX.Direct3D11.Device;
using DxgiFactory1 = SharpDX.DXGI.Factory1;

namespace helengine.sharpdx {
    public class SharpDXRenderManager3D : RenderManager3D {
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        int drawCallsThisFrame;
        int lastDrawCalls;
        double lastFps;
        double lastFrameTimeMs;
        List<SharpDXWindow> windows;
        Dictionary<IntPtr, SharpDXWindow> windowsDict;

        public D3DDevice Device { get; private set; }
        public Adapter1 Adapter { get; private set; }

        private InputLayout layout;
        private Buffer constantBuffer;

        private VertexShader vertexShader;
        private PixelShader pixelShader;

        private BlendState blendState;

        private SharpDXRenderManager2D render2D;
        public RenderManager2D Render2D => render2D;
        private RasterizerState rasterizerState3D;
        DepthStencilState depthStencilState3D;

        public SharpDXRenderManager3D() {
            windows = new List<SharpDXWindow>();
            windowsDict = new Dictionary<nint, SharpDXWindow>();

            // Subscribe to resize events
            WindowResized += OnWindowResized;

            var factory = new DxgiFactory1();

            Adapter = factory.GetAdapter1(0);

            Device = new D3DDevice(Adapter, DeviceCreationFlags.None, new[]
            {
                FeatureLevel.Level_12_1,
                FeatureLevel.Level_12_0,
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0,
                FeatureLevel.Level_10_0,
                FeatureLevel.Level_9_3,
                FeatureLevel.Level_9_2,
                FeatureLevel.Level_9_1,
            });

            var vertexShaderByteCode = ShaderBytecode.CompileFromFile("shaders\\MiniCube.fx", "VS", "vs_4_0");
            vertexShader = new VertexShader(Device, vertexShaderByteCode);

            var pixelShaderByteCode = ShaderBytecode.CompileFromFile("shaders\\MiniCube.fx", "PS", "ps_4_0");
            pixelShader = new PixelShader(Device, pixelShaderByteCode);

            var signature = ShaderSignature.GetInputSignature(vertexShaderByteCode);

            layout = new InputLayout(
                Device,
                signature,
                VertexPositionNormalUV.Elements
            );

            constantBuffer = new Buffer(Device, Utilities.SizeOf<float4x4>(), ResourceUsage.Default,
                BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);

            blendState = CreateBlendState(BlendOption.SourceAlpha, BlendOption.InverseSourceAlpha, BlendOperation.Add);

            render2D = new SharpDXRenderManager2D(this);

            // Register debug provider for FPS/draw calls
            DebugInfoRegistry.Register(new RMDebugProvider(this));

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

        public override void Dispose() {
            base.Dispose();

            if (windows != null) {
                lock (windows) {
                    // reset per-frame counters
                    lastDrawCalls = drawCallsThisFrame;
                    drawCallsThisFrame = 0;
                    // FPS
                    double ms = stopwatch.Elapsed.TotalMilliseconds;
                    lastFrameTimeMs = ms;
                    lastFps = ms > 0 ? 1000.0 / ms : 0;
                    stopwatch.Restart();

                    for (int i = 0; i < windows.Count; i++) {
                        windows[i].Dispose();
                    }
                }
                windows = null;
            }
        }

        private BlendState CreateBlendState(BlendOption srcBlend, BlendOption destBlend, BlendOperation blendOp) {
            var blendStateDesc = new BlendStateDescription {
                AlphaToCoverageEnable = false,
                IndependentBlendEnable = false
            };

            // Configure blend mode for the first render target
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

        public override void AddWindow(IntPtr handle, int width, int height) {
            base.AddWindow(handle, width, height);

            using (var factory = Adapter.GetParent<Factory>()) {
                SharpDXWindow window = new SharpDXWindow();
                windows.Add(window);
                windowsDict.Add(handle, window);

                window.Width = width;
                window.Height = height;

                var desc = new SwapChainDescription() {
                    BufferCount = 2,
                    ModeDescription = new ModeDescription(width, height, new Rational(60, 1), Format.B8G8R8A8_UNorm),
                    IsWindowed = true,
                    OutputHandle = handle,
                    SampleDescription = new SampleDescription(1, 0),
                    SwapEffect = SwapEffect.FlipDiscard,
                    Usage = Usage.RenderTargetOutput,
                    Flags = SwapChainFlags.AllowModeSwitch
                };

                // Create swap chain
                var swapChain = new SwapChain(factory, Device, desc);
                window.Chain = swapChain;

                // Create render target view
                using (var backBuffer = swapChain.GetBackBuffer<Texture2D>(0)) {
                    var renderView = new RenderTargetView(Device, backBuffer);
                    window.RenderTarget = renderView;
                }

                Texture2D depthBuffer = new Texture2D(Device,
                new Texture2DDescription() {
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

                window.DepthView = new DepthStencilView(Device, depthBuffer);

                // Prevent window scaling
                factory.MakeWindowAssociation(handle, WindowAssociationFlags.IgnoreAll);
            }
        }

        /// <summary>
        /// Handles window resize by disposing and recreating swap chain and related resources
        /// </summary>
        void OnWindowResized(IntPtr handle, int newWidth, int newHeight) {
            if (!windowsDict.TryGetValue(handle, out SharpDXWindow? window)) {
                return; // Window not found
            }

            // Dispose current resources
            window.RenderTarget?.Dispose();
            window.DepthView?.Dispose();

            // Update window size
            window.Width = newWidth;
            window.Height = newHeight;

            // Resize the swap chain
            window.Chain.ResizeBuffers(2, newWidth, newHeight, Format.B8G8R8A8_UNorm, SwapChainFlags.AllowModeSwitch);

            // Recreate render target view
            using (var backBuffer = window.Chain.GetBackBuffer<Texture2D>(0)) {
                window.RenderTarget = new RenderTargetView(Device, backBuffer);
            }

            // Recreate depth buffer
            var depthBuffer = new Texture2D(Device, new Texture2DDescription() {
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

            window.DepthView = new DepthStencilView(Device, depthBuffer);
        }



        public override RuntimeModel BuildModelFromRaw(ModelAsset data) {
            SharpDXModelRuntimeData model = new SharpDXModelRuntimeData();

            VertexPositionNormalUV[] vertices = new VertexPositionNormalUV[data.Positions.Length];

            for (int i = 0; i < data.Positions.Length; i++) {
                float3 pos = data.Positions[i];
                float3 normal = data.Normals[i];
                float2 tex = data.TexCoords[i];
                vertices[i] = new VertexPositionNormalUV(pos, normal, tex);
            }

            model.VertexBuffer = Buffer.Create(
               Device,
               BindFlags.VertexBuffer,
               vertices);

            if (data.Indices16 == null) {

            } else {
                model.Indices = (ushort)data.Indices16.Length;
                model.IndexBuffer = Buffer.Create(Device, BindFlags.IndexBuffer, data.Indices16);
            }

            return model;
        }


        private void drawCamera(SharpDXWindow window, ICamera camera) {
            Device.ImmediateContext.ClearDepthStencilView(window.DepthView, DepthStencilClearFlags.Depth, 1.0f, 0);

            var context = Device.ImmediateContext;
            context.OutputMerger.SetBlendState(blendState);
            context.Rasterizer.State = rasterizerState3D;
            context.OutputMerger.SetDepthStencilState(depthStencilState3D, 0);

            int totalVariants = Core.Instance.ObjectManager.TotalVariants3D;
            var drawables = Core.Instance.ObjectManager.Drawables3D;

            float4x4 view;
            float3 cameraPos = camera.Parent.Position;
            float3 cameraTarget = new float3(0, 0, 0);
            float3 cameraUp = new float3(0, 1, 0);
            float4x4.CreateLookAt(ref cameraPos, ref cameraTarget, ref cameraUp, out view);

            float4 viewport = camera.Viewport;

            Device.ImmediateContext.Rasterizer.SetViewport(
                viewport.X,
                viewport.Y,
                viewport.Z,
                viewport.W
            );

            float4x4 projection;
            float4x4.CreatePerspectiveFieldOfView((float)Math.PI / 4.0f, (viewport.Z / viewport.W), 0.1f, 100f, out projection);

            float4x4 viewProj;
            float4x4.Multiply(ref view, ref projection, out viewProj);

            var buckets3D = camera.RenderBuckets3D;
            for (int variant = 0; variant < buckets3D.Length; variant++) {
                var variantBuckets = buckets3D[variant];

                for (int bucket = 0; bucket < variantBuckets.Length; bucket++) {
                    var binBuckets = variantBuckets[bucket];
                    for (int bin = 0; bin < binBuckets.Length; bin++) {
                        var rb = binBuckets[bin];
                        for (int i = 0; i < rb.Count; i++) {
                            IDrawable3D drawable = rb.Items[i];
                            if (drawable?.Parent == null || !drawable.Parent.Enabled) { continue; }

                            Entity parent = drawable.Parent;

                            SharpDXModelRuntimeData data = (SharpDXModelRuntimeData)drawable.Model;

                            // state change
                            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
                            context.VertexShader.Set(vertexShader);
                            context.PixelShader.Set(pixelShader);
                            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(data.VertexBuffer, 32, 0));
                            context.InputAssembler.SetIndexBuffer(data.IndexBuffer, Format.R16_UInt, 0);
                            context.VertexShader.SetConstantBuffer(0, constantBuffer);

                            // draw
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

                            context.DrawIndexed(data.Indices, 0, 0);
                            context.Flush();
                        }
                    }
                }
            }

            render2D.DrawCamera(window, camera);
        }



        // Debug helper to switch UI backend
        public void SetUIBackend(string mode) {
            render2D.SetUIBackend(mode);
        }

        public void IncrementDrawCalls(int n) { drawCallsThisFrame += n; }

        class RMDebugProvider : IDebugInfoProvider {
            private readonly SharpDXRenderManager3D rm;
            public RMDebugProvider(SharpDXRenderManager3D r) { rm = r; }
            public string Category => "Renderer";
            public void AppendInfo(List<(string Key, string Value)> items) {
                items.Add(("FPS", rm.lastFps.ToString("0.0")));
                items.Add(("Draw Calls", rm.lastDrawCalls.ToString()));
                items.Add(("Frame (ms)", rm.lastFrameTimeMs.ToString("0.00")));
            }
        }

        public override void Draw() {
            base.Draw();

            if (windows == null) {
                return;
            }

            var context = Device.ImmediateContext;
            context.InputAssembler.InputLayout = layout;

            var cameraBuckets = Core.Instance.ObjectManager.Cameras;

            // reset per-frame counters
            lastDrawCalls = drawCallsThisFrame;
            drawCallsThisFrame = 0;
            // FPS
            double ms = stopwatch.Elapsed.TotalMilliseconds;
            lastFrameTimeMs = ms;
            lastFps = ms > 0 ? 1000.0 / ms : 0;
            stopwatch.Restart();

            for (int i = 0; i < windows.Count; i++) {
                SharpDXWindow window = windows[i];

                Device.ImmediateContext.OutputMerger.SetTargets(window.DepthView, window.RenderTarget);
                Device.ImmediateContext.ClearRenderTargetView(window.RenderTarget, new RawColor4(1f, 0.5f, 0, 1.0f));

                for (int k = 0; k < cameraBuckets.Length; k++) {
                    var cameras = cameraBuckets[k];

                    for (int j = 0; j < cameras.Count; j++) {
                        var camera = cameras[j];
                        drawCamera(window, camera);
                    }
                }

                window.Chain.Present(0, PresentFlags.None);
            }
        }
    }
}






