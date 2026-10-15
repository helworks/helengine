using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace helengine.player.tests {
    /// <summary>
    /// Renders a real DirectX11 frame through a fixed-render-size window with a non-square display pixel aspect and checks
    /// every presented pixel: rows are repeated by the integer multiple and columns are stretched by the pixel aspect.
    /// An integer column stretch keeps hard point-sampled edges; a non-integer one is sharp-bilinear, blending only the
    /// two columns on either side of a column boundary by their coverage. The core keeps seeing the render size, the
    /// window reports its pixel aspect, and the camera projects with the displayed aspect, which is the client's width
    /// over its height.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class DirectX11FixedRenderSizePixelAspectPresentTests {
        /// <summary>Sprite left edge in render pixels.</summary>
        const int SpriteX = 2;

        /// <summary>Sprite top edge in render pixels.</summary>
        const int SpriteY = 4;

        /// <summary>Sprite width in render pixels.</summary>
        const int SpriteWidth = 3;

        /// <summary>Sprite height in render pixels.</summary>
        const int SpriteHeight = 2;

        /// <summary>Opaque blue camera clear, in the back buffer's BGRA byte order.</summary>
        static readonly byte4 ClearPixel = new byte4(255, 0, 0, 255);

        /// <summary>Opaque red sprite, in the back buffer's BGRA byte order.</summary>
        static readonly byte4 SpritePixel = new byte4(0, 0, 255, 255);

        /// <summary>
        /// A 32x12 render at pixel aspect 0.5 is shown in a 32x24 client (2x, a 4:3 display): columns keep their width and
        /// rows double. An 8x10 render at pixel aspect 2 is shown in a 16x10 client (1x): columns double and rows keep
        /// their height. (An 8x12 render at pixel aspect 2 would also display 4:3, but a target whose height is 1.5 times
        /// its width is read as two stacked dual screens by <see cref="CameraViewportResolver"/>, so it is not used here.)
        /// </summary>
        /// <param name="renderWidth">Fixed render width.</param>
        /// <param name="renderHeight">Fixed render height.</param>
        /// <param name="pixelAspect">Display pixel aspect of the rendered pixels.</param>
        /// <param name="clientWidth">Window client width.</param>
        /// <param name="clientHeight">Window client height.</param>
        [Theory]
        [InlineData(32, 12, 0.5d, 32, 24)]
        [InlineData(8, 10, 2d, 16, 10)]
        public void FixedRenderSizeWindow_StretchesColumnsByThePixelAspect(int renderWidth, int renderHeight, double pixelAspect, int clientWidth, int clientHeight) {
            StaTestThread.Run(() => RenderAndAssert(renderWidth, renderHeight, pixelAspect, clientWidth, clientHeight));
        }

        /// <summary>
        /// The real PS2 case: columns are stretched by a non-integer factor. A 640x448 framebuffer shown 4:3 is 1195 pixels
        /// wide at 2x (1.867 client pixels per column), and a 640x512 framebuffer 683 pixels wide at 1x (1.067 per column).
        /// The 320- and 256-wide modes are shown 3:2 and 22:15 by gsKit's integer magnification: 320x224 is 336 pixels wide at
        /// 1x (1.05 per column) and 256x224 is 329 pixels wide at 1x (1.285 per column). The window is magnified
        /// sharp-bilinear, like a TV that is continuous across a line and discrete between lines: every client pixel is the
        /// area-coverage blend of at most two adjacent rendered columns, so every rendered column covers the same r = client
        /// width / render width client pixels and none is lost, while every client row is an exact copy of one rendered row.
        /// </summary>
        /// <param name="renderWidth">Fixed render width.</param>
        /// <param name="renderHeight">Fixed render height.</param>
        /// <param name="displayAspectWidth">Width term of the aspect the whole framebuffer is shown at.</param>
        /// <param name="displayAspectHeight">Height term of the aspect the whole framebuffer is shown at.</param>
        /// <param name="clientWidth">Window client width: round(k * renderHeight * displayAspectWidth / displayAspectHeight).</param>
        /// <param name="clientHeight">Window client height: renderHeight * k.</param>
        [Theory]
        [InlineData(640, 448, 4, 3, 1195, 896)]
        [InlineData(640, 512, 4, 3, 683, 512)]
        [InlineData(320, 224, 3, 2, 336, 224)]
        [InlineData(256, 224, 22, 15, 329, 224)]
        public void FixedRenderSizeWindow_WithANonIntegerColumnStretch_GivesEveryRenderedColumnTheSameCoverage(int renderWidth, int renderHeight, int displayAspectWidth, int displayAspectHeight, int clientWidth, int clientHeight) {
            double pixelAspect = (double)displayAspectWidth * renderHeight / ((double)displayAspectHeight * renderWidth);

            StaTestThread.Run(() => AssertColumnCoverage(RenderPattern(renderWidth, renderHeight, pixelAspect, clientWidth, clientHeight), renderWidth, renderHeight));
        }

        /// <summary>
        /// When the client width is an integer multiple of the render width the magnification stays pure point sampling,
        /// byte-identical to the original point-magnify pass: every client pixel is exactly the rendered pixel it lies in.
        /// Covers square pixels at 3x and 2x (a 640x448 frame at 1280x896) and the two non-square integer stretches
        /// (32x12 at pixel aspect 0.5 and 2x, 8x10 at pixel aspect 2 and 1x).
        /// </summary>
        /// <param name="renderWidth">Fixed render width.</param>
        /// <param name="renderHeight">Fixed render height.</param>
        /// <param name="pixelAspect">Display pixel aspect of the rendered pixels.</param>
        /// <param name="clientWidth">Window client width, an integer multiple of the render width.</param>
        /// <param name="clientHeight">Window client height, an integer multiple of the render height.</param>
        [Theory]
        [InlineData(16, 12, 1d, 48, 36)]
        [InlineData(640, 448, 1d, 1280, 896)]
        [InlineData(32, 12, 0.5d, 32, 24)]
        [InlineData(8, 10, 2d, 16, 10)]
        public void FixedRenderSizeWindow_WithAnIntegerColumnStretch_CopiesEveryRenderedPixelExactly(int renderWidth, int renderHeight, double pixelAspect, int clientWidth, int clientHeight) {
            StaTestThread.Run(() => AssertExactCopy(RenderPattern(renderWidth, renderHeight, pixelAspect, clientWidth, clientHeight), renderWidth, renderHeight));
        }

        /// <summary>
        /// Renders two frames of a full-frame sprite showing the column pattern into an off-desktop window added at a fixed
        /// render size, and returns the second presented frame. In the pattern every rendered pixel lights exactly one of
        /// red, green and blue at full intensity: channel (column + row) mod 3. Adjacent columns therefore always differ, any
        /// three adjacent columns light all three channels, and adjacent rows differ, so blends across rows or across more
        /// than two columns are visible in the presented bytes.
        /// </summary>
        /// <param name="renderWidth">Fixed render width.</param>
        /// <param name="renderHeight">Fixed render height.</param>
        /// <param name="pixelAspect">Display pixel aspect of the rendered pixels.</param>
        /// <param name="clientWidth">Window client width.</param>
        /// <param name="clientHeight">Window client height.</param>
        /// <returns>The presented client-sized frame, indexed [row, column].</returns>
        static byte4[,] RenderPattern(int renderWidth, int renderHeight, double pixelAspect, int clientWidth, int clientHeight) {
            using CapturingDirectX11Renderer3D renderer = new CapturingDirectX11Renderer3D();
            using Form window = new Form {
                FormBorderStyle = FormBorderStyle.None, ClientSize = new Size(clientWidth, clientHeight),
                ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000)
            };
            window.Show();
            Application.DoEvents();
            renderer.AddWindowWithFixedRenderSize(window.Handle, clientWidth, clientHeight, renderWidth, renderHeight, pixelAspect);

            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(renderer, renderer.Render2D, null, new PlatformInfo("windows", "test"));
            Entity cameraEntity = CreateEntity(core);
            cameraEntity.AddComponent(new CameraComponent {
                LayerMask = 1,
                ClearSettings = new CameraClearSettings(true, new float4(0, 0, 1, 1), true, 1, false, 0)
            });
            byte[] colors = new byte[renderWidth * renderHeight * 4];
            for (int row = 0; row < renderHeight; row++) {
                for (int column = 0; column < renderWidth; column++) {
                    int offset = (row * renderWidth + column) * 4;
                    colors[offset + PatternChannel(column, row)] = 255;
                    colors[offset + 3] = 255;
                }
            }
            RuntimeTexture pattern = renderer.Render2D.BuildTextureFromRaw(new TextureAsset {
                Width = (ushort)renderWidth, Height = (ushort)renderHeight, Colors = colors
            });
            Entity spriteEntity = CreateEntity(core);
            spriteEntity.AddComponent(new SpriteComponent {
                Texture = pattern, Size = new int2(renderWidth, renderHeight)
            });

            renderer.Draw();
            renderer.Draw();

            byte4[,] pixels = renderer.LastPresentedPixels;
            Assert.Equal(clientHeight, pixels.GetLength(0));
            Assert.Equal(clientWidth, pixels.GetLength(1));
            return pixels;
        }

        /// <summary>
        /// Checks a sharp-bilinear frame of the column pattern. Vertically: every client row is a byte-exact copy of the
        /// first client row of its rendered row, so no rows are blended. Horizontally, walking each rendered row's first
        /// copy left to right: every client pixel holds at most two adjacent rendered columns, its red, green and blue
        /// summing to full intensity; each column's share is its coverage of the pixel (within one 8-bit step); the walk
        /// reaches every rendered column, in order; and every column's total coverage is r = client width / render width
        /// client pixels (within 3/255), so no two columns differ by as much as one client pixel.
        /// </summary>
        /// <param name="pixels">Presented frame, indexed [row, column].</param>
        /// <param name="renderWidth">Fixed render width.</param>
        /// <param name="renderHeight">Fixed render height.</param>
        static void AssertColumnCoverage(byte4[,] pixels, int renderWidth, int renderHeight) {
            int clientHeight = pixels.GetLength(0);
            int clientWidth = pixels.GetLength(1);
            int multiplier = clientHeight / renderHeight;
            double stretch = (double)clientWidth / renderWidth;
            for (int y = 0; y < clientHeight; y++) {
                int firstCopy = y / multiplier * multiplier;
                for (int x = 0; x < clientWidth; x++) {
                    if (Pack(pixels[y, x]) != Pack(pixels[firstCopy, x])) {
                        Assert.Fail($"Row {y} column {x} is {pixels[y, x]} but row {firstCopy}, the first copy of rendered row {y / multiplier}, is {pixels[firstCopy, x]}; every client row must be an exact copy of one rendered row.");
                    }
                }
            }

            for (int row = 0; row < renderHeight; row++) {
                int y = row * multiplier;
                double[] coverage = new double[renderWidth];
                int column = 0;
                for (int x = 0; x < clientWidth; x++) {
                    byte4 pixel = pixels[y, x];
                    double current = ChannelValue(pixel, PatternChannel(column, row)) / 255d;
                    double next = ChannelValue(pixel, PatternChannel(column + 1, row)) / 255d;
                    double third = ChannelValue(pixel, PatternChannel(column + 2, row)) / 255d;
                    if (pixel.W != 255 || third != 0d || Math.Abs(current + next - 1d) > 2d / 255d) {
                        Assert.Fail($"Row {y} column {x} is {pixel}: it must blend only rendered columns {column} and {column + 1} of rendered row {row} at full intensity.");
                    } else if (next > 0d && column + 1 >= renderWidth) {
                        Assert.Fail($"Row {y} column {x} is {pixel}: it reaches past the last rendered column {renderWidth - 1}.");
                    }

                    double expectedCurrent = CoverageOf(column, x, stretch);
                    double expectedNext = CoverageOf(column + 1, x, stretch);
                    if (Math.Abs(current - expectedCurrent) > 1d / 255d || Math.Abs(next - expectedNext) > 1d / 255d) {
                        Assert.Fail($"Row {y} column {x} is {pixel}: rendered columns {column} and {column + 1} hold {current:0.###} and {next:0.###} of it, but they cover {expectedCurrent:0.###} and {expectedNext:0.###}.");
                    }

                    coverage[column] += current;
                    if (next > 0d) {
                        coverage[column + 1] += next;
                        column++;
                    }
                }

                Assert.True(column == renderWidth - 1, $"Row {y} reaches rendered column {column}; expected every column up to {renderWidth - 1}.");
                double narrowest = double.MaxValue;
                double widest = 0d;
                for (int c = 0; c < renderWidth; c++) {
                    if (coverage[c] <= 0d) {
                        Assert.Fail($"Row {y}: rendered column {c} does not contribute to any client pixel.");
                    } else if (Math.Abs(coverage[c] - stretch) > 3d / 255d) {
                        Assert.Fail($"Row {y}: rendered column {c} covers {coverage[c]:0.###} client pixels; every column must cover {stretch:0.###}.");
                    }

                    narrowest = Math.Min(narrowest, coverage[c]);
                    widest = Math.Max(widest, coverage[c]);
                }

                Assert.True(widest - narrowest < 1d, $"Row {y}: column coverage ranges from {narrowest:0.###} to {widest:0.###} client pixels, not uniform within one pixel.");
            }
        }

        /// <summary>
        /// Checks a point-sampled frame of the column pattern: every client pixel equals, byte for byte, the rendered pixel
        /// whose block it lies in (column x * render width / client width, row y * render height / client height).
        /// </summary>
        /// <param name="pixels">Presented frame, indexed [row, column].</param>
        /// <param name="renderWidth">Fixed render width.</param>
        /// <param name="renderHeight">Fixed render height.</param>
        static void AssertExactCopy(byte4[,] pixels, int renderWidth, int renderHeight) {
            int clientHeight = pixels.GetLength(0);
            int clientWidth = pixels.GetLength(1);
            List<string> mismatches = new List<string>();
            int mismatchCount = 0;
            for (int y = 0; y < clientHeight; y++) {
                for (int x = 0; x < clientWidth; x++) {
                    int column = x * renderWidth / clientWidth;
                    int row = y * renderHeight / clientHeight;
                    byte4 pixel = pixels[y, x];
                    int channel = PatternChannel(column, row);
                    bool exact = pixel.W == 255;
                    for (int c = 0; c < 3; c++) {
                        exact &= ChannelValue(pixel, c) == (c == channel ? 255 : 0);
                    }
                    if (!exact) {
                        mismatchCount++;
                        if (mismatches.Count < 8) {
                            mismatches.Add($"({x},{y}) is {pixel}, not rendered pixel ({column},{row})");
                        }
                    }
                }
            }

            Assert.True(mismatchCount == 0, $"{mismatchCount} of {clientWidth * clientHeight} presented pixels are not exact copies: " + string.Join("; ", mismatches));
        }

        /// <summary>
        /// Resolves the share of client pixel <paramref name="x"/> covered by rendered column <paramref name="column"/>
        /// when columns are stretched by <paramref name="stretch"/>: the overlap of the pixel's span in rendered columns,
        /// [x / stretch, (x + 1) / stretch), with [column, column + 1), divided by the span's width.
        /// </summary>
        /// <param name="column">Rendered column.</param>
        /// <param name="x">Client column.</param>
        /// <param name="stretch">Client pixels per rendered column.</param>
        /// <returns>The covered share, from 0 to 1.</returns>
        static double CoverageOf(int column, int x, double stretch) {
            double overlap = Math.Min((x + 1) / stretch, column + 1d) - Math.Max(x / stretch, column);
            return Math.Max(overlap, 0d) * stretch;
        }

        /// <summary>
        /// Resolves which colour channel the column pattern lights at one rendered pixel: 0 red, 1 green, 2 blue.
        /// </summary>
        /// <param name="column">Rendered column.</param>
        /// <param name="row">Rendered row.</param>
        /// <returns>(column + row) mod 3.</returns>
        static int PatternChannel(int column, int row) {
            return (column + row) % 3;
        }

        /// <summary>
        /// Reads one colour channel of a presented pixel, whose bytes are in the back buffer's BGRA order.
        /// </summary>
        /// <param name="pixel">Presented pixel.</param>
        /// <param name="channel">0 red, 1 green, 2 blue.</param>
        /// <returns>The channel's byte.</returns>
        static int ChannelValue(byte4 pixel, int channel) {
            if (channel == 0) {
                return pixel.Z;
            } else if (channel == 1) {
                return pixel.Y;
            }

            return pixel.X;
        }

        /// <summary>Packs a presented pixel's four bytes into one integer so rows can be compared cheaply.</summary>
        /// <param name="pixel">Presented pixel.</param>
        /// <returns>The pixel's bytes as one integer.</returns>
        static int Pack(byte4 pixel) {
            return pixel.X | pixel.Y << 8 | pixel.Z << 16 | pixel.W << 24;
        }

        /// <summary>
        /// Renders two frames of a cleared view with one sprite into an off-desktop window added at a fixed render size
        /// and checks the core sizes, the window pixel aspect, the projection aspect and every presented pixel.
        /// </summary>
        /// <param name="renderWidth">Fixed render width.</param>
        /// <param name="renderHeight">Fixed render height.</param>
        /// <param name="pixelAspect">Display pixel aspect of the rendered pixels.</param>
        /// <param name="clientWidth">Window client width.</param>
        /// <param name="clientHeight">Window client height.</param>
        static void RenderAndAssert(int renderWidth, int renderHeight, double pixelAspect, int clientWidth, int clientHeight) {
            using CapturingDirectX11Renderer3D renderer = new CapturingDirectX11Renderer3D();
            using Form window = new Form {
                FormBorderStyle = FormBorderStyle.None, ClientSize = new Size(clientWidth, clientHeight),
                ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000)
            };
            window.Show();
            Application.DoEvents();
            renderer.AddWindowWithFixedRenderSize(window.Handle, clientWidth, clientHeight, renderWidth, renderHeight, pixelAspect);

            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(renderer, renderer.Render2D, null, new PlatformInfo("windows", "test"));
            Entity cameraEntity = CreateEntity(core);
            cameraEntity.AddComponent(new CameraComponent {
                LayerMask = 1,
                ClearSettings = new CameraClearSettings(true, new float4(0, 0, 1, 1), true, 1, false, 0)
            });
            RuntimeTexture red = renderer.Render2D.BuildTextureFromRaw(new TextureAsset {
                Width = 1, Height = 1, Colors = [255, 0, 0, 255]
            });
            Entity spriteEntity = CreateEntity(core);
            spriteEntity.LocalPosition = new float3(SpriteX, SpriteY, 0);
            spriteEntity.AddComponent(new SpriteComponent {
                Texture = red, Size = new int2(SpriteWidth, SpriteHeight)
            });

            renderer.Draw();
            renderer.Draw();

            Assert.Equal(new int2(renderWidth, renderHeight), renderer.MainWindowSize);
            Assert.Equal(pixelAspect, renderer.MainWindowPixelAspect);
            Assert.Equal(pixelAspect, renderer.GetWindowPixelAspect(window.Handle));
            Assert.Equal((double)clientWidth / clientHeight, (double)renderer.LastCameraProjection.M22 / renderer.LastCameraProjection.M11, 4);
            byte4[,] pixels = renderer.LastPresentedPixels;
            Assert.Equal(clientHeight, pixels.GetLength(0));
            Assert.Equal(clientWidth, pixels.GetLength(1));
            int scaleX = clientWidth / renderWidth;
            int scaleY = clientHeight / renderHeight;
            int left = SpriteX * scaleX;
            int top = SpriteY * scaleY;
            int right = (SpriteX + SpriteWidth) * scaleX;
            int bottom = (SpriteY + SpriteHeight) * scaleY;
            List<string> mismatches = new List<string>();
            int mismatchCount = 0;
            for (int y = 0; y < clientHeight; y++) {
                for (int x = 0; x < clientWidth; x++) {
                    byte4 expected = x >= left && x < right && y >= top && y < bottom ? SpritePixel : ClearPixel;
                    if (!pixels[y, x].Equals(expected)) {
                        mismatchCount++;
                        if (mismatches.Count < 8) {
                            mismatches.Add($"({x},{y}) expected {expected} got {pixels[y, x]}");
                        }
                    }
                }
            }

            Assert.True(mismatchCount == 0,
                $"{mismatchCount} of {clientWidth * clientHeight} presented pixels differ from a sprite at ({left},{top})-({right},{bottom}): " + string.Join("; ", mismatches));
        }

        /// <summary>Creates an initialized runtime entity on layer 1.</summary>
        /// <param name="core">Core that owns the entity.</param>
        /// <returns>Initialized entity.</returns>
        static Entity CreateEntity(Core core) {
            Entity entity = new Entity(core) { LayerMask = 1 };
            entity.InitComponents();
            entity.InitChildren();
            return entity;
        }
    }
}
