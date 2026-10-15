using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Keeps the speaker's head inside the face zone (<see cref="VideoEdit.Framing"/>) and reports where it lands. The take
    /// media's head boxes (<see cref="VideoMediaAnalysis.Heads"/>, normalized source frame) are placed in scene time through
    /// the take segments and speed, then mapped to output pixels exactly as the renderer draws the layer: the source is
    /// fitted into the layer viewport (cover or contain), the zoom crops the fitted viewport around the focus (clamped inside
    /// it, see <see cref="PresentationTransform"/>), and position, scale and rotation move the result around the viewport
    /// center, every property evaluated from the layer's animation tracks at that instant. When the head leaves the zone the
    /// punch-in is limited (every zoom keyframe clamped to one ceiling) and the focus shifted (every focus_y keyframe moved by
    /// one offset), so the motion keeps its shape and gains no jitter; the scene gets a <c>framing_adjusted</c> warning.
    /// </summary>
    public static class VideoFaceFraming {
        /// <summary>
        /// Default highest point of the head in vertical formats, as a fraction of the output height.
        /// </summary>
        public const double DefaultFaceTop = 0.16;

        /// <summary>
        /// Default lowest point of the head in vertical formats, as a fraction of the output height.
        /// </summary>
        public const double DefaultFaceBottom = 0.70;

        /// <summary>
        /// How far, in source seconds, a head sample just outside a take segment still describes its edge (half the spacing
        /// of 10 Hz samples, rounded up).
        /// </summary>
        public const double SampleReachSec = 0.15;

        /// <summary>
        /// Most head samples the framing search checks per scene; longer scenes are thinned evenly.
        /// </summary>
        const int MaximumSamples = 60;

        /// <summary>
        /// Pixels a head may leave the zone without the motion being changed.
        /// </summary>
        const double TolerancePixels = 0.5;

        /// <summary>
        /// Zoom ceiling step of the search.
        /// </summary>
        const double ZoomStep = 0.02;

        /// <summary>
        /// Largest focus offset the search tries, in normalized source height.
        /// </summary>
        const double FocusReach = 0.5;

        /// <summary>
        /// Gets the face zone of an edit: its <see cref="VideoEdit.Framing"/> (missing edges take the defaults), or the default
        /// zone for a vertical format.
        /// </summary>
        /// <param name="edit">Edit.</param>
        /// <returns>Top and bottom as fractions of the output height, or null when the edit has no (valid) zone.</returns>
        public static (double Top, double Bottom)? Zone(VideoEdit edit) {
            if (edit?.Framing == null) {
                return edit?.Format != null && edit.Format.Height > edit.Format.Width ? (DefaultFaceTop, DefaultFaceBottom) : null;
            }
            double top = edit.Framing.FaceTop ?? DefaultFaceTop, bottom = edit.Framing.FaceBottom ?? DefaultFaceBottom;
            return double.IsFinite(top) && double.IsFinite(bottom) && top >= 0 && bottom <= 1 && top < bottom ? (top, bottom) : null;
        }

        /// <summary>
        /// Finds the take layer whose head the scene shows: its first take layer, or the implicit full-frame take of a scene
        /// with a take and no layers.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <returns>Take layer, or null when the scene shows no take.</returns>
        public static VideoLayer TakeLayer(VideoScene scene) {
            if (scene?.Take == null) {
                return null;
            }
            return scene.Layers.Count == 0 ? new VideoLayer { Id = "take", Kind = "take", Fit = "cover" } : scene.Layers.FirstOrDefault(layer => layer?.Kind == "take");
        }

        /// <summary>
        /// Keeps the head of a compiled take layer inside the face zone (see the class summary) and, when
        /// <paramref name="record"/> is set and the scene has no head track yet, records where the head lands in
        /// <see cref="VideoCompileState.Heads"/>.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        /// <param name="span">Scene span.</param>
        /// <param name="layer">Compiled take layer spanning the scene, before it is split into segments; changed in place.</param>
        /// <param name="path">JSON path used in diagnostics.</param>
        /// <param name="record">Whether this layer is the one whose head the scene shows.</param>
        /// <returns>The change made, to repeat on copies of the take (a blurred background), or null when nothing changed.</returns>
        internal static VideoFramingChange Guard(VideoCompileState state, VideoSceneSpan span, VisualLayer layer, string path, bool record) {
            VideoTake take = span.Scene.Take;
            if (take == null || !state.Media.TryGetValue(take.Media ?? "", out VideoMedia media) || media.Analysis?.Heads is not { Count: > 0 } heads || media.Width <= 0 || media.Height <= 0 || layer.Viewport == null || !layer.Viewport.IsValid() || layer.Fit is not ("cover" or "contain")) {
                return null;
            }
            List<VideoTakePiece> pieces = VideoTakeTiming.Pieces(take);
            List<(MediaTime Local, double[] Box)> samples = new List<(MediaTime, double[])>();
            foreach (double[] head in heads.Where(head => head is { Length: 5 } && head.All(double.IsFinite) && head[3] > head[1] && head[4] > head[2]).OrderBy(head => head[0])) {
                VideoTakePiece piece = VideoTakeTiming.PieceAt(pieces, head[0]) ?? pieces.FirstOrDefault(item => head[0] >= item.Segment.InSec - SampleReachSec && head[0] < item.Segment.OutSec + SampleReachSec);
                if (piece != null) {
                    samples.Add((piece.SceneTime(head[0]), FaceCore(head[1], head[2], head[3], head[4])));
                }
            }
            if (samples.Count == 0) {
                return null;
            }
            Geometry geometry = new Geometry(state.Edit.Format.Width, state.Edit.Format.Height, layer, media);
            VideoFramingChange change = null;
            if (Zone(state.Edit) is { } zone) {
                change = Search(geometry, layer, samples, zone.Top * geometry.FrameHeight, zone.Bottom * geometry.FrameHeight, out double remaining);
                if (change != null) {
                    change.Apply(layer);
                    string still = remaining > TolerancePixels ? $" The head still leaves the zone by {remaining:0} px." : "";
                    state.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Warning, "framing_adjusted", span.Scene.Id, path,
                        $"The motion was changed so the speaker's head stays between {zone.Top:0.##} and {zone.Bottom:0.##} of the frame height: zoom limited to {change.ZoomCeiling:0.##}x, focus moved by {change.FocusShift:+0.##;-0.##;0}.{still}"));
                }
            }
            if (record && !state.Heads.ContainsKey(span.Scene.Id)) {
                List<VideoHeadSample> track = new List<VideoHeadSample>();
                foreach ((MediaTime local, double[] box) in samples) {
                    if (local > span.Duration) {
                        continue;
                    }
                    VideoGraphicRectangle bounds = geometry.Project(box, Pose.At(layer, local).Resolve(null));
                    if (bounds != null) {
                        track.Add(new VideoHeadSample(span.Start + local, bounds));
                    }
                }
                state.Heads[span.Scene.Id] = track;
            }
            return change;
        }

        /// <summary>
        /// Gets the union of the head boxes on screen between two instants: the samples of every scene showing during the
        /// interval that lie inside it (widened by <see cref="SampleReachSec"/>).
        /// </summary>
        /// <param name="state">Compilation state with the head tracks.</param>
        /// <param name="start">Absolute start.</param>
        /// <param name="end">Absolute end.</param>
        /// <returns>Head box in output pixels, or null when no head is on screen.</returns>
        public static VideoGraphicRectangle Union(VideoCompileState state, MediaTime start, MediaTime end) {
            MediaTime reach = MediaTime.FromSeconds(SampleReachSec);
            VideoGraphicRectangle union = null;
            foreach (VideoSceneSpan span in state.Spans) {
                if (span.Start >= end || span.End <= start || !state.Heads.TryGetValue(span.Scene.Id, out List<VideoHeadSample> track)) {
                    continue;
                }
                foreach (VideoHeadSample sample in track.Where(sample => sample.Time >= start - reach && sample.Time <= end + reach)) {
                    VideoGraphicRectangle box = sample.Bounds;
                    union = union == null ? box : new VideoGraphicRectangle(Math.Min(union.Left, box.Left), Math.Min(union.Top, box.Top), Math.Max(union.Right, box.Right), Math.Max(union.Bottom, box.Bottom));
                }
            }
            return union;
        }

        /// <summary>
        /// Searches the largest zoom ceiling, then the focus offset nearest zero, that keep every sampled head inside the
        /// zone; when none does, the combination leaving the head least outside it. For one ceiling, how far the head rises
        /// above the zone only grows as the focus moves down and how far it sinks below only shrinks, so the offsets that
        /// work form one interval found by bisection.
        /// </summary>
        /// <param name="geometry">Layer geometry.</param>
        /// <param name="layer">Compiled take layer.</param>
        /// <param name="samples">Head samples in layer time.</param>
        /// <param name="top">Zone top in output pixels.</param>
        /// <param name="bottom">Zone bottom in output pixels.</param>
        /// <param name="remaining">Pixels the head still leaves the zone with the returned change.</param>
        /// <returns>Change to apply, or null when the motion is fine or cannot be improved.</returns>
        static VideoFramingChange Search(Geometry geometry, VisualLayer layer, List<(MediaTime Local, double[] Box)> samples, double top, double bottom, out double remaining) {
            int stride = Math.Max(1, (int)Math.Ceiling(samples.Count / (double)MaximumSamples));
            List<(Pose Pose, double[] Box)> checkedSamples = samples.Where((sample, index) => index % stride == 0 || index == samples.Count - 1).Select(sample => (Pose.At(layer, sample.Local), sample.Box)).ToList();
            // Above: worst rise of the head over the zone top; below: worst drop under the zone bottom.
            (double Above, double Below) Violation(VideoFramingChange change) {
                double above = 0, below = 0;
                foreach ((Pose pose, double[] box) in checkedSamples) {
                    VideoGraphicRectangle bounds = geometry.Project(box, pose.Resolve(change));
                    if (bounds != null) {
                        above = Math.Max(above, top - bounds.Top);
                        below = Math.Max(below, bounds.Bottom - bottom);
                    }
                }
                return (above, below);
            }
            double Cost((double Above, double Below) violation) {
                return Math.Max(0, violation.Above) + Math.Max(0, violation.Below);
            }
            remaining = Cost(Violation(null));
            double highest = checkedSamples.Max(sample => sample.Pose.HighestZoom);
            if (remaining <= TolerancePixels || highest <= 1 + 1e-9) {
                return null;
            }
            // The offset where rising above starts to outweigh sinking below, between -reach and +reach.
            double Balance(double ceiling) {
                double low = -FocusReach, high = FocusReach;
                for (int step = 0; step < 32; step++) {
                    double middle = (low + high) / 2;
                    (double above, double below) = Violation(new VideoFramingChange(ceiling, middle));
                    if (above - below < 0) {
                        low = middle;
                    } else {
                        high = middle;
                    }
                }
                return (low + high) / 2;
            }
            // The offset nearest zero, between zero and a working offset, that still works.
            double Nearest(double ceiling, double working) {
                double inside = working, outside = 0;
                for (int step = 0; step < 32; step++) {
                    double middle = (inside + outside) / 2;
                    if (Cost(Violation(new VideoFramingChange(ceiling, middle))) <= TolerancePixels) {
                        inside = middle;
                    } else {
                        outside = middle;
                    }
                }
                return inside;
            }
            VideoFramingChange best = null;
            double bestCost = remaining;
            for (double ceiling = highest; ; ceiling = Math.Max(1, Math.Round((Math.Ceiling(ceiling / ZoomStep - 1e-6) - 1) * ZoomStep, 4))) {
                VideoFramingChange still = new VideoFramingChange(ceiling, 0);
                double cost = Cost(Violation(still));
                VideoFramingChange candidate = still;
                if (cost > TolerancePixels) {
                    double balance = Balance(ceiling);
                    candidate = new VideoFramingChange(ceiling, balance);
                    cost = Cost(Violation(candidate));
                    if (cost <= TolerancePixels) {
                        candidate = new VideoFramingChange(ceiling, Math.Round(Nearest(ceiling, balance), 4));
                        cost = Cost(Violation(candidate));
                    }
                }
                if (cost <= TolerancePixels) {
                    remaining = cost;
                    return candidate;
                }
                if (cost < bestCost - TolerancePixels) {
                    best = candidate;
                    bestCost = cost;
                }
                if (ceiling <= 1) {
                    break;
                }
            }
            remaining = bestCost;
            return best;
        }

        /// <summary>
        /// The fixed geometry of one layer: frame size, viewport in pixels, fit and source size.
        /// </summary>
        sealed class Geometry {
            readonly double ViewportX, ViewportY, ViewportWidth, ViewportHeight, FittedWidth, FittedHeight, ImageX, ImageY;
            readonly bool Clip;

            public Geometry(double frameWidth, double frameHeight, VisualLayer layer, VideoMedia media) {
                FrameWidth = frameWidth;
                FrameHeight = frameHeight;
                ViewportX = layer.Viewport.X * frameWidth;
                ViewportY = layer.Viewport.Y * frameHeight;
                ViewportWidth = layer.Viewport.Width * frameWidth;
                ViewportHeight = layer.Viewport.Height * frameHeight;
                double scale = layer.Fit == "cover" ? Math.Max(ViewportWidth / media.Width, ViewportHeight / media.Height) : Math.Min(ViewportWidth / media.Width, ViewportHeight / media.Height);
                FittedWidth = media.Width * scale;
                FittedHeight = media.Height * scale;
                ImageX = (ViewportWidth - FittedWidth) / 2;
                ImageY = (ViewportHeight - FittedHeight) / 2;
                Clip = layer.ClipToViewport;
            }

            public double FrameWidth { get; }

            public double FrameHeight { get; }

            /// <summary>
            /// Maps a normalized source box to output pixels the way the layer pass draws it, clipped to the viewport when
            /// the layer clips and to the frame.
            /// </summary>
            /// <param name="box">Box [x0, y0, x1, y1] in normalized source coordinates.</param>
            /// <param name="pose">Evaluated transform.</param>
            /// <returns>Box in output pixels, or null when it is off screen.</returns>
            public VideoGraphicRectangle Project(double[] box, LayerTransform pose) {
                double zoom = Math.Max(1, pose.Zoom), focusX = Math.Clamp(pose.FocusX, 0, 1), focusY = Math.Clamp(pose.FocusY, 0, 1);
                double cropWidth = ViewportWidth / zoom, cropHeight = ViewportHeight / zoom;
                double cropX = Math.Clamp(ImageX + focusX * FittedWidth - cropWidth / 2, 0, ViewportWidth - cropWidth);
                double cropY = Math.Clamp(ImageY + focusY * FittedHeight - cropHeight / 2, 0, ViewportHeight - cropHeight);
                double angle = pose.RotationDegrees * Math.PI / 180, cosine = Math.Cos(angle), sine = Math.Sin(angle);
                double centerX = ViewportX + ViewportWidth / 2 + pose.PositionX * ViewportWidth, centerY = ViewportY + ViewportHeight / 2 + pose.PositionY * ViewportHeight;
                double left = double.MaxValue, top = double.MaxValue, right = double.MinValue, bottom = double.MinValue;
                foreach ((double u, double v) in new[] { (box[0], box[1]), (box[2], box[1]), (box[0], box[3]), (box[2], box[3]) }) {
                    // Inverse of the layer shader: local = rotate(relative) / scale + viewport / 2.
                    double localX = (ImageX + u * FittedWidth - cropX) * zoom, localY = (ImageY + v * FittedHeight - cropY) * zoom;
                    double scaledX = (localX - ViewportWidth / 2) * pose.ScaleX, scaledY = (localY - ViewportHeight / 2) * pose.ScaleY;
                    double x = centerX + cosine * scaledX - sine * scaledY, y = centerY + sine * scaledX + cosine * scaledY;
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
                double minimumX = Clip ? Math.Max(0, ViewportX) : 0, minimumY = Clip ? Math.Max(0, ViewportY) : 0;
                double maximumX = Clip ? Math.Min(FrameWidth, ViewportX + ViewportWidth) : FrameWidth, maximumY = Clip ? Math.Min(FrameHeight, ViewportY + ViewportHeight) : FrameHeight;
                left = Math.Max(left, minimumX);
                top = Math.Max(top, minimumY);
                right = Math.Min(right, maximumX);
                bottom = Math.Min(bottom, maximumY);
                return right > left && bottom > top ? new VideoGraphicRectangle(left, top, right, bottom) : null;
            }
        }

        /// <summary>
        /// The layer transform at one instant, with the zoom and focus_y kept as functions of their keyframe values so a
        /// candidate change can be evaluated without rebuilding the tracks.
        /// </summary>
        sealed class Pose {
            LayerTransform Fixed;
            Sampled ZoomValue, FocusYValue;

            /// <summary>
            /// Gets the largest zoom value the governing zoom keyframes (or the static zoom) hold.
            /// </summary>
            public double HighestZoom { get; private set; }

            /// <summary>
            /// Evaluates every animated property of a layer at a local instant, last track of a property winning as in the
            /// composition evaluator.
            /// </summary>
            /// <param name="layer">Layer.</param>
            /// <param name="local">Layer-local instant.</param>
            /// <returns>Pose.</returns>
            public static Pose At(VisualLayer layer, MediaTime local) {
                LayerTransform transform = layer.Transform?.Copy() ?? new LayerTransform();
                foreach (PropertyAnimation animation in layer.Animations.Where(Usable)) {
                    if (animation.Property is "position_x" or "position_y" or "scale_x" or "scale_y" or "rotation_deg" or "focus_x") {
                        transform.Set(animation.Property, animation.Evaluate(local));
                    }
                }
                Pose pose = new Pose { Fixed = transform };
                pose.ZoomValue = Sampled.At(layer.Animations.LastOrDefault(animation => animation.Property == "zoom" && Usable(animation)), local, transform.Zoom);
                pose.FocusYValue = Sampled.At(layer.Animations.LastOrDefault(animation => animation.Property == "focus_y" && Usable(animation)), local, transform.FocusY);
                pose.HighestZoom = pose.ZoomValue.Highest;
                return pose;
            }

            /// <summary>
            /// Resolves the transform with a candidate change applied to the zoom and focus_y keyframes.
            /// </summary>
            /// <param name="change">Change, or null for the layer as it is.</param>
            /// <returns>Transform.</returns>
            public LayerTransform Resolve(VideoFramingChange change) {
                LayerTransform transform = Fixed.Copy();
                transform.Zoom = change == null ? ZoomValue.Value(value => value) : ZoomValue.Value(change.Zoom);
                transform.FocusY = change == null ? FocusYValue.Value(value => value) : FocusYValue.Value(change.Focus);
                return transform;
            }

            /// <summary>
            /// Reports whether a track can be evaluated: keyframes in strictly increasing time with known curves.
            /// </summary>
            /// <param name="animation">Track.</param>
            /// <returns>True when usable.</returns>
            static bool Usable(PropertyAnimation animation) {
                List<AnimationKeyframe> frames = animation?.Keyframes;
                return frames is { Count: > 0 } && frames.All(frame => frame != null && frame.Time.Denominator > 0 && MediaCurve.Supports(frame.Curve)) && frames.Zip(frames.Skip(1), (left, right) => left.Time < right.Time).All(ordered => ordered);
            }
        }

        /// <summary>
        /// One property at one instant as the interpolation between two keyframe values (or a static value).
        /// </summary>
        sealed class Sampled {
            double From, To, Progress;

            public double Highest { get; private set; }

            public static Sampled At(PropertyAnimation track, MediaTime local, double fixedValue) {
                if (track == null) {
                    return new Sampled { From = fixedValue, To = fixedValue, Highest = fixedValue };
                }
                List<AnimationKeyframe> frames = track.Keyframes;
                Sampled sampled = new Sampled { From = frames[^1].Value, To = frames[^1].Value, Highest = frames.Max(frame => frame.Value) };
                if (local <= frames[0].Time) {
                    sampled.From = sampled.To = frames[0].Value;
                    return sampled;
                }
                for (int index = 1; index < frames.Count; index++) {
                    if (local <= frames[index].Time) {
                        sampled.From = frames[index - 1].Value;
                        sampled.To = frames[index].Value;
                        sampled.Progress = MediaCurve.Evaluate(frames[index - 1].Curve, (local - frames[index - 1].Time).ToSeconds() / (frames[index].Time - frames[index - 1].Time).ToSeconds());
                        break;
                    }
                }
                return sampled;
            }

            public double Value(Func<double, double> map) {
                double from = map(From), to = map(To);
                return from + (to - from) * Progress;
            }
        }
        /// <summary>
        /// The part of a measured head box that text must not cover: the face itself, from the forehead to the chin. The
        /// measured box is grown for hair and chin (+40% of the face height up, +12% down, +15% of the face width per side); this
        /// takes it back to the face with a small margin (+5% up, +3% down, +3% per side), since text over the hair or the neck
        /// is fine but not over the eyes and mouth.
        /// </summary>
        /// <param name="x0">Head box left (normalized).</param>
        /// <param name="y0">Head box top.</param>
        /// <param name="x1">Head box right.</param>
        /// <param name="y1">Head box bottom.</param>
        /// <returns>The face box [x0, y0, x1, y1].</returns>
        public static double[] FaceCore(double x0, double y0, double x1, double y1) {
            double height = y1 - y0, width = x1 - x0;
            return [x0 + width * (0.12 / 1.3), y0 + height * (0.35 / 1.52), x1 - width * (0.12 / 1.3), y1 - height * (0.09 / 1.52)];
        }
    }

    /// <summary>
    /// A framing change of a take layer: every zoom value capped at <see cref="ZoomCeiling"/> and every focus_y value moved
    /// by <see cref="FocusShift"/> (clamped to 0..1).
    /// </summary>
    public sealed class VideoFramingChange {
        /// <summary>
        /// Creates a change.
        /// </summary>
        /// <param name="zoomCeiling">Highest zoom kept, at least 1.</param>
        /// <param name="focusShift">Offset added to the focus_y values, in normalized source height.</param>
        public VideoFramingChange(double zoomCeiling, double focusShift) {
            ZoomCeiling = Math.Max(1, zoomCeiling);
            FocusShift = focusShift;
        }

        /// <summary>
        /// Gets the highest zoom kept.
        /// </summary>
        public double ZoomCeiling { get; }

        /// <summary>
        /// Gets the offset added to the focus_y values.
        /// </summary>
        public double FocusShift { get; }

        /// <summary>
        /// Maps one zoom value.
        /// </summary>
        /// <param name="value">Original value.</param>
        /// <returns>Capped value.</returns>
        public double Zoom(double value) {
            return Math.Min(value, ZoomCeiling);
        }

        /// <summary>
        /// Maps one focus_y value.
        /// </summary>
        /// <param name="value">Original value.</param>
        /// <returns>Shifted value.</returns>
        public double Focus(double value) {
            return Math.Clamp(value + FocusShift, 0, 1);
        }

        /// <summary>
        /// Applies the change to a compiled layer: its static zoom and focus and every keyframe of its zoom and focus_y tracks.
        /// </summary>
        /// <param name="layer">Layer changed in place.</param>
        public void Apply(VisualLayer layer) {
            layer.Transform ??= new LayerTransform();
            layer.Transform.Zoom = Zoom(layer.Transform.Zoom);
            layer.Transform.FocusY = Focus(layer.Transform.FocusY);
            foreach (PropertyAnimation animation in layer.Animations) {
                foreach (AnimationKeyframe frame in animation.Keyframes.Where(frame => frame != null)) {
                    if (animation.Property == "zoom") {
                        frame.Value = Zoom(frame.Value);
                    } else if (animation.Property == "focus_y") {
                        frame.Value = Focus(frame.Value);
                    }
                }
            }
        }
    }
}
