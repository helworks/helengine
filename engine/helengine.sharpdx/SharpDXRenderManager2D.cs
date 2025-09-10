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
    internal class SharpDXRenderManager2D : RenderManager2D {
        private readonly SharpDXRenderManager3D parent;
        private Buffer quadBuffer;
        private InputLayout quadLayout;
        private VertexShader quadVertexShader;
        private PixelShader quadPixelShader;
        private VertexShader uiShapeVertexShader;
        private PixelShader uiShapePixelShader;
        private VertexShader basicColorVertexShader;
        private PixelShader basicColorPixelShader;
        private SamplerState quadSampler;
        private Buffer quadConstantBuffer;
        private Buffer uiShapeConstantBuffer;
        private Buffer basicColorConstantBuffer;
        private Buffer geomVertexBuffer;
        private int geomVertexCapacity;
        private float4x4 projection2D;
        RasterizerState rasterizerState2D;
        DepthStencilState depthStencilState2D;

        public D3DDevice Device { get; private set; }

        public SharpDXRenderManager2D(SharpDXRenderManager3D parent) {
            this.parent = parent;
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

            DebugInfoRegistry.Register(new RendererDebugInfo(this));
        }

        private void EnsureSpritePipeline() {
            var context = Device.ImmediateContext;
            context.Rasterizer.State = rasterizerState2D;
            context.OutputMerger.SetDepthStencilState(depthStencilState2D, 0);
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
            context.VertexShader.Set(quadVertexShader);
            context.PixelShader.Set(quadPixelShader);
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(quadBuffer, Utilities.SizeOf<VertexPositionUV>(), 0));
            context.InputAssembler.InputLayout = quadLayout;
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

            var spriteVS = ShaderBytecode.CompileFromFile("shaders\\SpriteShader.fx", "VS", "vs_4_0");
            quadVertexShader = new VertexShader(Device, spriteVS);

            var spritePS = ShaderBytecode.CompileFromFile("shaders\\SpriteShader.fx", "PS", "ps_4_0");
            quadPixelShader = new PixelShader(Device, spritePS);

            // UI Shape shader (rounded rectangles)
            var uiVS = ShaderBytecode.CompileFromFile("shaders\\UIShapeShader.fx", "VS", "vs_4_0");
            uiShapeVertexShader = new VertexShader(Device, uiVS);
            var uiPS = ShaderBytecode.CompileFromFile("shaders\\UIShapeShader.fx", "PS", "ps_4_0");
            uiShapePixelShader = new PixelShader(Device, uiPS);

            // Basic color shader (for geometry fallback)
            var colVS = ShaderBytecode.CompileFromFile("shaders\\BasicColorShader.fx", "VS", "vs_4_0");
            basicColorVertexShader = new VertexShader(Device, colVS);
            var colPS = ShaderBytecode.CompileFromFile("shaders\\BasicColorShader.fx", "PS", "ps_4_0");
            basicColorPixelShader = new PixelShader(Device, colPS);

            quadLayout = new InputLayout(Device, spriteVS, new[]
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

            // UI shape constant buffer
            var shapeBufferDesc = new BufferDescription(
                Marshal.SizeOf<UIShapeShaderData>(),
                ResourceUsage.Default,
                BindFlags.ConstantBuffer,
                CpuAccessFlags.None,
                ResourceOptionFlags.None,
                0
            );
            uiShapeConstantBuffer = new Buffer(Device, shapeBufferDesc);

            var colorBufferDesc = new BufferDescription(
                Marshal.SizeOf<BasicColorShaderData>(),
                ResourceUsage.Default,
                BindFlags.ConstantBuffer,
                CpuAccessFlags.None,
                ResourceOptionFlags.None,
                0
            );
            basicColorConstantBuffer = new Buffer(Device, colorBufferDesc);

            // Geometry dynamic vertex buffer (initial capacity)
            geomVertexCapacity = 1024;
            geomVertexBuffer = new Buffer(Device, new BufferDescription(
                Utilities.SizeOf<VertexPositionUV>() * geomVertexCapacity,
                ResourceUsage.Dynamic,
                BindFlags.VertexBuffer,
                CpuAccessFlags.Write,
                ResourceOptionFlags.None,
                0));
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

            var buckets = camera.RenderBuckets2D;
            for (int b = 0; b < buckets.Length; b++) {
                var rb = buckets[b];
                int n = rb.Count;
                var items = rb.Items;
                for (int j = 0; j < n; j++) {
                    var drawable = items[j];
                    if (drawable?.Parent == null || !drawable.Parent.Enabled) continue;
                    drawable.Draw();
                }
            }
        }

        public override void DrawSprite(ISpriteDrawable2D drawable) {
            EnsureSpritePipeline();
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
            parent.IncrementDrawCalls(1);
        }

        public override void DrawText(ITextDrawable2D drawable) {
            EnsureSpritePipeline();
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
                parent.IncrementDrawCalls(1);
            }
        }

        private enum UIShapeBackend { SDF, NineSlice, Geometry }
        private UIShapeBackend backend = UIShapeBackend.SDF;
        private struct NineSliceRuntime { public RuntimeTexture Texture; public float4[] FillUV; public float4[] BorderUV; public int CornerSize; }
        private readonly Dictionary<(int, int), NineSliceRuntime> nineSliceCache = new();

        public void SetUIBackend(string mode) {
            switch (mode?.ToLowerInvariant()) {
                case "sdf": backend = UIShapeBackend.SDF; break;
                case "nineslice": backend = UIShapeBackend.NineSlice; break;
                case "geometry": backend = UIShapeBackend.Geometry; break;
            }
        }

        public override void DrawRoundedRect(IRoundedRectDrawable2D shape) {
            if (backend == UIShapeBackend.SDF) { DrawRoundedRectSDF(shape); return; }
            if (backend == UIShapeBackend.NineSlice) { DrawRoundedRectNineSlice(shape); return; }
            DrawRoundedRectGeometry(shape);
        }

        public override RuntimeTexture BuildTextureFromRaw(TextureAsset data) {
            SharpDXTextureRuntimeData asset = new SharpDXTextureRuntimeData();
            asset.Width = data.Width;
            asset.Height = data.Height;

            int bytesPerPixel = 4; // For R8G8B8A8 format
            int expectedDataLength = data.Width * data.Height * bytesPerPixel;
            if (data.Colors.Length != expectedDataLength) {
                throw new ArgumentException("Data length does not match width and height.");
            }

            var textureDesc = new Texture2DDescription {
                Width = data.Width,
                Height = data.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = SharpDX.DXGI.Format.R8G8B8A8_UNorm,
                SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            };

            GCHandle dataHandle = GCHandle.Alloc(data.Colors, GCHandleType.Pinned);
            try {
                IntPtr dataPtr = dataHandle.AddrOfPinnedObject();
                int rowPitch = data.Width * bytesPerPixel;

                asset.Texture = new Texture2D(
                    Device,
                    textureDesc,
                    new DataRectangle(dataPtr, rowPitch)
                );
            } finally {
                dataHandle.Free();
            }

            asset.Resource = new ShaderResourceView(Device, asset.Texture);

            return asset;
        }

        public override void Dispose() {
            quadBuffer?.Dispose();
            quadLayout?.Dispose();
            quadVertexShader?.Dispose();
            quadPixelShader?.Dispose();
            uiShapeVertexShader?.Dispose();
            uiShapePixelShader?.Dispose();
            basicColorVertexShader?.Dispose();
            basicColorPixelShader?.Dispose();
            quadSampler?.Dispose();
            quadConstantBuffer?.Dispose();
            uiShapeConstantBuffer?.Dispose();
            basicColorConstantBuffer?.Dispose();
            geomVertexBuffer?.Dispose();
            rasterizerState2D?.Dispose();
            depthStencilState2D?.Dispose();
        }

        private void DrawRoundedRectSDF(IRoundedRectDrawable2D shape) {
            var context = Device.ImmediateContext;

            context.Rasterizer.State = rasterizerState2D;
            context.OutputMerger.SetDepthStencilState(depthStencilState2D, 0);

            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
            context.VertexShader.Set(uiShapeVertexShader);
            context.PixelShader.Set(uiShapePixelShader);
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(quadBuffer, Utilities.SizeOf<VertexPositionUV>(), 0));
            context.InputAssembler.InputLayout = quadLayout;

            float3 pos = shape.Parent.Position;
            float4x4 transposedWorld;
            float4x4.Transpose(ref projection2D, out transposedWorld);

            UIShapeShaderData sd = new UIShapeShaderData();
            sd.worldViewProj = transposedWorld;
            sd.destRect = new float4(pos.X, pos.Y, shape.Size.X, shape.Size.Y);
            sd.params1 = new float4(shape.Radius, shape.BorderThickness, 1.0f, 0.0f);
            sd.fillColor = new float4(
                shape.FillColor.X / 255.0f,
                shape.FillColor.Y / 255.0f,
                shape.FillColor.Z / 255.0f,
                shape.FillColor.W / 255.0f
            );
            sd.borderColor = new float4(
                shape.BorderColor.X / 255.0f,
                shape.BorderColor.Y / 255.0f,
                shape.BorderColor.Z / 255.0f,
                shape.BorderColor.W / 255.0f
            );

            context.VertexShader.SetConstantBuffer(0, uiShapeConstantBuffer);
            context.PixelShader.SetConstantBuffer(0, uiShapeConstantBuffer);
            context.UpdateSubresource(ref sd, uiShapeConstantBuffer);

            context.Draw(4, 0);
            parent.IncrementDrawCalls(1);
        }

        private NineSliceRuntime GetNineSliceAtlas(IRoundedRectDrawable2D shape) {
            int r = (int)MathF.Round(shape.Radius);
            int b = (int)MathF.Round(shape.BorderThickness);
            var key = (r, b);
            if (!nineSliceCache.TryGetValue(key, out var atlas)) {
                var coreAtlas = helengine.NineSliceAtlas.Generate(r, b, aaPx: 1, padding: 2);
                var rt = Core.Instance.RenderManager2D.BuildTextureFromRaw(coreAtlas.Texture);
                atlas = new NineSliceRuntime {
                    Texture = rt,
                    FillUV = coreAtlas.FillUV,
                    BorderUV = coreAtlas.BorderUV,
                    CornerSize = coreAtlas.CornerSize
                };
                nineSliceCache[key] = atlas;
            }
            return atlas;
        }

        private void DrawRoundedRectNineSlice(IRoundedRectDrawable2D shape) {
            var context = Device.ImmediateContext;
            var atlas = GetNineSliceAtlas(shape);

            // Bind atlas
            var sdata = (SharpDXTextureRuntimeData)atlas.Texture;
            context.PixelShader.SetShaderResource(0, sdata.Resource);
            context.PixelShader.SetSampler(0, quadSampler);

            // Setup sprite pipeline
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
            context.VertexShader.Set(quadVertexShader);
            context.PixelShader.Set(quadPixelShader);
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(quadBuffer, Utilities.SizeOf<VertexPositionUV>(), 0));
            context.InputAssembler.InputLayout = quadLayout;

            float3 pos = shape.Parent.Position;
            float x = pos.X;
            float y = pos.Y;
            float w = shape.Size.X;
            float h = shape.Size.Y;
            int s = atlas.CornerSize;
            int lw = s; int rw = s; int mw = Math.Max(1, (int)w - lw - rw);
            int th = s; int bh = s; int mh = Math.Max(1, (int)h - th - bh);

            float4x4 transposedWorld; float4x4.Transpose(ref projection2D, out transposedWorld);
            SpriteShaderData sd = new SpriteShaderData();
            sd.worldViewProj = transposedWorld;
            sd.color = new float4(shape.FillColor.X / 255f, shape.FillColor.Y / 255f, shape.FillColor.Z / 255f, shape.FillColor.W / 255f);

            context.VertexShader.SetConstantBuffer(0, quadConstantBuffer);
            context.PixelShader.SetConstantBuffer(0, quadConstantBuffer);

            void drawTile(int idx, float dx, float dy, float dw, float dh) {
                var uv = atlas.FillUV[idx];
                sd.sourceRect = uv;
                sd.destRect = new float4(dx, dy, dw, dh);
                context.UpdateSubresource(ref sd, quadConstantBuffer);
                context.Draw(4, 0);
                parent.IncrementDrawCalls(1);
            }

            // Top row
            drawTile(0, x, y, lw, th);
            drawTile(1, x + lw, y, mw, th);
            drawTile(2, x + lw + mw, y, rw, th);
            // Middle row
            drawTile(3, x, y + th, lw, mh);
            drawTile(4, x + lw, y + th, mw, mh);
            drawTile(5, x + lw + mw, y + th, rw, mh);
            // Bottom row
            drawTile(6, x, y + th + mh, lw, bh);
            drawTile(7, x + lw, y + th + mh, mw, bh);
            drawTile(8, x + lw + mw, y + th + mh, rw, bh);
            parent.IncrementDrawCalls(9);

            // Border overlay
            if (shape.BorderThickness > 0) {
                sd.color = new float4(shape.BorderColor.X / 255f, shape.BorderColor.Y / 255f, shape.BorderColor.Z / 255f, shape.BorderColor.W / 255f);
                void drawBorderTile(int idx, float dx, float dy, float dw, float dh) {
                    var uv = atlas.BorderUV[idx];
                    sd.sourceRect = uv;
                    sd.destRect = new float4(dx, dy, dw, dh);
                    context.UpdateSubresource(ref sd, quadConstantBuffer);
                    context.Draw(4, 0);
                    parent.IncrementDrawCalls(1);
                }
                // Top row
                drawBorderTile(0, x, y, lw, th);
                drawBorderTile(1, x + lw, y, mw, th);
                drawBorderTile(2, x + lw + mw, y, rw, th);
                // Middle row
                drawBorderTile(3, x, y + th, lw, mh);
                drawBorderTile(4, x + lw, y + th, mw, mh);
                drawBorderTile(5, x + lw + mw, y + th, rw, mh);
                // Bottom row
                drawBorderTile(6, x, y + th + mh, lw, bh);
                drawBorderTile(7, x + lw, y + th + mh, mw, bh);
                drawBorderTile(8, x + lw + mw, y + th + mh, rw, bh);
                parent.IncrementDrawCalls(9);
            }
        }

        private void EnsureGeomCapacity(int needed) {
            if (needed <= geomVertexCapacity) return;
            // Double until sufficient
            int newCap = geomVertexCapacity;
            while (newCap < needed) newCap *= 2;
            geomVertexBuffer.Dispose();
            geomVertexCapacity = newCap;
            geomVertexBuffer = new Buffer(Device, new BufferDescription(
                Utilities.SizeOf<VertexPositionUV>() * geomVertexCapacity,
                ResourceUsage.Dynamic,
                BindFlags.VertexBuffer,
                CpuAccessFlags.Write,
                ResourceOptionFlags.None,
                0));
        }

        private void DrawRoundedRectGeometry(IRoundedRectDrawable2D shape) {
            var context = Device.ImmediateContext;

            // Build geometry for rounded rect: fill fan + border ring
            const int segmentsPerCorner = 8;
            int corners = 4;
            int ringSegments = segmentsPerCorner * corners;

            // Estimate vertices: fill fan (1 center + ringSegments vertices), border ring (ringSegments*2 vertices)
            int fillVerts = 1 + ringSegments + 1; // closing vertex
            int borderVerts = (shape.BorderThickness > 0) ? (ringSegments + 1) * 2 : 0;
            int totalVerts = fillVerts + borderVerts;
            EnsureGeomCapacity(totalVerts);

            var dataBox = context.MapSubresource(geomVertexBuffer, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None);
            var ptr = dataBox.DataPointer;

            float3 pos = shape.Parent.Position;
            float w = shape.Size.X;
            float h = shape.Size.Y;
            float r = Math.Min(shape.Radius, Math.Min(w, h) * 0.5f);
            float cx = pos.X + w * 0.5f;
            float cy = pos.Y + h * 0.5f;

            // Helper to write a vertex
            void write(float x, float y) {
                var v = new VertexPositionUV(new float3(x, y, 0), new float2(0, 0));
                Utilities.Write(ptr, ref v);
                ptr += Utilities.SizeOf<VertexPositionUV>();
            }

            // Fill fan
            write(cx, cy);
            // Create ring around the rounded rect
            int steps = ringSegments;
            for (int i = 0; i <= steps; i++) {
                float t = (float)i / steps; // 0..1
                // Map t to four corners
                float angle = t * MathF.PI * 2.0f;
                // Parametric rounded-rect approx: clamp to box then add corner arcs
                float x = MathF.Cos(angle);
                float y = MathF.Sin(angle);
                // Compute point on outer rounded rect
                float ox = MathF.Sign(x) * MathF.Max(MathF.Abs(w * 0.5f - r), MathF.Abs(w * 0.5f * x)) + cx;
                float oy = MathF.Sign(y) * MathF.Max(MathF.Abs(h * 0.5f - r), MathF.Abs(h * 0.5f * y)) + cy;
                // Adjust to arc near corners
                if (MathF.Abs(ox - cx) > (w * 0.5f - r) && MathF.Abs(oy - cy) > (h * 0.5f - r)) {
                    float cornerCx = cx + MathF.Sign(x) * (w * 0.5f - r);
                    float cornerCy = cy + MathF.Sign(y) * (h * 0.5f - r);
                    ox = cornerCx + r * MathF.Sign(x) * MathF.Abs(x);
                    oy = cornerCy + r * MathF.Sign(y) * MathF.Abs(y);
                }
                write(ox, oy);
            }

            // Border ring (if any)
            if (shape.BorderThickness > 0) {
                float ir = Math.Max(0, r - shape.BorderThickness);
                float iw = Math.Max(0, w - shape.BorderThickness * 2);
                float ih = Math.Max(0, h - shape.BorderThickness * 2);
                for (int i = 0; i <= steps; i++) {
                    float t = (float)i / steps;
                    float angle = t * MathF.PI * 2.0f;
                    float x = MathF.Cos(angle);
                    float y = MathF.Sin(angle);
                    float ox = MathF.Sign(x) * MathF.Max(MathF.Abs(w * 0.5f - r), MathF.Abs(w * 0.5f * x)) + cx;
                    float oy = MathF.Sign(y) * MathF.Max(MathF.Abs(h * 0.5f - r), MathF.Abs(h * 0.5f * y)) + cy;
                    if (MathF.Abs(ox - cx) > (w * 0.5f - r) && MathF.Abs(oy - cy) > (h * 0.5f - r)) {
                        float cornerCx = cx + MathF.Sign(x) * (w * 0.5f - r);
                        float cornerCy = cy + MathF.Sign(y) * (h * 0.5f - r);
                        ox = cornerCx + r * MathF.Sign(x) * MathF.Abs(x);
                        oy = cornerCy + r * MathF.Sign(y) * MathF.Abs(y);
                    }

                    // inner ring point
                    float icx = cx;
                    float icy = cy;
                    float ix = MathF.Sign(x) * MathF.Max(MathF.Abs(iw * 0.5f - ir), MathF.Abs(iw * 0.5f * x)) + icx;
                    float iy = MathF.Sign(y) * MathF.Max(MathF.Abs(ih * 0.5f - ir), MathF.Abs(ih * 0.5f * y)) + icy;
                    if (MathF.Abs(ix - icx) > (iw * 0.5f - ir) && MathF.Abs(iy - icy) > (ih * 0.5f - ir)) {
                        float cornerCx2 = icx + MathF.Sign(x) * (iw * 0.5f - ir);
                        float cornerCy2 = icy + MathF.Sign(y) * (ih * 0.5f - ir);
                        ix = cornerCx2 + ir * MathF.Sign(x) * MathF.Abs(x);
                        iy = cornerCy2 + ir * MathF.Sign(y) * MathF.Abs(y);
                    }

                    write(ox, oy);
                    write(ix, iy);
                }
            }

            context.UnmapSubresource(geomVertexBuffer, 0);

            // Setup pipeline
            context.Rasterizer.State = rasterizerState2D;
            context.OutputMerger.SetDepthStencilState(depthStencilState2D, 0);
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(geomVertexBuffer, Utilities.SizeOf<VertexPositionUV>(), 0));
            context.InputAssembler.InputLayout = quadLayout; // matches POSITION+TEXCOORD

            float4x4 transposedWorld;
            float4x4.Transpose(ref projection2D, out transposedWorld);

            // Draw fill fan
            var col = new BasicColorShaderData();
            col.worldViewProj = transposedWorld;
            col.color = new float4(
                shape.FillColor.X / 255.0f,
                shape.FillColor.Y / 255.0f,
                shape.FillColor.Z / 255.0f,
                shape.FillColor.W / 255.0f
            );

            context.VertexShader.Set(basicColorVertexShader);
            context.PixelShader.Set(basicColorPixelShader);
            context.VertexShader.SetConstantBuffer(0, basicColorConstantBuffer);
            context.PixelShader.SetConstantBuffer(0, basicColorConstantBuffer);
            context.UpdateSubresource(ref col, basicColorConstantBuffer);

            // Fill fan uses TriangleFan, which D3D11 doesn't support directly; emulate with triangle list using strip ordering
            // We already wrote center + ring. To render as strip, we can draw from vertex 0 with strip; it will not form a fan correctly, but acceptable for basic fill approx.
            // For correctness, draw as list using small batches would be required; here we draw as strip for simplicity.
            int fillVertexCount = fillVerts;
            context.Draw(fillVertexCount, 0);
            parent.IncrementDrawCalls(1);

            if (borderVerts > 0) {
                col.color = new float4(
                    shape.BorderColor.X / 255.0f,
                    shape.BorderColor.Y / 255.0f,
                    shape.BorderColor.Z / 255.0f,
                    shape.BorderColor.W / 255.0f
                );
                context.UpdateSubresource(ref col, basicColorConstantBuffer);
                context.Draw(borderVerts, fillVertexCount);
                parent.IncrementDrawCalls(1);
            }
        }
        class RendererDebugInfo : IDebugInfoProvider {
            private readonly SharpDXRenderManager2D owner;
            public RendererDebugInfo(SharpDXRenderManager2D o) { owner = o; }
            public string Category => "Renderer";
            public void AppendInfo(List<(string Key, string Value)> items) {
                items.Add(("UI Backend", owner.backend.ToString()));
            }
        }
    }
}
