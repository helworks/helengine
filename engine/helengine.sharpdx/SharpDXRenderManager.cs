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
    public class SharpDXRenderManager : RenderManager {
        List<SharpDXWindow> windows;
        Dictionary<IntPtr, SharpDXWindow> windowsDict;

        public D3DDevice Device { get; private set; }
        public Adapter1 Adapter { get; private set; }

        private InputLayout layout;
        private Buffer constantBuffer;

        private VertexShader vertexShader;
        private PixelShader pixelShader;

        private Buffer quadBuffer;
        private InputLayout quadLayout;
        private VertexShader quadVertexShader;
        private PixelShader quadPixelShader;
        private SamplerState quadSampler;
        private Buffer quadConstantBuffer;
        private BlendState blendState;

        public SharpDXRenderManager() {
            windows = new List<SharpDXWindow>();
            windowsDict = new Dictionary<nint, SharpDXWindow>();

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

            initSpriteQuad();

            blendState = CreateBlendState(BlendOption.SourceAlpha, BlendOption.InverseSourceAlpha, BlendOperation.Add);
        }

        private void initSpriteQuad() {
            var vertices = new[]
            {
                new VertexPositionUV(new float3(-0.5f, -0.5f, 0), new float2(0, 1)),
                new VertexPositionUV(new float3(-0.5f, 0.5f, 0), new float2(0, 0)),
                new VertexPositionUV(new float3(0.5f, -0.5f, 0), new float2(1, 1)),
                new VertexPositionUV(new float3(0.5f, 0.5f, 0), new float2(1, 0))
            };

            // Create the vertex buffer
            quadBuffer = Buffer.Create(Device, BindFlags.VertexBuffer, vertices);

            var vertexShaderByteCode = ShaderBytecode.CompileFromFile("shaders\\SpriteShader.fx", "VS", "vs_4_0");
            quadVertexShader = new VertexShader(Device, vertexShaderByteCode);

            var pixelShaderByteCode = ShaderBytecode.CompileFromFile("shaders\\SpriteShader.fx", "PS", "ps_4_0");
            quadPixelShader = new PixelShader(Device, pixelShaderByteCode);

            quadLayout = new InputLayout(Device, vertexShaderByteCode, new[]
            {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0)
            });

            var samplerDesc = new SamplerStateDescription() {
                Filter = Filter.MinMagMipLinear, // Linear filtering
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                ComparisonFunction = Comparison.Never,
                MinimumLod = 0,
                MaximumLod = float.MaxValue
            };

            quadSampler = new SamplerState(Device, samplerDesc);

            var bufferDesc = new BufferDescription(
                Marshal.SizeOf<SpriteShaderData>(),
                ResourceUsage.Default,
                BindFlags.ConstantBuffer,
                CpuAccessFlags.None,
                ResourceOptionFlags.None,
                0
            );

            quadConstantBuffer = new Buffer(Device, bufferDesc);
        }

        public override void Dispose() {
            base.Dispose();

            if (windows != null) {
                lock (windows) {
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

        public override RuntimeTexture BuildTextureFromRaw(TextureAsset data) {
            SharpDXTextureRuntimeData asset = new SharpDXTextureRuntimeData();
            asset.Width = data.Width;
            asset.Height = data.Height;

            // Validate the data length matches the expected dimensions
            int bytesPerPixel = 4; // For R8G8B8A8 format
            int expectedDataLength = data.Width * data.Height * bytesPerPixel;
            if (data.Colors.Length != expectedDataLength) {
                throw new ArgumentException("Data length does not match width and height.");
            }

            // Define the texture description
            var textureDesc = new Texture2DDescription {
                Width = data.Width,
                Height = data.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = SharpDX.DXGI.Format.R8G8B8A8_UNorm, // 32-bit RGBA format
                SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            };

            // Pin the byte array to access its memory
            GCHandle dataHandle = GCHandle.Alloc(data.Colors, GCHandleType.Pinned);
            try {
                IntPtr dataPtr = dataHandle.AddrOfPinnedObject();
                int rowPitch = data.Width * bytesPerPixel; // Bytes per row

                // Create the texture with the initial data
                asset.Texture = new Texture2D(
                    Device,
                    textureDesc,
                    new DataRectangle(dataPtr, rowPitch)
                );
            } finally {
                dataHandle.Free(); // Unpin the array
            }

            asset.Resource = new ShaderResourceView(Device, asset.Texture);

            return asset;
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

        private float4x4 projection2D;

        private void drawCamera(SharpDXWindow window, ICamera camera) {
            var context = Device.ImmediateContext;

            int totalVariants = Core.Instance.ObjectManager.TotalVariants3D;
            var drawables = Core.Instance.ObjectManager.Drawables3D;

            float4x4 view;
            float3 cameraPos = camera.Parent.Position;
            float3 cameraTarget = new float3(0, 0, 0);
            float3 cameraUp = new float3(0, 1, 0);
            float4x4.CreateLookAt(ref cameraPos, ref cameraTarget, ref cameraUp, out view);

            float4 viewport = camera.Viewport;

            Device.ImmediateContext.Rasterizer.SetViewport(
                viewport.X * (float)window.Width,
                viewport.Y * (float)window.Height,
                viewport.Z * (float)window.Width,
                viewport.W * (float)window.Height
            );

            float4x4 projection;
            float4x4.CreatePerspectiveFieldOfView((float)Math.PI / 4.0f, ((viewport.Z * (float)window.Width) / (viewport.W * (float)window.Height)), 0.1f, 100f, out projection);

            float4x4 viewProj;
            float4x4.Multiply(ref view, ref projection, out viewProj);

            List<int>[][] renderIndices = camera.RenderIndices3D;
            for (int variant = 0; variant < renderIndices.Length; variant++) {
                List<int>[] buckets = renderIndices[variant];

                for (int bucket = 0; bucket < buckets.Length; bucket++) {
                    List<int> indices = buckets[bucket];

                    for (int i = 0; i < indices.Count; i++) {
                        int indice = indices[i];
                        IDrawable3D drawable = drawables[indice];

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

            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
            context.VertexShader.Set(quadVertexShader);
            context.PixelShader.Set(quadPixelShader);
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(quadBuffer, Utilities.SizeOf<VertexPositionUV>(), 0));
            context.InputAssembler.InputLayout = quadLayout;
            context.OutputMerger.SetBlendState(blendState);

            float4x4.CreateOrthographicOffCenter(0, viewport.Z * window.Width, viewport.W * -window.Height, 0, -10, 10, out projection2D);

            var drawables2D = Core.Instance.ObjectManager.Drawables2D;
            List<int>[] renderBuckets2D = camera.RenderIndices2D;
            for (int bucket = 0; bucket < renderBuckets2D.Length; bucket++) {
                List<int> indices = renderBuckets2D[bucket];

                for (int j = 0; j < indices.Count; j++) {
                    int indice = indices[j];
                    IDrawable2D drawable = drawables2D[indice];

                    drawable.Draw();
                }
            }
        }

        public override void DrawSprite(ISpriteDrawable2D drawable) {
            var context = Device.ImmediateContext;
            SharpDXTextureRuntimeData data = (SharpDXTextureRuntimeData)drawable.Texture;

            // Bind the texture
            context.PixelShader.SetShaderResource(0, data.Resource);
            context.PixelShader.SetSampler(0, quadSampler);

            int2 size = drawable.Size;
            float3 pos = drawable.Parent.Position;
            byte4 color = drawable.Color;

            float4x4 transposedWorld;
            float4x4.Transpose(ref projection2D, out transposedWorld);

            context.VertexShader.SetConstantBuffer(0, quadConstantBuffer);
            context.PixelShader.SetConstantBuffer(0, quadConstantBuffer);

            SpriteShaderData shaderData = new SpriteShaderData();
            shaderData.worldViewProj = transposedWorld;
            shaderData.sourceRect = drawable.SourceRect;
            shaderData.destRect = new float4(pos.X, pos.Y, size.X, size.Y);
            shaderData.color = new float4(color.X / 255.0f, color.Y / 255.0f, color.Z / 255.0f, color.W / 255.0f);
            context.UpdateSubresource(ref shaderData, quadConstantBuffer);

            context.Draw(4, 0);
        }

        public override void DrawText(ITextDrawable2D drawable) {
            var context = Device.ImmediateContext;

            FontAsset font = drawable.Font;
            SharpDXTextureRuntimeData data = (SharpDXTextureRuntimeData)font.Texture;

            // bind the texture
            context.PixelShader.SetShaderResource(0, data.Resource);
            context.PixelShader.SetSampler(0, quadSampler);

            float3 pos = drawable.Parent.Position;
            byte4 color = drawable.Color;

            float4x4 transposedWorld;
            float4x4.Transpose(ref projection2D, out transposedWorld);

            context.VertexShader.SetConstantBuffer(0, quadConstantBuffer);
            context.PixelShader.SetConstantBuffer(0, quadConstantBuffer);

            SpriteShaderData shaderData = new SpriteShaderData();
            shaderData.worldViewProj = transposedWorld;
            shaderData.color = new float4(color.X / 255.0f, color.Y / 255.0f, color.Z / 255.0f, color.W / 255.0f);

            string text = drawable.Text;
            float offsetX = 0;
            for (int i = 0; i < text.Length; i++) {
                char c = text[i];

                if (c == ' ') {
                    offsetX += font.FontInfo.SpaceWidth;
                    continue;
                }

                FontChar info = font.Characters[c];

                shaderData.sourceRect = info.SourceRect;
                shaderData.destRect = new float4(
                    pos.X + offsetX,
                    pos.Y,
                    shaderData.sourceRect.Z * data.Width,
                    shaderData.sourceRect.W * data.Height
                );

                offsetX += shaderData.sourceRect.Z * data.Width;

                context.UpdateSubresource(ref shaderData, quadConstantBuffer);

                context.Draw(4, 0);
            }
        }

        public override void Draw() {
            base.Draw();

            var context = Device.ImmediateContext;
            context.InputAssembler.InputLayout = layout;

            var cameraBuckets = Core.Instance.ObjectManager.Cameras;

            for (int i = 0; i < windows.Count; i++) {
                SharpDXWindow window = windows[i];

                Device.ImmediateContext.OutputMerger.SetTargets(window.DepthView, window.RenderTarget);
                Device.ImmediateContext.ClearDepthStencilView(window.DepthView, DepthStencilClearFlags.Depth, 1.0f, 0);
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
