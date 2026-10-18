namespace helengine.core.tests.components {
    /// <summary>Checks shared numerical layout results independent of scene lifecycle.</summary>
    public sealed class ViewportLayoutApplierTests {
        /// <summary>Stretch preserves independent axes and the established glyph scaling convention.</summary>
        [Fact]
        public void Stretch_UsesIndependentAxes() {
            ViewportLayoutMetrics metrics = ViewportLayoutMath.Resolve(2, new int2(1280, 720), new int2(320, 240), 1, 1, 160, 160);
            Assert.Equal(new float3(96, 30, 7), ViewportLayoutApplier.ResolvePosition(new float3(384, 90, 7), metrics));
            RoundedRectComponent rectangle = new RoundedRectComponent { Size = new int2(512, 96), Radius = 6, BorderThickness = 3 };
            ViewportLayoutApplier.ApplyComponent(rectangle, metrics);
            Assert.Equal(new int2(128, 32), rectangle.Size);
            Assert.Equal(2, rectangle.Radius);
            Assert.Equal(1, rectangle.BorderThickness);
        }

        /// <summary>Fit centers a 16:9 reference in a 4:3 pixel surface.</summary>
        [Fact]
        public void Fit_UsesLetterboxOrigin() {
            ViewportLayoutMetrics metrics = ViewportLayoutMath.Resolve(1, new int2(1280, 720), new int2(320, 240), 1, 1, 160, 160);
            Assert.Equal(new int2(320, 180), metrics.AnchorSize);
            Assert.Equal(new float2(0, 30), metrics.Origin);
            Assert.Equal(new float3(96, 52.5f, 0), ViewportLayoutApplier.ResolvePosition(new float3(384, 90, 0), metrics));
        }

        /// <summary>Non-square pixels alter horizontal geometry but preserve displayed glyph size.</summary>
        [Fact]
        public void NonSquarePixels_PreserveGlyphScale() {
            ViewportLayoutMetrics metrics = ViewportLayoutMath.Resolve(1, new int2(320, 240), new int2(256, 240), 1.25, 1, 160, 160);
            Assert.Equal(0.8, metrics.ScaleX, 8);
            Assert.Equal(1, metrics.GlyphScale, 8);
            SpriteComponent sprite = new SpriteComponent { Size = new int2(100, 40) };
            ViewportLayoutApplier.ApplyComponent(sprite, metrics);
            Assert.Equal(new int2(80, 40), sprite.Size);
        }

        /// <summary>Physical density is resolved from explicit DPI rather than an invented fallback.</summary>
        [Fact]
        public void Density_UsesDpi() {
            ViewportLayoutMetrics metrics = ViewportLayoutMath.Resolve(3, new int2(1280, 720), new int2(320, 240), 1, 1, 320, 160);
            Assert.Equal(2, metrics.ScaleX);
            Assert.Equal(2, metrics.ScaleY);
            Assert.Equal(2, metrics.GlyphScale);
            Assert.Throws<InvalidOperationException>(() => ViewportLayoutMath.Resolve(3, new int2(1280, 720), new int2(320, 240), 1, 1, 0, 160));
        }

        /// <summary>Native glyph pixels remain at one while their layout area scales.</summary>
        [Fact]
        public void NativePixelFont_PreservesScale() {
            TextComponent text = new TextComponent { Size = new int2(100, 40), NativeFontPixels = TextNativePixelMode.Disabled };
            text.NativeFontPixels = (TextNativePixelMode)1;
            ViewportLayoutApplier.ApplyComponent(text, ViewportLayoutMath.Resolve(2, new int2(1280, 720), new int2(320, 240), 1, 1, 160, 160));
            Assert.Equal(new int2(25, 13), text.Size);
            Assert.Equal(1, text.FontScale);
        }

        /// <summary>A bounds-only viewport leaves authoring geometry unchanged.</summary>
        [Fact]
        public void NoScaling_IsIdentity() {
            ViewportLayoutMetrics metrics = ViewportLayoutMath.Resolve(0, new int2(1280, 720), new int2(320, 240), 1, 1, 0, 0);
            Assert.Equal(new float3(384, 90, 0), ViewportLayoutApplier.ResolvePosition(new float3(384, 90, 0), metrics));
        }
    }
}
