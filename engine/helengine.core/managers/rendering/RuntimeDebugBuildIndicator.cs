#if HELENGINE_ENV_DEBUG
namespace helengine {
    /// <summary>Builds a cached final DEBUG marker from borrowed loaded font atlases, independently of scene entities and cameras.</summary>
    internal sealed class RuntimeDebugBuildIndicator : IDisposable {
        /// <summary>Invariant ASCII build label, independent of the process culture.</summary>
        const string Label = "DEBUG";
        /// <summary>Physical inset from the output's top and left edges.</summary>
        const int ScreenInset = 4;
        /// <summary>Physical black padding around the visible glyph ink.</summary>
        const int TextPadding = 2;
        /// <summary>Renderer whose lifetime owns this registry; fonts and textures remain borrowed.</summary>
        readonly RenderManager2D Renderer;
        /// <summary>Borrowed fonts in loading order, changed only when their lifetime or atlas changes.</summary>
        readonly List<FontAsset> Fonts;
        /// <summary>Single reusable command payload, allocated only after a usable atlas is available.</summary>
        [NativeOwnedMember] RenderCommandList2D Commands;
        /// <summary>Borrowed font selected from the current registration set.</summary>
        FontAsset SelectedFont;
        /// <summary>Whether a loading or unloading event requires selecting a usable font again.</summary>
        bool SelectionDirty = true;
        /// <summary>Whether cached coordinates must be recomputed after a font event.</summary>
        bool LayoutDirty = true;
        /// <summary>Physical width used by the current cached command layout.</summary>
        int Width;
        /// <summary>Physical height used by the current cached command layout.</summary>
        int Height;

        /// <summary>Creates a font registry without allocating any atlas, entity or command payload.</summary>
        public RuntimeDebugBuildIndicator([NativeRetainsBorrow] RenderManager2D renderer) {
            Renderer = renderer;
            Fonts = new List<FontAsset>();
        }

        /// <summary>Registers a borrowed font once; registration never transfers font or texture ownership.</summary>
        public void RegisterFont([NativeRetainsBorrow] FontAsset font) {
            if (!Fonts.Contains(font)) {
                Fonts.Add(font);
                Invalidate();
            }
        }

        /// <summary>Removes references before a font can be destroyed by its native owner.</summary>
        public void UnregisterFont([NativeNoEscape] FontAsset font) {
            if (Fonts.Remove(font)) {
                Invalidate();
            }
        }

        /// <summary>Clears borrowed command references and defers selection/layout until the next final pass.</summary>
        public void Invalidate() {
            SelectedFont = null;
            SelectionDirty = true;
            LayoutDirty = true;
            if (Commands != null) {
                Commands.Reset();
            }
        }

        /// <summary>Returns borrowed commands for a physical output, or null until a usable font and visible target exist.</summary>
        public RenderCommandList2D Build(int width, int height) {
            if (width <= ScreenInset * 2 + TextPadding * 2 || height <= ScreenInset * 2 + TextPadding * 2) {
                return null;
            }
            if (SelectionDirty) {
                SelectedFont = null;
                for (int index = 0; index < Fonts.Count; index++) {
                    if (CanRenderLabel(Fonts[index])) {
                        SelectedFont = Fonts[index];
                        break;
                    }
                }
                SelectionDirty = false;
            }
            if (SelectedFont == null) {
                return null;
            }
            if (!LayoutDirty && Width == width && Height == height) {
                return Commands;
            }
            return Rebuild(width, height);
        }

        /// <summary>Checks all five required glyphs before choosing a font so the marker cannot become a partial word.</summary>
        static bool CanRenderLabel([NativeNoEscape] FontAsset font) {
            if (font == null || font.IsDisposed || font.Texture == null || font.Texture.IsDisposed
                || font.Characters == null || !(font.LineHeight > 0f) || !IsFinite(font.LineHeight)
                || font.AtlasWidth <= 0 || font.AtlasHeight <= 0) {
                return false;
            }
            RealEvaluation pen = 0f;
            RealEvaluation minX = 0f;
            RealEvaluation minY = 0f;
            RealEvaluation maxX = 0f;
            RealEvaluation maxY = 0f;
            for (int index = 0; index < Label.Length; index++) {
                FontChar glyph;
                if (!font.Characters.TryGetValue(Label[index], out glyph)
                    || !(glyph.SourceRect.X >= 0f) || !(glyph.SourceRect.Y >= 0f)
                    || !(glyph.SourceRect.Z > 0f) || !(glyph.SourceRect.W > 0f)
                    || !(glyph.SourceRect.X + glyph.SourceRect.Z <= 1f) || !(glyph.SourceRect.Y + glyph.SourceRect.W <= 1f)
                    || !IsFinite(glyph.BearingX) || !IsFinite(glyph.OffsetY) || !IsFinite(glyph.AdvanceWidth)) {
                    return false;
                }
                RealEvaluation width = glyph.SourceRect.Z * font.AtlasWidth;
                RealEvaluation height = glyph.SourceRect.W * font.AtlasHeight;
                if (!IsFinite(pen + glyph.BearingX + width) || !IsFinite(glyph.OffsetY + height)) {
                    return false;
                }
                RealEvaluation x = pen + glyph.BearingX;
                RealEvaluation y = glyph.OffsetY;
                if (index == 0) {
                    minX = x;
                    minY = y;
                    maxX = x + width;
                    maxY = y + height;
                } else {
                    minX = RealMath.Min(minX, x);
                    minY = RealMath.Min(minY, y);
                    maxX = RealMath.Max(maxX, x + width);
                    maxY = RealMath.Max(maxY, y + height);
                }
                pen += glyph.AdvanceWidth > 0f ? glyph.AdvanceWidth : width;
                if (!IsFinite(pen)) {
                    return false;
                }
            }
            RealEvaluation inkWidth = maxX - minX;
            RealEvaluation inkHeight = maxY - minY;
            return inkWidth > 0f && inkHeight > 0f && IsFinite(inkWidth) && IsFinite(inkHeight);
        }

        /// <summary>Classifies metrics using the configured evaluation representation without widening them to double.</summary>
        static bool IsFinite(RealEvaluation value) {
            return (value >= 0f || value < 0f) && !RealMath.IsPositiveInfinity(RealMath.Abs(value));
        }

        /// <summary>Builds one opaque rectangle followed by five green glyphs using the configured numeric arithmetic.</summary>
        RenderCommandList2D Rebuild(int width, int height) {
            FontAsset font = SelectedFont;
            RealEvaluation pen = 0f;
            RealEvaluation minX = 0f;
            RealEvaluation minY = 0f;
            RealEvaluation maxX = 0f;
            RealEvaluation maxY = 0f;
            for (int index = 0; index < Label.Length; index++) {
                FontChar glyph = font.Characters[Label[index]];
                RealEvaluation glyphWidth = glyph.SourceRect.Z * font.AtlasWidth;
                RealEvaluation glyphHeight = glyph.SourceRect.W * font.AtlasHeight;
                RealEvaluation x = pen + glyph.BearingX;
                RealEvaluation y = glyph.OffsetY;
                if (index == 0) {
                    minX = x;
                    minY = y;
                    maxX = x + glyphWidth;
                    maxY = y + glyphHeight;
                } else {
                    minX = RealMath.Min(minX, x);
                    minY = RealMath.Min(minY, y);
                    maxX = RealMath.Max(maxX, x + glyphWidth);
                    maxY = RealMath.Max(maxY, y + glyphHeight);
                }
                pen += glyph.AdvanceWidth > 0f ? glyph.AdvanceWidth : glyphWidth;
            }
            RealEvaluation inkWidth = maxX - minX;
            RealEvaluation inkHeight = maxY - minY;
            // Preserve single-texel strokes in small atlases while retaining the physical marker cap.
            RealEvaluation targetInkHeight = RealMath.Min(24, RealMath.Max(inkHeight, RealMath.Max(8, height / 40)));
            // Font leading must not reduce the marker's physical visible height.
            RealEvaluation scale = targetInkHeight / inkHeight;
            scale = RealMath.Min(scale, (width - ScreenInset * 2 - TextPadding * 2) / inkWidth);
            scale = RealMath.Min(scale, (height - ScreenInset * 2 - TextPadding * 2) / inkHeight);
            if (!(scale > 0f)) {
                return null;
            }
            if (Commands == null) {
                NativeOwnership.DisposeAndRelease(ref Commands);
                Commands = new RenderCommandList2D(0);
            } else {
                Commands.Reset();
            }
            byte4 black = new byte4(0, 0, 0, 255);
            byte4 green = new byte4(0, 255, 0, 255);
            Commands.AddRoundedRect(new float4(ScreenInset, ScreenInset,
                RealMath.Ceiling(inkWidth * scale) + TextPadding * 2,
                RealMath.Ceiling(inkHeight * scale) + TextPadding * 2), 0f, 0f, RoundedRectCorners.All, black, black);
            pen = 0f;
            for (int index = 0; index < Label.Length; index++) {
                FontChar glyph = font.Characters[Label[index]];
                RealEvaluation glyphWidth = glyph.SourceRect.Z * font.AtlasWidth;
                RealEvaluation glyphHeight = glyph.SourceRect.W * font.AtlasHeight;
                Commands.AddGlyphQuad(font.Texture, new float4(
                    ScreenInset + TextPadding + (pen + glyph.BearingX - minX) * scale,
                    ScreenInset + TextPadding + (glyph.OffsetY - minY) * scale,
                    glyphWidth * scale, glyphHeight * scale), glyph.SourceRect, green);
                pen += glyph.AdvanceWidth > 0f ? glyph.AdvanceWidth : glyphWidth;
            }
            Width = width;
            Height = height;
            LayoutDirty = false;
            return Commands;
        }

        /// <summary>Releases only registry/command storage and detaches callbacks so fonts can outlive their renderer safely.</summary>
        public void Dispose() {
            for (int index = 0; index < Fonts.Count; index++) {
                Fonts[index].DetachRuntimeRenderer(Renderer);
            }
            Fonts.Clear();
            SelectedFont = null;
            NativeOwnership.Delete(Fonts);
            NativeOwnership.DisposeAndRelease(ref Commands);
        }
    }
}
#endif
