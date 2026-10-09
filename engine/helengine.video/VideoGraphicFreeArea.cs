using System.Text.Json;

namespace helengine.video {
    /// <summary>
    /// Keeps a graphic off the scene's pictures. The style's safe area (wrapping width, frame margins, caption band) is
    /// cut into the largest rectangles no picture covers (keeping a small margin from each picture); the graphic is laid
    /// out in each of them, centered in the box, and the one that keeps the text largest wins, preferring the box closest
    /// to the style's preferred center when sizes are within a few percent. When no picture touches the safe area the graphic is laid out
    /// exactly as before, and when no free box keeps the text at a readable size (for example a full-frame take or image)
    /// it falls back to the whole safe area, where text over the picture is the intended look.
    /// </summary>
    public static class VideoGraphicFreeArea {
        /// <summary>
        /// Smallest fit scale a free box must allow; below it the text would be too small to be worth avoiding the picture.
        /// </summary>
        public const double MinimumScale = 0.4;

        /// <summary>
        /// Gap kept between the graphic and a picture edge, as a fraction of the frame height.
        /// </summary>
        public const double PictureMargin = 0.012;

        /// <summary>
        /// Narrowest free box worth trying, as a fraction of the frame width.
        /// </summary>
        const double MinimumWidth = 0.25;

        /// <summary>
        /// Shortest free box worth trying, as a fraction of the frame height.
        /// </summary>
        const double MinimumHeight = 0.04;

        /// <summary>
        /// Relative font size difference under which two boxes count as equally good and the nearer one wins.
        /// </summary>
        const double SizeTolerance = 0.03;

        /// <summary>
        /// Lays out the blocks in the free part of the safe area that keeps them largest, or in the whole safe area when
        /// no picture overlaps it or no free part is large enough.
        /// </summary>
        /// <param name="blocks">Blocks in reading order; their sizes and centers are written.</param>
        /// <param name="template">Template supplying the layouts and gaps.</param>
        /// <param name="requested">auto, vertical or horizontal.</param>
        /// <param name="style">Overlay text style snapshot.</param>
        /// <param name="measurer">Text measurer.</param>
        /// <param name="area">The style's safe area.</param>
        /// <param name="pictures">Frame rectangles covered by the scene's pictures.</param>
        /// <returns>Arrangement of the chosen box.</returns>
        public static VideoGraphicArrangement Arrange(IReadOnlyList<VideoGraphicBlock> blocks, GraphicTemplateAsset template, string requested, JsonElement style, IVideoTextMeasurer measurer, VideoGraphicSafeArea area, IReadOnlyList<VideoGraphicRectangle> pictures) {
            VideoGraphicRectangle bounds = area.Bounds;
            if (!pictures.Any(picture => picture.Overlaps(bounds))) {
                return VideoGraphicLayout.Arrange(blocks, template, requested, style, measurer, area);
            }
            VideoGraphicSafeArea chosen = null;
            double chosenSize = 0;
            double minimumSize = VideoTextStyles.Number(style, "FontSize", VideoTextStyles.DefaultFontSize) * MinimumScale;
            foreach (VideoGraphicSafeArea candidate in Find(area, pictures)) {
                double size = VideoGraphicLayout.Arrange(blocks, template, requested, style, measurer, candidate).FontSize;
                if (size < minimumSize) {
                    continue;
                }
                bool larger = size > chosenSize * (1 + SizeTolerance);
                bool similar = !larger && size >= chosenSize * (1 - SizeTolerance);
                if (chosen == null || larger || (similar && Distance(candidate, area.CenterY) < Distance(chosen, area.CenterY))) {
                    chosen = candidate;
                    chosenSize = size;
                }
            }
            return VideoGraphicLayout.Arrange(blocks, template, requested, style, measurer, chosen ?? area);
        }

        /// <summary>
        /// Finds the maximal rectangles of the safe area that no picture (grown by <see cref="PictureMargin"/>) overlaps,
        /// ignoring boxes too small to hold text.
        /// </summary>
        /// <param name="area">The style's safe area.</param>
        /// <param name="pictures">Frame rectangles covered by the scene's pictures.</param>
        /// <returns>Free boxes, each preferring its own vertical middle; empty when the pictures cover the area.</returns>
        public static List<VideoGraphicSafeArea> Find(VideoGraphicSafeArea area, IReadOnlyList<VideoGraphicRectangle> pictures) {
            VideoGraphicRectangle bounds = area.Bounds;
            double margin = PictureMargin * area.FrameHeight;
            List<VideoGraphicRectangle> obstacles = pictures.Select(picture => picture.Inflate(margin)).Where(picture => picture.Overlaps(bounds)).ToList();
            List<double> xs = Edges(bounds.Left, bounds.Right, obstacles.SelectMany(obstacle => new[] { obstacle.Left, obstacle.Right }));
            List<double> ys = Edges(bounds.Top, bounds.Bottom, obstacles.SelectMany(obstacle => new[] { obstacle.Top, obstacle.Bottom }));
            List<VideoGraphicRectangle> free = new List<VideoGraphicRectangle>();
            for (int left = 0; left < xs.Count; left++) {
                for (int right = left + 1; right < xs.Count; right++) {
                    if (xs[right] - xs[left] < MinimumWidth * area.FrameWidth) {
                        continue;
                    }
                    for (int top = 0; top < ys.Count; top++) {
                        for (int bottom = top + 1; bottom < ys.Count; bottom++) {
                            if (ys[bottom] - ys[top] < MinimumHeight * area.FrameHeight) {
                                continue;
                            }
                            VideoGraphicRectangle box = new VideoGraphicRectangle(xs[left], ys[top], xs[right], ys[bottom]);
                            if (!obstacles.Any(obstacle => obstacle.Overlaps(box))) {
                                free.Add(box);
                            }
                        }
                    }
                }
            }
            return free.Where(box => !free.Any(other => other != box && other.Contains(box) && !box.Contains(other)))
                .Select(box => new VideoGraphicSafeArea(area.FrameWidth, area.FrameHeight, box.Left, box.Width, box.Top, box.Bottom, (box.Top + box.Bottom) / 2))
                .ToList();
        }

        /// <summary>
        /// Collects the distinct candidate edges along one axis: the area bounds plus every obstacle edge strictly inside.
        /// </summary>
        /// <param name="low">Lower bound of the area.</param>
        /// <param name="high">Upper bound of the area.</param>
        /// <param name="edges">Obstacle edges.</param>
        /// <returns>Sorted distinct edges.</returns>
        static List<double> Edges(double low, double high, IEnumerable<double> edges) {
            return edges.Where(edge => edge > low && edge < high).Append(low).Append(high).Distinct().OrderBy(edge => edge).ToList();
        }

        /// <summary>
        /// How far a box's band is from the style's preferred vertical center; zero when the center lies inside it.
        /// </summary>
        /// <param name="area">Box.</param>
        /// <param name="preferred">Style's preferred vertical center.</param>
        /// <returns>Distance in pixels.</returns>
        static double Distance(VideoGraphicSafeArea area, double preferred) {
            return Math.Abs(Math.Clamp(preferred, area.Top, area.Bottom) - preferred);
        }
    }
}
