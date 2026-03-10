namespace helengine.editor {
    /// <summary>
    /// Generates centered billboard mesh geometry for short transform-gizmo axis labels.
    /// </summary>
    public static class TransformGizmoAxisLabelModelFactory {
        /// <summary>
        /// Padding added around each glyph quad in atlas pixels so the material can render a 1-pixel outline outside the glyph body.
        /// </summary>
        const double GlyphOutlinePaddingPixels = 1.0;

        /// <summary>
        /// Creates a centered quad mesh for the supplied axis-label string using glyph data from the font atlas.
        /// </summary>
        /// <param name="font">Font atlas and glyph metrics used to build the label mesh.</param>
        /// <param name="text">Axis-label text to convert into billboard geometry.</param>
        /// <returns>Model asset containing camera-facing quads for the supplied text.</returns>
        public static ModelAsset Create(FontAsset font, string text) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            if (string.IsNullOrWhiteSpace(text)) {
                throw new ArgumentException("Axis-label text must be provided.", nameof(text));
            }

            if (font.AtlasWidth <= 0) {
                throw new InvalidOperationException("Font atlas width must be greater than zero.");
            }

            if (font.AtlasHeight <= 0) {
                throw new InvalidOperationException("Font atlas height must be greater than zero.");
            }

            List<float3> positions = new List<float3>(text.Length * 4);
            List<float3> normals = new List<float3>(text.Length * 4);
            List<float2> texCoords = new List<float2>(text.Length * 4);
            List<ushort> indices = new List<ushort>(text.Length * 6);

            double cursorX = 0.0;
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;
            int glyphCount = 0;

            for (int characterIndex = 0; characterIndex < text.Length; characterIndex++) {
                char character = text[characterIndex];
                if (character == '\n' || character == '\r') {
                    throw new InvalidOperationException("Transform-gizmo axis labels must be single-line strings.");
                }

                if (character == ' ') {
                    cursorX += font.FontInfo.SpaceWidth;
                    continue;
                }

                if (!font.Characters.TryGetValue(character, out FontChar glyph)) {
                    throw new InvalidOperationException($"Font atlas does not contain the '{character}' glyph required for transform-gizmo axis labels.");
                }

                double glyphWidth = glyph.SourceRect.Z * font.AtlasWidth;
                double glyphHeight = glyph.SourceRect.W * font.AtlasHeight;
                double left = cursorX - GlyphOutlinePaddingPixels;
                double top = -glyph.OffsetY + GlyphOutlinePaddingPixels;
                double right = cursorX + glyphWidth + GlyphOutlinePaddingPixels;
                double bottom = (-glyph.OffsetY) - glyphHeight - GlyphOutlinePaddingPixels;

                if (positions.Count > ushort.MaxValue - 4) {
                    throw new InvalidOperationException("Transform-gizmo axis-label mesh exceeded the 16-bit vertex limit.");
                }

                ushort baseVertex = (ushort)positions.Count;
                positions.Add(new float3((float)left, (float)top, 0f));
                positions.Add(new float3((float)right, (float)top, 0f));
                positions.Add(new float3((float)right, (float)bottom, 0f));
                positions.Add(new float3((float)left, (float)bottom, 0f));

                normals.Add(new float3(0f, 0f, 1f));
                normals.Add(new float3(0f, 0f, 1f));
                normals.Add(new float3(0f, 0f, 1f));
                normals.Add(new float3(0f, 0f, 1f));

                double uPadding = GlyphOutlinePaddingPixels / font.AtlasWidth;
                double vPadding = GlyphOutlinePaddingPixels / font.AtlasHeight;
                float u0 = (float)Math.Max(0.0, glyph.SourceRect.X - uPadding);
                float v0 = (float)Math.Max(0.0, glyph.SourceRect.Y - vPadding);
                float u1 = (float)Math.Min(1.0, glyph.SourceRect.X + glyph.SourceRect.Z + uPadding);
                float v1 = (float)Math.Min(1.0, glyph.SourceRect.Y + glyph.SourceRect.W + vPadding);
                texCoords.Add(new float2(u0, v0));
                texCoords.Add(new float2(u1, v0));
                texCoords.Add(new float2(u1, v1));
                texCoords.Add(new float2(u0, v1));

                AddDoubleSidedQuadIndices(indices, baseVertex);

                minX = Math.Min(minX, left);
                minY = Math.Min(minY, bottom);
                maxX = Math.Max(maxX, right);
                maxY = Math.Max(maxY, top);

                double advance = glyph.AdvanceWidth > 0f ? glyph.AdvanceWidth : glyphWidth;
                cursorX += advance;
                glyphCount++;
            }

            if (glyphCount == 0) {
                throw new InvalidOperationException("Transform-gizmo axis labels must include at least one renderable glyph.");
            }

            CenterPositions(positions, minX, minY, maxX, maxY);
            return new ModelAsset {
                Positions = positions.ToArray(),
                Normals = normals.ToArray(),
                TexCoords = texCoords.ToArray(),
                Indices16 = indices.ToArray()
            };
        }

        /// <summary>
        /// Recenters generated glyph vertices so the billboard origin stays at the middle of the label bounds.
        /// </summary>
        /// <param name="positions">Vertex positions to recenter in place.</param>
        /// <param name="minX">Minimum X bound across all glyph vertices.</param>
        /// <param name="minY">Minimum Y bound across all glyph vertices.</param>
        /// <param name="maxX">Maximum X bound across all glyph vertices.</param>
        /// <param name="maxY">Maximum Y bound across all glyph vertices.</param>
        static void CenterPositions(List<float3> positions, double minX, double minY, double maxX, double maxY) {
            if (positions == null) {
                throw new ArgumentNullException(nameof(positions));
            }

            double centerX = (minX + maxX) * 0.5;
            double centerY = (minY + maxY) * 0.5;
            for (int positionIndex = 0; positionIndex < positions.Count; positionIndex++) {
                float3 position = positions[positionIndex];
                positions[positionIndex] = new float3(
                    (float)(position.X - centerX),
                    (float)(position.Y - centerY),
                    position.Z);
            }
        }

        /// <summary>
        /// Appends both winding orders for one glyph quad so the billboard stays visible regardless of backend culling conventions.
        /// </summary>
        /// <param name="indices">Index list receiving the generated triangles.</param>
        /// <param name="baseVertex">First vertex index of the four-vertex glyph quad.</param>
        static void AddDoubleSidedQuadIndices(List<ushort> indices, ushort baseVertex) {
            if (indices == null) {
                throw new ArgumentNullException(nameof(indices));
            }

            indices.Add(baseVertex);
            indices.Add((ushort)(baseVertex + 3));
            indices.Add((ushort)(baseVertex + 2));
            indices.Add(baseVertex);
            indices.Add((ushort)(baseVertex + 2));
            indices.Add((ushort)(baseVertex + 1));

            indices.Add(baseVertex);
            indices.Add((ushort)(baseVertex + 2));
            indices.Add((ushort)(baseVertex + 3));
            indices.Add(baseVertex);
            indices.Add((ushort)(baseVertex + 1));
            indices.Add((ushort)(baseVertex + 2));
        }
    }
}
