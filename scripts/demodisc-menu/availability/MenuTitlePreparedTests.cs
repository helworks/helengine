namespace DemoDisc.MenuContractTests {
    /// <summary>Exercises the canonical title-menu readiness callback with real visible-width font metrics.</summary>
    public sealed class MenuTitlePreparedTests {
        /// <summary>Native headings keep their atlas pixel scale and effect sizes even when their painted width exceeds the authored alignment box.</summary>
        [Fact]
        public void NativePixelScenePreparedAlignsWithoutResizing() {
            using MenuContractFixture fixture = new MenuContractFixture("empty", false);
            Dictionary<char, FontChar> glyphs = new Dictionary<char, FontChar>();
            foreach (char character in "HELANGIDMOSC ") {
                glyphs[character] = new FontChar(new float4(0f, 0f, 20f / 512f, 25f / 256f), 8f, 21f, 0f, 0f);
            }
            using FontAsset font = new FontAsset(new FontInfo("Native uppercase", 38, 9f), null, glyphs, 37f, 512, 256);
            Entity overlay = fixture.CreateNode();
            fixture.Root.AddChild(overlay);
            TextComponent first = AddHeading(fixture, overlay, font, "HELENGINE");
            TextComponent second = AddHeading(fixture, overlay, font, "DEMO DISC");
            first.NativeFontPixels = TextNativePixelMode.RoundNearest;
            second.NativeFontPixels = TextNativePixelMode.RoundNearest;
            fixture.Menu.ScenePrepared(fixture.Root);
            Assert.Equal(1f, first.FontScale);
            Assert.Equal(1f, second.FontScale);
            Assert.Equal(2f / 3f, first.OutlineScale);
            Assert.Equal(2f / 3f, second.OutlineScale);
            Assert.True(VisibleWidth(first) > 70d);
            Assert.Equal(TextAlignment.Right, first.Alignment);
            Assert.Equal(first.Parent.LocalPosition.X, second.Parent.LocalPosition.X);
            float3 position = first.Parent.LocalPosition;
            fixture.Menu.ScenePrepared(fixture.Root);
            Assert.Equal(position, first.Parent.LocalPosition);
            Assert.Equal(1f, first.FontScale);
            Assert.Equal(34, fixture.Panels.Values.Sum(panel => panel.Items.Length));
        }

        /// <summary>Binding and title fitting finish during ScenePrepared without an input update, while all unavailable rows remain present.</summary>
        [Fact]
        public void CanonicalScenePreparedFitsBothTitlesAndRetainsCompleteMenu() {
            using MenuContractFixture fixture = new MenuContractFixture("empty", false);
            Dictionary<char, FontChar> glyphs = new Dictionary<char, FontChar>();
            foreach (char character in "HELANGIDMOSC ") {
                glyphs[character] = new FontChar(new float4(0f, 0f, 20f / 512f, 25f / 256f), 8f, 21f, 0f, 0f);
            }
            using FontAsset font = new FontAsset(new FontInfo("Contract uppercase", 38, 9f), null, glyphs, 37f, 512, 256);
            Entity overlay = fixture.CreateNode();
            overlay.LocalPosition = new float3(309f, 7f, 0f);
            fixture.Root.AddChild(overlay);
            TextComponent first = AddHeading(fixture, overlay, font, "HELENGINE");
            TextComponent second = AddHeading(fixture, overlay, font, "DEMO DISC");
            fixture.Menu.ScenePrepared(fixture.Root);
            Assert.True(fixture.Menu.IsInitialized);
            Assert.Equal(34, fixture.Panels.Values.Sum(panel => panel.Items.Length));
            Assert.Equal(TextAlignment.Right, first.Alignment);
            Assert.Equal(TextAlignment.Right, second.Alignment);
            Assert.Equal(first.FontScale, second.FontScale);
            Assert.InRange(VisibleWidth(first) + first.OutlineScale, 0d, 69.0001d);
            Assert.InRange(VisibleWidth(second) + second.OutlineScale, 0d, 69.0001d);
            Assert.Equal(first.Parent.LocalPosition.X, second.Parent.LocalPosition.X);
            float scale = first.FontScale;
            float3 position = first.Parent.LocalPosition;
            fixture.Menu.ScenePrepared(fixture.Root);
            Assert.Equal(scale, first.FontScale);
            Assert.Equal(position, first.Parent.LocalPosition);
        }

        /// <summary>Adds one of the two approved heading strings beneath their common right-anchor entity.</summary>
        /// <param name="fixture">Fixture supplying engine ownership.</param>
        /// <param name="overlay">Shared right-anchor parent.</param>
        /// <param name="font">Borrowed font containing case-sensitive metrics.</param>
        /// <param name="content">Exact heading text found by the canonical binder.</param>
        /// <returns>The heading owned by its new child entity.</returns>
        static TextComponent AddHeading(MenuContractFixture fixture, Entity overlay, FontAsset font, string content) {
            Entity entity = fixture.CreateNode();
            overlay.AddChild(entity);
            TextComponent text = new TextComponent {
                Text = content, Font = font, FontScale = 2f / 3f, OutlineScale = 2f / 3f,
                Size = new int2(70, 7), ShadowOffset = new float2()
            };
            entity.AddComponent(text);
            return text;
        }

        /// <summary>Uses the same painted-glyph width routine consumed by the actual rendering and fitting code.</summary>
        /// <param name="text">Prepared title with borrowed font metrics.</param>
        /// <returns>The visible width at its final scale.</returns>
        static double VisibleWidth(TextComponent text) {
            return TextLayoutAlignmentUtils.MeasureVisibleLineWidth(text.Text, text.Font, text.FontScale, text.Font.AtlasWidth);
        }
    }
}
