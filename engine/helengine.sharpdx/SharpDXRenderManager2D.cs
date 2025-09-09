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
    internal class SharpDXRenderManager2D {
        private Buffer quadBuffer;
        private InputLayout quadLayout;
        private VertexShader quadVertexShader;
        private PixelShader quadPixelShader;
        private SamplerState quadSampler;
        private Buffer quadConstantBuffer;
        private float4x4 projection2D;
        RasterizerState rasterizerState2D;
        DepthStencilState depthStencilState2D;

        public D3DDevice Device { get; private set; }

        public SharpDXRenderManager2D(SharpDXRenderManager parent) {
            Device = parent.Device;

            initSpriteQuad();

            var rasterizerDesc = new RasterizerStateDescription {
                CullMode = CullMode.None,
                FillMode = FillMode.Solid,
                IsDepthClipEnabled = false
            };
            rasterizerState2D = new RasterizerState(Device, rasterizerDesc);

            var depthStencilDesc = new DepthStencilStateDescription {
                IsDepthEnabled = false,
                DepthWriteMask = DepthWriteMask.Zero,
                DepthComparison = Comparison.Always
            };
            depthStencilState2D = new DepthStencilState(Device, depthStencilDesc);
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
                Filter = Filter.MinMagMipPoint, // Point sampling to avoid glyph bleeding
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

        internal void DrawCamera(SharpDXWindow window, ICamera camera) {
            var context = Device.ImmediateContext;

            context.Rasterizer.State = rasterizerState2D;
            context.OutputMerger.SetDepthStencilState(depthStencilState2D, 0);

            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
            context.VertexShader.Set(quadVertexShader);
            context.PixelShader.Set(quadPixelShader);
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(quadBuffer, Utilities.SizeOf<VertexPositionUV>(), 0));
            context.InputAssembler.InputLayout = quadLayout;

            float4 viewport = camera.Viewport;
            float4x4.CreateOrthographicOffCenter(0, viewport.Z, -viewport.W, 0, -10, 10, out projection2D);

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

        internal void DrawSprite(ISpriteDrawable2D drawable) {
            if (drawable.Texture == null) {
                return;
            }

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

        internal void DrawText(ITextDrawable2D drawable) {
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
            float offsetX = 0f;
            float offsetY = 0f;
            float lineHeight = Math.Max(font.LineHeight, 1f);

            for (int i = 0; i < text.Length; i++) {
                char c = text[i];

                if (c == '\n') {
                    offsetY += lineHeight;
                    offsetX = 0f;
                    continue;
                }

                if (c == ' ') {
                    offsetX += font.FontInfo.SpaceWidth;
                    continue;
                }

                if (!font.Characters.TryGetValue(c, out FontChar info)) {
                    continue; // skip missing glyphs silently
                }

                shaderData.sourceRect = info.SourceRect;
                float pixelW = shaderData.sourceRect.Z * data.Width;
                float pixelH = shaderData.sourceRect.W * data.Height;

                shaderData.destRect = new float4(
                    pos.X + offsetX,
                    pos.Y + offsetY + info.OffsetY,
                    pixelW,
                    pixelH
                );

                float advance = info.AdvanceWidth > 0 ? info.AdvanceWidth : pixelW;
                offsetX += advance;

                context.UpdateSubresource(ref shaderData, quadConstantBuffer);

                context.Draw(4, 0);
            }
        }
    }
}
