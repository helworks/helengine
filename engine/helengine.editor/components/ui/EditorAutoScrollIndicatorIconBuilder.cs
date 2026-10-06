namespace helengine.editor {
    /// <summary>Rasterizes the approved chevrons and center dot into one antialiased atlas in memory.</summary>
    internal static class EditorAutoScrollIndicatorIconBuilder {
        /// <summary>Odd marker size that keeps its center on a single pixel column and row.</summary>
        internal const int Size = 33;
        /// <summary>Atlas slot containing the upward chevron.</summary>
        internal const int Up = 0;
        /// <summary>Atlas slot containing the downward chevron.</summary>
        internal const int Down = 1;
        /// <summary>Atlas slot containing the leftward chevron.</summary>
        internal const int Left = 2;
        /// <summary>Atlas slot containing the rightward chevron.</summary>
        internal const int Right = 3;
        /// <summary>Atlas slot containing the center dot.</summary>
        internal const int Dot = 4;
        /// <summary>Number of independently tintable glyphs in the atlas.</summary>
        const int TileCount = 5;
        /// <summary>Subpixel samples per axis used to smooth diagonal edges.</summary>
        const int Samples = 8;

        /// <summary>Builds white glyph masks whose alpha coverage can be tinted independently by the overlay.</summary>
        /// <returns>RGBA atlas owned by the caller until it has been uploaded to the renderer.</returns>
        internal static TextureAsset CreateAtlas() {
            ushort width = Size * TileCount;
            byte[] pixels = new byte[width * Size * 4];
            for (int tile = 0; tile < TileCount; tile++) {
                for (int y = 0; y < Size; y++) {
                    for (int x = 0; x < Size; x++) {
                        int coverage = 0;
                        for (int sampleY = 0; sampleY < Samples; sampleY++) {
                            for (int sampleX = 0; sampleX < Samples; sampleX++) {
                                double localX = x + (sampleX + 0.5d) / Samples;
                                double localY = y + (sampleY + 0.5d) / Samples;
                                if (ContainsGlyph(tile, localX, localY)) {
                                    coverage++;
                                }
                            }
                        }

                        int offset = (y * width + tile * Size + x) * 4;
                        pixels[offset] = 255;
                        pixels[offset + 1] = 255;
                        pixels[offset + 2] = 255;
                        pixels[offset + 3] = (byte)((coverage * 255 + Samples * Samples / 2) / (Samples * Samples));
                    }
                }
            }
            return new TextureAsset { Width = width, Height = Size, Colors = pixels };
        }

        /// <summary>Resolves normalized texture coordinates for one glyph without rotating render geometry.</summary>
        /// <param name="tile">Direction or dot slot in the atlas.</param>
        /// <returns>Source rectangle for a sprite covering the complete marker area.</returns>
        internal static float4 GetSourceRect(int tile) {
            return new float4(tile / (float)TileCount, 0f, 1f / TileCount, 1f);
        }

        /// <summary>Reflects the approved upward glyph into the requested direction, or samples the centered circular dot.</summary>
        /// <param name="tile">Glyph whose mask is being sampled.</param>
        /// <param name="x">Horizontal subpixel coordinate.</param>
        /// <param name="y">Vertical subpixel coordinate.</param>
        /// <returns>Whether the subpixel is inside the glyph.</returns>
        static bool ContainsGlyph(int tile, double x, double y) {
            if (tile == Up) {
                return ContainsUpChevron(x, y);
            } else if (tile == Down) {
                return ContainsUpChevron(x, Size - y);
            } else if (tile == Left) {
                return ContainsUpChevron(y, x);
            } else if (tile == Right) {
                return ContainsUpChevron(y, Size - x);
            }
            double center = Size / 2d;
            double deltaX = x - center;
            double deltaY = y - center;
            return deltaX * deltaX + deltaY * deltaY <= 2.25d;
        }

        /// <summary>Samples the wide, hollow chevron with beveled ends used in the approved 33-pixel preview.</summary>
        /// <param name="x">Horizontal subpixel coordinate.</param>
        /// <param name="y">Vertical subpixel coordinate.</param>
        /// <returns>Whether the sample lies between the chevron's outer and inner edges.</returns>
        static bool ContainsUpChevron(double x, double y) {
            double distance = Math.Abs(x - Size / 2d);
            if (distance > 10d) {
                return false;
            }
            double upperEdge = 3.5d + 0.8d * distance;
            double lowerEdge = distance <= 8d ? 7.5d + 0.8125d * distance : 24d - 1.25d * distance;
            return y >= upperEdge && y <= lowerEdge;
        }
    }
}
