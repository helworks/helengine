using helengine;
using helengine.vfx;
using helengine.vfx.directx11;
using helengine.vfx.effects;
using helengine.vfx.io;
using Xunit;

namespace helengine.vfx.cli.tests {
    /// <summary>
    /// Drives the whole export pipeline (EXR sequence discovery, GPU effect run, EXR writeback) over
    /// small synthetic fixtures.
    ///
    /// HARDWARE REQUIREMENT: these tests create a real Direct3D11 device through
    /// <see cref="DirectX11VfxDevice"/> and will fail on a machine with no D3D11-capable adapter.
    /// They are tagged with the RequiresGpu trait so such a machine can filter them out with
    /// <c>dotnet test --filter RequiresGpu!=true</c> instead of hitting an unexplained device
    /// creation failure.
    /// </summary>
    [Trait("RequiresGpu", "true")]
    public class EndToEndExportTests {
        /// <summary>
        /// Pixel width of every synthetic fixture frame.
        /// </summary>
        const int FixtureWidth = 8;

        /// <summary>
        /// Pixel height of every synthetic fixture frame.
        /// </summary>
        const int FixtureHeight = 8;

        /// <summary>
        /// Number of frames in every synthetic fixture sequence.
        /// </summary>
        const int FixtureFrameCount = 3;

        /// <summary>
        /// Red component of the synthetic source plate.
        /// </summary>
        const float SourceRed = 0.2f;

        /// <summary>
        /// Green component of the synthetic source plate.
        /// </summary>
        const float SourceGreen = 0.4f;

        /// <summary>
        /// Blue component of the synthetic source plate.
        /// </summary>
        const float SourceBlue = 0.6f;

        /// <summary>
        /// Confirms the run writes one output frame per input frame at the clip resolution.
        /// </summary>
        [Fact]
        public void Run_RainbowExpand_WritesExpectedFrameCountAndResolution() {
            string root = CreateFixtureRoot(out string sourceFolder, out string maskFolder, out string outputFolder);
            try {
                WriteFixtureSequences(sourceFolder, maskFolder);
                RunEffect(sourceFolder, maskFolder, outputFolder, new Dictionary<string, string>());

                string[] outputFiles = Directory.GetFiles(outputFolder, "*.exr");
                Assert.Equal(FixtureFrameCount, outputFiles.Length);

                foreach (string outputFile in outputFiles) {
                    FloatImageAsset frame = ExrFrameReader.ReadFrame(outputFile);
                    Assert.Equal(FixtureWidth, frame.Width);
                    Assert.Equal(FixtureHeight, frame.Height);
                    Assert.Contains(frame.Pixels, value => value != 0f);
                    frame.Dispose();
                }
            } finally {
                DeleteFixtureRoot(root);
            }
        }

        /// <summary>
        /// With no hue rotation and no scaling, the effect must be a near-identity pass over a fully
        /// opaque mask: every output pixel has to carry the source plate's color back out. This is what
        /// proves the shader actually samples and returns the source, rather than the previous
        /// "some channel is non-zero" assertion which the hardcoded output alpha of 1 satisfied alone.
        /// </summary>
        [Fact]
        public void Run_IdentityParameters_ReproducesSourceColor() {
            string root = CreateFixtureRoot(out string sourceFolder, out string maskFolder, out string outputFolder);
            try {
                WriteFixtureSequences(sourceFolder, maskFolder);

                var parameters = new Dictionary<string, string> {
                    ["HueCyclesPerClip"] = "0",
                    ["StartScale"] = "1",
                    ["EndScale"] = "1"
                };
                RunEffect(sourceFolder, maskFolder, outputFolder, parameters);

                foreach (string outputFile in Directory.GetFiles(outputFolder, "*.exr")) {
                    FloatImageAsset frame = ExrFrameReader.ReadFrame(outputFile);
                    try {
                        for (int pixelIndex = 0; pixelIndex < FixtureWidth * FixtureHeight; pixelIndex++) {
                            AssertPixelColor(frame, pixelIndex, SourceRed, SourceGreen, SourceBlue);
                        }
                    } finally {
                        frame.Dispose();
                    }
                }
            } finally {
                DeleteFixtureRoot(root);
            }
        }

        /// <summary>
        /// With the subject scaled down to half the frame, the outer pixels sample outside the source
        /// image and must be filled with the configured background color, while the inner pixels still
        /// carry the source plate. This exercises the out-of-bounds branch of the shader, which no
        /// other test reaches.
        /// </summary>
        [Fact]
        public void Run_ScaleSmallerThanFrame_FillsOutOfBoundsPixelsWithBackgroundColor() {
            const float backgroundRed = 0.9f;
            const float backgroundGreen = 0.1f;
            const float backgroundBlue = 0.25f;

            string root = CreateFixtureRoot(out string sourceFolder, out string maskFolder, out string outputFolder);
            try {
                WriteFixtureSequences(sourceFolder, maskFolder);

                var parameters = new Dictionary<string, string> {
                    ["HueCyclesPerClip"] = "0",
                    ["StartScale"] = "0.5",
                    ["EndScale"] = "0.5",
                    ["BackgroundColor"] = "0.9,0.1,0.25"
                };
                RunEffect(sourceFolder, maskFolder, outputFolder, parameters);

                foreach (string outputFile in Directory.GetFiles(outputFolder, "*.exr")) {
                    FloatImageAsset frame = ExrFrameReader.ReadFrame(outputFile);
                    try {
                        // At scale 0.5 the shader samples at 2 * UV - 0.5, so the corner pixel maps to
                        // -0.375 and falls outside the source image.
                        AssertPixelColor(frame, 0, backgroundRed, backgroundGreen, backgroundBlue);
                        AssertPixelColor(frame, (FixtureWidth * FixtureHeight) - 1, backgroundRed, backgroundGreen, backgroundBlue);
                        // The middle of the frame still maps inside the source image.
                        AssertPixelColor(frame, (3 * FixtureWidth) + 3, SourceRed, SourceGreen, SourceBlue);
                    } finally {
                        frame.Dispose();
                    }
                }
            } finally {
                DeleteFixtureRoot(root);
            }
        }

        /// <summary>
        /// Creates a unique fixture directory tree for one test run.
        /// </summary>
        /// <param name="sourceFolder">Receives the created source sequence folder.</param>
        /// <param name="maskFolder">Receives the created mask sequence folder.</param>
        /// <param name="outputFolder">Receives the (not yet created) export output folder.</param>
        /// <returns>Root folder that must be deleted when the test finishes.</returns>
        static string CreateFixtureRoot(out string sourceFolder, out string maskFolder, out string outputFolder) {
            string root = Path.Combine(Path.GetTempPath(), "helengine-vfx-e2e-" + Guid.NewGuid().ToString("N"));
            sourceFolder = Path.Combine(root, "source");
            maskFolder = Path.Combine(root, "mask");
            outputFolder = Path.Combine(root, "output");
            Directory.CreateDirectory(sourceFolder);
            Directory.CreateDirectory(maskFolder);
            return root;
        }

        /// <summary>
        /// Removes a fixture directory tree, tolerating the transient Windows file locks that can
        /// otherwise turn a genuine assertion failure into an unrelated cleanup exception.
        /// </summary>
        /// <param name="root">Fixture root folder to remove.</param>
        static void DeleteFixtureRoot(string root) {
            try {
                Directory.Delete(root, recursive: true);
            } catch (IOException) {
                // Leftover temp fixtures are harmless; never let cleanup mask the real test result.
            } catch (UnauthorizedAccessException) {
                // Same as above: a locked fixture file must not replace the assertion failure.
            }
        }

        /// <summary>
        /// Writes the synthetic source plate and a fully opaque RGBA matte for every fixture frame.
        /// </summary>
        /// <param name="sourceFolder">Folder to write the source sequence into.</param>
        /// <param name="maskFolder">Folder to write the mask sequence into.</param>
        static void WriteFixtureSequences(string sourceFolder, string maskFolder) {
            for (int i = 0; i < FixtureFrameCount; i++) {
                WriteSolidFrame(Path.Combine(sourceFolder, $"frame.{i:D4}.exr"), SourceRed, SourceGreen, SourceBlue, 1f);
                WriteSolidFrame(Path.Combine(maskFolder, $"frame.{i:D4}.exr"), 1f, 1f, 1f, 1f);
            }
        }

        /// <summary>
        /// Runs the RainbowExpand effect over the fixture sequences on a real Direct3D11 device.
        /// </summary>
        /// <param name="sourceFolder">Folder holding the source sequence.</param>
        /// <param name="maskFolder">Folder holding the mask sequence.</param>
        /// <param name="outputFolder">Folder the processed frames are written into.</param>
        /// <param name="parameters">Effect parameter values for the run.</param>
        static void RunEffect(string sourceFolder, string maskFolder, string outputFolder, IReadOnlyDictionary<string, string> parameters) {
            ImageSequence source = ExrSequenceReader.ReadSequence(sourceFolder);
            ImageSequence mask = ExrSequenceReader.ReadSequence(maskFolder);
            VfxClip clip = new VfxClip(source, mask);
            IVfxEffect effect = new RainbowExpandEffect();

            using (DirectX11VfxDevice device = new DirectX11VfxDevice())
            using (DirectX11VfxEffectRunner runner = new DirectX11VfxEffectRunner(device, effect)) {
                runner.Run(clip, effect, parameters, outputFolder);
            }
        }

        /// <summary>
        /// Asserts one output pixel's RGB channels, using the same two-decimal tolerance the EXR
        /// round-trip test uses to absorb the format's quantization.
        /// </summary>
        /// <param name="frame">Decoded output frame.</param>
        /// <param name="pixelIndex">Index of the pixel, counting left to right then top to bottom.</param>
        /// <param name="expectedRed">Expected red channel value.</param>
        /// <param name="expectedGreen">Expected green channel value.</param>
        /// <param name="expectedBlue">Expected blue channel value.</param>
        static void AssertPixelColor(FloatImageAsset frame, int pixelIndex, float expectedRed, float expectedGreen, float expectedBlue) {
            int offset = pixelIndex * 4;
            Assert.Equal(expectedRed, frame.Pixels[offset + 0], 2);
            Assert.Equal(expectedGreen, frame.Pixels[offset + 1], 2);
            Assert.Equal(expectedBlue, frame.Pixels[offset + 2], 2);
        }

        /// <summary>
        /// Writes a single-color RGBA EXR frame used as synthetic fixture input.
        /// </summary>
        /// <param name="path">Destination EXR path.</param>
        /// <param name="r">Red channel value for every pixel.</param>
        /// <param name="g">Green channel value for every pixel.</param>
        /// <param name="b">Blue channel value for every pixel.</param>
        /// <param name="a">Alpha channel value for every pixel.</param>
        static void WriteSolidFrame(string path, float r, float g, float b, float a) {
            float[] pixels = new float[FixtureWidth * FixtureHeight * 4];
            for (int i = 0; i < FixtureWidth * FixtureHeight; i++) {
                pixels[(i * 4) + 0] = r;
                pixels[(i * 4) + 1] = g;
                pixels[(i * 4) + 2] = b;
                pixels[(i * 4) + 3] = a;
            }
            FloatImageAsset frame = new FloatImageAsset { Width = FixtureWidth, Height = FixtureHeight, Pixels = pixels };
            ExrFrameWriter.WriteFrame(frame, path);
            frame.Dispose();
        }
    }
}
