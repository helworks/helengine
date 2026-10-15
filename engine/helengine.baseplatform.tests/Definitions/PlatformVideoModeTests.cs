using helengine.baseplatform.Definitions;

namespace helengine.baseplatform.tests.Definitions {
    /// <summary>
    /// Verifies console video modes and the closed mode list a platform declares.
    /// </summary>
    public sealed class PlatformVideoModeTests {
        /// <summary>A valid mode keeps its fields and labels itself by size and scan.</summary>
        [Fact]
        public void Ctor_KeepsFieldsAndLabels() {
            PlatformVideoMode mode = new PlatformVideoMode("ntsc-320x224p", 320, 224, true, 59.94d, 60d, 4, 3);

            Assert.Equal("ntsc-320x224p", mode.Id);
            Assert.Equal(320, mode.Width);
            Assert.Equal(224, mode.Height);
            Assert.True(mode.IsProgressive);
            Assert.Equal(59.94d, mode.RefreshRateHz);
            Assert.Equal(60d, mode.PlayerFrameRate);
            Assert.Equal(4, mode.DisplayAspectWidth);
            Assert.Equal(3, mode.DisplayAspectHeight);
            Assert.Equal(4d * 224 / (3d * 320), mode.PixelAspect, 12);
            Assert.Equal("320×224p", mode.Label);
            Assert.Equal("640×448i", new PlatformVideoMode("ntsc-640x448i", 640, 448, false, 59.94d, 60d, 4, 3).Label);
        }

        /// <summary>Every invalid field is rejected.</summary>
        [Fact]
        public void Ctor_RejectsInvalidFields() {
            Assert.Throws<ArgumentException>(() => new PlatformVideoMode(" ", 320, 224, true, 59.94d, 60d, 4, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformVideoMode("m", 0, 224, true, 59.94d, 60d, 4, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformVideoMode("m", 320, 4097, true, 59.94d, 60d, 4, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformVideoMode("m", 320, 224, true, double.NaN, 60d, 4, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformVideoMode("m", 320, 224, true, 59.94d, 0d, 4, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformVideoMode("m", 320, 224, true, 59.94d, 60d, 0, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformVideoMode("m", 320, 224, true, 59.94d, 60d, 4, -3));
        }

        /// <summary>Width and both display-aspect terms accept 4096 and reject 4097.</summary>
        [Fact]
        public void Ctor_RejectsTermsAboveTheMaximumDimension() {
            Assert.Equal(4096, new PlatformVideoMode("m", 4096, 224, true, 59.94d, 60d, 4096, 4096).Width);
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformVideoMode("m", 4097, 224, true, 59.94d, 60d, 4, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformVideoMode("m", 320, 224, true, 59.94d, 60d, 4097, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformVideoMode("m", 320, 224, true, 59.94d, 60d, 4, 4097));
        }

        /// <summary>A zero, negative or infinite refresh rate is rejected.</summary>
        [Fact]
        public void Ctor_RejectsNonPositiveOrInfiniteRefreshRate() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformVideoMode("m", 320, 224, true, 0d, 60d, 4, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformVideoMode("m", 320, 224, true, -59.94d, 60d, 4, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformVideoMode("m", 320, 224, true, double.PositiveInfinity, 60d, 4, 3));
        }

        /// <summary>
        /// A square-pixel mode has pixel aspect exactly 1; a 4:3 mode on a 640x448 framebuffer has 14/15. A display aspect
        /// other than 4:3 is used as declared: the PS2 320x224 mode shown 3:2 has 21/20, and the 256x224 mode shown 22:15
        /// has 77/60.
        /// </summary>
        [Fact]
        public void PixelAspect_FollowsTheDisplayAspect() {
            Assert.Equal(1d, new PlatformVideoMode("square", 640, 480, true, 60d, 60d, 4, 3).PixelAspect);
            Assert.Equal(14d / 15d, new PlatformVideoMode("ntsc-640x448i", 640, 448, false, 59.94d, 60d, 4, 3).PixelAspect, 12);
            Assert.Equal(21d / 20d, new PlatformVideoMode("ntsc-320x224p", 320, 224, true, 59.94d, 60d, 3, 2).PixelAspect, 12);
            Assert.Equal(77d / 60d, new PlatformVideoMode("ntsc-256x224p", 256, 224, true, 59.94d, 60d, 22, 15).PixelAspect, 12);
        }

        /// <summary>The capability resolves each id and fails naming an unknown id and the platform.</summary>
        [Fact]
        public void Capability_ResolvesKnownAndRejectsUnknown() {
            PlatformVideoModeCapability capability = new PlatformVideoModeCapability("video-mode", [
                new PlatformVideoMode("a", 640, 448, false, 59.94d, 60d, 4, 3),
                new PlatformVideoMode("b", 320, 224, true, 59.94d, 60d, 4, 3)
            ]);

            Assert.Equal(new[] { "a", "b" }, capability.GetModeIds());
            Assert.Equal(320, capability.Resolve("b", "ps2").Width);
            Assert.True(capability.Contains("a"));
            Assert.False(capability.Contains("A"));
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => capability.Resolve("c", "ps2"));
            Assert.Contains("'c'", exception.Message);
            Assert.Contains("'ps2'", exception.Message);
        }

        /// <summary>The capability needs a setting id, at least one mode and unique ids.</summary>
        [Fact]
        public void Capability_RejectsInvalidDeclarations() {
            PlatformVideoMode mode = new PlatformVideoMode("a", 640, 448, false, 59.94d, 60d, 4, 3);

            Assert.Throws<ArgumentException>(() => new PlatformVideoModeCapability(" ", [mode]));
            Assert.Throws<ArgumentException>(() => new PlatformVideoModeCapability("video-mode", []));
            Assert.Throws<ArgumentException>(() => new PlatformVideoModeCapability("video-mode", [mode, mode]));
            Assert.Throws<ArgumentException>(() => new PlatformVideoModeCapability("video-mode", [mode, null]));
        }

        /// <summary>The capability copies the declared modes, so changing the caller's array afterwards changes nothing.</summary>
        [Fact]
        public void Capability_ModesAreACopyOfTheDeclaredArray() {
            PlatformVideoMode first = new PlatformVideoMode("a", 640, 448, false, 59.94d, 60d, 4, 3);
            PlatformVideoMode[] declared = [first, new PlatformVideoMode("b", 320, 224, true, 59.94d, 60d, 4, 3)];
            PlatformVideoModeCapability capability = new PlatformVideoModeCapability("video-mode", declared);

            declared[0] = new PlatformVideoMode("c", 256, 224, true, 59.94d, 60d, 4, 3);

            Assert.Same(first, capability.Modes[0]);
            Assert.Equal(2, capability.Modes.Count);
            Assert.True(capability.Contains("a"));
            Assert.False(capability.Contains("c"));
            Assert.Equal(new[] { "a", "b" }, capability.GetModeIds());
        }
    }
}
