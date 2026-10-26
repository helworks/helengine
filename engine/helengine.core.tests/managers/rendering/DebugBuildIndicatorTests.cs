using helengine;

namespace helengine.core.tests.managers.rendering {
    /// <summary>Verifies final-pass debug commands, borrowed font lifetime and the selected build environment.</summary>
    public sealed class DebugBuildIndicatorTests {
        /// <summary>A target without a loaded font never invents font or camera resources.</summary>
        [Fact]
        public void NoFontOrMinimizedTargetProducesNoCommands() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            Assert.Null(Build(renderer, 320, 240));
            using FontAsset font = CreateFont(renderer);
            Attach(font, renderer);
            Assert.Null(Build(renderer, 0, 240));
            Assert.Null(Build(renderer, 320, 0));
        }

#if HELENGINE_ENV_DEBUG
        /// <summary>The overlay is one opaque background followed by the five borrowed green glyphs.</summary>
        [Fact]
        public void DebugCommandsUseBlackBackgroundAndExistingGreenFontGlyphs() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using FontAsset font = CreateFont(renderer);
            Attach(font, renderer);
            RenderCommandList2D commands = Build(renderer, 320, 240);
            Assert.NotNull(commands);
            Assert.Equal(6, commands.Count);
            Assert.Equal(RenderCommand2DType.RoundedRect, commands.GetCommandType(0));
            int background = commands.GetRoundedRectPayloadIndex(0);
            Assert.Equal(new byte4(0, 0, 0, 255), commands.GetRoundedRectFillColor(background));
            Assert.Equal(0d, (double)commands.GetRoundedRectRadius(background));
            float4 bounds = commands.GetRoundedRectBounds(background);
            Assert.Equal(4d, (double)bounds.X);
            Assert.Equal(4d, (double)bounds.Y);
            for (int index = 1; index < 6; index++) {
                Assert.Equal(RenderCommand2DType.GlyphQuad, commands.GetCommandType(index));
                int glyph = commands.GetGlyphQuadPayloadIndex(index);
                Assert.Same(font.Texture, commands.GetGlyphQuadTexture(glyph));
                Assert.Equal(new byte4(0, 255, 0, 255), commands.GetGlyphQuadColor(glyph));
                float4 glyphBounds = commands.GetGlyphQuadBounds(glyph);
                Assert.True(glyphBounds.X >= bounds.X && glyphBounds.Y >= bounds.Y);
                Assert.True(glyphBounds.X + glyphBounds.Z <= bounds.X + bounds.Z);
                Assert.True(glyphBounds.Y + glyphBounds.W <= bounds.Y + bounds.W);
                Assert.Equal(font.Characters["DEBUG"[index - 1]].SourceRect, commands.GetGlyphQuadSourceRect(glyph));
            }
        }

        /// <summary>Font leading must not shrink visible glyph ink below the physical marker size.</summary>
        [Theory]
        [InlineData(256, 192, 8)]
        [InlineData(320, 240, 8)]
        [InlineData(1280, 720, 18)]
        [InlineData(1920, 1080, 24)]
        public void FontLeadingDoesNotReduceVisibleGlyphHeight(int width, int height, double expectedInkHeight) {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            // Each glyph has eight atlas pixels of ink inside a 32-pixel line.
            using FontAsset font = CreateFont(renderer, 32f);
            Attach(font, renderer);
            RenderCommandList2D commands = Build(renderer, width, height);
            Assert.NotNull(commands);
            double minY = double.PositiveInfinity;
            double maxY = double.NegativeInfinity;
            for (int index = 1; index < commands.Count; index++) {
                float4 bounds = commands.GetGlyphQuadBounds(commands.GetGlyphQuadPayloadIndex(index));
                minY = Math.Min(minY, (double)bounds.Y);
                maxY = Math.Max(maxY, (double)bounds.Y + (double)bounds.W);
            }
            Assert.InRange(maxY - minY, expectedInkHeight - 0.05d, expectedInkHeight + 0.05d);
        }

        /// <summary>Nearest-sampled low-resolution atlases retain their single-texel strokes at SD output sizes.</summary>
        [Theory]
        [InlineData(11)]
        [InlineData(14)]
        public void SmallAtlasGlyphsAreNotMinifiedAtSdResolution(int atlasInkHeight) {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using FontAsset font = CreateFont(renderer, 32f);
            foreach (char character in "DEBUG") {
                FontChar glyph = font.Characters[character];
                glyph.SourceRect.W = atlasInkHeight / 32f;
                font.Characters[character] = glyph;
            }
            Attach(font, renderer);
            RenderCommandList2D commands = Build(renderer, 320, 240);
            Assert.NotNull(commands);
            for (int index = 1; index < commands.Count; index++) {
                float4 bounds = commands.GetGlyphQuadBounds(commands.GetGlyphQuadPayloadIndex(index));
                Assert.InRange((double)bounds.W, atlasInkHeight - 0.05d, atlasInkHeight + 0.05d);
                Assert.InRange((double)bounds.Z, 7.95d, 8.05d);
            }
        }

        /// <summary>Unchanged frames retain one command buffer; a resize rebuilds geometry in the same buffer.</summary>
        [Fact]
        public void UnchangedFramesReuseCommandsAndResizeChangesTheirLayout() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using FontAsset font = CreateFont(renderer);
            Attach(font, renderer);
            RenderCommandList2D commands = Build(renderer, 320, 240);
            int version = commands.Version;
            float4 original = commands.GetRoundedRectBounds(0);
            for (int index = 0; index < 10; index++) {
                Assert.Same(commands, Build(renderer, 320, 240));
                Assert.Equal(version, commands.Version);
            }
            Assert.Same(commands, Build(renderer, 1920, 1080));
            Assert.NotEqual(version, commands.Version);
            Assert.True(commands.GetRoundedRectBounds(0).W > original.W);
        }

        /// <summary>Disposal clears borrowed commands before the native font may be deleted, then selects a surviving font.</summary>
        [Fact]
        public void FontDisposalClearsBorrowedTextureAndSelectsAnotherLoadedFont() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using FontAsset first = CreateFont(renderer);
            using FontAsset second = CreateFont(renderer);
            Attach(first, renderer);
            Attach(second, renderer);
            RenderCommandList2D commands = Build(renderer, 320, 240);
            Assert.Same(first.Texture, commands.GetGlyphQuadTexture(0));
            first.Dispose();
            Assert.Equal(0, commands.Count);
            Assert.Same(commands, Build(renderer, 320, 240));
            Assert.Same(second.Texture, commands.GetGlyphQuadTexture(0));
            second.Dispose();
            Assert.Null(Build(renderer, 320, 240));
        }

        /// <summary>Renderers never borrow font textures owned by another core or destroy those borrowed fonts.</summary>
        [Fact]
        public void RendererLifetimeAndFontSelectionRemainIndependent() {
            Batch2DTestRenderManager firstRenderer = new Batch2DTestRenderManager();
            using Batch2DTestRenderManager secondRenderer = new Batch2DTestRenderManager();
            using FontAsset firstFont = CreateFont(firstRenderer);
            using FontAsset secondFont = CreateFont(secondRenderer);
            Attach(firstFont, firstRenderer);
            Attach(secondFont, secondRenderer);
            RenderCommandList2D retainedCommands = Build(firstRenderer, 320, 240);
            Assert.Same(firstFont.Texture, retainedCommands.GetGlyphQuadTexture(0));
            Assert.Same(secondFont.Texture, Build(secondRenderer, 320, 240).GetGlyphQuadTexture(0));
            firstRenderer.Dispose();
            Assert.Equal(0, retainedCommands.Count);
            Assert.False(firstFont.IsDisposed);
            firstFont.Dispose();
            Assert.Same(secondFont.Texture, Build(secondRenderer, 320, 240).GetGlyphQuadTexture(0));
        }

        /// <summary>A font without all required letters cannot create a partial or misleading build marker.</summary>
        [Fact]
        public void FontWithoutDebugGlyphsDoesNotHideAUsableLoadedFont() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using FontAsset incomplete = CreateFont(renderer);
            incomplete.Characters.Remove('D');
            Attach(incomplete, renderer);
            Assert.Null(Build(renderer, 320, 240));
            using FontAsset complete = CreateFont(renderer);
            Attach(complete, renderer);
            Assert.Same(complete.Texture, Build(renderer, 320, 240).GetGlyphQuadTexture(0));
        }

        /// <summary>Invalid atlas coordinates or numeric font metrics cannot block a valid font or emit rejected native commands.</summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        public void InvalidFontCannotHideAnotherUsableFont(int invalidCase) {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using FontAsset invalid = CreateFont(renderer, invalidCase == 0 ? float.PositiveInfinity : 16f);
            FontChar glyph = invalid.Characters['D'];
            if (invalidCase == 1) {
                glyph.SourceRect.X = -0.1f;
            } else if (invalidCase == 2) {
                glyph.SourceRect.Z = float.PositiveInfinity;
            } else if (invalidCase == 3) {
                glyph.OffsetY = float.NaN;
            } else if (invalidCase == 4) {
                glyph.AdvanceWidth = float.PositiveInfinity;
            } else if (invalidCase == 5) {
                glyph.SourceRect.X = 0.95f;
            } else if (invalidCase == 6) {
                glyph.BearingX = -float.MaxValue;
                FontChar last = invalid.Characters['E'];
                last.BearingX = float.MaxValue;
                invalid.Characters['E'] = last;
            }
            invalid.Characters['D'] = glyph;
            Attach(invalid, renderer);
            if (invalidCase == 6 && RealRepresentation.ArithmeticBits == 64) {
                Assert.NotNull(Build(renderer, 320, 240));
                return;
            }
            Assert.Null(Build(renderer, 320, 240));
            using FontAsset valid = CreateFont(renderer);
            Attach(valid, renderer);
            RenderCommandList2D commands = Build(renderer, 320, 240);
            Assert.NotNull(commands);
            Assert.Same(valid.Texture, commands.GetGlyphQuadTexture(0));
        }
#else
        /// <summary>The ordinary .NET DEBUG compiler symbol must not make a release-environment artifact display DEBUG.</summary>
        [Fact]
        public void NonDebugEnvironmentNeverProducesIndicatorCommands() {
            using Batch2DTestRenderManager renderer = new Batch2DTestRenderManager();
            using FontAsset font = CreateFont(renderer);
            Attach(font, renderer);
            Assert.Null(Build(renderer, 320, 240));
        }
#endif

        /// <summary>Constructs a loaded atlas with distinct UVs and nonzero glyph bearings.</summary>
        static FontAsset CreateFont(Batch2DTestRenderManager renderer, float lineHeight = 16f) {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
            string label = "DEBUG";
            for (int index = 0; index < label.Length; index++) {
                characters.Add(label[index], new FontChar(new float4(index / 8f, 0f, 0.125f, 0.25f), -2f, 9f, -1f, 0f));
            }
            return new FontAsset(new FontInfo("DebugTest", 16, 4f), renderer.CreateTexture(64, 32), characters, lineHeight, 64, 32);
        }

        /// <summary>Exercises the public final-pass integration point with physical framebuffer dimensions.</summary>
        static RenderCommandList2D Build(RenderManager2D renderer, int width, int height) {
            return renderer.BuildDebugBuildIndicatorCommands(width, height);
        }

        /// <summary>Registers a font using the same public ownership hook required by native custom font loaders.</summary>
        static void Attach(FontAsset font, RenderManager2D renderer) {
            font.AttachRuntimeRenderer(renderer);
        }
    }
}
