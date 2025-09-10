namespace helengine {
    public class NineSliceAtlas {
        public TextureAsset Texture { get; private set; }
        public float4[] FillUV { get; private set; } = new float4[9];
        public float4[] BorderUV { get; private set; } = new float4[9];
        public int CornerSize { get; private set; }
        public int EdgeThickness { get; private set; }
        public int Padding { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        private NineSliceAtlas() { }

        public static NineSliceAtlas Generate(int radiusPx, int borderPx, int aaPx = 1, int padding = 2) {
            int s = Math.Max(1, radiusPx);
            int e = 1;
            int pad = Math.Max(1, padding);

            // Reference render area
            int refW = 2 * s + e;
            int refH = 2 * s + e;

            // Tile sizes
            int tileW0 = s; // corners
            int tileW1 = e; // mid
            int tileH0 = s;
            int tileH1 = e;

            int atlasW = pad + tileW0 + pad + tileW1 + pad + tileW0 + pad;
            int atlasH = pad + tileH0 + pad + tileH1 + pad + tileH0 + pad +
                         pad + tileH0 + pad + tileH1 + pad + tileH0 + pad;

            byte[] rgba = new byte[atlasW * atlasH * 4];

            float[] refFill = RasterizeRoundedRectAlpha(refW, refH, s, aaPx);
            float[] refBorder = borderPx > 0 ? RasterizeRoundedRectBorderAlpha(refW, refH, s, borderPx, aaPx) : new float[refW * refH];

            // Source rectangles
            Rect srcTopLeft = new Rect(0, 0, s, s);
            Rect srcTop = new Rect(s, 0, e, s);
            Rect srcTopRight = new Rect(s + e, 0, s, s);
            Rect srcMidLeft = new Rect(0, s, s, e);
            Rect srcMid = new Rect(s, s, e, e);
            Rect srcMidRight = new Rect(s + e, s, s, e);
            Rect srcBotLeft = new Rect(0, s + e, s, s);
            Rect srcBot = new Rect(s, s + e, e, s);
            Rect srcBotRight = new Rect(s + e, s + e, s, s);

            Rect Dst(int row, int col, int w, int h) {
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
                return new Rect(x, y, w, h);
            }

            // Fill blits rows 0..2
            Blit(refFill, refW, refH, srcTopLeft, rgba, atlasW, atlasH, Dst(0, 0, tileW0, tileH0));
            Blit(refFill, refW, refH, srcTop, rgba, atlasW, atlasH, Dst(0, 1, tileW1, tileH0));
            Blit(refFill, refW, refH, srcTopRight, rgba, atlasW, atlasH, Dst(0, 2, tileW0, tileH0));
            Blit(refFill, refW, refH, srcMidLeft, rgba, atlasW, atlasH, Dst(1, 0, tileW0, tileH1));
            Blit(refFill, refW, refH, srcMid, rgba, atlasW, atlasH, Dst(1, 1, tileW1, tileH1));
            Blit(refFill, refW, refH, srcMidRight, rgba, atlasW, atlasH, Dst(1, 2, tileW0, tileH1));
            Blit(refFill, refW, refH, srcBotLeft, rgba, atlasW, atlasH, Dst(2, 0, tileW0, tileH0));
            Blit(refFill, refW, refH, srcBot, rgba, atlasW, atlasH, Dst(2, 1, tileW1, tileH0));
            Blit(refFill, refW, refH, srcBotRight, rgba, atlasW, atlasH, Dst(2, 2, tileW0, tileH0));

            // Border rows 3..5
            if (borderPx > 0) {
                Blit(refBorder, refW, refH, srcTopLeft, rgba, atlasW, atlasH, Dst(3, 0, tileW0, tileH0));
                Blit(refBorder, refW, refH, srcTop, rgba, atlasW, atlasH, Dst(3, 1, tileW1, tileH0));
                Blit(refBorder, refW, refH, srcTopRight, rgba, atlasW, atlasH, Dst(3, 2, tileW0, tileH0));
                Blit(refBorder, refW, refH, srcMidLeft, rgba, atlasW, atlasH, Dst(4, 0, tileW0, tileH1));
                Blit(refBorder, refW, refH, srcMid, rgba, atlasW, atlasH, Dst(4, 1, tileW1, tileH1));
                Blit(refBorder, refW, refH, srcMidRight, rgba, atlasW, atlasH, Dst(4, 2, tileW0, tileH1));
                Blit(refBorder, refW, refH, srcBotLeft, rgba, atlasW, atlasH, Dst(5, 0, tileW0, tileH0));
                Blit(refBorder, refW, refH, srcBot, rgba, atlasW, atlasH, Dst(5, 1, tileW1, tileH0));
                Blit(refBorder, refW, refH, srcBotRight, rgba, atlasW, atlasH, Dst(5, 2, tileW0, tileH0));
            }

            var tex = new TextureAsset {
                Colors = rgba,
                Width = (ushort)atlasW,
                Height = (ushort)atlasH
            };

            var atlas = new NineSliceAtlas();
            atlas.Texture = tex;
            atlas.CornerSize = s;
            atlas.EdgeThickness = e;
            atlas.Padding = pad;
            atlas.Width = atlasW;
            atlas.Height = atlasH;

            float AW = atlasW;
            float AH = atlasH;
            float x0(int col) => (col == 0 ? pad : (col == 1 ? pad + tileW0 + pad : pad + tileW0 + pad + tileW1 + pad));
            float y0(int row) {
                int y = pad;
                for (int r = 0; r < row; r++) {
                    int rh = (r % 3 == 0 || r % 3 == 2) ? tileH0 : tileH1;
                    y += rh + pad;
                }
                return y;
            }
            int tw(int col) => (col == 1 ? tileW1 : tileW0);
            int th(int row) => ((row % 3 == 0 || row % 3 == 2) ? tileH0 : tileH1);

            for (int r = 0; r < 3; r++) {
                for (int c = 0; c < 3; c++) {
                    float ux = x0(c) / AW;
                    float uy = y0(r) / AH;
                    float uw = tw(c) / AW;
                    float uh = th(r) / AH;
                    atlas.FillUV[r * 3 + c] = new float4(ux, uy, uw, uh);
                }
            }
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
        }

        private struct Rect { public int X,Y,W,H; public Rect(int x,int y,int w,int h){X=x;Y=y;W=w;H=h;} }

        private static void Blit(float[] src, int srcW, int srcH, Rect s, byte[] dst, int dstW, int dstH, Rect d) {
            for (int y = 0; y < s.H; y++) {
                for (int x = 0; x < s.W; x++) {
                    int sx = s.X + x; int sy = s.Y + y;
                    float a = src[sy * srcW + sx];
                    if (a <= 0) continue;
                    int dx = d.X + x; int dy = d.Y + y;
                    int di = (dy * dstW + dx) * 4;
                    byte A = (byte)Math.Clamp((int)(a * 255), 0, 255);
                    dst[di+0] = 255;
                    dst[di+1] = 255;
                    dst[di+2] = 255;
                    dst[di+3] = A;
                }
            }
        }

        private static float[] RasterizeRoundedRectAlpha(int w, int h, int radius, int aa) {
            float[] a = new float[w * h];
            float cx = w * 0.5f, cy = h * 0.5f;
            float halfX = w * 0.5f, halfY = h * 0.5f;
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
            float[] outer = RasterizeRoundedRectAlpha(w, h, radius, aa);
            int ir = Math.Max(0, radius - borderPx);
            float[] inner = RasterizeRoundedRectAlpha(w, h, ir, aa);
            float[] a = new float[w * h];
            for (int i = 0; i < a.Length; i++) {
                float v = outer[i] - inner[i];
                if (v < 0) v = 0;
                if (v > 1) v = 1;
                a[i] = v;
            }
            return a;
        }

        private static float SmoothStep(float edge0, float edge1, float x) {
            float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}

