using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace helengine.sharpdx {
    internal class UINineSliceAtlas {
        public SharpDXTextureRuntimeData Texture;
        public float4[] FillUV = new float4[9];
        public float4[] BorderUV = new float4[9];
        public int CornerSize; // pixels (square)
        public int EdgeThickness; // pixels used for center edges (usually 1)
        public int Padding; // pixels between tiles
        public int Width;
        public int Height;

        public static UINineSliceAtlas Generate(int radiusPx, int borderPx, int aaPx = 1, int padding = 2) {
            int s = Math.Max(1, radiusPx);
            int e = 1; // edge sample thickness
            int pad = Math.Max(1, padding);

            // Reference full rect size to rasterize: (2*s + e) x (2*s + e)
            int refW = 2 * s + e;
            int refH = 2 * s + e;

            // Layout: 6 rows (3 fill, 3 border) x 3 columns (left, mid, right)
            int tileW0 = s;  // left/right corner width
            int tileW1 = e;  // mid width
            int tileH0 = s;
            int tileH1 = e;

            int atlasW = pad + tileW0 + pad + tileW1 + pad + tileW0 + pad;
            int atlasH = pad + tileH0 + pad + tileH1 + pad + tileH0 + pad + // fill rows
                         pad + tileH0 + pad + tileH1 + pad + tileH0 + pad;  // border rows

            var bmp = new Bitmap(atlasW, atlasH, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp)) {
                g.Clear(Color.Transparent);
            }

            // Render reference masks once into an offscreen bitmap
            float[] refFill = RasterizeRoundedRectAlpha(refW, refH, s, 0, aaPx);
            float[] refBorder = borderPx > 0 ? RasterizeRoundedRectBorderAlpha(refW, refH, s, borderPx, aaPx) : new float[refW * refH];

            // Helper to blit a subrect from reference into atlas at dst
            void blitFill(Rectangle src, Rectangle dst) {
                for (int y = 0; y < src.Height; y++) {
                    for (int x = 0; x < src.Width; x++) {
                        int sx = src.X + x;
                        int sy = src.Y + y;
                        float a = refFill[sy * refW + sx];
                        if (a <= 0) continue;
                        int dx = dst.X + x;
                        int dy = dst.Y + y;
                        var c = Color.FromArgb((int)(a * 255), 255, 255, 255);
                        bmp.SetPixel(dx, dy, c);
                    }
                }
            }
            void blitBorder(Rectangle src, Rectangle dst) {
                for (int y = 0; y < src.Height; y++) {
                    for (int x = 0; x < src.Width; x++) {
                        int sx = src.X + x;
                        int sy = src.Y + y;
                        float a = refBorder[sy * refW + sx];
                        if (a <= 0) continue;
                        int dx = dst.X + x;
                        int dy = dst.Y + y;
                        var c = Color.FromArgb((int)(a * 255), 255, 255, 255);
                        bmp.SetPixel(dx, dy, c);
                    }
                }
            }

            // Source rectangles within reference
            var srcTopLeft = new Rectangle(0, 0, s, s);
            var srcTop = new Rectangle(s, 0, e, s);
            var srcTopRight = new Rectangle(s + e, 0, s, s);
            var srcMidLeft = new Rectangle(0, s, s, e);
            var srcMid = new Rectangle(s, s, e, e);
            var srcMidRight = new Rectangle(s + e, s, s, e);
            var srcBotLeft = new Rectangle(0, s + e, s, s);
            var srcBot = new Rectangle(s, s + e, e, s);
            var srcBotRight = new Rectangle(s + e, s + e, s, s);

            // Destination positions helper (row, col) with offsets
            Rectangle Dst(int row, int col, int w, int h) {
                int y = pad;
                for (int r = 0; r < row; r++) {
                    int rh = (r % 3 == 0 || r % 3 == 2) ? tileH0 : tileH1;
                    y += rh + pad;
                }
                int x = pad;
                for (int c = 0; c < col; c++) {
                    int cw = (c == 1) ? tileW1 : tileW0;
                    x += cw + pad;
                }
                return new Rectangle(x, y, w, h);
            }

            // Blit fill tiles (rows 0..2)
            blitFill(srcTopLeft, Dst(0, 0, tileW0, tileH0));
            blitFill(srcTop, Dst(0, 1, tileW1, tileH0));
            blitFill(srcTopRight, Dst(0, 2, tileW0, tileH0));
            blitFill(srcMidLeft, Dst(1, 0, tileW0, tileH1));
            blitFill(srcMid, Dst(1, 1, tileW1, tileH1));
            blitFill(srcMidRight, Dst(1, 2, tileW0, tileH1));
            blitFill(srcBotLeft, Dst(2, 0, tileW0, tileH0));
            blitFill(srcBot, Dst(2, 1, tileW1, tileH0));
            blitFill(srcBotRight, Dst(2, 2, tileW0, tileH0));

            // Blit border tiles (rows 3..5)
            if (borderPx > 0) {
                blitBorder(srcTopLeft, Dst(3, 0, tileW0, tileH0));
                blitBorder(srcTop, Dst(3, 1, tileW1, tileH0));
                blitBorder(srcTopRight, Dst(3, 2, tileW0, tileH0));
                blitBorder(srcMidLeft, Dst(4, 0, tileW0, tileH1));
                blitBorder(srcMid, Dst(4, 1, tileW1, tileH1));
                blitBorder(srcMidRight, Dst(4, 2, tileW0, tileH1));
                blitBorder(srcBotLeft, Dst(5, 0, tileW0, tileH0));
                blitBorder(srcBot, Dst(5, 1, tileW1, tileH0));
                blitBorder(srcBotRight, Dst(5, 2, tileW0, tileH0));
            }

            // Extract raw colors in RGBA
            var rectAll = new Rectangle(0, 0, atlasW, atlasH);
            var data = bmp.LockBits(rectAll, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try {
                int bytes = Math.Abs(data.Stride) * atlasH;
                byte[] argb = new byte[bytes];
                Marshal.Copy(data.Scan0, argb, 0, bytes);

                byte[] rgba = new byte[bytes];
                for (int y = 0; y < atlasH; y++) {
                    int srcRow = y * data.Stride;
                    int dstRow = y * data.Stride;
                    for (int x = 0; x < atlasW; x++) {
                        int si = srcRow + x * 4;
                        int di = dstRow + x * 4;
                        byte A = argb[si + 3];
                        byte R = argb[si + 2];
                        byte G = argb[si + 1];
                        byte B = argb[si + 0];
                        rgba[di + 0] = R;
                        rgba[di + 1] = G;
                        rgba[di + 2] = B;
                        rgba[di + 3] = A;
                    }
                }

                var texAsset = new helengine.TextureAsset {
                    Colors = rgba,
                    Width = (ushort)atlasW,
                    Height = (ushort)atlasH
                };

                var runtimeTex = (SharpDXTextureRuntimeData)helengine.Core.Instance.RenderManager.BuildTextureFromRaw(texAsset);
                // Build UVs (normalized)
                float AW = atlasW;
                float AH = atlasH;
                float x0(int col) => (col == 0 ? pad : (col == 1 ? pad + tileW0 + pad : pad + tileW0 + pad + tileW1 + pad));
                float y0(int row) {
                    int yOff = pad;
                    for (int r = 0; r < row; r++) {
                        int rh = (r % 3 == 0 || r % 3 == 2) ? tileH0 : tileH1;
                        yOff += rh + pad;
                    }
                    return yOff;
                }
                int tw(int col) => (col == 1 ? tileW1 : tileW0);
                int th(int row) => ((row % 3 == 0 || row % 3 == 2) ? tileH0 : tileH1);

                var atlas = new UINineSliceAtlas {
                    Texture = runtimeTex,
                    CornerSize = s,
                    EdgeThickness = e,
                    Padding = pad,
                    Width = atlasW,
                    Height = atlasH
                };

                // Fill UVs rows 0..2
                for (int r = 0; r < 3; r++) {
                    for (int c = 0; c < 3; c++) {
                        float ux = x0(c) / AW;
                        float uy = y0(r) / AH;
                        float uw = tw(c) / AW;
                        float uh = th(r) / AH;
                        atlas.FillUV[r * 3 + c] = new float4(ux, uy, uw, uh);
                    }
                }
                // Border UVs rows 3..5
                for (int r = 0; r < 3; r++) {
                    for (int c = 0; c < 3; c++) {
                        float ux = x0(c) / AW;
                        float uy = y0(r + 3) / AH;
                        float uw = tw(c) / AW;
                        float uh = th(r) / AH;
                        atlas.BorderUV[r * 3 + c] = new float4(ux, uy, uw, uh);
                    }
                }

                return atlas;
            } finally {
                bmp.UnlockBits(data);
            }
        }

        private static float[] RasterizeRoundedRectAlpha(int w, int h, int radius, int border, int aa) {
            float[] a = new float[w * h];
            float cx = w * 0.5f; float cy = h * 0.5f;
            float halfX = w * 0.5f; float halfY = h * 0.5f;
            float r = radius;
            for (int y = 0; y < h; y++) {
                for (int x = 0; x < w; x++) {
                    float px = (x + 0.5f - cx);
                    float py = (y + 0.5f - cy);
                    float qx = MathF.Abs(px) - (halfX - r);
                    float qy = MathF.Abs(py) - (halfY - r);
                    float qxm = MathF.Max(qx, 0);
                    float qym = MathF.Max(qy, 0);
                    float dist = MathF.Sqrt(qxm * qxm + qym * qym) + MathF.Min(MathF.Max(qx, qy), 0.0f) - r;
                    float alpha = 1.0f - SmoothStep(-aa, aa, dist);
                    if (alpha < 0) alpha = 0;
                    if (alpha > 1) alpha = 1;
                    a[y * w + x] = alpha;
                }
            }
            return a;
        }

        private static float[] RasterizeRoundedRectBorderAlpha(int w, int h, int radius, int borderPx, int aa) {
            float[] outer = RasterizeRoundedRectAlpha(w, h, radius, 0, aa);
            int ir = Math.Max(0, radius - borderPx);
            float[] inner = RasterizeRoundedRectAlpha(w, h, ir, 0, aa);
            float[] a = new float[w * h];
            for (int i = 0; i < a.Length; i++) {
                float v = outer[i] - inner[i];
                if (v < 0) v = 0;
                if (v > 1) v = 1;
                a[i] = v;
            }
            return a;
        }

        // Helpers for math and smoothstep
        private static float SmoothStep(float edge0, float edge1, float x) {
            float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}
