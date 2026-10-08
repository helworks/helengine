using System.Runtime.InteropServices;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using D3DBuffer = SharpDX.Direct3D11.Buffer;
using D3DDevice = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace helengine.vfx.directx11 {
    /// <summary>
    /// Runs one <see cref="EffectAsset"/> on a Direct3D 11 device: compiles every pass once, allocates the effect's
    /// scaled intermediate targets, and draws the passes in order, each as a fullscreen triangle sampling its declared
    /// inputs and targets. Both the offline exporter and the media compositor execute effects through this class, so a
    /// definition behaves identically in every tool.
    /// </summary>
    public sealed class DirectX11EffectExecutor : IDisposable {
        /// <summary>
        /// Device every shader, target and buffer is created on.
        /// </summary>
        readonly D3DDevice Device;

        /// <summary>
        /// Catalog entry whose definition this executor runs.
        /// </summary>
        readonly VfxEffectCatalogEntry Entry;

        /// <summary>
        /// Shared fullscreen-triangle vertex shader from <c>VfxCommon.hlsli</c>.
        /// </summary>
        readonly VertexShader FullscreenVertexShader;

        /// <summary>
        /// Compiled pixel shader per pass, in pass order.
        /// </summary>
        readonly PixelShader[] PassPixelShaders;

        /// <summary>
        /// Dynamic constant buffer rewritten before every pass.
        /// </summary>
        readonly D3DBuffer Constants;

        /// <summary>
        /// Bilinear clamped sampler at s0 shared by every input.
        /// </summary>
        readonly SamplerState Sampler;

        /// <summary>
        /// Rasterizer state with culling disabled, required by the fullscreen triangle's winding.
        /// </summary>
        readonly RasterizerState Raster;

        /// <summary>
        /// Intermediate targets keyed by name, sized for <see cref="TargetsWidth"/> by <see cref="TargetsHeight"/>.
        /// </summary>
        readonly Dictionary<string, DirectX11EffectTarget> Targets = new Dictionary<string, DirectX11EffectTarget>(StringComparer.Ordinal);

        /// <summary>
        /// Main input width the intermediate targets were allocated for.
        /// </summary>
        int TargetsWidth;

        /// <summary>
        /// Main input height the intermediate targets were allocated for.
        /// </summary>
        int TargetsHeight;

        /// <summary>
        /// Gets the effect definition this executor runs.
        /// </summary>
        public EffectAsset Effect {
            get {
                return Entry.Effect;
            }
        }

        /// <summary>
        /// Compiles every pass of an effect and creates the fixed pipeline state.
        /// </summary>
        /// <param name="device">Device to run on.</param>
        /// <param name="entry">Catalog entry to execute.</param>
        /// <param name="compiler">Shader compiler shared across executors.</param>
        public DirectX11EffectExecutor(D3DDevice device, VfxEffectCatalogEntry entry, DirectX11EffectShaderCompiler compiler) {
            if (device == null) {
                throw new ArgumentNullException(nameof(device));
            }
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }
            if (compiler == null) {
                throw new ArgumentNullException(nameof(compiler));
            }

            Device = device;
            Entry = entry;
            EffectPassAsset[] passes = entry.Effect.Passes;
            FullscreenVertexShader = new VertexShader(device, compiler.Compile(entry.ResolveShaderPath(passes[0]), "FullscreenVS", ShaderStage.Vertex));
            PassPixelShaders = new PixelShader[passes.Length];
            for (int index = 0; index < passes.Length; index++) {
                PassPixelShaders[index] = new PixelShader(device, compiler.Compile(entry.ResolveShaderPath(passes[index]), passes[index].PixelEntryPoint, ShaderStage.Pixel));
            }
            Constants = new D3DBuffer(device, new BufferDescription {
                SizeInBytes = VfxFrameConstants.TotalFloatCount * sizeof(float),
                Usage = ResourceUsage.Dynamic,
                BindFlags = BindFlags.ConstantBuffer,
                CpuAccessFlags = CpuAccessFlags.Write
            });
            Sampler = new SamplerState(device, new SamplerStateDescription {
                Filter = Filter.MinMagMipLinear,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                MaximumLod = float.MaxValue
            });
            Raster = new RasterizerState(device, new RasterizerStateDescription {
                CullMode = CullMode.None,
                FillMode = FillMode.Solid,
                IsDepthClipEnabled = true
            });
        }

        /// <summary>
        /// Executes every pass, writing the effect result into <paramref name="output"/>.
        /// </summary>
        /// <param name="inputs">Shader views for the effect inputs, in <see cref="EffectAsset.Inputs"/> order.</param>
        /// <param name="width">Main input width, which is also the output width.</param>
        /// <param name="height">Main input height, which is also the output height.</param>
        /// <param name="parameterSlots">Resolved parameter slots.</param>
        /// <param name="normalizedTime">Clip progress in [0, 1].</param>
        /// <param name="output">Render target view of the caller-owned output texture.</param>
        public void Execute(IReadOnlyList<ShaderResourceView> inputs, int width, int height, float[] parameterSlots, float normalizedTime, RenderTargetView output) {
            if (inputs == null || inputs.Count != Effect.Inputs.Length) {
                throw new ArgumentException($"Effect '{Effect.EffectId}' needs {Effect.Inputs.Length} input(s).", nameof(inputs));
            }
            if (output == null) {
                throw new ArgumentNullException(nameof(output));
            }
            if (parameterSlots.Any(value => !float.IsFinite(value))) {
                throw new InvalidDataException($"Effect '{Effect.EffectId}' resolved a non-finite shader parameter.");
            }

            EnsureTargets(width, height);
            Dictionary<string, ShaderResourceView> readable = new Dictionary<string, ShaderResourceView>(StringComparer.Ordinal);
            for (int index = 0; index < inputs.Count; index++) {
                readable[Effect.Inputs[index].Name] = inputs[index];
            }

            DeviceContext context = Device.ImmediateContext;
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.InputAssembler.InputLayout = null;
            context.OutputMerger.SetBlendState(null);
            context.Rasterizer.State = Raster;
            context.VertexShader.Set(FullscreenVertexShader);
            context.PixelShader.SetConstantBuffer(0, Constants);
            context.PixelShader.SetSampler(0, Sampler);

            for (int index = 0; index < Effect.Passes.Length; index++) {
                EffectPassAsset pass = Effect.Passes[index];
                bool final = pass.Writes == EffectAsset.OutputTargetName;
                DirectX11EffectTarget target = final ? null : Targets[pass.Writes];
                int targetWidth = final ? width : target.Width;
                int targetHeight = final ? height : target.Height;

                WriteConstants(context, VfxFrameConstants.Build(normalizedTime, targetWidth, targetHeight, parameterSlots, pass.PassConstants, width, height));
                context.OutputMerger.SetTargets(final ? output : target.RenderView);
                context.Rasterizer.SetViewport(0, 0, targetWidth, targetHeight, 0f, 1f);
                context.PixelShader.Set(PassPixelShaders[index]);
                for (int slot = 0; slot < pass.Reads.Length; slot++) {
                    context.PixelShader.SetShaderResource(slot, readable[pass.Reads[slot]]);
                }
                context.Draw(3, 0);
                for (int slot = 0; slot < pass.Reads.Length; slot++) {
                    context.PixelShader.SetShaderResource(slot, null);
                }
                context.OutputMerger.SetTargets(Array.Empty<RenderTargetView>());
                if (!final) {
                    readable[pass.Writes] = target.ShaderView;
                }
            }
        }

        /// <summary>
        /// Allocates the intermediate targets for a main input size, reusing them while the size is unchanged.
        /// </summary>
        /// <param name="width">Main input width.</param>
        /// <param name="height">Main input height.</param>
        void EnsureTargets(int width, int height) {
            if (width == TargetsWidth && height == TargetsHeight) {
                return;
            }
            DisposeTargets();
            foreach (EffectTargetAsset declaration in Effect.Targets) {
                int targetWidth = Math.Max(1, (int)Math.Round(width * (double)declaration.Scale));
                int targetHeight = Math.Max(1, (int)Math.Round(height * (double)declaration.Scale));
                Format format = declaration.Format == EffectTargetFormat.Rgba8 ? Format.R8G8B8A8_UNorm : Format.R16G16B16A16_Float;
                Targets[declaration.Name] = new DirectX11EffectTarget(Device, targetWidth, targetHeight, format);
            }
            TargetsWidth = width;
            TargetsHeight = height;
        }

        /// <summary>
        /// Uploads one constant buffer payload.
        /// </summary>
        /// <param name="context">Immediate context.</param>
        /// <param name="values">Payload built by <see cref="VfxFrameConstants"/>.</param>
        void WriteConstants(DeviceContext context, float[] values) {
            SharpDX.DataBox box = context.MapSubresource(Constants, 0, MapMode.WriteDiscard, MapFlags.None);
            try {
                Marshal.Copy(values, 0, box.DataPointer, values.Length);
            } finally {
                context.UnmapSubresource(Constants, 0);
            }
        }

        /// <summary>
        /// Releases the intermediate targets.
        /// </summary>
        void DisposeTargets() {
            foreach (DirectX11EffectTarget target in Targets.Values) {
                target.Dispose();
            }
            Targets.Clear();
            TargetsWidth = 0;
            TargetsHeight = 0;
        }

        /// <summary>
        /// Releases every GPU resource this executor created.
        /// </summary>
        public void Dispose() {
            DisposeTargets();
            foreach (PixelShader shader in PassPixelShaders) {
                shader.Dispose();
            }
            FullscreenVertexShader.Dispose();
            Constants.Dispose();
            Sampler.Dispose();
            Raster.Dispose();
        }
    }
}
