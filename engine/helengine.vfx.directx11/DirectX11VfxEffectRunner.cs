using System.Runtime.InteropServices;
using helengine.directx11;
using helengine.vfx.io;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using D3DBuffer = SharpDX.Direct3D11.Buffer;
using D3DDevice = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace helengine.vfx.directx11 {
    /// <summary>
    /// Runs one VFX effect over every frame of a clip using a headless DirectX11 device, writing
    /// each processed frame out as an EXR file.
    /// </summary>
    public sealed class DirectX11VfxEffectRunner : IDisposable {
        /// <summary>
        /// Headless device the shaders and textures for this run are created on.
        /// </summary>
        readonly D3DDevice GraphicsDevice;

        /// <summary>
        /// Immediate context used to issue every draw, map, and copy for this run.
        /// </summary>
        readonly DeviceContext ImmediateContext;

        /// <summary>
        /// Id of the effect whose shaders were compiled by the constructor. Run refuses to execute a
        /// different effect, because the compiled shaders would not match the resolved parameters.
        /// </summary>
        readonly string CompiledEffectId;

        /// <summary>
        /// Compiled fullscreen-triangle vertex shader for the effect.
        /// </summary>
        readonly VertexShader EffectVertexShader;

        /// <summary>
        /// Compiled pixel shader carrying the effect's actual image processing.
        /// </summary>
        readonly PixelShader EffectPixelShader;

        /// <summary>
        /// Bilinear, clamped sampler bound to s0 for both the source and mask textures.
        /// </summary>
        readonly SamplerState LinearClampSampler;

        /// <summary>
        /// Dynamic constant buffer holding the per-frame VfxFrameConstants payload at b0.
        /// </summary>
        readonly D3DBuffer FrameConstantBuffer;

        /// <summary>
        /// Rasterizer state with culling disabled; mandatory for the fullscreen-triangle vertex shader.
        /// </summary>
        readonly RasterizerState NoCullRasterizerState;

        /// <summary>
        /// Float render target the effect draws into, recreated when the clip resolution changes.
        /// </summary>
        Texture2D RenderTarget;

        /// <summary>
        /// Render target view bound to the output merger for <see cref="RenderTarget"/>.
        /// </summary>
        RenderTargetView RenderTargetColorView;

        /// <summary>
        /// CPU-readable staging copy of <see cref="RenderTarget"/> used to pull processed pixels back.
        /// </summary>
        Texture2D StagingTexture;

        /// <summary>
        /// Width the current render target and staging texture were created at.
        /// </summary>
        int TargetWidth;

        /// <summary>
        /// Height the current render target and staging texture were created at.
        /// </summary>
        int TargetHeight;

        /// <summary>
        /// Compiles the effect's shaders and builds the fixed pipeline state the run reuses for every frame.
        /// </summary>
        /// <param name="vfxDevice">Headless Direct3D11 device to run on.</param>
        /// <param name="effect">Effect whose vertex and pixel entry points should be compiled.</param>
        public DirectX11VfxEffectRunner(DirectX11VfxDevice vfxDevice, IVfxEffect effect) {
            if (vfxDevice == null) {
                throw new ArgumentNullException(nameof(vfxDevice));
            }
            if (effect == null) {
                throw new ArgumentNullException(nameof(effect));
            }

            GraphicsDevice = vfxDevice.Device;
            ImmediateContext = GraphicsDevice.ImmediateContext;
            CompiledEffectId = effect.Id;

            ShaderCompileService compileService = CreateCompileService();
            ShaderCompileResult vsResult = CompileEntryPoint(compileService, effect, effect.VertexEntryPoint, ShaderStage.Vertex);
            ShaderCompileResult psResult = CompileEntryPoint(compileService, effect, effect.PixelEntryPoint, ShaderStage.Pixel);

            EffectVertexShader = new VertexShader(GraphicsDevice, vsResult.Binary.Bytecode);
            EffectPixelShader = new PixelShader(GraphicsDevice, psResult.Binary.Bytecode);

            LinearClampSampler = new SamplerState(GraphicsDevice, new SamplerStateDescription {
                Filter = Filter.MinMagMipLinear,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                MaximumLod = float.MaxValue
            });

            FrameConstantBuffer = new D3DBuffer(GraphicsDevice, new BufferDescription {
                SizeInBytes = VfxFrameConstants.TotalFloatCount * sizeof(float),
                Usage = ResourceUsage.Dynamic,
                BindFlags = BindFlags.ConstantBuffer,
                CpuAccessFlags = CpuAccessFlags.Write
            });

            // CullMode.None is mandatory, not a default: the shared FullscreenVS in VfxCommon.hlsli
            // emits its big triangle in clockwise winding, which the default rasterizer state treats
            // as back-facing and culls, leaving the render target completely black. Do not "tidy" this
            // back to a default rasterizer state without also reversing the vertex order in FullscreenVS.
            NoCullRasterizerState = new RasterizerState(GraphicsDevice, new RasterizerStateDescription {
                CullMode = CullMode.None,
                FillMode = FillMode.Solid,
                IsDepthClipEnabled = true
            });
        }

        /// <summary>
        /// Builds a shader compile service whose include resolver is rooted at the application base
        /// directory, so effect shaders can #include the shared VfxCommon.hlsli declarations.
        /// </summary>
        /// <returns>A compile service with the DirectX11 backend registered.</returns>
        static ShaderCompileService CreateCompileService() {
            var includeResolver = new ShaderFilesystemIncludeResolver(AppContext.BaseDirectory);
            var cache = new ShaderMemoryCompileCache();
            var hasher = new ShaderSourceHasher();
            var service = new ShaderCompileService(includeResolver, cache, hasher);
            service.RegisterBackend(new DirectX11ShaderBackend());
            return service;
        }

        /// <summary>
        /// Compiles one entry point out of an effect's shader file.
        /// </summary>
        /// <param name="compileService">Compile service to use.</param>
        /// <param name="effect">Effect that owns the shader file.</param>
        /// <param name="entryPoint">Entry point function name to compile.</param>
        /// <param name="stage">Pipeline stage the entry point targets.</param>
        /// <returns>The successful compile result.</returns>
        static ShaderCompileResult CompileEntryPoint(ShaderCompileService compileService, IVfxEffect effect, string entryPoint, ShaderStage stage) {
            string path = Path.Combine(AppContext.BaseDirectory, effect.ShaderResourcePath);
            var options = new ShaderCompileOptions(ShaderBindingPolicies.Default, generateDebugInfo: false, optimize: true, treatWarningsAsErrors: false);
            ShaderCompileResult result = compileService.CompileFromFile(
                path,
                effect.Id + "." + entryPoint,
                entryPoint,
                stage,
                ShaderCompileTarget.DirectX11,
                new ShaderModel(4, 0),
                "default",
                Array.Empty<ShaderDefine>(),
                options);

            if (!result.Success) {
                throw new InvalidOperationException($"Failed to compile '{entryPoint}' in '{path}'.");
            }

            return result;
        }

        /// <summary>
        /// Processes every frame of a clip through the effect and writes the results out as EXR files.
        /// </summary>
        /// <param name="clip">Source/mask clip to process.</param>
        /// <param name="effect">Effect to run; must be the same effect this runner compiled.</param>
        /// <param name="parameterValues">Raw parameter name/value pairs for the effect.</param>
        /// <param name="outputFolder">Folder the processed frames are written into; created when missing.</param>
        /// <param name="frameFileNamePattern">Composite format string producing each output file name from its frame index.</param>
        public void Run(VfxClip clip, IVfxEffect effect, IReadOnlyDictionary<string, string> parameterValues, string outputFolder, string frameFileNamePattern = "frame.{0:D4}.exr") {
            if (clip == null) {
                throw new ArgumentNullException(nameof(clip));
            }
            if (effect == null) {
                throw new ArgumentNullException(nameof(effect));
            }
            if (parameterValues == null) {
                throw new ArgumentNullException(nameof(parameterValues));
            }
            if (string.IsNullOrWhiteSpace(outputFolder)) {
                throw new ArgumentException("Output folder must be provided.", nameof(outputFolder));
            }
            if (!string.Equals(effect.Id, CompiledEffectId, StringComparison.Ordinal)) {
                throw new InvalidOperationException(
                    $"This runner compiled the shaders for effect '{CompiledEffectId}' but Run was given effect '{effect.Id}'. "
                    + "Construct a new runner for each effect.");
            }

            Directory.CreateDirectory(outputFolder);
            EnsureRenderTarget(clip.Width, clip.Height);

            float[] paramSlots = effect.ResolveParameterSlots(parameterValues);

            for (int frameIndex = 0; frameIndex < clip.FrameCount; frameIndex++) {
                string sourcePath = clip.Source.FramePaths[frameIndex];
                string maskPath = clip.Mask.FramePaths[frameIndex];

                FloatImageAsset sourceFrame = ExrFrameReader.ReadFrame(sourcePath);
                FloatImageAsset maskFrame = ExrFrameReader.ReadFrame(maskPath, out int maskChannelCount);
                try {
                    ValidateFrameResolution(sourceFrame, sourcePath, clip.Width, clip.Height);
                    ValidateFrameResolution(maskFrame, maskPath, clip.Width, clip.Height);
                    ValidateMaskCarriesAlpha(maskPath, maskChannelCount);
                } catch {
                    sourceFrame.Dispose();
                    maskFrame.Dispose();
                    throw;
                }

                using Texture2D sourceTexture = CreateInputTexture(sourceFrame);
                using ShaderResourceView sourceView = new ShaderResourceView(GraphicsDevice, sourceTexture);
                using Texture2D maskTexture = CreateInputTexture(maskFrame);
                using ShaderResourceView maskView = new ShaderResourceView(GraphicsDevice, maskTexture);

                sourceFrame.Dispose();
                maskFrame.Dispose();

                float normalizedTime = clip.FrameCount > 1 ? (float)frameIndex / (clip.FrameCount - 1) : 0f;
                UpdateConstantBuffer(normalizedTime, clip.Width, clip.Height, paramSlots);

                DrawFrame(sourceView, maskView);

                FloatImageAsset outputFrame = ReadBackFrame(clip.Width, clip.Height);
                string outputPath = Path.Combine(outputFolder, string.Format(frameFileNamePattern, frameIndex));
                ExrFrameWriter.WriteFrame(outputFrame, outputPath);
                outputFrame.Dispose();
            }
        }

        /// <summary>
        /// Rejects a frame whose resolution differs from the clip's. Only the first frame of each
        /// sequence is probed when the clip is built, and UV sampling would silently rescale a
        /// mismatched frame instead of failing, so the mismatch has to be caught explicitly here.
        /// </summary>
        /// <param name="frame">Decoded frame to check.</param>
        /// <param name="framePath">Path the frame was read from, used in the error message.</param>
        /// <param name="expectedWidth">Clip width every frame must match.</param>
        /// <param name="expectedHeight">Clip height every frame must match.</param>
        static void ValidateFrameResolution(FloatImageAsset frame, string framePath, int expectedWidth, int expectedHeight) {
            if (frame.Width == expectedWidth && frame.Height == expectedHeight) {
                return;
            }

            throw new InvalidOperationException(
                $"Frame '{framePath}' is {frame.Width}x{frame.Height} but the clip is {expectedWidth}x{expectedHeight}. "
                + "Every frame in a sequence must share the clip resolution.");
        }

        /// <summary>
        /// Rejects a mask frame that carries no alpha channel. Such a frame would be expanded to a
        /// fully opaque alpha of 1, which makes the compositing lerp a no-op and silently disables
        /// masking for the whole export.
        /// </summary>
        /// <param name="maskPath">Path the mask frame was read from, used in the error message.</param>
        /// <param name="maskChannelCount">Channel count the mask file actually stored.</param>
        static void ValidateMaskCarriesAlpha(string maskPath, int maskChannelCount) {
            // 4+ channels are RGBA; exactly 2 channels are gray+alpha. Both carry a real alpha
            // channel. 1 and 3 channel frames do not, and get an alpha of 1 synthesized on read.
            if (maskChannelCount >= 4 || maskChannelCount == 2) {
                return;
            }

            throw new InvalidOperationException(
                $"Mask frame '{maskPath}' stores {maskChannelCount} channel(s) and carries no alpha data. "
                + "Mask sequences must be RGBA (or gray+alpha) EXR frames, otherwise every pixel would be treated as fully opaque and masking would silently do nothing.");
        }

        /// <summary>
        /// Recreates the render target and its staging copy when the requested output size changes.
        /// </summary>
        /// <param name="width">Required output width in pixels.</param>
        /// <param name="height">Required output height in pixels.</param>
        void EnsureRenderTarget(int width, int height) {
            if (RenderTarget != null && TargetWidth == width && TargetHeight == height) {
                return;
            }

            RenderTargetColorView?.Dispose();
            RenderTarget?.Dispose();
            StagingTexture?.Dispose();

            var colorDescription = new Texture2DDescription {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.R32G32B32A32_Float,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            };
            RenderTarget = new Texture2D(GraphicsDevice, colorDescription);
            RenderTargetColorView = new RenderTargetView(GraphicsDevice, RenderTarget);

            var stagingDescription = colorDescription;
            stagingDescription.BindFlags = BindFlags.None;
            stagingDescription.Usage = ResourceUsage.Staging;
            stagingDescription.CpuAccessFlags = CpuAccessFlags.Read;
            StagingTexture = new Texture2D(GraphicsDevice, stagingDescription);

            TargetWidth = width;
            TargetHeight = height;
        }

        /// <summary>
        /// Uploads one decoded frame into an immutable float texture the pixel shader can sample.
        /// </summary>
        /// <param name="frame">Decoded RGBA float frame to upload.</param>
        /// <returns>The created GPU texture.</returns>
        Texture2D CreateInputTexture(FloatImageAsset frame) {
            GCHandle handle = GCHandle.Alloc(frame.Pixels, GCHandleType.Pinned);
            try {
                var description = new Texture2DDescription {
                    Width = frame.Width,
                    Height = frame.Height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = Format.R32G32B32A32_Float,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Immutable,
                    BindFlags = BindFlags.ShaderResource,
                    CpuAccessFlags = CpuAccessFlags.None,
                    OptionFlags = ResourceOptionFlags.None
                };
                var dataRectangle = new DataRectangle(handle.AddrOfPinnedObject(), frame.Width * 4 * sizeof(float));
                return new Texture2D(GraphicsDevice, description, dataRectangle);
            } finally {
                handle.Free();
            }
        }

        /// <summary>
        /// Rewrites the per-frame constant buffer with the current clip progress and parameter slots.
        /// </summary>
        /// <param name="normalizedTime">Clip progress in [0, 1] for the frame about to be drawn.</param>
        /// <param name="width">Output width in pixels.</param>
        /// <param name="height">Output height in pixels.</param>
        /// <param name="paramSlots">Resolved effect parameter slots.</param>
        void UpdateConstantBuffer(float normalizedTime, int width, int height, float[] paramSlots) {
            float[] frameConstants = VfxFrameConstants.Build(normalizedTime, width, height, paramSlots);
            DataBox box = ImmediateContext.MapSubresource(FrameConstantBuffer, 0, MapMode.WriteDiscard, MapFlags.None);
            Marshal.Copy(frameConstants, 0, box.DataPointer, frameConstants.Length);
            ImmediateContext.UnmapSubresource(FrameConstantBuffer, 0);
        }

        /// <summary>
        /// Issues the single fullscreen-triangle draw that processes one frame, then unbinds the
        /// input views so the next frame's textures can be created without a lingering reference.
        /// </summary>
        /// <param name="sourceView">Shader resource view for the source color frame, bound to t0.</param>
        /// <param name="maskView">Shader resource view for the mask frame, bound to t1.</param>
        void DrawFrame(ShaderResourceView sourceView, ShaderResourceView maskView) {
            ImmediateContext.OutputMerger.SetRenderTargets(RenderTargetColorView);
            ImmediateContext.Rasterizer.State = NoCullRasterizerState;
            ImmediateContext.Rasterizer.SetViewport(0, 0, TargetWidth, TargetHeight, 0f, 1f);
            ImmediateContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            ImmediateContext.InputAssembler.InputLayout = null;
            ImmediateContext.VertexShader.Set(EffectVertexShader);
            ImmediateContext.PixelShader.Set(EffectPixelShader);
            ImmediateContext.PixelShader.SetConstantBuffer(0, FrameConstantBuffer);
            ImmediateContext.PixelShader.SetShaderResource(0, sourceView);
            ImmediateContext.PixelShader.SetShaderResource(1, maskView);
            ImmediateContext.PixelShader.SetSampler(0, LinearClampSampler);

            ImmediateContext.Draw(3, 0);

            ImmediateContext.PixelShader.SetShaderResource(0, null);
            ImmediateContext.PixelShader.SetShaderResource(1, null);
        }

        /// <summary>
        /// Copies the render target into the staging texture and reads it back row by row, honoring
        /// the staging texture's row pitch, into a tightly packed RGBA float image.
        /// </summary>
        /// <param name="width">Output width in pixels.</param>
        /// <param name="height">Output height in pixels.</param>
        /// <returns>The processed frame, top row first.</returns>
        FloatImageAsset ReadBackFrame(int width, int height) {
            ImmediateContext.CopyResource(RenderTarget, StagingTexture);
            DataBox dataBox = ImmediateContext.MapSubresource(StagingTexture, 0, MapMode.Read, MapFlags.None);
            try {
                float[] pixels = new float[width * height * 4];
                int rowFloats = width * 4;
                for (int y = 0; y < height; y++) {
                    IntPtr rowPointer = dataBox.DataPointer + (y * dataBox.RowPitch);
                    Marshal.Copy(rowPointer, pixels, y * rowFloats, rowFloats);
                }
                return new FloatImageAsset { Width = (ushort)width, Height = (ushort)height, Pixels = pixels };
            } finally {
                ImmediateContext.UnmapSubresource(StagingTexture, 0);
            }
        }

        /// <summary>
        /// Releases every GPU resource this runner created. The device itself is owned by the
        /// DirectX11VfxDevice that was passed in and is deliberately not disposed here.
        /// </summary>
        public void Dispose() {
            StagingTexture?.Dispose();
            RenderTargetColorView?.Dispose();
            RenderTarget?.Dispose();
            NoCullRasterizerState.Dispose();
            FrameConstantBuffer.Dispose();
            LinearClampSampler.Dispose();
            EffectPixelShader.Dispose();
            EffectVertexShader.Dispose();
        }
    }
}
