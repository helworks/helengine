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
    internal class DirectX11Renderer2D : RenderManager2D, IRenderVisitor2D {
        const int InitialGeometryVertexCapacity = 1024;

        readonly DirectX11Renderer3D parentRenderer;
        Buffer spriteQuadBuffer = null!;
        InputLayout spriteInputLayout = null!;
        InputLayout uiShapeInputLayout = null!;
        InputLayout basicColorInputLayout = null!;
        VertexShader spriteVertexShader = null!;
        PixelShader spritePixelShader = null!;
        VertexShader uiShapeVertexShader = null!;
        PixelShader uiShapePixelShader = null!;
        VertexShader basicColorVertexShader = null!;
        PixelShader basicColorPixelShader = null!;
        SamplerState spriteSampler = null!;
        Buffer spriteConstantBuffer = null!;
        Buffer uiShapeConstantBuffer = null!;
        Buffer basicColorConstantBuffer = null!;
        /// <summary>
        /// Blend state used for alpha-blended 2D UI rendering.
        /// </summary>
        BlendState alphaBlendState2D = null!;
        Buffer geometryVertexBuffer = null!;
        int geometryVertexCapacity;
        float4x4 projectionMatrix2D;
        RasterizerState rasterizerState2D;
        DepthStencilState depthStencilState2D;
        RoundedRectBackend roundedRectBackend = RoundedRectBackend.Sdf;
        Dictionary<(int Radius, int Border), NineSliceCacheEntry> nineSliceCache = new();
        /// <summary>
        /// Reusable helper that resolves nested clip chains for the current drawable.
        /// </summary>
        readonly ClipRegionStackBuilder2D ClipRegionStackBuilder;
        /// <summary>
        /// Clip owners currently active in the render traversal.
        /// </summary>
        readonly List<IClipRegion2D> ActiveClipChain;
        /// <summary>
        /// Clip owners resolved for the drawable currently being visited.
        /// </summary>
        readonly List<IClipRegion2D> NextClipChain;
        /// <summary>
        /// Effective clip rectangles for the active clip chain.
        /// </summary>
        readonly List<float4> ActiveClipRects;
        /// <summary>
        /// Left edge of the current camera scissor rectangle.
        /// </summary>
        int currentScissorLeft;
        /// <summary>
        /// Top edge of the current camera scissor rectangle.
        /// </summary>
        int currentScissorTop;
        /// <summary>
        /// Right edge of the current camera scissor rectangle.
        /// </summary>
        int currentScissorRight;
        /// <summary>
        /// Bottom edge of the current camera scissor rectangle.
        /// </summary>
        int currentScissorBottom;

        /// <summary>
        /// Initializes the 2D renderer and builds the required GPU resources.
        /// </summary>
        /// <param name="parentRenderer">Owning 3D renderer.</param>
        public DirectX11Renderer2D(DirectX11Renderer3D parentRenderer) {
            this.parentRenderer = parentRenderer;
            Device = parentRenderer.Device;
            ClipRegionStackBuilder = new ClipRegionStackBuilder2D();
            ActiveClipChain = new List<IClipRegion2D>();
            NextClipChain = new List<IClipRegion2D>();
            ActiveClipRects = new List<float4>();

            InitializeSpritePipeline();

            var rasterizerDesc = new RasterizerStateDescription {
                CullMode = CullMode.None,
                FillMode = FillMode.Solid,
                IsDepthClipEnabled = false,
                IsScissorEnabled = true
            };
            rasterizerState2D = new RasterizerState(Device, rasterizerDesc);

            var depthStencilDesc = new DepthStencilStateDescription {
                IsDepthEnabled = false,
                DepthWriteMask = DepthWriteMask.Zero,
                DepthComparison = Comparison.Always
            };
            depthStencilState2D = new DepthStencilState(Device, depthStencilDesc);

            DebugInfoRegistry.Register(new DirectX11Renderer2DDebugInfoProvider(this));
        }

        /// <summary>
        /// Gets the Direct3D device used by this renderer.
        /// </summary>
        public D3DDevice Device { get; }

        /// <summary>
        /// Gets the currently selected rounded-rect backend.
        /// </summary>
        internal RoundedRectBackend CurrentRoundedRectBackend => roundedRectBackend;

        /// <summary>
        /// Sets the rendering backend used for rounded rectangles.
        /// </summary>
        /// <param name="backend">Backend to use.</param>
        internal void SetRoundedRectBackend(RoundedRectBackend backend) {
            roundedRectBackend = backend;
        }

        /// <summary>
        /// Renders all 2D drawables for a camera.
        /// </summary>
        /// <param name="camera">Camera supplying the render queue.</param>
        internal void RenderCamera(ICamera camera) {
            ConfigureSpritePipeline(spriteInputLayout);

            float4 viewport = ResolveCameraViewport(camera);
            Device.ImmediateContext.Rasterizer.SetViewport(viewport.X, viewport.Y, viewport.Z, viewport.W);
            currentScissorLeft = (int)Math.Round(viewport.X);
            currentScissorTop = (int)Math.Round(viewport.Y);
            currentScissorRight = (int)Math.Round(viewport.X + viewport.Z);
            currentScissorBottom = (int)Math.Round(viewport.Y + viewport.W);
            ApplyCameraScissor();
            ActiveClipChain.Clear();
            NextClipChain.Clear();
            ActiveClipRects.Clear();
            float4x4.CreateOrthographicOffCenter(
                viewport.X,
                viewport.X + viewport.Z,
                -(viewport.Y + viewport.W),
                -viewport.Y,
                -10,
                10,
                out projectionMatrix2D);

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

            int2 mainWindowSize = parentRenderer.MainWindowSize;
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

            ClipRegionStackBuilder.BuildClipChain(drawable, NextClipChain);
            SyncClipTransitions();
            drawable.Draw();
        }

        /// <summary>
        /// Draws a sprite using the sprite shader pipeline.
        /// </summary>
        /// <param name="drawable">Sprite drawable.</param>
        public override void DrawSprite(ISpriteDrawable2D drawable) {
            ConfigureSpritePipeline(spriteInputLayout);

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
            context.PixelShader.SetSampler(0, spriteSampler);

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
            float4x4.Transpose(ref projectionMatrix2D, out transposedWorld);

            context.VertexShader.SetConstantBuffer(0, spriteConstantBuffer);
            context.PixelShader.SetConstantBuffer(0, spriteConstantBuffer);

            var shaderData = new SpriteShaderData {
                worldViewProj = transposedWorld,
                sourceRect = drawable.SourceRect,
                destRect = new float4(pos.X, pos.Y, width, height),
                spriteTransform = new float4(rotation, 0f, 0f, 0f),
                color = new float4(color.X / 255.0f, color.Y / 255.0f, color.Z / 255.0f, color.W / 255.0f)
            };
            context.UpdateSubresource(ref shaderData, spriteConstantBuffer);

            context.Draw(4, 0);
            parentRenderer.IncrementDrawCalls(1);
        }

        /// <summary>
        /// Draws a text string using the font texture and sprite pipeline.
        /// </summary>
        /// <param name="drawable">Text drawable.</param>
        public override void DrawText(ITextDrawable2D drawable) {
            ConfigureSpritePipeline(spriteInputLayout);

            var context = Device.ImmediateContext;
            FontAsset font = drawable.Font;
            var data = (DirectX11TextureResource)font.Texture;

            context.PixelShader.SetShaderResource(0, data.Resource);
            context.PixelShader.SetSampler(0, spriteSampler);

            float3 pos = drawable.Parent.Position;
            byte4 color = drawable.Color;

            float4x4 transposedWorld;
            float4x4.Transpose(ref projectionMatrix2D, out transposedWorld);

            context.VertexShader.SetConstantBuffer(0, spriteConstantBuffer);
            context.PixelShader.SetConstantBuffer(0, spriteConstantBuffer);

            var shaderData = new SpriteShaderData {
                worldViewProj = transposedWorld,
                color = new float4(color.X / 255.0f, color.Y / 255.0f, color.Z / 255.0f, color.W / 255.0f)
            };

            string text = drawable.Text ?? string.Empty;
            double fontScale = Math.Max((double)drawable.FontScale, 0.0001d);
            if (drawable.WrapText) {
                text = TextLayoutUtils.WrapText(text, font, Math.Max(1, (int)Math.Round(drawable.Size.X / fontScale)));
            }

            double[] lineOffsets = BuildTextLineOffsets(drawable, font, text, fontScale, data.Width);
            double offsetX = 0d;
            double offsetY = 0d;
            double lineHeight = Math.Max((double)font.LineHeight * fontScale, 1d);
            // Snap the baseline to whole pixels to avoid clipped glyph edges at fractional offsets.
            double baseX = Math.Round(pos.X);
            double baseY = Math.Round(pos.Y);
            int lineIndex = 0;
            double lineOriginX = baseX + ResolveTextLineOffset(lineOffsets, lineIndex);

            for (int i = 0; i < text.Length; i++) {
                char c = text[i];

                if (c == (char)10) {
                    offsetY += lineHeight;
                    offsetX = 0d;
                    lineIndex++;
                    lineOriginX = baseX + ResolveTextLineOffset(lineOffsets, lineIndex);
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
                shaderData.destRect = new float4(
                    (float)(lineOriginX + offsetX),
                    (float)(baseY + snappedLineOffsetY + (info.OffsetY * fontScale)),
                    (float)pixelW,
                    (float)pixelH
                );

                double advance = info.AdvanceWidth > 0
                    ? info.AdvanceWidth * fontScale
                    : pixelW;
                offsetX += advance;

                context.UpdateSubresource(ref shaderData, spriteConstantBuffer);
                context.Draw(4, 0);
                parentRenderer.IncrementDrawCalls(1);
            }
        }

        /// <summary>
        /// Builds one horizontal offset per rendered text line so authored text alignment is respected consistently across wrapped and non-wrapped content.
        /// </summary>
        /// <param name="drawable">Text drawable that owns the authored layout box.</param>
        /// <param name="font">Font used to render the text.</param>
        /// <param name="text">Final rendered text content after wrapping has been applied.</param>
        /// <param name="fontScale">Resolved glyph scale.</param>
        /// <param name="textureWidth">Font-atlas texture width used to resolve glyph bounds.</param>
        /// <returns>One horizontal offset per rendered line.</returns>
        static double[] BuildTextLineOffsets(ITextDrawable2D drawable, FontAsset font, string text, double fontScale, int textureWidth) {
            if (drawable == null) {
                throw new ArgumentNullException(nameof(drawable));
            } else if (font == null) {
                throw new ArgumentNullException(nameof(font));
            } else if (text == null) {
                throw new ArgumentNullException(nameof(text));
            } else if (fontScale <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(fontScale), "Font scale must be greater than zero.");
            } else if (textureWidth <= 0) {
                throw new ArgumentOutOfRangeException(nameof(textureWidth), "Texture width must be greater than zero.");
            }

            string[] lines = text.Split('\n');
            double[] lineOffsets = new double[lines.Length];
            for (int index = 0; index < lines.Length; index++) {
                double visibleWidth = TextLayoutAlignmentUtils.MeasureVisibleLineWidth(lines[index], font, fontScale, textureWidth);
                lineOffsets[index] = TextLayoutAlignmentUtils.ResolveHorizontalOffset(drawable.Alignment, drawable.Size.X, visibleWidth);
            }

            return lineOffsets;
        }

        /// <summary>
        /// Resolves one previously measured line offset or returns zero when the requested line index is outside the rendered line array.
        /// </summary>
        /// <param name="lineOffsets">Per-line horizontal offsets computed for the rendered text.</param>
        /// <param name="lineIndex">Rendered line index whose offset should be returned.</param>
        /// <returns>Horizontal line offset in pixels.</returns>
        static double ResolveTextLineOffset(double[] lineOffsets, int lineIndex) {
            if (lineOffsets == null) {
                throw new ArgumentNullException(nameof(lineOffsets));
            }

            if (lineIndex < 0 || lineIndex >= lineOffsets.Length) {
                return 0d;
            }

            return lineOffsets[lineIndex];
        }

        /// <summary>
        /// Draws a rounded rectangle using the configured backend.
        /// </summary>
        /// <param name="shape">Rounded rectangle drawable.</param>
        public override void DrawRoundedRect(IRoundedRectDrawable2D shape) {
            if (roundedRectBackend != RoundedRectBackend.Sdf && shape.Corners != RoundedRectCorners.All) {
                DrawRoundedRectSdf(shape);
                return;
            }

            switch (roundedRectBackend) {
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
            return asset;
        }

        /// <summary>
        /// Releases one DirectX11 runtime texture previously created by this renderer.
        /// </summary>
        /// <param name="texture">Runtime texture that should release its Direct3D resources.</param>
        public override void ReleaseTexture(RuntimeTexture texture) {
            if (texture == null) {
                throw new ArgumentNullException(nameof(texture));
            }
            if (texture is not DirectX11TextureResource directX11TextureResource) {
                throw new InvalidOperationException("Released runtime texture was not created by the DirectX11 2D renderer.");
            }

            directX11TextureResource.Resource?.Dispose();
            directX11TextureResource.Resource = null;
            directX11TextureResource.Texture?.Dispose();
            directX11TextureResource.Texture = null;
            base.ReleaseTexture(texture);
        }

        /// <summary>
        /// Releases all GPU resources created by the 2D renderer.
        /// </summary>
        public override void Dispose() {
            spriteQuadBuffer?.Dispose();
            spriteInputLayout?.Dispose();
            uiShapeInputLayout?.Dispose();
            basicColorInputLayout?.Dispose();
            spriteVertexShader?.Dispose();
            spritePixelShader?.Dispose();
            uiShapeVertexShader?.Dispose();
            uiShapePixelShader?.Dispose();
            basicColorVertexShader?.Dispose();
            basicColorPixelShader?.Dispose();
            spriteSampler?.Dispose();
            spriteConstantBuffer?.Dispose();
            uiShapeConstantBuffer?.Dispose();
            basicColorConstantBuffer?.Dispose();
            alphaBlendState2D?.Dispose();
            geometryVertexBuffer?.Dispose();
            rasterizerState2D?.Dispose();
            depthStencilState2D?.Dispose();
        }

        /// <summary>
        /// Configures shared state for 2D sprite rendering.
        /// </summary>
        /// <param name="inputLayout">Input layout to use.</param>
        void ConfigureSpritePipeline(InputLayout inputLayout) {
            var context = Device.ImmediateContext;
            context.Rasterizer.State = rasterizerState2D;
            context.OutputMerger.SetDepthStencilState(depthStencilState2D, 0);
            context.OutputMerger.SetBlendState(alphaBlendState2D);
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
            context.VertexShader.Set(spriteVertexShader);
            context.PixelShader.Set(spritePixelShader);
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(spriteQuadBuffer, Utilities.SizeOf<VertexPositionUV>(), 0));
            context.InputAssembler.InputLayout = inputLayout;
        }

        /// <summary>
        /// Configures shared state for the SDF rounded-rect shader pipeline.
        /// </summary>
        void ConfigureUiShapePipeline() {
            var context = Device.ImmediateContext;
            context.Rasterizer.State = rasterizerState2D;
            context.OutputMerger.SetDepthStencilState(depthStencilState2D, 0);
            context.OutputMerger.SetBlendState(alphaBlendState2D);
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
            context.VertexShader.Set(uiShapeVertexShader);
            context.PixelShader.Set(uiShapePixelShader);
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(spriteQuadBuffer, Utilities.SizeOf<VertexPositionUV>(), 0));
            context.InputAssembler.InputLayout = uiShapeInputLayout;
        }

        /// <summary>
        /// Configures shared state for the solid-color geometry pipeline used by rounded rectangles.
        /// </summary>
        void ConfigureBasicColorPipeline() {
            var context = Device.ImmediateContext;
            context.Rasterizer.State = rasterizerState2D;
            context.OutputMerger.SetDepthStencilState(depthStencilState2D, 0);
            context.OutputMerger.SetBlendState(alphaBlendState2D);
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(geometryVertexBuffer, Utilities.SizeOf<VertexPositionUV>(), 0));
            context.InputAssembler.InputLayout = basicColorInputLayout;
            context.VertexShader.Set(basicColorVertexShader);
            context.PixelShader.Set(basicColorPixelShader);
        }

        /// <summary>
        /// Synchronizes the active clip stack with the drawable currently being visited and applies the resulting scissor rectangle.
        /// </summary>
        void SyncClipTransitions() {
            int sharedPrefixLength = GetSharedPrefixLength();

            while (ActiveClipChain.Count > sharedPrefixLength) {
                ActiveClipChain.RemoveAt(ActiveClipChain.Count - 1);
                ActiveClipRects.RemoveAt(ActiveClipRects.Count - 1);
            }

            while (ActiveClipChain.Count < NextClipChain.Count) {
                IClipRegion2D clipRegion = NextClipChain[ActiveClipChain.Count];
                float4 resolvedRect = ResolveClipRectForPush(clipRegion);
                ActiveClipChain.Add(clipRegion);
                ActiveClipRects.Add(resolvedRect);
            }

            if (ActiveClipRects.Count > 0) {
                ApplyClipScissor(ActiveClipRects[ActiveClipRects.Count - 1]);
            } else {
                ApplyCameraScissor();
            }
        }

        /// <summary>
        /// Returns the number of leading clip owners shared between the current and next clip chains.
        /// </summary>
        /// <returns>Shared clip-chain prefix length.</returns>
        int GetSharedPrefixLength() {
            int sharedPrefixLength = 0;
            int maxSharedLength = Math.Min(ActiveClipChain.Count, NextClipChain.Count);
            while (sharedPrefixLength < maxSharedLength &&
                   ReferenceEquals(ActiveClipChain[sharedPrefixLength], NextClipChain[sharedPrefixLength])) {
                sharedPrefixLength++;
            }

            return sharedPrefixLength;
        }

        /// <summary>
        /// Resolves one clip region against the current active clip stack.
        /// </summary>
        /// <param name="clipRegion">Clip region to resolve.</param>
        /// <returns>Effective clip rectangle in logical screen coordinates.</returns>
        float4 ResolveClipRectForPush(IClipRegion2D clipRegion) {
            float4 resolvedRect = clipRegion.GetClipRect();
            if (ActiveClipRects.Count <= 0) {
                return resolvedRect;
            }

            float4 currentRect = ActiveClipRects[ActiveClipRects.Count - 1];
            return ClipRegionStackBuilder.Intersect(currentRect, resolvedRect);
        }

        /// <summary>
        /// Restores the camera viewport scissor after the clip stack empties.
        /// </summary>
        void ApplyCameraScissor() {
            Device.ImmediateContext.Rasterizer.SetScissorRectangle(
                currentScissorLeft,
                currentScissorTop,
                currentScissorRight,
                currentScissorBottom);
        }

        /// <summary>
        /// Applies one resolved clip rectangle to the active DirectX scissor state.
        /// </summary>
        /// <param name="clipRect">Logical clip rectangle resolved for the current drawable.</param>
        void ApplyClipScissor(float4 clipRect) {
            float4 viewportRect = new float4(currentScissorLeft, currentScissorTop, currentScissorRight - currentScissorLeft, currentScissorBottom - currentScissorTop);
            float4 effectiveRect = ClipRegionStackBuilder.Intersect(viewportRect, clipRect);

            int scissorLeft = (int)Math.Round(effectiveRect.X);
            int scissorTop = (int)Math.Round(effectiveRect.Y);
            int scissorRight = (int)Math.Round(effectiveRect.X + effectiveRect.Z);
            int scissorBottom = (int)Math.Round(effectiveRect.Y + effectiveRect.W);
            Device.ImmediateContext.Rasterizer.SetScissorRectangle(scissorLeft, scissorTop, scissorRight, scissorBottom);
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

            spriteQuadBuffer = Buffer.Create(Device, BindFlags.VertexBuffer, vertices);

            using (var spriteVs = DirectX11ShaderSourceCompiler.CompileFromContent("shaders\\SpriteShader.fx", "VS", "vs_4_0")) {
                spriteVertexShader = new VertexShader(Device, spriteVs);
                spriteInputLayout = new InputLayout(Device, spriteVs, new[] {
                    new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                    new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0)
                });
            }

            using (var spritePs = DirectX11ShaderSourceCompiler.CompileFromContent("shaders\\SpriteShader.fx", "PS", "ps_4_0")) {
                spritePixelShader = new PixelShader(Device, spritePs);
            }

            using (var uiVs = DirectX11ShaderSourceCompiler.CompileFromContent("shaders\\UIShapeShader.fx", "VS", "vs_4_0")) {
                uiShapeVertexShader = new VertexShader(Device, uiVs);
                uiShapeInputLayout = new InputLayout(Device, uiVs, new[] {
                    new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                    new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0)
                });
            }

            using (var uiPs = DirectX11ShaderSourceCompiler.CompileFromContent("shaders\\UIShapeShader.fx", "PS", "ps_4_0")) {
                uiShapePixelShader = new PixelShader(Device, uiPs);
            }

            using (var colVs = DirectX11ShaderSourceCompiler.CompileFromContent("shaders\\BasicColorShader.fx", "VS", "vs_4_0")) {
                basicColorVertexShader = new VertexShader(Device, colVs);
                basicColorInputLayout = new InputLayout(Device, colVs, new[] {
                    new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                    new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0)
                });
            }

            using (var colPs = DirectX11ShaderSourceCompiler.CompileFromContent("shaders\\BasicColorShader.fx", "PS", "ps_4_0")) {
                basicColorPixelShader = new PixelShader(Device, colPs);
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

            spriteSampler = new SamplerState(Device, samplerDesc);

            alphaBlendState2D = new BlendState(Device, new BlendStateDescription {
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

            spriteConstantBuffer = new Buffer(Device, new BufferDescription(
                Marshal.SizeOf<SpriteShaderData>(),
                ResourceUsage.Default,
                BindFlags.ConstantBuffer,
                CpuAccessFlags.None,
                ResourceOptionFlags.None,
                0
            ));

            uiShapeConstantBuffer = new Buffer(Device, new BufferDescription(
                Marshal.SizeOf<UIShapeShaderData>(),
                ResourceUsage.Default,
                BindFlags.ConstantBuffer,
                CpuAccessFlags.None,
                ResourceOptionFlags.None,
                0
            ));

            basicColorConstantBuffer = new Buffer(Device, new BufferDescription(
                Marshal.SizeOf<BasicColorShaderData>(),
                ResourceUsage.Default,
                BindFlags.ConstantBuffer,
                CpuAccessFlags.None,
                ResourceOptionFlags.None,
                0
            ));

            geometryVertexCapacity = InitialGeometryVertexCapacity;
            geometryVertexBuffer = new Buffer(Device, new BufferDescription(
                Utilities.SizeOf<VertexPositionUV>() * geometryVertexCapacity,
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
            float4x4.Transpose(ref projectionMatrix2D, out transposedWorld);

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

            context.VertexShader.SetConstantBuffer(0, uiShapeConstantBuffer);
            context.PixelShader.SetConstantBuffer(0, uiShapeConstantBuffer);
            context.UpdateSubresource(ref shaderData, uiShapeConstantBuffer);

            context.Draw(4, 0);
            parentRenderer.IncrementDrawCalls(1);
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
            if (!nineSliceCache.TryGetValue(key, out var atlas)) {
                var coreAtlas = helengine.NineSliceAtlas.Generate(radius, border, aaPx: 1, padding: 2);
                var rt = Core.Instance.RenderManager2D.BuildTextureFromRaw(coreAtlas.Texture);
                atlas = new NineSliceCacheEntry {
                    Texture = rt,
                    FillUv = coreAtlas.FillUV,
                    BorderUv = coreAtlas.BorderUV,
                    CornerSize = coreAtlas.CornerSize
                };
                nineSliceCache[key] = atlas;
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
            context.PixelShader.SetSampler(0, spriteSampler);

            ConfigureSpritePipeline(spriteInputLayout);

            float3 pos = shape.Parent.Position;
            float x = pos.X;
            float y = pos.Y;
            float w = shape.Size.X;
            float h = shape.Size.Y;
            int s = atlas.CornerSize;
            int lw = s;
            int rw = s;
            int mw = Math.Max(1, (int)w - lw - rw);
            int th = s;
            int bh = s;
            int mh = Math.Max(1, (int)h - th - bh);

            float4x4 transposedWorld;
            float4x4.Transpose(ref projectionMatrix2D, out transposedWorld);
            var shaderData = new SpriteShaderData {
                worldViewProj = transposedWorld,
                color = new float4(shape.FillColor.X / 255f, shape.FillColor.Y / 255f, shape.FillColor.Z / 255f, shape.FillColor.W / 255f)
            };

            context.VertexShader.SetConstantBuffer(0, spriteConstantBuffer);
            context.PixelShader.SetConstantBuffer(0, spriteConstantBuffer);

            void DrawTile(int idx, float dx, float dy, float dw, float dh) {
                var uv = atlas.FillUv[idx];
                shaderData.sourceRect = uv;
                shaderData.destRect = new float4(dx, dy, dw, dh);
                context.UpdateSubresource(ref shaderData, spriteConstantBuffer);
                context.Draw(4, 0);
                parentRenderer.IncrementDrawCalls(1);
            }

            DrawTile(0, x, y, lw, th);
            DrawTile(1, x + lw, y, mw, th);
            DrawTile(2, x + lw + mw, y, rw, th);
            DrawTile(3, x, y + th, lw, mh);
            DrawTile(4, x + lw, y + th, mw, mh);
            DrawTile(5, x + lw + mw, y + th, rw, mh);
            DrawTile(6, x, y + th + mh, lw, bh);
            DrawTile(7, x + lw, y + th + mh, mw, bh);
            DrawTile(8, x + lw + mw, y + th + mh, rw, bh);

            if (shape.BorderThickness > 0) {
                shaderData.color = new float4(shape.BorderColor.X / 255f, shape.BorderColor.Y / 255f, shape.BorderColor.Z / 255f, shape.BorderColor.W / 255f);
                void DrawBorderTile(int idx, float dx, float dy, float dw, float dh) {
                    var uv = atlas.BorderUv[idx];
                    shaderData.sourceRect = uv;
                    shaderData.destRect = new float4(dx, dy, dw, dh);
                    context.UpdateSubresource(ref shaderData, spriteConstantBuffer);
                    context.Draw(4, 0);
                    parentRenderer.IncrementDrawCalls(1);
                }

                DrawBorderTile(0, x, y, lw, th);
                DrawBorderTile(1, x + lw, y, mw, th);
                DrawBorderTile(2, x + lw + mw, y, rw, th);
                DrawBorderTile(3, x, y + th, lw, mh);
                DrawBorderTile(4, x + lw, y + th, mw, mh);
                DrawBorderTile(5, x + lw + mw, y + th, rw, mh);
                DrawBorderTile(6, x, y + th + mh, lw, bh);
                DrawBorderTile(7, x + lw, y + th + mh, mw, bh);
                DrawBorderTile(8, x + lw + mw, y + th + mh, rw, bh);
            }
        }

        /// <summary>
        /// Ensures the geometry vertex buffer can hold at least the given number of vertices.
        /// </summary>
        /// <param name="needed">Required vertex capacity.</param>
        void EnsureGeometryCapacity(int needed) {
            if (needed <= geometryVertexCapacity) {
                return;
            }

            int newCap = geometryVertexCapacity;
            while (newCap < needed) {
                newCap *= 2;
            }

            geometryVertexBuffer.Dispose();
            geometryVertexCapacity = newCap;
            geometryVertexBuffer = new Buffer(Device, new BufferDescription(
                Utilities.SizeOf<VertexPositionUV>() * geometryVertexCapacity,
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

            var dataBox = context.MapSubresource(geometryVertexBuffer, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None);
            var ptr = dataBox.DataPointer;

            float3 pos = shape.Parent.Position;
            float w = shape.Size.X;
            float h = shape.Size.Y;
            float r = Math.Min(shape.Radius, Math.Min(w, h) * 0.5f);
            float cx = pos.X + w * 0.5f;
            float cy = pos.Y + h * 0.5f;

            void WriteVertex(float x, float y) {
                var v = new VertexPositionUV(new float3(x, y, 0), new float2(0, 0));
                Utilities.Write(ptr, ref v);
                ptr += Utilities.SizeOf<VertexPositionUV>();
            }

            void OuterAt(float angle, out float ox, out float oy) {
                float x = MathF.Cos(angle);
                float y = MathF.Sin(angle);
                ox = MathF.Sign(x) * MathF.Max(MathF.Abs(w * 0.5f - r), MathF.Abs(w * 0.5f * x)) + cx;
                oy = MathF.Sign(y) * MathF.Max(MathF.Abs(h * 0.5f - r), MathF.Abs(h * 0.5f * y)) + cy;
                if (MathF.Abs(ox - cx) > (w * 0.5f - r) && MathF.Abs(oy - cy) > (h * 0.5f - r)) {
                    float cornerCx = cx + MathF.Sign(x) * (w * 0.5f - r);
                    float cornerCy = cy + MathF.Sign(y) * (h * 0.5f - r);
                    ox = cornerCx + r * MathF.Sign(x) * MathF.Abs(x);
                    oy = cornerCy + r * MathF.Sign(y) * MathF.Abs(y);
                }
            }

            void InnerAt(float angle, float ir, float iw, float ih, out float ix, out float iy) {
                float x = MathF.Cos(angle);
                float y = MathF.Sin(angle);
                float icx = cx;
                float icy = cy;
                ix = MathF.Sign(x) * MathF.Max(MathF.Abs(iw * 0.5f - ir), MathF.Abs(iw * 0.5f * x)) + icx;
                iy = MathF.Sign(y) * MathF.Max(MathF.Abs(ih * 0.5f - ir), MathF.Abs(ih * 0.5f * y)) + icy;
                if (MathF.Abs(ix - icx) > (iw * 0.5f - ir) && MathF.Abs(iy - icy) > (ih * 0.5f - ir)) {
                    float cornerCx2 = icx + MathF.Sign(x) * (iw * 0.5f - ir);
                    float cornerCy2 = icy + MathF.Sign(y) * (ih * 0.5f - ir);
                    ix = cornerCx2 + ir * MathF.Sign(x) * MathF.Abs(x);
                    iy = cornerCy2 + ir * MathF.Sign(y) * MathF.Abs(y);
                }
            }

            for (int i = 0; i < steps; i++) {
                float a0 = (i / (float)steps) * MathF.PI * 2.0f;
                float a1 = ((i + 1) % steps) / (float)steps * MathF.PI * 2.0f;
                OuterAt(a0, out float ox0, out float oy0);
                OuterAt(a1, out float ox1, out float oy1);
                WriteVertex(cx, cy);
                WriteVertex(ox0, oy0);
                WriteVertex(ox1, oy1);
            }

            if (shape.BorderThickness > 0) {
                float ir = Math.Max(0, r - shape.BorderThickness);
                float iw = Math.Max(0, w - shape.BorderThickness * 2);
                float ih = Math.Max(0, h - shape.BorderThickness * 2);
                for (int i = 0; i < steps; i++) {
                    float a0 = (i / (float)steps) * MathF.PI * 2.0f;
                    float a1 = ((i + 1) % steps) / (float)steps * MathF.PI * 2.0f;
                    OuterAt(a0, out float ox0, out float oy0);
                    OuterAt(a1, out float ox1, out float oy1);
                    InnerAt(a0, ir, iw, ih, out float ix0, out float iy0);
                    InnerAt(a1, ir, iw, ih, out float ix1, out float iy1);

                    WriteVertex(ox0, oy0);
                    WriteVertex(ox1, oy1);
                    WriteVertex(ix1, iy1);
                    WriteVertex(ox0, oy0);
                    WriteVertex(ix1, iy1);
                    WriteVertex(ix0, iy0);
                }
            }

            context.UnmapSubresource(geometryVertexBuffer, 0);

            ConfigureBasicColorPipeline();

            float4x4 transposedWorld;
            float4x4.Transpose(ref projectionMatrix2D, out transposedWorld);

            var colorData = new BasicColorShaderData {
                worldViewProj = transposedWorld,
                color = new float4(
                    shape.FillColor.X / 255.0f,
                    shape.FillColor.Y / 255.0f,
                    shape.FillColor.Z / 255.0f,
                    shape.FillColor.W / 255.0f
                )
            };

            context.VertexShader.SetConstantBuffer(0, basicColorConstantBuffer);
            context.PixelShader.SetConstantBuffer(0, basicColorConstantBuffer);
            context.UpdateSubresource(ref colorData, basicColorConstantBuffer);

            context.Draw(fillVerts, 0);
            parentRenderer.IncrementDrawCalls(1);

            if (borderVerts > 0) {
                colorData.color = new float4(
                    shape.BorderColor.X / 255.0f,
                    shape.BorderColor.Y / 255.0f,
                    shape.BorderColor.Z / 255.0f,
                    shape.BorderColor.W / 255.0f
                );
                context.UpdateSubresource(ref colorData, basicColorConstantBuffer);
                context.Draw(borderVerts, fillVerts);
                parentRenderer.IncrementDrawCalls(1);
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
