using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.Mathematics.Interop;

namespace helengine.directx11 {
    /// <summary>
    /// Copies a rendered frame into a larger back buffer with point sampling (nearest texel), the desktop equivalent of a
    /// console's video output magnifying its framebuffer. Draws one full-screen triangle generated from the vertex id, so
    /// it needs no vertex buffer or input layout.
    /// </summary>
    public sealed class DirectX11MagnifyPass : IDisposable {
        /// <summary>HLSL source of the full-screen triangle and the point-sampled copy.</summary>
        const string ShaderSource = @"
Texture2D SourceTexture : register(t0);
SamplerState PointSampler : register(s0);
struct VertexOutput { float4 Position : SV_POSITION; float2 TexCoord : TEXCOORD0; };
VertexOutput VSMain(uint vertexId : SV_VertexID) {
    VertexOutput output;
    float2 uv = float2((vertexId << 1) & 2, vertexId & 2);
    output.TexCoord = uv;
    output.Position = float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
    return output;
}
float4 PSMain(VertexOutput input) : SV_TARGET {
    return SourceTexture.Sample(PointSampler, input.TexCoord);
}";

        /// <summary>Vertex shader generating the full-screen triangle.</summary>
        readonly VertexShader VertexShader;

        /// <summary>Pixel shader sampling the source with point filtering.</summary>
        readonly PixelShader PixelShader;

        /// <summary>Point, clamp sampler.</summary>
        readonly SamplerState PointSampler;

        /// <summary>
        /// Compiles the pass for one device.
        /// </summary>
        /// <param name="device">Device that owns the pass resources.</param>
        public DirectX11MagnifyPass(SharpDX.Direct3D11.Device device) {
            if (device == null) {
                throw new ArgumentNullException(nameof(device));
            }

            using (CompilationResult vertex = ShaderBytecode.Compile(ShaderSource, "VSMain", "vs_4_0")) {
                VertexShader = new VertexShader(device, vertex.Bytecode);
            }
            using (CompilationResult pixel = ShaderBytecode.Compile(ShaderSource, "PSMain", "ps_4_0")) {
                PixelShader = new PixelShader(device, pixel.Bytecode);
            }
            PointSampler = new SamplerState(device, new SamplerStateDescription {
                Filter = Filter.MinMagMipPoint,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                MinimumLod = 0f,
                MaximumLod = float.MaxValue
            });
        }

        /// <summary>
        /// Draws the source into the whole target with point sampling.
        /// </summary>
        /// <param name="context">Immediate context.</param>
        /// <param name="source">Rendered frame.</param>
        /// <param name="target">Back buffer view.</param>
        /// <param name="targetWidth">Back buffer width.</param>
        /// <param name="targetHeight">Back buffer height.</param>
        public void Execute(DeviceContext context, ShaderResourceView source, RenderTargetView target, int targetWidth, int targetHeight) {
            context.OutputMerger.SetRenderTargets((DepthStencilView)null, target);
            context.OutputMerger.SetBlendState(null);
            context.OutputMerger.SetDepthStencilState(null);
            context.Rasterizer.State = null;
            context.Rasterizer.SetViewport(new RawViewportF { X = 0f, Y = 0f, Width = targetWidth, Height = targetHeight, MinDepth = 0f, MaxDepth = 1f });
            context.InputAssembler.InputLayout = null;
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.VertexShader.Set(VertexShader);
            context.PixelShader.Set(PixelShader);
            context.PixelShader.SetShaderResource(0, source);
            context.PixelShader.SetSampler(0, PointSampler);
            context.Draw(3, 0);
            context.PixelShader.SetShaderResource(0, null);
        }

        /// <summary>Releases the shaders and sampler.</summary>
        public void Dispose() {
            PointSampler.Dispose();
            PixelShader.Dispose();
            VertexShader.Dispose();
        }
    }
}
