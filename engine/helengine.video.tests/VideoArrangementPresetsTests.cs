using System.Text.Json;
using helengine.media;

namespace helengine.video.tests {
    /// <summary>
    /// Verifies the built-in arrangement presets: orientation classification, region geometry on 9:16, 16:9 and 1:1 frames,
    /// the stack/split swap by orientation, the caption-free safe frame and the capability descriptors.
    /// </summary>
    public class VideoArrangementPresetsTests {
        /// <summary>
        /// Frames are classified by their aspect ratio, with near-square frames counting as square.
        /// </summary>
        [Fact]
        public void Orientation_ClassifiesFramesByAspect() {
            Assert.Equal(VideoArrangementPresets.Portrait, VideoArrangementPresets.Orientation(1080, 1920));
            Assert.Equal(VideoArrangementPresets.Portrait, VideoArrangementPresets.Orientation(1080, 1350));
            Assert.Equal(VideoArrangementPresets.Square, VideoArrangementPresets.Orientation(1080, 1080));
            Assert.Equal(VideoArrangementPresets.Landscape, VideoArrangementPresets.Orientation(1920, 1080));
        }

        /// <summary>
        /// On a 9:16 frame a stack puts the graphic band above the picture, both inside the safe frame above the captions,
        /// with pixel sizes matching their fractions.
        /// </summary>
        [Fact]
        public void Resolve_StackOnPortrait_PutsGraphicBandAbovePicture() {
            VideoResolvedArrangement stack = VideoArrangementPresets.Resolve("stack", new VideoFormat { Width = 1080, Height = 1920 });

            VideoArrangementRegion top = stack.Find("top"), main = stack.Find("main");
            Assert.Equal(VideoArrangementPresets.Portrait, stack.Orientation);
            Assert.Equal(VideoArrangementPresets.GraphicRole, top.Role);
            Assert.Equal(VideoArrangementPresets.PictureRole, main.Role);
            Assert.True(top.Y + top.Height < main.Y);
            Assert.Equal(VideoArrangementPresets.SafeMargin, top.Y, 9);
            Assert.Equal(VideoArrangementPresets.DefaultSafeBottom, main.Y + main.Height, 9);
            Assert.True(main.Height > top.Height * 1.5);
            Assert.Equal((int)Math.Round(main.Width * 1080), main.PixelWidth);
            Assert.Equal((int)Math.Round(main.Height * 1920), main.PixelHeight);
        }

        /// <summary>
        /// On a 16:9 frame a split puts the picture and the graphic side by side, each spanning most of the safe frame.
        /// </summary>
        [Fact]
        public void Resolve_SplitOnLandscape_PutsRegionsSideBySide() {
            VideoResolvedArrangement split = VideoArrangementPresets.Resolve("split", new VideoFormat { Width = 1920, Height = 1080 });

            VideoArrangementRegion left = split.Find("left"), right = split.Find("right");
            Assert.Equal(VideoArrangementPresets.Landscape, split.Orientation);
            Assert.True(left.X + left.Width < right.X);
            Assert.True(left.Height > 0.6 && right.Height > 0.5);
            Assert.True(left.Y + left.Height <= VideoArrangementPresets.DefaultSafeBottom + 1e-9 && right.Y + right.Height <= VideoArrangementPresets.DefaultSafeBottom + 1e-9);
        }

        /// <summary>
        /// stack and split keep their region names on every frame but swap geometry: a stack on a wide frame matches a
        /// split, and a split on a tall frame matches a stack.
        /// </summary>
        [Fact]
        public void Resolve_StackAndSplit_SwapGeometryByOrientation() {
            VideoFormat portrait = new VideoFormat { Width = 1080, Height = 1920 }, landscape = new VideoFormat { Width = 1920, Height = 1080 };

            AssertSame(VideoArrangementPresets.Resolve("split", landscape).Find("left"), VideoArrangementPresets.Resolve("stack", landscape).Find("main"));
            AssertSame(VideoArrangementPresets.Resolve("split", landscape).Find("right"), VideoArrangementPresets.Resolve("stack", landscape).Find("top"));
            AssertSame(VideoArrangementPresets.Resolve("stack", portrait).Find("main"), VideoArrangementPresets.Resolve("split", portrait).Find("left"));
            AssertSame(VideoArrangementPresets.Resolve("stack", portrait).Find("top"), VideoArrangementPresets.Resolve("split", portrait).Find("right"));
        }

        /// <summary>
        /// A 1:1 frame stacks, and every region of every preset stays inside the frame.
        /// </summary>
        [Fact]
        public void Resolve_Square_StacksAndStaysInsideTheFrame() {
            VideoFormat square = new VideoFormat { Width = 1080, Height = 1080 };
            VideoResolvedArrangement stack = VideoArrangementPresets.Resolve("stack", square);

            Assert.Equal(VideoArrangementPresets.Square, stack.Orientation);
            Assert.True(stack.Find("top").Y + stack.Find("top").Height < stack.Find("main").Y);
            foreach (string preset in VideoArrangementPresets.Ids) {
                foreach (VideoFormat format in new[] { square, new VideoFormat { Width = 1080, Height = 1920 }, new VideoFormat { Width = 1920, Height = 1080 } }) {
                    Assert.All(VideoArrangementPresets.Resolve(preset, format).Regions, region => Assert.True(region.Viewport().IsValid(), preset + " " + region.Name));
                }
            }
        }

        /// <summary>
        /// Backgrounds and the full region cover the frame, captions included; graphic_only is one centered graphic region.
        /// </summary>
        [Fact]
        public void Resolve_FullFrameRegionsCoverTheFrame() {
            VideoFormat format = new VideoFormat { Width = 1080, Height = 1920 };
            VideoArrangementRegion background = VideoArrangementPresets.Resolve("take_with_graphic", format).Find("background");
            VideoArrangementRegion full = VideoArrangementPresets.Resolve("full", format).Find("main");
            VideoArrangementRegion only = Assert.Single(VideoArrangementPresets.Resolve("graphic_only", format).Regions);

            Assert.Equal([0d, 0, 1, 1], new[] { background.X, background.Y, background.Width, background.Height });
            Assert.Equal([0d, 0, 1, 1], new[] { full.X, full.Y, full.Width, full.Height });
            Assert.Equal(VideoArrangementPresets.GraphicRole, only.Role);
            Assert.Equal(0.5, only.X + only.Width / 2, 6);
        }

        /// <summary>
        /// Resolved for an edit, the safe frame ends above the estimated caption band: large captions on a narrow frame
        /// wrap into more lines and push the regions up; an edit without captions uses the frame between margins.
        /// </summary>
        [Fact]
        public void Resolve_ForEdit_KeepsRegionsAboveTheCaptions() {
            VideoEdit edit = new VideoEdit {
                Format = new VideoFormat { Width = 540, Height = 960 },
                TextStyles = new(StringComparer.Ordinal) { ["caption"] = JsonSerializer.SerializeToElement(new { FontSize = 54, CenterY = 0.79, MaxWidth = 0.86 }) },
                Tracks = new VideoTracks { Captions = new VideoCaptionTrack { WordsPerCue = 8, WordsPerLine = 4 } }
            };

            VideoCaptionBand band = VideoCaptionBand.Estimate(edit);
            VideoResolvedArrangement stack = VideoArrangementPresets.Resolve("stack", edit);

            Assert.Equal(0.79 - 4 * 54 * VideoTextStyles.LineHeight / 2 / 960, band.Top, 6);
            Assert.Equal(band.Top - VideoCaptionBand.Gap, stack.SafeBottom, 6);
            VideoArrangementRegion main = stack.Find("main");
            Assert.Equal(stack.SafeBottom, main.Y + main.Height, 6);
            edit.Tracks.Captions = null;
            Assert.Equal(1 - VideoArrangementPresets.SafeMargin, VideoArrangementPresets.Resolve("stack", edit).SafeBottom, 9);
        }

        /// <summary>
        /// Unknown presets and regions are rejected with the valid choices.
        /// </summary>
        [Fact]
        public void Resolve_UnknownPresetOrRegion_Throws() {
            VideoFormat format = new VideoFormat { Width = 1080, Height = 1920 };

            Assert.Contains("graphic_only", Assert.Throws<InvalidDataException>(() => VideoArrangementPresets.Resolve("mosaic", format)).Message);
            Assert.Throws<InvalidDataException>(() => VideoArrangementPresets.Resolve("stack", format).Find("left"));
        }

        /// <summary>
        /// The capability catalog lists every preset with its regions, roles and planner descriptions.
        /// </summary>
        [Fact]
        public void Publish_ListsEveryArrangementWithRegions() {
            MediaCapabilities capabilities = MediaCapabilities.Basic();
            VideoArrangementPresets.Publish(capabilities);

            Assert.Equal(["full", "stack", "split", "take_with_graphic", "take_behind_graphic", "graphic_only"], capabilities.Arrangements.Select(item => item.Id));
            MediaArrangementDescriptor stack = capabilities.Arrangements.Single(item => item.Id == "stack");
            Assert.Equal(["top", "main"], stack.Regions.Select(region => region.Name));
            Assert.Equal(["graphic", "picture"], stack.Regions.Select(region => region.Role));
            Assert.All(capabilities.Arrangements, item => Assert.False(string.IsNullOrWhiteSpace(item.Description)));
            Assert.Contains("landscape", stack.Regions[0].Description);
            JsonElement described = capabilities.Describe().GetProperty("arrangements")[1];
            Assert.Equal("stack", described.GetProperty("id").GetString());
            Assert.Equal("picture", described.GetProperty("regions")[1].GetProperty("role").GetString());
        }

        /// <summary>
        /// Asserts two resolved regions cover the same rectangle.
        /// </summary>
        /// <param name="expected">Expected region.</param>
        /// <param name="actual">Actual region.</param>
        static void AssertSame(VideoArrangementRegion expected, VideoArrangementRegion actual) {
            Assert.Equal(expected.X, actual.X, 9);
            Assert.Equal(expected.Y, actual.Y, 9);
            Assert.Equal(expected.Width, actual.Width, 9);
            Assert.Equal(expected.Height, actual.Height, 9);
            Assert.Equal(expected.Role, actual.Role);
        }
    }
}
