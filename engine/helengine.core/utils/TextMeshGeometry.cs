namespace helengine {
    /// <summary>Builds planar 3D glyph meshes from the engine's explicitly generated font atlas and metrics.</summary>
    public static class TextMeshGeometry {
        /// <summary>Creates owned local-XY geometry at the font's native pixel size, with lines descending along negative Y.</summary>
        /// <param name="font">Borrowed engine font containing every requested non-control glyph.</param>
        /// <param name="text">Complete text; CR is ignored, LF starts a line and TAB advances by four spaces.</param>
        /// <returns>Owned model data; runs without visible glyphs contain no submeshes or triangles.</returns>
        [NativeOwnedReturn]
        public static ModelAsset Build(FontAsset font, string text) {
            if (font == null) { throw new ArgumentNullException(nameof(font)); }
            if (text == null) { throw new ArgumentNullException(nameof(text)); }
            if (font.IsDisposed || font.Characters == null || font.FontInfo == null || font.AtlasWidth < 1 || font.AtlasHeight < 1
                || !RealEvaluation.IsFinite(font.LineHeight) || font.LineHeight <= 0 || !float.IsFinite(font.FontInfo.SpaceWidth) || font.FontInfo.SpaceWidth < 0) {
                throw new InvalidOperationException("Text geometry requires a live font with valid atlas and layout metrics.");
            }
            int count = CountGlyphs(font, text);
            if (count > int.MaxValue / 6) { throw new InvalidOperationException("Text geometry exceeds the model index capacity."); }
            int vertexCount = count * 4;
            ModelAsset model = new ModelAsset {
                Id = string.Empty,
                Positions = new float3[vertexCount],
                Normals = new float3[vertexCount],
                TexCoords = new float2[vertexCount],
                Indices16 = vertexCount <= 65536 ? new ushort[count * 6] : null,
                Indices32 = vertexCount > 65536 ? new uint[count * 6] : null,
                Submeshes = count == 0 ? new ModelSubmeshAsset[0] : new ModelSubmeshAsset[]{new ModelSubmeshAsset {
                    MaterialSlotName="Font",IndexStart=0,IndexCount=count*6
                }}
            };
            RealEvaluation penX = 0; RealEvaluation penY = 0; int glyphIndex = 0;
            for (int index = 0; index < text.Length; index++) {
                char character = text[index];
                if (character == '\r') { continue; }
                if (character == '\n') { penX = 0; penY -= font.LineHeight; continue; }
                if (character == ' ' || character == '\t') { penX += font.FontInfo.SpaceWidth * (character == '\t' ? 4 : 1); continue; }
                FontChar glyph = font.Characters[character];
                RealEvaluation width = glyph.SourceRect.Z * font.AtlasWidth;
                RealEvaluation height = glyph.SourceRect.W * font.AtlasHeight;
                RealEvaluation x = penX + glyph.BearingX; RealEvaluation y = penY - glyph.OffsetY;
                penX += glyph.AdvanceWidth > 0 ? glyph.AdvanceWidth : width;
                if (width <= 0 || height <= 0) { continue; }
                EmitGlyph(model, glyphIndex, glyph, x, y, width, height);
                glyphIndex++;
            }
            return model;
        }
        /// <summary>Validates the complete run before allocating geometry, rejecting missing or unrepresentable characters explicitly.</summary>
        static int CountGlyphs(FontAsset font, string text) {
            int count = 0;
            for (int index = 0; index < text.Length; index++) {
                char character = text[index];
                if (character == '\r' || character == '\n' || character == ' ' || character == '\t') { continue; }
                if (character >= '\uD800' && character <= '\uDFFF') {
                    throw new NotSupportedException("The current font atlas format cannot represent UTF-16 surrogate glyphs.");
                }
                if (!font.Characters.TryGetValue(character, out FontChar glyph)) {
                    throw new InvalidOperationException("The requested character is absent from the generated font atlas: " + character);
                }
                if (!RealEvaluation.IsFinite(glyph.SourceRect.X) || !RealEvaluation.IsFinite(glyph.SourceRect.Y)
                    || !RealEvaluation.IsFinite(glyph.SourceRect.Z) || !RealEvaluation.IsFinite(glyph.SourceRect.W)
                    || !float.IsFinite(glyph.AdvanceWidth) || !float.IsFinite(glyph.BearingX) || !float.IsFinite(glyph.OffsetY)
                    || glyph.SourceRect.Z < 0 || glyph.SourceRect.W < 0) {
                    throw new InvalidOperationException("Font glyph metrics must be finite and dimensions nonnegative.");
                }
                if (glyph.SourceRect.Z > 0 && glyph.SourceRect.W > 0) { count++; }
            }
            return count;
        }
        /// <summary>Writes one quad with atlas UVs, positive-Z winding and visible bounds independent of logical pen advances.</summary>
        static void EmitGlyph(ModelAsset model, int glyphIndex, FontChar glyph, RealEvaluation x, RealEvaluation y, RealEvaluation width, RealEvaluation height) {
            int vertex = glyphIndex * 4;
            model.Positions[vertex] = new float3((Real)x, (Real)y, 0);
            model.Positions[vertex + 1] = new float3((Real)(x + width), (Real)y, 0);
            model.Positions[vertex + 2] = new float3((Real)(x + width), (Real)(y - height), 0);
            model.Positions[vertex + 3] = new float3((Real)x, (Real)(y - height), 0);
            RealEvaluation right = glyph.SourceRect.X + glyph.SourceRect.Z;
            RealEvaluation bottom = glyph.SourceRect.Y + glyph.SourceRect.W;
            model.TexCoords[vertex] = new float2(glyph.SourceRect.X, glyph.SourceRect.Y);
            model.TexCoords[vertex + 1] = new float2((Real)right, glyph.SourceRect.Y);
            model.TexCoords[vertex + 2] = new float2((Real)right, (Real)bottom);
            model.TexCoords[vertex + 3] = new float2(glyph.SourceRect.X, (Real)bottom);
            for (int offset = 0; offset < 4; offset++) { model.Normals[vertex + offset] = float3.UnitZ; }
            WriteIndex(model, glyphIndex * 6, vertex); WriteIndex(model, glyphIndex * 6 + 1, vertex + 2); WriteIndex(model, glyphIndex * 6 + 2, vertex + 1);
            WriteIndex(model, glyphIndex * 6 + 3, vertex); WriteIndex(model, glyphIndex * 6 + 4, vertex + 3); WriteIndex(model, glyphIndex * 6 + 5, vertex + 2);
            float3 minimum = model.Positions[vertex + 3]; float3 maximum = model.Positions[vertex + 1];
            if (glyphIndex == 0) { model.BoundsMin = minimum; model.BoundsMax = maximum; } else {
                model.BoundsMin = new float3((Real)RealMath.Min(model.BoundsMin.X, minimum.X), (Real)RealMath.Min(model.BoundsMin.Y, minimum.Y), 0);
                model.BoundsMax = new float3((Real)RealMath.Max(model.BoundsMax.X, maximum.X), (Real)RealMath.Max(model.BoundsMax.Y, maximum.Y), 0);
            }
        }
        /// <summary>Writes a validated vertex index to exactly one selected index-buffer representation.</summary>
        static void WriteIndex(ModelAsset model, int offset, int vertex) {
            if (model.Indices16 != null) { model.Indices16[offset] = (ushort)vertex; } else { model.Indices32[offset] = (uint)vertex; }
        }
    }
}