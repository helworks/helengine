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
        readonly D3DDevice device;
        readonly DeviceContext context;
        readonly VertexShader vertexShader;
        readonly PixelShader pixelShader;
        readonly SamplerState sampler;
        readonly D3DBuffer constantBuffer;

        Texture2D renderTarget;
        RenderTargetView renderTargetView;
        Texture2D stagingTexture;
        int targetWidth;
        int targetHeight;

        public DirectX11VfxEffectRunner(DirectX11VfxDevice vfxDevice, IVfxEffect effect) {
            if (vfxDevice == null) {
                throw new ArgumentNullException(nameof(vfxDevice));
            }
            if (effect == null) {
                throw new ArgumentNullException(nameof(effect));
            }

            device = vfxDevice.Device;
            context = device.ImmediateContext;

            ShaderCompileService compileService = CreateCompileService();
            ShaderCompileResult vsResult = CompileEntryPoint(compileService, effect, effect.VertexEntryPoint, ShaderStage.Vertex);
            ShaderCompileResult psResult = CompileEntryPoint(compileService, effect, effect.PixelEntryPoint, ShaderStage.Pixel);

            vertexShader = new VertexShader(device, vsResult.Binary.Bytecode);
            pixelShader = new PixelShader(device, psResult.Binary.Bytecode);

            sampler = new SamplerState(device, new SamplerStateDescription {
                Filter = Filter.MinMagMipLinear,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                MaximumLod = float.MaxValue
            });

            constantBuffer = new D3DBuffer(device, new BufferDescription {
                SizeInBytes = VfxFrameConstants.TotalFloatCount * sizeof(float),
                Usage = ResourceUsage.Dynamic,
                BindFlags = BindFlags.ConstantBuffer,
                CpuAccessFlags = CpuAccessFlags.Write
            });
        }

        static ShaderCompileService CreateCompileService() {
            var includeResolver = new ShaderFilesystemIncludeResolver(AppContext.BaseDirectory);
            var cache = new ShaderMemoryCompileCache();
            var hasher = new ShaderSourceHasher();
            var service = new ShaderCompileService(includeResolver, cache, hasher);
            service.RegisterBackend(new DirectX11ShaderBackend());
            return service;
        }

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

            Directory.CreateDirectory(outputFolder);
            EnsureRenderTarget(clip.Width, clip.Height);

            float[] paramSlots = effect.ResolveParameterSlots(parameterValues);

            for (int frameIndex = 0; frameIndex < clip.FrameCount; frameIndex++) {
                FloatImageAsset sourceFrame = ExrFrameReader.ReadFrame(clip.Source.FramePaths[frameIndex]);
                FloatImageAsset maskFrame = ExrFrameReader.ReadFrame(clip.Mask.FramePaths[frameIndex]);

                using Texture2D sourceTexture = CreateInputTexture(sourceFrame);
                using ShaderResourceView sourceView = new ShaderResourceView(device, sourceTexture);
                using Texture2D maskTexture = CreateInputTexture(maskFrame);
                using ShaderResourceView maskView = new ShaderResourceView(device, maskTexture);

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

        void EnsureRenderTarget(int width, int height) {
            if (renderTarget != null && targetWidth == width && targetHeight == height) {
                return;
            }

            renderTargetView?.Dispose();
            renderTarget?.Dispose();
            stagingTexture?.Dispose();

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
            renderTarget = new Texture2D(device, colorDescription);
            renderTargetView = new RenderTargetView(device, renderTarget);

            var stagingDescription = colorDescription;
            stagingDescription.BindFlags = BindFlags.None;
            stagingDescription.Usage = ResourceUsage.Staging;
            stagingDescription.CpuAccessFlags = CpuAccessFlags.Read;
            stagingTexture = new Texture2D(device, stagingDescription);

            targetWidth = width;
            targetHeight = height;
        }

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
                return new Texture2D(device, description, dataRectangle);
            } finally {
                handle.Free();
            }
        }

        void UpdateConstantBuffer(float normalizedTime, int width, int height, float[] paramSlots) {
            float[] frameConstants = VfxFrameConstants.Build(normalizedTime, width, height, paramSlots);
            DataBox box = context.MapSubresource(constantBuffer, 0, MapMode.WriteDiscard, MapFlags.None);
            Marshal.Copy(frameConstants, 0, box.DataPointer, frameConstants.Length);
            context.UnmapSubresource(constantBuffer, 0);
        }

        void DrawFrame(ShaderResourceView sourceView, ShaderResourceView maskView) {
            context.OutputMerger.SetRenderTargets(renderTargetView);
            context.Rasterizer.SetViewport(0, 0, targetWidth, targetHeight, 0f, 1f);
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.InputAssembler.InputLayout = null;
            context.VertexShader.Set(vertexShader);
            context.PixelShader.Set(pixelShader);
            context.PixelShader.SetConstantBuffer(0, constantBuffer);
            context.PixelShader.SetShaderResource(0, sourceView);
            context.PixelShader.SetShaderResource(1, maskView);
            context.PixelShader.SetSampler(0, sampler);

            context.Draw(3, 0);

            context.PixelShader.SetShaderResource(0, null);
            context.PixelShader.SetShaderResource(1, null);
        }

        FloatImageAsset ReadBackFrame(int width, int height) {
            context.CopyResource(renderTarget, stagingTexture);
            DataBox dataBox = context.MapSubresource(stagingTexture, 0, MapMode.Read, MapFlags.None);
            try {
                float[] pixels = new float[width * height * 4];
                int rowFloats = width * 4;
                for (int y = 0; y < height; y++) {
                    IntPtr rowPointer = dataBox.DataPointer + (y * dataBox.RowPitch);
                    Marshal.Copy(rowPointer, pixels, y * rowFloats, rowFloats);
                }
                return new FloatImageAsset { Width = (ushort)width, Height = (ushort)height, Pixels = pixels };
            } finally {
                context.UnmapSubresource(stagingTexture, 0);
            }
        }

        public void Dispose() {
            stagingTexture?.Dispose();
            renderTargetView?.Dispose();
            renderTarget?.Dispose();
            constantBuffer.Dispose();
            sampler.Dispose();
            pixelShader.Dispose();
            vertexShader.Dispose();
        }
    }
}
