using System.Text.Json;
using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Keeps text off the speaker's face. Over the time an overlay or a caption cue is on screen, the head boxes of every
    /// scene showing then (<see cref="VideoCompileState.Heads"/>, a held overlay included over the later scenes it crosses)
    /// are joined into one box. An overlay whose box meets it moves, scaled down uniformly when it must, into the free band
    /// above the head; below the head is left to the captions. A caption cue meeting it moves below the head when it fits above the bottom margin, otherwise as low as that margin allows (never above the head, which is where the graphics go),
    /// never off screen. Text that finds no room stays where it is with a <c>text_covers_face</c> warning. Moves are made on
    /// the compiled layer (position and scale around the frame center, entrance lifts kept), so every overlay kind moves
    /// the same way, and the overlay placements report the moved boxes.
    /// </summary>
    public static class VideoFaceClearance {
        /// <summary>
        /// Smallest uniform scale an overlay may be shrunk to when it moves off the face.
        /// </summary>
        public const double MinimumScale = 0.5;

        /// <summary>
        /// Margin kept from the top and bottom frame edges, as a fraction of the frame height (as for graphics).
        /// </summary>
        public const double FrameMargin = 0.07;

        /// <summary>
        /// Smallest free band reported to planners, as a fraction of the frame height.
        /// </summary>
        public const double MinimumBand = 0.04;

        /// <summary>
        /// Moves every overlay placement whose box meets the head on screen during its interval (see the class summary).
        /// </summary>
        /// <param name="state">Compilation state with every scene compiled.</param>
        public static void Overlays(VideoCompileState state) {
            if (state.Heads.Count == 0) {
                return;
            }
            double width = state.Edit.Format.Width, height = state.Edit.Format.Height, gap = VideoCaptionBand.Gap * height;
            // A stacked list's overlays share one layer; it moves once, clear of the head over all their intervals.
            foreach (IGrouping<VisualLayer, VideoOverlayPlacement> group in state.OverlayPlacements.Where(placement => placement.Bounds != null).GroupBy(placement => placement.Layer).ToList()) {
                VideoOverlayPlacement first = group.First();
                VideoGraphicRectangle head = VideoFaceFraming.Union(state, group.Min(placement => placement.Start), group.Max(placement => placement.End));
                VideoGraphicRectangle bounds = first.Bounds;
                if (head == null || !bounds.Overlaps(head.Inflate(gap))) {
                    continue;
                }
                VideoGraphicRectangle keepOut = head.Inflate(gap);
                (double Top, double Bottom) chosen = default;
                double scale = 0;
                // Below the head is the captions' place: graphics and cards only move up, smaller if they must.
                foreach ((double Top, double Bottom) band in Bands(state.Edit, keepOut).Where(band => band.Bottom <= keepOut.Top + 1e-6)) {
                    double fit = Math.Min(1, (band.Bottom - band.Top) / bounds.Height);
                    bool better = fit > scale + 1e-9 || (fit >= 1 && scale >= 1 && Distance(band, bounds) < Distance(chosen, bounds));
                    if (better) {
                        chosen = band;
                        scale = fit;
                    }
                }
                if (scale < MinimumScale || !FullFrame(first.Layer)) {
                    state.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Warning, "text_covers_face", first.Span.Scene.Id, first.Path,
                        $"Overlay '{first.Overlay.Id}' covers the speaker's face and there is no room for it above the head (below it is kept for the captions)."));
                    continue;
                }
                double center = (bounds.Top + bounds.Bottom) / 2, half = bounds.Height * scale / 2;
                double target = Math.Clamp(center, chosen.Top + half, chosen.Bottom - half);
                VideoGraphicRectangle moved = Move(first.Layer, width, height, bounds, scale, target);
                foreach (VideoOverlayPlacement placement in group) {
                    placement.Bounds = moved;
                }
                if (state.OverlayBounds.ContainsKey(first.Layer.Id)) {
                    state.OverlayBounds[first.Layer.Id] = moved;
                }
                state.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Info, "text_moved_from_face", first.Span.Scene.Id, first.Path,
                    $"Overlay '{first.Overlay.Id}' was moved {(target < head.Top ? "above" : "below")} the speaker's head" + (scale < 1 ? $" and scaled to {scale:0.##}." : ".")));
            }
        }

        /// <summary>
        /// Moves one caption cue off the head on screen while it shows (see the class summary).
        /// </summary>
        /// <param name="state">Compilation state with the head tracks.</param>
        /// <param name="span">Scene span owning the cue.</param>
        /// <param name="layer">Compiled caption cue layer, moved in place.</param>
        /// <param name="text">Cue text.</param>
        /// <param name="wordsPerLine">Caption words per line.</param>
        public static void Caption(VideoCompileState state, VideoSceneSpan span, VisualLayer layer, string text, int wordsPerLine) {
            if (state.Heads.Count == 0 || layer.Text?.Style.ValueKind != JsonValueKind.Object || !FullFrame(layer)) {
                return;
            }
            VideoGraphicRectangle head = VideoFaceFraming.Union(state, layer.Start, layer.End);
            double height = state.Edit.Format.Height, gap = VideoCaptionBand.Gap * height;
            VideoGraphicRectangle bounds = TextBounds(state, layer.Text.Style, text, wordsPerLine);
            if (head == null || !bounds.Overlaps(head.Inflate(gap))) {
                return;
            }
            VideoGraphicRectangle keepOut = head.Inflate(gap);
            double half = bounds.Height / 2, center = (bounds.Top + bounds.Bottom) / 2;
            // Captions never climb over the face into the top of the frame, where the graphics and cards live: a cue moves down
            // below the head when it fits, otherwise as low as the bottom margin allows, touching the chin at worst.
            double lowest = (1 - FrameMargin) * height - half;
            double target = Math.Min(Math.Max(center, keepOut.Bottom + half), lowest);
            target = Math.Clamp(target, Math.Min(half, height / 2), Math.Max(height - half, height / 2));
            Move(layer, state.Edit.Format.Width, height, bounds, 1, target);
            if (new VideoGraphicRectangle(bounds.Left, target - half, bounds.Right, target + half).Overlaps(head)) {
                state.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Warning, "text_covers_face", span.Scene.Id, $"scenes[{span.Index}].captions",
                    $"The caption \"{text}\" touches the speaker's face: there is no room below the head, and captions stay at the bottom."));
            }
        }

        /// <summary>
        /// Gets the vertical bands free for text around a head: above it (below the top margin) and below it, above the caption
        /// band when the captions sit lower (otherwise above the bottom margin).
        /// </summary>
        /// <param name="edit">Edit supplying the format and caption style.</param>
        /// <param name="keepOut">Head box with its gap, in output pixels.</param>
        /// <returns>Bands as [top, bottom] in output pixels, top first; empty bands are left out.</returns>
        public static List<(double Top, double Bottom)> Bands(VideoEdit edit, VideoGraphicRectangle keepOut) {
            double height = edit.Format.Height, top = FrameMargin * height, bottom = (1 - FrameMargin) * height;
            VideoCaptionBand captions = VideoCaptionBand.Estimate(edit);
            if (captions != null && captions.Center * height > keepOut.Bottom) {
                bottom = Math.Min(bottom, (captions.Top - VideoCaptionBand.Gap) * height);
            }
            List<(double Top, double Bottom)> bands = [(top, Math.Min(bottom, keepOut.Top)), (Math.Max(top, keepOut.Bottom), bottom)];
            return bands.Where(band => band.Bottom - band.Top > 1e-6).ToList();
        }

        /// <summary>
        /// Estimates the box of a caption cue: its text at the style font size, wrapped to the style width and to the words
        /// per line, centered on the style placement.
        /// </summary>
        /// <param name="state">Compilation state supplying the measurer and format.</param>
        /// <param name="style">Caption style snapshot.</param>
        /// <param name="text">Cue text.</param>
        /// <param name="wordsPerLine">Words per line.</param>
        /// <returns>Box in output pixels.</returns>
        static VideoGraphicRectangle TextBounds(VideoCompileState state, JsonElement style, string text, int wordsPerLine) {
            double width = state.Edit.Format.Width, height = state.Edit.Format.Height;
            double size = VideoTextStyles.Number(style, "FontSize", VideoTextStyles.DefaultFontSize);
            double maxWidth = width * Math.Clamp(VideoTextStyles.Number(style, "MaxWidth", VideoTextStyles.DefaultMaxWidth), 0.1, 1);
            double advance = (state.Context.TextMeasurer ?? new VideoTextEstimator()).Measure(style, text ?? "", size).Width;
            int words = (text ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;
            double lines = Math.Max(1, Math.Max(Math.Ceiling(advance / maxWidth), Math.Ceiling(words / (double)Math.Max(1, wordsPerLine))));
            double boxWidth = Math.Min(advance, maxWidth), boxHeight = lines * size * VideoTextStyles.LineHeight;
            double centerX = VideoTextStyles.Number(style, "CenterX", 0.5) * width, centerY = VideoTextStyles.Number(style, "CenterY", VideoTextStyles.DefaultCenterY) * height;
            return new VideoGraphicRectangle(centerX - boxWidth / 2, centerY - boxHeight / 2, centerX + boxWidth / 2, centerY + boxHeight / 2);
        }

        /// <summary>
        /// Scales a full-frame text or group layer uniformly around the frame center and moves it so a box of it lands
        /// centered on <paramref name="target"/>; position and scale tracks (the entrance lift) keep their shape.
        /// </summary>
        /// <param name="layer">Layer moved in place.</param>
        /// <param name="width">Frame width in pixels.</param>
        /// <param name="height">Frame height in pixels.</param>
        /// <param name="bounds">Box of the layer content at rest, in output pixels.</param>
        /// <param name="scale">Uniform scale, at most 1.</param>
        /// <param name="target">Vertical center the box moves to, in output pixels.</param>
        /// <returns>Moved box.</returns>
        static VideoGraphicRectangle Move(VisualLayer layer, double width, double height, VideoGraphicRectangle bounds, double scale, double target) {
            double center = (bounds.Top + bounds.Bottom) / 2;
            double offset = (target - height / 2 - scale * (center - height / 2)) / height;
            layer.Transform ??= new LayerTransform();
            layer.Transform.PositionY += offset;
            layer.Transform.ScaleX *= scale;
            layer.Transform.ScaleY *= scale;
            foreach (PropertyAnimation animation in layer.Animations) {
                foreach (AnimationKeyframe frame in animation.Keyframes.Where(frame => frame != null)) {
                    if (animation.Property == "position_y") {
                        frame.Value += offset;
                    } else if (animation.Property is "scale_x" or "scale_y") {
                        frame.Value *= scale;
                    }
                }
            }
            double half = bounds.Height * scale / 2, left = width / 2 + scale * (bounds.Left - width / 2), right = width / 2 + scale * (bounds.Right - width / 2);
            return new VideoGraphicRectangle(left, target - half, right, target + half);
        }

        /// <summary>
        /// Reports whether a layer draws the full frame, so moving it moves its content one to one.
        /// </summary>
        /// <param name="layer">Layer.</param>
        /// <returns>True for a full-frame viewport.</returns>
        static bool FullFrame(VisualLayer layer) {
            LayerViewport viewport = layer.Viewport;
            return viewport == null || (Math.Abs(viewport.X) < 1e-9 && Math.Abs(viewport.Y) < 1e-9 && Math.Abs(viewport.Width - 1) < 1e-9 && Math.Abs(viewport.Height - 1) < 1e-9);
        }

        /// <summary>
        /// Distance a box's center must travel to reach a band.
        /// </summary>
        /// <param name="band">Band.</param>
        /// <param name="bounds">Box.</param>
        /// <returns>Pixels.</returns>
        static double Distance((double Top, double Bottom) band, VideoGraphicRectangle bounds) {
            double center = (bounds.Top + bounds.Bottom) / 2;
            return Math.Abs(Math.Clamp(center, band.Top, band.Bottom) - center);
        }
    }
}
