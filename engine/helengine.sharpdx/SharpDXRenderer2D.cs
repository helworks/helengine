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

namespace helengine.sharpdx {
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
    /// SharpDX-backed renderer responsible for 2D sprites, text, and UI shapes.
    /// </summary>
    internal class SharpDXRenderer2D : RenderManager2D {
        const int InitialGeometryVertexCapacity = 1024;

        readonly SharpDXRenderer3D parentRenderer;
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
        Buffer geometryVertexBuffer = null!;
        int geometryVertexCapacity;
        float4x4 projectionMatrix2D;
        RasterizerState rasterizerState2D;
        DepthStencilState depthStencilState2D;
        RoundedRectBackend roundedRectBackend = RoundedRectBackend.Sdf;
        Dictionary<(int Radius, int Border), NineSliceCacheEntry> nineSliceCache = new();

        /// <summary>
        /// Initializes the 2D renderer and builds the required GPU resources.
        /// </summary>
        /// <param name="parentRenderer">Owning 3D renderer.</param>
        public SharpDXRenderer2D(SharpDXRenderer3D parentRenderer) {
            this.parentRenderer = parentRenderer;
            Device = parentRenderer.Device;

            InitializeSpritePipeline();

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

            DebugInfoRegistry.Register(new SharpDXRenderer2DDebugInfoProvider(this));
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
        /// <param name="camera">Camera supplying buckets.</param>
        internal void RenderCamera(ICamera camera) {
            ConfigureSpritePipeline(spriteInputLayout);

            float4 viewport = camera.Viewport;
            float4x4.CreateOrthographicOffCenter(0, viewport.Z, -viewport.W, 0, -10, 10, out projectionMatrix2D);

            var buckets = camera.RenderBuckets2D;
            for (int b = 0; b < buckets.Length; b++) {
                var rb = buckets[b];
                int n = rb.Count;
                var items = rb.Items;
                for (int j = 0; j < n; j++) {
                    var drawable = items[j];
                    if (drawable?.Parent == null || !drawable.Parent.Enabled) {
                        continue;
                    }
                    drawable.Draw();
                }
            }
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
            var data = (SharpDXTextureResource)drawable.Texture;

            context.PixelShader.SetShaderResource(0, data.Resource);
            context.PixelShader.SetSampler(0, spriteSampler);

            int2 size = drawable.Size;
            if (size.X <= 0 || size.Y <= 0) {
                size = new int2(data.Width, data.Height);
            }

            float3 pos = drawable.Parent.Position;
            byte4 color = drawable.Color;

            float4x4 transposedWorld;
            float4x4.Transpose(ref projectionMatrix2D, out transposedWorld);

            context.VertexShader.SetConstantBuffer(0, spriteConstantBuffer);
            context.PixelShader.SetConstantBuffer(0, spriteConstantBuffer);

            var shaderData = new SpriteShaderData {
                worldViewProj = transposedWorld,
                sourceRect = drawable.SourceRect,
                destRect = new float4(pos.X, pos.Y, size.X, size.Y),
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
            var data = (SharpDXTextureResource)font.Texture;

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

            string text = drawable.Text;
            float offsetX = 0f;
            float offsetY = 0f;
            float lineHeight = Math.Max(font.LineHeight, 1f);

            for (int i = 0; i < text.Length; i++) {
                char c = text[i];

                if (c == (char)10) {
                    offsetY += lineHeight;
                    offsetX = 0f;
                    continue;
                }

                if (c == ' ') {
                    offsetX += font.FontInfo.SpaceWidth;
                    continue;
                }

                if (!font.Characters.TryGetValue(c, out FontChar info)) {
                    continue;
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

                context.UpdateSubresource(ref shaderData, spriteConstantBuffer);
                context.Draw(4, 0);
                parentRenderer.IncrementDrawCalls(1);
            }
        }

        /// <summary>
        /// Draws a rounded rectangle using the configured backend.
        /// </summary>
        /// <param name="shape">Rounded rectangle drawable.</param>
        public override void DrawRoundedRect(IRoundedRectDrawable2D shape) {
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
            var asset = new SharpDXTextureResource {
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
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
            context.VertexShader.Set(uiShapeVertexShader);
            context.PixelShader.Set(uiShapePixelShader);
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(spriteQuadBuffer, Utilities.SizeOf<VertexPositionUV>(), 0));
            context.InputAssembler.InputLayout = uiShapeInputLayout;
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

            using (var spriteVs = ShaderBytecode.CompileFromFile("shaders\\SpriteShader.fx", "VS", "vs_4_0")) {
                spriteVertexShader = new VertexShader(Device, spriteVs);
                spriteInputLayout = new InputLayout(Device, spriteVs, new[] {
                    new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                    new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0)
                });
            }

            using (var spritePs = ShaderBytecode.CompileFromFile("shaders\\SpriteShader.fx", "PS", "ps_4_0")) {
                spritePixelShader = new PixelShader(Device, spritePs);
            }

            using (var uiVs = ShaderBytecode.CompileFromFile("shaders\\UIShapeShader.fx", "VS", "vs_4_0")) {
                uiShapeVertexShader = new VertexShader(Device, uiVs);
                uiShapeInputLayout = new InputLayout(Device, uiVs, new[] {
                    new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                    new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0)
                });
            }

            using (var uiPs = ShaderBytecode.CompileFromFile("shaders\\UIShapeShader.fx", "PS", "ps_4_0")) {
                uiShapePixelShader = new PixelShader(Device, uiPs);
            }

            using (var colVs = ShaderBytecode.CompileFromFile("shaders\\BasicColorShader.fx", "VS", "vs_4_0")) {
                basicColorVertexShader = new VertexShader(Device, colVs);
                basicColorInputLayout = new InputLayout(Device, colVs, new[] {
                    new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                    new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0)
                });
            }

            using (var colPs = ShaderBytecode.CompileFromFile("shaders\\BasicColorShader.fx", "PS", "ps_4_0")) {
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
                params1 = new float4(shape.Radius, shape.BorderThickness, 1.0f, 0.0f),
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

            var sdata = (SharpDXTextureResource)atlas.Texture;
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

            context.Rasterizer.State = rasterizerState2D;
            context.OutputMerger.SetDepthStencilState(depthStencilState2D, 0);
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(geometryVertexBuffer, Utilities.SizeOf<VertexPositionUV>(), 0));
            context.InputAssembler.InputLayout = basicColorInputLayout;

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

            context.VertexShader.Set(basicColorVertexShader);
            context.PixelShader.Set(basicColorPixelShader);
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
