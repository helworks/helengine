using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.Mathematics.Interop;

namespace helengine.directx11 {
    /// <summary>
    /// Copies a rendered frame into a larger back buffer, the desktop equivalent of a console's video output magnifying
    /// its framebuffer. Rows are always point sampled: the back buffer height is an integer multiple k of the frame height,
    /// so every rendered row becomes exactly k identical back buffer rows. Columns are point sampled when the back buffer
    /// width is an integer multiple of the frame width. Otherwise (a console mode shown at its display aspect, such as
    /// 640 columns stretched to 1195) columns are magnified sharp-bilinear, like a TV that is continuous along a scanline:
    /// each back buffer pixel is the area-coverage blend of the one or two rendered columns its footprint overlaps, so
    /// every rendered column covers the same width, column interiors stay crisp and only the pixel straddling a column
    /// boundary is blended. Draws one full-screen triangle generated from the vertex id, so it needs no vertex buffer or
    /// input layout.
    /// </summary>
    public sealed class DirectX11MagnifyPass : IDisposable {
        /// <summary>
        /// HLSL source of the full-screen triangle, the point-sampled copy (<c>PSPoint</c>) and the sharp-bilinear column
        /// blend (<c>PSColumnCoverage</c>).
        /// <para>
        /// <c>PSColumnCoverage</c> works in exact integer arithmetic. For back buffer column x the pixel centre in rendered
        /// columns is t = (2x + 1)·W / (2·cw) and its footprint is 1/r = W/cw columns wide. B, the column boundary nearest
        /// the centre, is round(t). The share of the pixel covered by column B (right of the boundary) is
        /// clamp(0.5 + (t - B)·r, 0, 1), which with the integer numerator n = (2x + 1)·W and denominator d = 2·cw is
        /// clamp((W + n - B·d) / (2W), 0, 1). The pixel is then the blend of columns B - 1 and B (clamped to the frame)
        /// by that share. Both texels are read with <c>Load</c> at the integer rendered row y·H / ch, so rows are exact
        /// copies by construction.
        /// </para>
        /// </summary>
        const string ShaderSource = @"
Texture2D SourceTexture : register(t0);
SamplerState PointSampler : register(s0);
cbuffer MagnifySizes : register(b0) {
    uint SourceWidth;
    uint SourceHeight;
    uint TargetWidth;
    uint TargetHeight;
};
struct VertexOutput { float4 Position : SV_POSITION; float2 TexCoord : TEXCOORD0; };
VertexOutput VSMain(uint vertexId : SV_VertexID) {
    VertexOutput output;
    float2 uv = float2((vertexId << 1) & 2, vertexId & 2);
    output.TexCoord = uv;
    output.Position = float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
    return output;
}
float4 PSPoint(VertexOutput input) : SV_TARGET {
    return SourceTexture.Sample(PointSampler, input.TexCoord);
}
float4 PSColumnCoverage(VertexOutput input) : SV_TARGET {
    uint2 target = uint2(input.Position.xy);
    uint row = target.y * SourceHeight / TargetHeight;
    uint centre = (2u * target.x + 1u) * SourceWidth;
    uint span = 2u * TargetWidth;
    uint boundary = (centre + TargetWidth) / span;
    float rightShare = saturate(float((int)SourceWidth + (int)centre - (int)(boundary * span)) / float(2u * SourceWidth));
    uint left = boundary > 0u ? boundary - 1u : 0u;
    uint right = min(boundary, SourceWidth - 1u);
    float4 leftColor = SourceTexture.Load(int3(left, row, 0));
    float4 rightColor = SourceTexture.Load(int3(right, row, 0));
    return lerp(leftColor, rightColor, rightShare);
}";

        /// <summary>Vertex shader generating the full-screen triangle.</summary>
        readonly VertexShader VertexShader;

        /// <summary>Pixel shader copying the source with point filtering, used for integer column stretches.</summary>
        readonly PixelShader PointPixelShader;

        /// <summary>Pixel shader blending each pixel's one or two covered columns, used for non-integer column stretches.</summary>
        readonly PixelShader ColumnCoveragePixelShader;

        /// <summary>Point, clamp sampler used by <see cref="PointPixelShader"/>.</summary>
        readonly SamplerState PointSampler;

        /// <summary>
        /// Constant buffer holding the source and target sizes (four 32-bit unsigned integers) read by
        /// <see cref="ColumnCoveragePixelShader"/>.
        /// </summary>
        readonly SharpDX.Direct3D11.Buffer SizeBuffer;

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
            using (CompilationResult pixel = ShaderBytecode.Compile(ShaderSource, "PSPoint", "ps_4_0")) {
                PointPixelShader = new PixelShader(device, pixel.Bytecode);
            }
            using (CompilationResult pixel = ShaderBytecode.Compile(ShaderSource, "PSColumnCoverage", "ps_4_0")) {
                ColumnCoveragePixelShader = new PixelShader(device, pixel.Bytecode);
            }
            PointSampler = new SamplerState(device, new SamplerStateDescription {
                Filter = Filter.MinMagMipPoint,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                MinimumLod = 0f,
                MaximumLod = float.MaxValue
            });
            SizeBuffer = new SharpDX.Direct3D11.Buffer(device, 16, ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
        }

        /// <summary>
        /// Draws the source into the whole target: point sampled when the target width is an integer multiple of the
        /// source width, otherwise with sharp-bilinear columns and point-sampled rows.
        /// </summary>
        /// <param name="context">Immediate context.</param>
        /// <param name="source">Rendered frame.</param>
        /// <param name="sourceWidth">Rendered frame width.</param>
        /// <param name="sourceHeight">Rendered frame height.</param>
        /// <param name="target">Back buffer view.</param>
        /// <param name="targetWidth">Back buffer width.</param>
        /// <param name="targetHeight">Back buffer height, an integer multiple of <paramref name="sourceHeight"/>.</param>
        public void Execute(DeviceContext context, ShaderResourceView source, int sourceWidth, int sourceHeight, RenderTargetView target, int targetWidth, int targetHeight) {
            context.OutputMerger.SetRenderTargets((DepthStencilView)null, target);
            context.OutputMerger.SetBlendState(null);
            context.OutputMerger.SetDepthStencilState(null);
            context.Rasterizer.State = null;
            context.Rasterizer.SetViewport(new RawViewportF { X = 0f, Y = 0f, Width = targetWidth, Height = targetHeight, MinDepth = 0f, MaxDepth = 1f });
            context.InputAssembler.InputLayout = null;
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.VertexShader.Set(VertexShader);
            if (targetWidth % sourceWidth == 0) {
                context.PixelShader.Set(PointPixelShader);
                context.PixelShader.SetSampler(0, PointSampler);
            } else {
                context.UpdateSubresource(new[] { sourceWidth, sourceHeight, targetWidth, targetHeight }, SizeBuffer);
                context.PixelShader.Set(ColumnCoveragePixelShader);
                context.PixelShader.SetConstantBuffer(0, SizeBuffer);
            }
            context.PixelShader.SetShaderResource(0, source);
            context.Draw(3, 0);
            context.PixelShader.SetShaderResource(0, null);
        }

        /// <summary>Releases the shaders, sampler and size buffer.</summary>
        public void Dispose() {
            SizeBuffer.Dispose();
            PointSampler.Dispose();
            ColumnCoveragePixelShader.Dispose();
            PointPixelShader.Dispose();
            VertexShader.Dispose();
        }
    }
}
