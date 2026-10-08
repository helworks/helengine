using System.Runtime.InteropServices;
using helengine.vfx.io;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using D3DDevice = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace helengine.vfx.directx11 {
    /// <summary>
    /// Runs one VFX effect over every frame of a clip using a headless DirectX11 device, writing
    /// each processed frame out as an EXR file. Pass execution is delegated to
    /// <see cref="DirectX11EffectExecutor"/>, the same executor the media compositor uses.
    /// </summary>
    public sealed class DirectX11VfxEffectRunner : IDisposable {
        /// <summary>
        /// Headless device the textures for this run are created on.
        /// </summary>
        readonly D3DDevice GraphicsDevice;

        /// <summary>
        /// Immediate context used for uploads and read-back.
        /// </summary>
        readonly DeviceContext ImmediateContext;

        /// <summary>
        /// Executor holding the effect's compiled passes and intermediate targets.
        /// </summary>
        readonly DirectX11EffectExecutor Executor;

        /// <summary>
        /// Full-precision float target the final pass renders into, recreated when the clip resolution changes.
        /// </summary>
        Texture2D RenderTarget;

        /// <summary>
        /// Render target view of <see cref="RenderTarget"/>.
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
        /// Compiles the effect's passes for the supplied device.
        /// </summary>
        /// <param name="vfxDevice">Headless Direct3D11 device to run on.</param>
        /// <param name="entry">Catalog entry of the effect to run.</param>
        public DirectX11VfxEffectRunner(DirectX11VfxDevice vfxDevice, VfxEffectCatalogEntry entry) {
            if (vfxDevice == null) {
                throw new ArgumentNullException(nameof(vfxDevice));
            }
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            GraphicsDevice = vfxDevice.Device;
            ImmediateContext = GraphicsDevice.ImmediateContext;
            Executor = new DirectX11EffectExecutor(GraphicsDevice, entry, new DirectX11EffectShaderCompiler());
        }

        /// <summary>
        /// Processes every frame of a clip through the effect and writes the results out as EXR files.
        /// </summary>
        /// <param name="clip">Input clip to process, carrying one sequence per <see cref="EffectAsset.Inputs"/> entry.</param>
        /// <param name="parameterValues">Raw parameter name/value pairs for the effect.</param>
        /// <param name="outputFolder">Folder the processed frames are written into; created when missing.</param>
        /// <param name="frameFileNamePattern">Composite format string producing each output file name from its frame index.</param>
        public void Run(VfxClip clip, IReadOnlyDictionary<string, string> parameterValues, string outputFolder, string frameFileNamePattern = "frame.{0:D4}.exr") {
            if (clip == null) {
                throw new ArgumentNullException(nameof(clip));
            }
            if (parameterValues == null) {
                throw new ArgumentNullException(nameof(parameterValues));
            }
            if (string.IsNullOrWhiteSpace(outputFolder)) {
                throw new ArgumentException("Output folder must be provided.", nameof(outputFolder));
            }

            Directory.CreateDirectory(outputFolder);
            EnsureRenderTarget(clip.Width, clip.Height);

            EffectAsset effect = Executor.Effect;
            float[] paramSlots = VfxParameterSlotResolver.Resolve(effect, parameterValues);
            int roleCount = effect.Inputs.Length;

            for (int frameIndex = 0; frameIndex < clip.FrameCount; frameIndex++) {
                Texture2D[] inputTextures = new Texture2D[roleCount];
                ShaderResourceView[] inputViews = new ShaderResourceView[roleCount];
                try {
                    for (int roleIndex = 0; roleIndex < roleCount; roleIndex++) {
                        EffectInputAsset input = effect.Inputs[roleIndex];
                        string framePath = clip.GetSequence(input.Name).FramePaths[frameIndex];

                        FloatImageAsset frame = ExrFrameReader.ReadFrame(framePath, out int channelCount);
                        try {
                            ValidateFrameResolution(frame, framePath, clip.Width, clip.Height);
                            if (input.RequiresAlpha) {
                                ValidateFrameCarriesAlpha(framePath, channelCount);
                            }
                            inputTextures[roleIndex] = CreateInputTexture(frame);
                        } finally {
                            frame.Dispose();
                        }
                        inputViews[roleIndex] = new ShaderResourceView(GraphicsDevice, inputTextures[roleIndex]);
                    }

                    float normalizedTime = clip.FrameCount > 1 ? (float)frameIndex / (clip.FrameCount - 1) : 0f;
                    Executor.Execute(inputViews, clip.Width, clip.Height, paramSlots, normalizedTime, RenderTargetColorView);

                    FloatImageAsset outputFrame = ReadBackFrame(clip.Width, clip.Height);
                    string outputPath = Path.Combine(outputFolder, string.Format(frameFileNamePattern, frameIndex));
                    ExrFrameWriter.WriteFrame(outputFrame, outputPath);
                    outputFrame.Dispose();
                } finally {
                    for (int roleIndex = 0; roleIndex < roleCount; roleIndex++) {
                        inputViews[roleIndex]?.Dispose();
                        inputTextures[roleIndex]?.Dispose();
                    }
                }
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
        /// Rejects a frame that carries no alpha channel for an input the effect declared as
        /// alpha-required. Such a frame would be expanded to a fully opaque alpha of 1, which silently
        /// disables whatever compositing that input's alpha was supposed to drive.
        /// </summary>
        /// <param name="framePath">Path the frame was read from, used in the error message.</param>
        /// <param name="channelCount">Channel count the frame actually stored.</param>
        static void ValidateFrameCarriesAlpha(string framePath, int channelCount) {
            if (channelCount >= 4 || channelCount == 2) {
                return;
            }

            throw new InvalidOperationException(
                $"Frame '{framePath}' stores {channelCount} channel(s) and carries no alpha data. "
                + "This effect requires this input role to be RGBA (or gray+alpha) EXR frames, otherwise every pixel would be treated as fully opaque and compositing would silently do nothing.");
        }

        /// <summary>
        /// Recreates the output render target and its staging copy when the requested output size changes.
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

            Texture2DDescription colorDescription = new Texture2DDescription {
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

            Texture2DDescription stagingDescription = colorDescription;
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
                Texture2DDescription description = new Texture2DDescription {
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
                DataRectangle dataRectangle = new DataRectangle(handle.AddrOfPinnedObject(), frame.Width * 4 * sizeof(float));
                return new Texture2D(GraphicsDevice, description, dataRectangle);
            } finally {
                handle.Free();
            }
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
            Executor.Dispose();
        }
    }
}
