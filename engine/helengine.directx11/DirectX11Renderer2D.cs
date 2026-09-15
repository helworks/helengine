using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Buffer = SharpDX.Direct3D11.Buffer;
using D3DDevice = SharpDX.Direct3D11.Device;

namespace helengine.directx11 {
    /// <summary>
    /// Selects the backend used to render rounded rectangles.
    /// </summary>
    public enum RoundedRectBackend {
        /// <summary>
        /// Signed distance field shader path.
        /// </summary>
        Sdf,
        /// <summary>
        /// Nine-slice atlas path.
        /// </summary>
        NineSlice,
        /// <summary>
        /// Procedural geometry path.
        /// </summary>
        Geometry
    }

    /// <summary>
    /// DirectX11-backed renderer responsible for 2D sprites, text, and UI shapes.
    /// </summary>
    [NativeMigrationRequired(
        "windows.native_directx_renderer",
        "Changes to this managed DirectX11 renderer must also be applied to the Windows native DirectX renderer implementation.")]
    internal class DirectX11Renderer2D : RenderManager2D, IRenderVisitor2D {
        const int InitialGeometryVertexCapacity = 1024;

        readonly DirectX11Renderer3D ParentRenderer;
        readonly HashSet<DirectX11TextureResource> OwnedTextures = new();
        Buffer SpriteQuadBuffer = null!;
        InputLayout SpriteInputLayout = null!;
        InputLayout UiShapeInputLayout = null!;
        InputLayout BasicColorInputLayout = null!;
        VertexShader SpriteVertexShader = null!;
        PixelShader SpritePixelShader = null!;
        VertexShader UiShapeVertexShader = null!;
        PixelShader UiShapePixelShader = null!;
        VertexShader BasicColorVertexShader = null!;
        PixelShader BasicColorPixelShader = null!;
        SamplerState SpriteSampler = null!;
        Buffer SpriteConstantBuffer = null!;
        Buffer UiShapeConstantBuffer = null!;
        Buffer BasicColorConstantBuffer = null!;
        /// <summary>
        /// Blend state used for alpha-blended 2D UI rendering.
        /// </summary>
        BlendState AlphaBlendState2D = null!;
        Buffer GeometryVertexBuffer = null!;
        int GeometryVertexCapacity;
        float4x4 ProjectionMatrix2D;
        RasterizerState RasterizerState2D;
        DepthStencilState DepthStencilState2D;
        RoundedRectBackend RoundedRectBackendValue = RoundedRectBackend.Sdf;
        Dictionary<(int Radius, int Border), NineSliceCacheEntry> NineSliceCache = new();
        /// <summary>
        /// Tracks nested clip regions during traversal and applies them to the DirectX scissor state.
        /// </summary>
        readonly DirectX11ClipScissorStack ClipScissorStack;
        /// <summary>
        /// Pixel-shader texture slots currently populated by the 2D renderer.
        /// </summary>
        readonly List<int> ActiveTextureSlots;
        /// <summary>
        /// Tracks whether the released-font-atlas skip diagnostic was already logged this session.
        /// </summary>
        bool HasLoggedReleasedFontAtlasSkip;
        /// <summary>
        /// Computes CPU-side boundary and tile geometry for the rounded-rect nine-slice and
        /// procedural-geometry rendering paths.
        /// </summary>
        readonly RoundedRectGeometryBuilder RoundedRectGeometryBuilder;

        /// <summary>
        /// Initializes the 2D renderer and builds the required GPU resources.
        /// </summary>
        /// <param name="parentRenderer">Owning 3D renderer.</param>
        public DirectX11Renderer2D(DirectX11Renderer3D parentRenderer) {
            ParentRenderer = parentRenderer;
            Device = parentRenderer.Device;
            ClipScissorStack = new DirectX11ClipScissorStack(Device);
            ActiveTextureSlots = new List<int>();
            RoundedRectGeometryBuilder = new RoundedRectGeometryBuilder();

            InitializeSpritePipeline();

            var rasterizerDesc = new RasterizerStateDescription {
                CullMode = CullMode.None,
                FillMode = FillMode.Solid,
                IsDepthClipEnabled = false,
                IsScissorEnabled = true
            };
            RasterizerState2D = new RasterizerState(Device, rasterizerDesc);

            var depthStencilDesc = new DepthStencilStateDescription {
                IsDepthEnabled = false,
                DepthWriteMask = DepthWriteMask.Zero,
                DepthComparison = Comparison.Always
            };
            DepthStencilState2D = new DepthStencilState(Device, depthStencilDesc);

            DebugInfoRegistry.Register(new DirectX11Renderer2DDebugInfoProvider(this));
        }

        /// <summary>
        /// Gets the Direct3D device used by this renderer.
        /// </summary>
        public D3DDevice Device { get; }

        /// <summary>
        /// Gets the currently selected rounded-rect backend.
        /// </summary>
        internal RoundedRectBackend CurrentRoundedRectBackend => RoundedRectBackendValue;

        /// <summary>
        /// Sets the rendering backend used for rounded rectangles.
        /// </summary>
        /// <param name="backend">Backend to use.</param>
        internal void SetRoundedRectBackend(RoundedRectBackend backend) {
            RoundedRectBackendValue = backend;
        }

        /// <summary>
        /// Renders all 2D drawables for a camera.
        /// </summary>
        /// <param name="camera">Camera supplying the render queue.</param>
        internal void RenderCamera(ICamera camera) {
            ConfigureSpritePipeline(SpriteInputLayout);

            float4 viewport = ResolveCameraViewport(camera);
            Device.ImmediateContext.Rasterizer.SetViewport(viewport.X, viewport.Y, viewport.Z, viewport.W);
            ClipScissorStack.SetCameraViewport(viewport);
            ClipScissorStack.Clear();
            float4x4.CreateOrthographicOffCenter(
                viewport.X,
                viewport.X + viewport.Z,
                -(viewport.Y + viewport.W),
                -viewport.Y,
                -10,
                10,
                out ProjectionMatrix2D);

            IRenderQueue2D renderQueue = camera.RenderQueue2D;
            renderQueue.VisitOrdered(this);
        }

        /// <summary>
        /// Resolves one authored camera viewport against the active backbuffer or explicit render target dimensions.
        /// </summary>
        /// <param name="camera">Camera whose viewport should be resolved.</param>
        /// <returns>Viewport rectangle expressed in pixel-space coordinates.</returns>
        float4 ResolveCameraViewport(ICamera camera) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            RenderTarget renderTarget = camera.RenderTarget;
            if (renderTarget != null) {
                return CameraViewportResolver.ResolveViewport(camera.Viewport, renderTarget.Width, renderTarget.Height);
            }

            int2 mainWindowSize = ParentRenderer.MainWindowSize;
            return CameraViewportResolver.ResolveViewport(camera.Viewport, mainWindowSize.X, mainWindowSize.Y);
        }

        /// <summary>
        /// Draws a single 2D drawable encountered during queue traversal.
        /// </summary>
        /// <param name="drawable">Drawable to render.</param>
        public void Visit(IDrawable2D drawable) {
            if (drawable?.Parent == null || !drawable.Parent.Enabled) {
                return;
            }

            ClipScissorStack.SyncForDrawable(drawable);
            drawable.Draw();
        }

        /// <summary>
        /// Draws a sprite using the sprite shader pipeline.
        /// </summary>
        /// <param name="drawable">Sprite drawable.</param>
        public override void DrawSprite(ISpriteDrawable2D drawable) {
            if (drawable == null || drawable.Parent == null || !drawable.Parent.Enabled) {
                return;
            }

            ConfigureSpritePipeline(SpriteInputLayout);

            if (drawable.Texture == null) {
                return;
            }

            var context = Device.ImmediateContext;
            ShaderResourceView resourceView;
            int textureWidth;
            int textureHeight;
            if (drawable.Texture is DirectX11TextureResource textureData) {
                resourceView = textureData.Resource;
                textureWidth = textureData.Width;
                textureHeight = textureData.Height;
            } else if (drawable.Texture is DirectX11RenderTargetResource renderTargetData) {
                resourceView = renderTargetData.ShaderResourceView;
                textureWidth = renderTargetData.Width;
                textureHeight = renderTargetData.Height;
            } else {
                throw new InvalidOperationException("Sprite textures must be DirectX11 texture or render target resources.");
            }

            context.PixelShader.SetShaderResource(0, resourceView);
            context.PixelShader.SetSampler(0, SpriteSampler);
            TrackActiveTextureSlot(0);

            int2 size = drawable.Size;
            if (size.X <= 0 || size.Y <= 0) {
                size = new int2(textureWidth, textureHeight);
            }

            float3 pos = drawable.Parent.Position;
            float3 scale = drawable.Parent.Scale;
            float width = size.X * scale.X;
            float height = size.Y * scale.Y;
            float3 rotatedRight = float4.RotateVector(float3.UnitX, drawable.Parent.Orientation);
            float rotation = (float)Math.Atan2(rotatedRight.Y, rotatedRight.X);
            byte4 color = drawable.Color;

            float4x4 transposedWorld;
            float4x4.Transpose(ref ProjectionMatrix2D, out transposedWorld);

            context.VertexShader.SetConstantBuffer(0, SpriteConstantBuffer);
            context.PixelShader.SetConstantBuffer(0, SpriteConstantBuffer);

            var shaderData = new SpriteShaderData {
                worldViewProj = transposedWorld,
                sourceRect = drawable.SourceRect,
                destRect = new float4(pos.X, pos.Y, width, height),
                spriteTransform = new float4(rotation, 0f, 0f, 0f),
                color = new float4(color.X / 255.0f, color.Y / 255.0f, color.Z / 255.0f, color.W / 255.0f)
            };
            context.UpdateSubresource(ref shaderData, SpriteConstantBuffer);

            context.Draw(4, 0);
            ParentRenderer.IncrementDrawCalls(1);
        }

        /// <summary>
        /// Draws a text string using the font texture and sprite pipeline.
        /// </summary>
        /// <param name="drawable">Text drawable.</param>
        public override void DrawText(ITextDrawable2D drawable) {
            FontAsset font = drawable.Font;
            if (font == null || font.Texture is not DirectX11TextureResource data || data.Resource == null) {
                // A released font atlas must not crash the in-flight frame; skip the drawable and surface one
                // diagnostic so the stale text registration can be tracked down.
                if (!HasLoggedReleasedFontAtlasSkip) {
                    HasLoggedReleasedFontAtlasSkip = true;
                    Logger.WriteError($"Text drawable skipped: its font atlas texture was released while the drawable was still registered (text '{drawable.Text}').");
                }
                return;
            }

            ConfigureSpritePipeline(SpriteInputLayout);

            var context = Device.ImmediateContext;

            context.PixelShader.SetShaderResource(0, data.Resource);
            context.PixelShader.SetSampler(0, SpriteSampler);
            TrackActiveTextureSlot(0);

            float3 pos = drawable.Parent.Position;
            List<TextRenderEffectPass> effectPasses = TextRenderEffectPassBuilder.Build(drawable);

            float4x4 transposedWorld;
            float4x4.Transpose(ref ProjectionMatrix2D, out transposedWorld);

            context.VertexShader.SetConstantBuffer(0, SpriteConstantBuffer);
            context.PixelShader.SetConstantBuffer(0, SpriteConstantBuffer);

            var shaderData = new SpriteShaderData {
                worldViewProj = transposedWorld
            };

            string text = drawable.Text ?? string.Empty;
            double fontScale = Math.Max((double)drawable.FontScale, 0.0001d);
            if (drawable.WrapText) {
                text = TextLayoutUtils.WrapText(text, font, Math.Max(1, (int)Math.Round(drawable.Size.X / fontScale)));
            }

            double[] lineOffsets = TextLineOffsets2D.Build(drawable, font, text, fontScale, data.Width);
            double offsetX = 0d;
            double offsetY = 0d;
            double lineHeight = Math.Max((double)font.LineHeight * fontScale, 1d);
            // Snap the baseline to whole pixels to avoid clipped glyph edges at fractional offsets.
            double baseX = Math.Round(pos.X);
            double baseY = Math.Round(pos.Y);
            int lineIndex = 0;
            double lineOriginX = baseX + TextLineOffsets2D.Resolve(lineOffsets, lineIndex);

            for (int i = 0; i < text.Length; i++) {
                char c = text[i];

                if (c == (char)10) {
                    offsetY += lineHeight;
                    offsetX = 0d;
                    lineIndex++;
                    lineOriginX = baseX + TextLineOffsets2D.Resolve(lineOffsets, lineIndex);
                    continue;
                }

                if (c == ' ') {
                    offsetX += font.FontInfo.SpaceWidth * fontScale;
                    continue;
                }

                if (!font.Characters.TryGetValue(c, out FontChar info)) {
                    continue;
                }

                shaderData.sourceRect = info.SourceRect;
                double pixelW = shaderData.sourceRect.Z * data.Width * fontScale;
                double pixelH = shaderData.sourceRect.W * data.Height * fontScale;

                double snappedLineOffsetY = Math.Round(offsetY);
                double advance = info.AdvanceWidth > 0
                    ? info.AdvanceWidth * fontScale
                    : pixelW;
                offsetX += advance;

                for (int passIndex = 0; passIndex < effectPasses.Count; passIndex++) {
                    TextRenderEffectPass pass = effectPasses[passIndex];
                    byte4 passColor = pass.Color;
                    shaderData.color = new float4(
                        passColor.X / 255.0f,
                        passColor.Y / 255.0f,
                        passColor.Z / 255.0f,
                        passColor.W / 255.0f);
                    shaderData.destRect = new float4(
                        (float)(lineOriginX + offsetX - advance + pass.Offset.X),
                        (float)(baseY + snappedLineOffsetY + (info.OffsetY * fontScale) + pass.Offset.Y),
                        (float)pixelW,
                        (float)pixelH
                    );

                    context.UpdateSubresource(ref shaderData, SpriteConstantBuffer);
                    context.Draw(4, 0);
                    ParentRenderer.IncrementDrawCalls(1);
                }
            }
        }

        /// <summary>
        /// Draws a rounded rectangle using the configured backend.
        /// </summary>
        /// <param name="shape">Rounded rectangle drawable.</param>
        public override void DrawRoundedRect(IRoundedRectDrawable2D shape) {
            if (RoundedRectBackendValue != RoundedRectBackend.Sdf && shape.Corners != RoundedRectCorners.All) {
                DrawRoundedRectSdf(shape);
                return;
            }

            switch (RoundedRectBackendValue) {
                case RoundedRectBackend.Sdf:
                    DrawRoundedRectSdf(shape);
                    return;
                case RoundedRectBackend.NineSlice:
                    DrawRoundedRectNineSlice(shape);
                    return;
                case RoundedRectBackend.Geometry:
                    DrawRoundedRectGeometry(shape);
                    return;
            }
        }

        /// <summary>
        /// Builds a runtime texture from raw RGBA texture data.
        /// </summary>
        /// <param name="data">Raw texture asset data.</param>
        /// <returns>GPU texture resource.</returns>
        public override RuntimeTexture BuildTextureFromRaw(TextureAsset data) {
            var asset = new DirectX11TextureResource {
                Width = data.Width,
                Height = data.Height
            };

            const int bytesPerPixel = 4;
            int expectedDataLength = data.Width * data.Height * bytesPerPixel;
            if (data.Colors.Length != expectedDataLength) {
                throw new ArgumentException("Data length does not match width and height.");
            }

            var textureDesc = new Texture2DDescription {
                Width = data.Width,
                Height = data.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.R8G8B8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            };

            GCHandle dataHandle = GCHandle.Alloc(data.Colors, GCHandleType.Pinned);
            try {
                IntPtr dataPtr = dataHandle.AddrOfPinnedObject();
                int rowPitch = data.Width * bytesPerPixel;

                asset.Texture = new Texture2D(Device, textureDesc, new DataRectangle(dataPtr, rowPitch));
            } finally {
                dataHandle.Free();
            }

            asset.Resource = new ShaderResourceView(Device, asset.Texture);
            OwnedTextures.Add(asset);
            return asset;
        }

        /// <summary>
        /// Uploads one validated RGBA8 rectangle into an existing Direct3D11 texture resource.
        /// </summary>
        /// <param name="texture">Runtime texture that owns the destination texture.</param>
        /// <param name="x">Destination X coordinate in pixels.</param>
        /// <param name="y">Destination Y coordinate in pixels.</param>
        /// <param name="width">Rectangle width in pixels.</param>
        /// <param name="height">Rectangle height in pixels.</param>
        /// <param name="rgba8">Validated RGBA8 source bytes.</param>
        /// <param name="sourceRowPitch">Source byte distance between rows.</param>
        protected override void UpdateTextureRegionCore(
            RuntimeTexture texture,
            int x,
            int y,
            int width,
            int height,
            [NativeNoEscape] byte[] rgba8,
            int sourceRowPitch) {
            if (texture is not DirectX11TextureResource directX11TextureResource ||
                !OwnedTextures.Contains(directX11TextureResource)) {
                throw new ArgumentException("Runtime texture was not created by the DirectX11 2D renderer.", nameof(texture));
            }
            if (directX11TextureResource.Texture == null) {
                throw new InvalidOperationException("DirectX11 runtime texture does not own a texture resource.");
            }

            GCHandle dataHandle = GCHandle.Alloc(rgba8, GCHandleType.Pinned);
            try {
                DataBox dataBox = new DataBox(dataHandle.AddrOfPinnedObject(), sourceRowPitch, 0);
                ResourceRegion region = new ResourceRegion(
                    x,
                    y,
                    0,
                    x + width,
                    y + height,
                    1);
                Device.ImmediateContext.UpdateSubresource(dataBox, directX11TextureResource.Texture, 0, region);
            } finally {
                dataHandle.Free();
            }
        }

        /// <summary>
        /// Releases one DirectX11 runtime texture previously created by this renderer.
        /// </summary>
        /// <param name="texture">Runtime texture that should release its Direct3D resources.</param>
        public override void ReleaseTexture(RuntimeTexture texture) {
            if (texture == null) {
                throw new ArgumentNullException(nameof(texture));
            }
            if (texture is not DirectX11TextureResource directX11TextureResource ||
                !OwnedTextures.Contains(directX11TextureResource)) {
                throw new ArgumentException("Runtime texture was not created by the DirectX11 2D renderer.", nameof(texture));
            }
            if (ParentRenderer.IsFrameActive) {
                throw new InvalidOperationException("Cannot release a DirectX11 texture while a frame is being rendered.");
            }

            directX11TextureResource.Resource?.Dispose();
            directX11TextureResource.Resource = null;
            directX11TextureResource.Texture?.Dispose();
            directX11TextureResource.Texture = null;
            base.ReleaseTexture(texture);
            OwnedTextures.Remove(directX11TextureResource);
        }

        /// <summary>
        /// Releases all GPU resources created by the 2D renderer.
        /// </summary>
        public override void Dispose() {
            DisposeDefaultTextures();
            List<DirectX11TextureResource> ownedTextureSnapshot = new List<DirectX11TextureResource>(OwnedTextures);
            for (int index = 0; index < ownedTextureSnapshot.Count; index++) {
                ReleaseTexture(ownedTextureSnapshot[index]);
            }
            OwnedTextures.Clear();

            SpriteQuadBuffer?.Dispose();
            SpriteInputLayout?.Dispose();
            UiShapeInputLayout?.Dispose();
            BasicColorInputLayout?.Dispose();
            SpriteVertexShader?.Dispose();
            SpritePixelShader?.Dispose();
            UiShapeVertexShader?.Dispose();
            UiShapePixelShader?.Dispose();
            BasicColorVertexShader?.Dispose();
            BasicColorPixelShader?.Dispose();
            SpriteSampler?.Dispose();
            SpriteConstantBuffer?.Dispose();
            UiShapeConstantBuffer?.Dispose();
            BasicColorConstantBuffer?.Dispose();
            AlphaBlendState2D?.Dispose();
            GeometryVertexBuffer?.Dispose();
            RasterizerState2D?.Dispose();
            DepthStencilState2D?.Dispose();
        }

        /// <summary>
        /// Configures shared state for 2D sprite rendering.
        /// </summary>
        /// <param name="inputLayout">Input layout to use.</param>
        void ConfigureSpritePipeline(InputLayout inputLayout) {
            var context = Device.ImmediateContext;
            context.Rasterizer.State = RasterizerState2D;
            context.OutputMerger.SetDepthStencilState(DepthStencilState2D, 0);
            context.OutputMerger.SetBlendState(AlphaBlendState2D);
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
            context.VertexShader.Set(SpriteVertexShader);
            context.PixelShader.Set(SpritePixelShader);
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(SpriteQuadBuffer, Utilities.SizeOf<VertexPositionUV>(), 0));
            context.InputAssembler.InputLayout = inputLayout;
        }

        /// <summary>
        /// Configures shared state for the SDF rounded-rect shader pipeline.
        /// </summary>
        void ConfigureUiShapePipeline() {
            var context = Device.ImmediateContext;
            context.Rasterizer.State = RasterizerState2D;
            context.OutputMerger.SetDepthStencilState(DepthStencilState2D, 0);
            context.OutputMerger.SetBlendState(AlphaBlendState2D);
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
            context.VertexShader.Set(UiShapeVertexShader);
            context.PixelShader.Set(UiShapePixelShader);
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(SpriteQuadBuffer, Utilities.SizeOf<VertexPositionUV>(), 0));
            context.InputAssembler.InputLayout = UiShapeInputLayout;
        }

        /// <summary>
        /// Configures shared state for the solid-color geometry pipeline used by rounded rectangles.
        /// </summary>
        void ConfigureBasicColorPipeline() {
            var context = Device.ImmediateContext;
            context.Rasterizer.State = RasterizerState2D;
            context.OutputMerger.SetDepthStencilState(DepthStencilState2D, 0);
            context.OutputMerger.SetBlendState(AlphaBlendState2D);
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(GeometryVertexBuffer, Utilities.SizeOf<VertexPositionUV>(), 0));
            context.InputAssembler.InputLayout = BasicColorInputLayout;
            context.VertexShader.Set(BasicColorVertexShader);
            context.PixelShader.Set(BasicColorPixelShader);
        }

        /// <summary>
        /// Clears every pixel-shader texture slot that was populated by 2D drawing so later passes cannot sample stale resources.
        /// </summary>
        [NativeMigrationRequired(
            "windows.native_directx_renderer",
            "Changes to DirectX11 2D texture-slot cleanup must also be applied to the Windows native DirectX renderer implementation.")]
        internal void ClearActiveTextureBindings() {
            var context = Device.ImmediateContext;
            for (int bindingIndex = 0; bindingIndex < ActiveTextureSlots.Count; bindingIndex++) {
                int slot = ActiveTextureSlots[bindingIndex];
                context.PixelShader.SetShaderResource(slot, null);
                context.PixelShader.SetSampler(slot, null);
            }

            ActiveTextureSlots.Clear();
        }

        /// <summary>
        /// Records one 2D pixel-shader texture slot as active so it can be cleared before the next render-target transition.
        /// </summary>
        /// <param name="slot">Texture slot that was populated for one 2D draw.</param>
        void TrackActiveTextureSlot(int slot) {
            if (slot < 0) {
                throw new ArgumentOutOfRangeException(nameof(slot), "Texture slot indices must be non-negative.");
            }
            if (ActiveTextureSlots.Contains(slot)) {
                return;
            }

            ActiveTextureSlots.Add(slot);
        }

        /// <summary>
        /// Builds sprite and UI-related shaders, buffers, and layouts.
        /// </summary>
        void InitializeSpritePipeline() {
            var vertices = new[] {
                new VertexPositionUV(new float3(-0.5f, -0.5f, 0), new float2(0, 1)),
                new VertexPositionUV(new float3(-0.5f, 0.5f, 0), new float2(0, 0)),
                new VertexPositionUV(new float3(0.5f, -0.5f, 0), new float2(1, 1)),
                new VertexPositionUV(new float3(0.5f, 0.5f, 0), new float2(1, 0))
            };

            SpriteQuadBuffer = Buffer.Create(Device, BindFlags.VertexBuffer, vertices);

            using (var spriteVs = DirectX11ShaderSourceCompiler.CompileFromContent("shaders\\SpriteShader.fx", "VS", "vs_4_0")) {
                SpriteVertexShader = new VertexShader(Device, spriteVs);
                SpriteInputLayout = new InputLayout(Device, spriteVs, new[] {
                    new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                    new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0)
                });
            }

            using (var spritePs = DirectX11ShaderSourceCompiler.CompileFromContent("shaders\\SpriteShader.fx", "PS", "ps_4_0")) {
                SpritePixelShader = new PixelShader(Device, spritePs);
            }

            using (var uiVs = DirectX11ShaderSourceCompiler.CompileFromContent("shaders\\UIShapeShader.fx", "VS", "vs_4_0")) {
                UiShapeVertexShader = new VertexShader(Device, uiVs);
                UiShapeInputLayout = new InputLayout(Device, uiVs, new[] {
                    new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                    new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0)
                });
            }

            using (var uiPs = DirectX11ShaderSourceCompiler.CompileFromContent("shaders\\UIShapeShader.fx", "PS", "ps_4_0")) {
                UiShapePixelShader = new PixelShader(Device, uiPs);
            }

            using (var colVs = DirectX11ShaderSourceCompiler.CompileFromContent("shaders\\BasicColorShader.fx", "VS", "vs_4_0")) {
                BasicColorVertexShader = new VertexShader(Device, colVs);
                BasicColorInputLayout = new InputLayout(Device, colVs, new[] {
                    new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                    new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0)
                });
            }

            using (var colPs = DirectX11ShaderSourceCompiler.CompileFromContent("shaders\\BasicColorShader.fx", "PS", "ps_4_0")) {
                BasicColorPixelShader = new PixelShader(Device, colPs);
            }

            var samplerDesc = new SamplerStateDescription {
                Filter = Filter.MinMagMipPoint,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                ComparisonFunction = Comparison.Never,
                MinimumLod = 0,
                MaximumLod = float.MaxValue
            };

            SpriteSampler = new SamplerState(Device, samplerDesc);

            AlphaBlendState2D = new BlendState(Device, new BlendStateDescription {
                AlphaToCoverageEnable = false,
                IndependentBlendEnable = false,
                RenderTarget = {
                    [0] = new RenderTargetBlendDescription {
                        IsBlendEnabled = true,
                        SourceBlend = BlendOption.SourceAlpha,
                        DestinationBlend = BlendOption.InverseSourceAlpha,
                        BlendOperation = BlendOperation.Add,
                        SourceAlphaBlend = BlendOption.One,
                        DestinationAlphaBlend = BlendOption.Zero,
                        AlphaBlendOperation = BlendOperation.Add,
                        RenderTargetWriteMask = ColorWriteMaskFlags.All
                    }
                }
            });

            SpriteConstantBuffer = new Buffer(Device, new BufferDescription(
                Marshal.SizeOf<SpriteShaderData>(),
                ResourceUsage.Default,
                BindFlags.ConstantBuffer,
                CpuAccessFlags.None,
                ResourceOptionFlags.None,
                0
            ));

            UiShapeConstantBuffer = new Buffer(Device, new BufferDescription(
                Marshal.SizeOf<UIShapeShaderData>(),
                ResourceUsage.Default,
                BindFlags.ConstantBuffer,
                CpuAccessFlags.None,
                ResourceOptionFlags.None,
                0
            ));

            BasicColorConstantBuffer = new Buffer(Device, new BufferDescription(
                Marshal.SizeOf<BasicColorShaderData>(),
                ResourceUsage.Default,
                BindFlags.ConstantBuffer,
                CpuAccessFlags.None,
                ResourceOptionFlags.None,
                0
            ));

            GeometryVertexCapacity = InitialGeometryVertexCapacity;
            GeometryVertexBuffer = new Buffer(Device, new BufferDescription(
                Utilities.SizeOf<VertexPositionUV>() * GeometryVertexCapacity,
                ResourceUsage.Dynamic,
                BindFlags.VertexBuffer,
                CpuAccessFlags.Write,
                ResourceOptionFlags.None,
                0));
        }

        /// <summary>
        /// Renders a rounded rectangle using the SDF shader path.
        /// </summary>
        /// <param name="shape">Rounded rectangle drawable.</param>
        void DrawRoundedRectSdf(IRoundedRectDrawable2D shape) {
            var context = Device.ImmediateContext;

            ConfigureUiShapePipeline();

            float3 pos = shape.Parent.Position;
            float4x4 transposedWorld;
            float4x4.Transpose(ref ProjectionMatrix2D, out transposedWorld);

            var shaderData = new UIShapeShaderData {
                worldViewProj = transposedWorld,
                destRect = new float4(pos.X, pos.Y, shape.Size.X, shape.Size.Y),
                params1 = new float4(shape.Radius, shape.BorderThickness, 1.0f, (float)shape.Corners),
                fillColor = new float4(
                    shape.FillColor.X / 255.0f,
                    shape.FillColor.Y / 255.0f,
                    shape.FillColor.Z / 255.0f,
                    shape.FillColor.W / 255.0f
                ),
                borderColor = new float4(
                    shape.BorderColor.X / 255.0f,
                    shape.BorderColor.Y / 255.0f,
                    shape.BorderColor.Z / 255.0f,
                    shape.BorderColor.W / 255.0f
                )
            };

            context.VertexShader.SetConstantBuffer(0, UiShapeConstantBuffer);
            context.PixelShader.SetConstantBuffer(0, UiShapeConstantBuffer);
            context.UpdateSubresource(ref shaderData, UiShapeConstantBuffer);

            context.Draw(4, 0);
            ParentRenderer.IncrementDrawCalls(1);
        }

        /// <summary>
        /// Retrieves or builds the nine-slice atlas entry for the given shape settings.
        /// </summary>
        /// <param name="shape">Rounded rectangle drawable.</param>
        /// <returns>Cached atlas entry.</returns>
        NineSliceCacheEntry GetNineSliceCacheEntry(IRoundedRectDrawable2D shape) {
            int radius = (int)MathF.Round(shape.Radius);
            int border = (int)MathF.Round(shape.BorderThickness);
            var key = (radius, border);
            if (!NineSliceCache.TryGetValue(key, out var atlas)) {
                var coreAtlas = helengine.NineSliceAtlas.Generate(radius, border, aaPx: 1, padding: 2);
                Core ownerCore = OwnerCore ?? throw new InvalidOperationException("DirectX11 renderer is not attached to an owning Core.");
                var rt = ownerCore.RenderManager2D.BuildTextureFromRaw(coreAtlas.Texture);
                atlas = new NineSliceCacheEntry {
                    Texture = rt,
                    FillUv = coreAtlas.FillUV,
                    BorderUv = coreAtlas.BorderUV,
                    CornerSize = coreAtlas.CornerSize
                };
                NineSliceCache[key] = atlas;
            }
            return atlas;
        }

        /// <summary>
        /// Renders a rounded rectangle using the nine-slice atlas path.
        /// </summary>
        /// <param name="shape">Rounded rectangle drawable.</param>
        void DrawRoundedRectNineSlice(IRoundedRectDrawable2D shape) {
            var context = Device.ImmediateContext;
            var atlas = GetNineSliceCacheEntry(shape);

            var sdata = (DirectX11TextureResource)atlas.Texture;
            context.PixelShader.SetShaderResource(0, sdata.Resource);
            context.PixelShader.SetSampler(0, SpriteSampler);
            TrackActiveTextureSlot(0);

            ConfigureSpritePipeline(SpriteInputLayout);

            float3 pos = shape.Parent.Position;
            float x = pos.X;
            float y = pos.Y;
            float w = shape.Size.X;
            float h = shape.Size.Y;
            int s = atlas.CornerSize;

            float4x4 transposedWorld;
            float4x4.Transpose(ref ProjectionMatrix2D, out transposedWorld);
            var shaderData = new SpriteShaderData {
                worldViewProj = transposedWorld,
                color = new float4(shape.FillColor.X / 255f, shape.FillColor.Y / 255f, shape.FillColor.Z / 255f, shape.FillColor.W / 255f)
            };

            context.VertexShader.SetConstantBuffer(0, SpriteConstantBuffer);
            context.PixelShader.SetConstantBuffer(0, SpriteConstantBuffer);

            float4[] tileRects = RoundedRectGeometryBuilder.BuildNineSliceTileRects(x, y, w, h, s);
            for (int tileIndex = 0; tileIndex < tileRects.Length; tileIndex++) {
                DrawNineSliceTile(context, ref shaderData, atlas.FillUv[tileIndex], tileRects[tileIndex]);
            }

            if (shape.BorderThickness > 0) {
                shaderData.color = new float4(shape.BorderColor.X / 255f, shape.BorderColor.Y / 255f, shape.BorderColor.Z / 255f, shape.BorderColor.W / 255f);
                for (int tileIndex = 0; tileIndex < tileRects.Length; tileIndex++) {
                    DrawNineSliceTile(context, ref shaderData, atlas.BorderUv[tileIndex], tileRects[tileIndex]);
                }
            }
        }

        /// <summary>
        /// Draws a single nine-slice tile quad, sourcing pixels from the given atlas UV rectangle
        /// and mapping them to the given destination rectangle.
        /// </summary>
        /// <param name="context">Immediate device context to issue the draw call on.</param>
        /// <param name="shaderData">Shader constant data reused across tile draws; its source and destination rectangles are overwritten before each draw.</param>
        /// <param name="sourceUv">Source UV rectangle within the atlas texture.</param>
        /// <param name="destinationRect">Destination rectangle, in shape-local pixel space.</param>
        void DrawNineSliceTile(DeviceContext context, ref SpriteShaderData shaderData, float4 sourceUv, float4 destinationRect) {
            shaderData.sourceRect = sourceUv;
            shaderData.destRect = destinationRect;
            context.UpdateSubresource(ref shaderData, SpriteConstantBuffer);
            context.Draw(4, 0);
            ParentRenderer.IncrementDrawCalls(1);
        }

        /// <summary>
        /// Ensures the geometry vertex buffer can hold at least the given number of vertices.
        /// </summary>
        /// <param name="needed">Required vertex capacity.</param>
        void EnsureGeometryCapacity(int needed) {
            if (needed <= GeometryVertexCapacity) {
                return;
            }

            int newCap = GeometryVertexCapacity;
            while (newCap < needed) {
                newCap *= 2;
            }

            GeometryVertexBuffer.Dispose();
            GeometryVertexCapacity = newCap;
            GeometryVertexBuffer = new Buffer(Device, new BufferDescription(
                Utilities.SizeOf<VertexPositionUV>() * GeometryVertexCapacity,
                ResourceUsage.Dynamic,
                BindFlags.VertexBuffer,
                CpuAccessFlags.Write,
                ResourceOptionFlags.None,
                0));
        }

        /// <summary>
        /// Renders a rounded rectangle by generating geometry on the CPU.
        /// </summary>
        /// <param name="shape">Rounded rectangle drawable.</param>
        void DrawRoundedRectGeometry(IRoundedRectDrawable2D shape) {
            var context = Device.ImmediateContext;

            const int segmentsPerCorner = 8;
            int corners = 4;
            int steps = segmentsPerCorner * corners;

            int fillVerts = steps * 3;
            int borderVerts = (shape.BorderThickness > 0) ? steps * 6 : 0;
            int totalVerts = fillVerts + borderVerts;
            EnsureGeometryCapacity(totalVerts);

            var dataBox = context.MapSubresource(GeometryVertexBuffer, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None);
            var ptr = dataBox.DataPointer;

            float3 pos = shape.Parent.Position;
            float w = shape.Size.X;
            float h = shape.Size.Y;
            float r = Math.Min(shape.Radius, Math.Min(w, h) * 0.5f);
            float cx = pos.X + w * 0.5f;
            float cy = pos.Y + h * 0.5f;

            float2[] fillPoints = RoundedRectGeometryBuilder.BuildFillRingVertices(steps, w, h, r, cx, cy);
            WriteGeometryVertices(fillPoints, ref ptr);

            if (shape.BorderThickness > 0) {
                float ir = Math.Max(0, r - shape.BorderThickness);
                float iw = Math.Max(0, w - shape.BorderThickness * 2);
                float ih = Math.Max(0, h - shape.BorderThickness * 2);
                float2[] borderPoints = RoundedRectGeometryBuilder.BuildBorderRingVertices(steps, w, h, r, iw, ih, ir, cx, cy);
                WriteGeometryVertices(borderPoints, ref ptr);
            }

            context.UnmapSubresource(GeometryVertexBuffer, 0);

            ConfigureBasicColorPipeline();

            float4x4 transposedWorld;
            float4x4.Transpose(ref ProjectionMatrix2D, out transposedWorld);

            var colorData = new BasicColorShaderData {
                worldViewProj = transposedWorld,
                color = new float4(
                    shape.FillColor.X / 255.0f,
                    shape.FillColor.Y / 255.0f,
                    shape.FillColor.Z / 255.0f,
                    shape.FillColor.W / 255.0f
                )
            };

            context.VertexShader.SetConstantBuffer(0, BasicColorConstantBuffer);
            context.PixelShader.SetConstantBuffer(0, BasicColorConstantBuffer);
            context.UpdateSubresource(ref colorData, BasicColorConstantBuffer);

            context.Draw(fillVerts, 0);
            ParentRenderer.IncrementDrawCalls(1);

            if (borderVerts > 0) {
                colorData.color = new float4(
                    shape.BorderColor.X / 255.0f,
                    shape.BorderColor.Y / 255.0f,
                    shape.BorderColor.Z / 255.0f,
                    shape.BorderColor.W / 255.0f
                );
                context.UpdateSubresource(ref colorData, BasicColorConstantBuffer);
                context.Draw(borderVerts, fillVerts);
                ParentRenderer.IncrementDrawCalls(1);
            }
        }

        /// <summary>
        /// Writes each vertex position in the given array to the mapped geometry vertex buffer at
        /// the given pointer, advancing the pointer past each written vertex. UV coordinates are
        /// left at zero, since the procedural-geometry rendering path is a solid-color fill.
        /// </summary>
        /// <param name="points">Vertex positions to write, in the order they should appear in the vertex buffer.</param>
        /// <param name="ptr">Pointer into the mapped vertex buffer; advanced by one vertex per written point.</param>
        void WriteGeometryVertices(float2[] points, ref IntPtr ptr) {
            for (int i = 0; i < points.Length; i++) {
                var vertex = new VertexPositionUV(new float3(points[i].X, points[i].Y, 0), new float2(0, 0));
                Utilities.Write(ptr, ref vertex);
                ptr += Utilities.SizeOf<VertexPositionUV>();
            }
        }

        /// <summary>
        /// Stores cached nine-slice atlas data for a given radius/border pair.
        /// </summary>
        struct NineSliceCacheEntry {
            /// <summary>
            /// Gets or sets the runtime texture holding the atlas.
            /// </summary>
            public RuntimeTexture Texture { get; set; }
            /// <summary>
            /// Gets or sets the UV rectangles for fill tiles.
            /// </summary>
            public float4[] FillUv { get; set; }
            /// <summary>
            /// Gets or sets the UV rectangles for border tiles.
            /// </summary>
            public float4[] BorderUv { get; set; }
            /// <summary>
            /// Gets or sets the corner size in pixels.
            /// </summary>
            public int CornerSize { get; set; }
        }
    }
}
