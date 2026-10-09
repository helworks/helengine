using System.Text.Json;
using System.Text.Json.Nodes;
using helengine.media;
using helengine.timeline;

namespace helengine.video {
    /// <summary>
    /// Compiles one overlay timeline into composition layers: a group spanning the overlay owning one layer per bound slot
    /// (text layer, image or video layer, or a rectangle drawn as a text panel) whose position, scale, rotation and opacity
    /// follow the flattened timeline as property animations.
    /// <para>
    /// Timing: the timeline's time 0 is the overlay <c>at</c> when given, otherwise the instant that puts its earliest
    /// mapped cue (by moment) on its moment (the scene start when no cue is mapped). Every mapped cue moves to its moment
    /// (clips anchored on it shift, never stretch). The overlay ends at <c>until</c> (or the scene end) or when the
    /// timeline ends, whichever comes first; when <c>until</c> cuts the timeline short the group fades out.
    /// </para>
    /// <para>
    /// Layout: positions are box units (x right, y down, -0.5..0.5 spans the box) of the overlay box: its arrangement region
    /// (safe from the caption band) or, without a region, the largest part of the safe area the scene's pictures leave
    /// free, at most as tall as it is wide. Text and media sizes are fractions of the box height. Every bound text is
    /// measured, the elements' extents are taken at every pose where they are visible, and the whole timeline is scaled
    /// uniformly (never enlarged, texts at most twice the style font size) so it stays inside the box.
    /// </para>
    /// </summary>
    public sealed class VideoTimelineCompiler {
        /// <summary>
        /// Font size texts are measured at before they are scaled to their final size.
        /// </summary>
        public const double ReferenceFontSize = 100;

        /// <summary>
        /// Largest growth over the style font size a timeline text may take.
        /// </summary>
        public const double MaximumTextScale = 2;

        /// <summary>
        /// Smallest font size a rectangle's panel is drawn at; thinner rectangles are scaled down from it, because glyph
        /// advances measured at tiny sizes are too coarse to size the panel precisely.
        /// </summary>
        const double PanelFontSize = 40;

        /// <summary>
        /// Opacity under which a pose does not count for the fit (elements may enter from outside the box while transparent).
        /// </summary>
        const double VisibleOpacity = 0.05;

        /// <summary>
        /// Length of the fade added when <c>until</c> cuts the timeline short, in seconds.
        /// </summary>
        const double ExitSeconds = 0.25;

        /// <summary>
        /// Smallest layer scale the composition accepts; smaller scales fade the layer out instead.
        /// </summary>
        const double MinimumScale = 0.01;

        /// <summary>
        /// Largest layer scale the composition accepts.
        /// </summary>
        const double MaximumScale = 16;

        /// <summary>
        /// Shared compilation state.
        /// </summary>
        readonly VideoCompileState State;

        /// <summary>
        /// Scene span owning the overlay.
        /// </summary>
        readonly VideoSceneSpan Span;

        /// <summary>
        /// Overlay being compiled.
        /// </summary>
        readonly VideoOverlay Overlay;

        /// <summary>
        /// JSON path of the overlay.
        /// </summary>
        readonly string OverlayPath;

        /// <summary>
        /// JSON path of the overlay timeline.
        /// </summary>
        readonly string Path;

        /// <summary>
        /// Overlay text style snapshot.
        /// </summary>
        readonly JsonElement Style;

        /// <summary>
        /// Validated timeline definition.
        /// </summary>
        readonly TimelineAsset Asset;

        /// <summary>
        /// Bound slots in draw order.
        /// </summary>
        readonly List<VideoTimelineElement> Elements = new List<VideoTimelineElement>();

        /// <summary>
        /// Text measurer: the context's renderer-backed one or the estimate.
        /// </summary>
        IVideoTextMeasurer Measurer;

        /// <summary>
        /// Flattened timeline with the cues on their moments.
        /// </summary>
        FlattenedTimeline Flat;

        /// <summary>
        /// Global instant of timeline time 0.
        /// </summary>
        MediaTime Origin;

        /// <summary>
        /// Global start of the overlay.
        /// </summary>
        MediaTime Start;

        /// <summary>
        /// Global end of the overlay.
        /// </summary>
        MediaTime End;

        /// <summary>
        /// Whether the overlay ends before the timeline does.
        /// </summary>
        bool CutShort;

        /// <summary>
        /// First visible instant in timeline seconds.
        /// </summary>
        double VisibleFrom;

        /// <summary>
        /// Last visible instant in timeline seconds.
        /// </summary>
        double VisibleTo;

        /// <summary>
        /// Chosen overlay box.
        /// </summary>
        VideoGraphicSafeArea Box;

        /// <summary>
        /// Uniform scale fitting the timeline in the box.
        /// </summary>
        double FitScale;

        /// <summary>
        /// Prepares the compilation of one overlay timeline whose definition is valid.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        /// <param name="span">Scene span.</param>
        /// <param name="overlay">Overlay with a timeline.</param>
        /// <param name="path">Overlay JSON path.</param>
        /// <param name="asset">Validated timeline definition.</param>
        VideoTimelineCompiler(VideoCompileState state, VideoSceneSpan span, VideoOverlay overlay, string path, TimelineAsset asset) {
            State = state;
            Span = span;
            Overlay = overlay;
            OverlayPath = path;
            Path = path + ".timeline";
            Asset = asset;
            Style = VideoSceneCompiler.TextStyle(state, overlay.Style, path + ".style");
        }

        /// <summary>
        /// Compiles one overlay timeline, adding its slot layers to the composition.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        /// <param name="span">Scene span.</param>
        /// <param name="overlay">Overlay with a timeline.</param>
        /// <param name="path">Overlay JSON path.</param>
        /// <returns>Group layer owning the slot layers, or null when nothing can be shown.</returns>
        public static VisualLayer Compile(VideoCompileState state, VideoSceneSpan span, VideoOverlay overlay, string path) {
            string timelinePath = path + ".timeline";
            if (!overlay.Timeline.Definition.HasValue) {
                state.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Error, "unresolved_timeline", span.Scene.Id, timelinePath, "The overlay timeline has no definition; the host must inline library timelines before compiling."));
                return null;
            }
            TimelineAsset asset = VideoTimelineValidator.Parse(overlay.Timeline.Definition.Value, out IReadOnlyList<TimelineDiagnostic> diagnostics);
            if (asset == null) {
                foreach (TimelineDiagnostic diagnostic in diagnostics) {
                    state.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Error, "invalid_timeline", span.Scene.Id, VideoTimelineValidator.DefinitionPath(timelinePath, diagnostic.Path), diagnostic.Message));
                }
                return null;
            }
            return new VideoTimelineCompiler(state, span, overlay, path, asset).Expand();
        }

        /// <summary>
        /// Resolves timing, measures and fits the elements and builds the group with its slot layers.
        /// </summary>
        /// <returns>Group layer, or null when nothing can be shown.</returns>
        VisualLayer Expand() {
            if (!Time()) {
                return null;
            }
            Measure();
            Layout();
            VisualLayer group = new VisualLayer { Id = Span.Scene.Id + "-overlay-" + Overlay.Id, Kind = "group", MediaId = "", Order = VideoSceneCompiler.OverlayOrder, Start = Start, End = End };
            foreach (VideoTimelineElement element in Elements) {
                VisualLayer layer = Layer(group.Id, element);
                if (layer != null) {
                    State.Document.Layers.Add(layer);
                    group.Members.Add(layer.Id);
                }
            }
            if (group.Members.Count == 0) {
                State.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Warning, "overlay_skipped", Span.Scene.Id, Path, "No timeline slot is active while the overlay is shown; the timeline was skipped."));
                return null;
            }
            double duration = (End - Start).ToSeconds(), exit = Math.Min(ExitSeconds, duration * 0.3);
            if (CutShort && exit > 0) {
                group.Animations.Add(VideoAnimationBuilder.Ramp("opacity", End - Start - MediaTime.FromSeconds(exit), 1, End - Start, 0, "smoothstep.v1"));
            }
            return group;
        }

        /// <summary>
        /// Places timeline time 0, moves the mapped cues to their moments, flattens the timeline and resolves the overlay
        /// interval.
        /// </summary>
        /// <returns>False when the timeline cannot be shown (reported).</returns>
        bool Time() {
            Dictionary<string, MediaTime> moments = new Dictionary<string, MediaTime>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, VideoMoment> cue in Overlay.Timeline.Cues) {
                moments[cue.Key] = State.Global(Span, cue.Value, $"{Path}.cues.{cue.Key}");
            }
            VideoMoment at = Overlay.At;
            bool explicitStart = at != null && (at.Sec.HasValue || at.FromEnd.HasValue || at.Fraction.HasValue || !string.IsNullOrWhiteSpace(at.Word));
            if (explicitStart) {
                Origin = State.Global(Span, at, OverlayPath + ".at");
            } else if (moments.Count > 0) {
                KeyValuePair<string, MediaTime> earliest = moments.OrderBy(entry => entry.Value).ThenBy(entry => entry.Key, StringComparer.Ordinal).First();
                Asset.TryGetCueTime(earliest.Key, out double authored);
                Origin = earliest.Value - MediaTime.FromSeconds(authored);
            } else {
                Origin = Span.Start;
            }
            Dictionary<string, double> cueTimes = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, MediaTime> moment in moments) {
                double seconds = (moment.Value - Origin).ToSeconds();
                if (seconds < 0) {
                    State.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Warning, "moment_clamped", Span.Scene.Id, $"{Path}.cues.{moment.Key}", "The cue moment falls before the overlay starts and was moved to the start."));
                    seconds = 0;
                }
                cueTimes[moment.Key] = seconds;
            }
            try {
                Flat = TimelineFlattener.Flatten(Asset, null, cueTimes, TimelineFlattener.DefaultBlendTolerance);
            } catch (TimelineFormatException exception) {
                foreach (TimelineDiagnostic diagnostic in exception.Diagnostics) {
                    State.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Error, "invalid_timeline", Span.Scene.Id, VideoTimelineValidator.DefinitionPath(Path, diagnostic.Path), "With the cues moved to their moments: " + diagnostic.Message));
                }
                return false;
            }
            MediaTime timelineEnd = Origin + MediaTime.FromSeconds(Flat.DurationSeconds);
            Start = Origin < Span.Start ? Span.Start : Origin;
            MediaTime until = Overlay.Until == null ? Span.End : State.Global(Span, Overlay.Until, OverlayPath + ".until");
            CutShort = until < timelineEnd;
            End = CutShort ? until : timelineEnd;
            if (End > Span.End) {
                End = Span.End;
                CutShort = true;
            }
            if (End <= Start) {
                State.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Warning, "overlay_skipped", Span.Scene.Id, Path, "The overlay timeline ends before it starts and was skipped."));
                return false;
            }
            VisibleFrom = (Start - Origin).ToSeconds();
            VisibleTo = (End - Origin).ToSeconds();
            return true;
        }

        /// <summary>
        /// Builds the elements of the bound slots in timeline order and measures their texts.
        /// </summary>
        void Measure() {
            Measurer = State.Context.TextMeasurer;
            if (Measurer == null) {
                Measurer = new VideoTextEstimator();
                State.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Info, "text_measure_estimated", Span.Scene.Id, Path, "No text measurer was supplied; the timeline layout uses estimated text widths."));
            }
            for (int index = 0; index < Asset.Slots.Count; index++) {
                TimelineSlotAsset slot = Asset.Slots[index];
                if (!Overlay.Timeline.Bindings.TryGetValue(slot.Name, out VideoTimelineBinding binding)) {
                    continue;
                }
                VideoTimelineElement element = new VideoTimelineElement {
                    Slot = slot.Name,
                    Kind = TimelineJson.SlotKindName(slot.Kind),
                    Order = index,
                    Binding = binding,
                    Motion = new VideoTimelineSlotMotion(Flat, slot.Name)
                };
                if (element.Kind == "text") {
                    string color = VideoTextStyles.Text(Style, "TextColor", "#FFFFFF");
                    if (binding.Color.HasValue) {
                        VideoTimelineColors.TryParse(binding.Color.Value, out color);
                    }
                    element.Style = TextStyle(color);
                    VideoTextExtent extent = Measurer.Measure(WithFontSize(element.Style, ReferenceFontSize), binding.Text, ReferenceFontSize);
                    element.TextWidth = extent.Width;
                    element.TextHeight = extent.Height;
                } else if (element.Kind == "media") {
                    element.Media = State.Media[binding.Media];
                } else {
                    VideoTimelineColors.TryParse(binding.Rect.Color, out string color);
                    element.Style = PanelStyle(color);
                }
                Elements.Add(element);
            }
            foreach (VideoTimelineElement element in Elements.Where(element => element.Kind == "rect" && element.Binding.Rect.Match != null)) {
                element.Match = Elements.First(candidate => candidate.Slot == element.Binding.Rect.Match);
            }
        }

        /// <summary>
        /// Chooses the overlay box and the uniform fit scale.
        /// </summary>
        void Layout() {
            List<VideoGraphicSafeArea> candidates = new List<VideoGraphicSafeArea>();
            VideoGraphicSafeArea preferred;
            if (Overlay.Region != null) {
                VideoGraphicRectangle region = VideoArrangementPresets.Region(State.Edit, Span.Scene, Overlay.Region).Rectangle(State.Edit.Format.Width, State.Edit.Format.Height);
                preferred = VideoGraphicSafeArea.ForRegion(State.Edit, region);
                candidates.Add(preferred);
            } else {
                preferred = VideoGraphicSafeArea.ForStyle(State.Edit, Style);
                List<VideoGraphicRectangle> pictures = VideoScenePictures.Collect(State, Span);
                if (pictures.Any(picture => picture.Overlaps(preferred.Bounds))) {
                    candidates.AddRange(VideoGraphicFreeArea.Find(preferred, pictures).Select(area => Band(area, preferred.CenterY)));
                }
                if (candidates.Count == 0) {
                    candidates.Add(Band(preferred, preferred.CenterY));
                }
            }
            double best = -1;
            foreach (VideoGraphicSafeArea candidate in candidates) {
                double fit = Fit(candidate), size = fit * candidate.Height;
                bool larger = size > best * 1.03;
                bool similar = !larger && size >= best * 0.97;
                if (Box == null || larger || (similar && Distance(candidate, preferred.CenterY) < Distance(Box, preferred.CenterY))) {
                    Box = candidate;
                    FitScale = fit;
                    best = size;
                }
            }
        }

        /// <summary>
        /// Limits a free area to a box at most as tall as it is wide, centered as close to the preferred height as the
        /// area allows, so box units keep sensible proportions in tall frames.
        /// </summary>
        /// <param name="area">Free area.</param>
        /// <param name="preferredY">Preferred vertical center in pixels.</param>
        /// <returns>Overlay box.</returns>
        static VideoGraphicSafeArea Band(VideoGraphicSafeArea area, double preferredY) {
            double height = Math.Min(area.Height, area.Width);
            double center = Math.Clamp(preferredY, area.Top + height / 2, area.Bottom - height / 2);
            return new VideoGraphicSafeArea(area.FrameWidth, area.FrameHeight, area.Left, area.Width, center - height / 2, center + height / 2, center);
        }

        /// <summary>
        /// Measures how far a box's center is from the preferred height.
        /// </summary>
        /// <param name="box">Candidate box.</param>
        /// <param name="preferred">Preferred vertical center in pixels.</param>
        /// <returns>Distance in pixels.</returns>
        static double Distance(VideoGraphicSafeArea box, double preferred) {
            return Math.Abs((box.Top + box.Bottom) / 2 - preferred);
        }

        /// <summary>
        /// Computes the uniform scale that keeps every visible pose of every element inside a box: the elements' rotated
        /// bounds are taken at every instant where their motion changes (and halfway between), and the scale never
        /// enlarges the timeline or grows a text beyond twice the style font size.
        /// </summary>
        /// <param name="box">Candidate box.</param>
        /// <returns>Fit scale in (0, 1].</returns>
        double Fit(VideoGraphicSafeArea box) {
            double left = 0, right = 0, top = 0, bottom = 0, font = 0;
            foreach (VideoTimelineElement element in Elements) {
                double width = element.RestWidth(box), height = element.RestHeight(box);
                if (element.Kind == "text") {
                    font = Math.Max(font, element.FontSize(box));
                }
                foreach (double time in Instants(element)) {
                    VideoTimelinePose pose = element.Motion.Pose(time);
                    if (pose.Opacity < VisibleOpacity) {
                        continue;
                    }
                    double angle = pose.Rotation * Math.PI / 180, cos = Math.Abs(Math.Cos(angle)), sin = Math.Abs(Math.Sin(angle));
                    double scaledWidth = Math.Abs(width * pose.ScaleX), scaledHeight = Math.Abs(height * pose.ScaleY);
                    double halfX = (scaledWidth * cos + scaledHeight * sin) / 2, halfY = (scaledWidth * sin + scaledHeight * cos) / 2;
                    double x = pose.X * box.Width, y = pose.Y * box.Height;
                    right = Math.Max(right, x + halfX);
                    left = Math.Max(left, halfX - x);
                    bottom = Math.Max(bottom, y + halfY);
                    top = Math.Max(top, halfY - y);
                }
            }
            double fit = 1;
            fit = Limit(fit, box.Width / 2, right);
            fit = Limit(fit, box.Width / 2, left);
            fit = Limit(fit, box.Height / 2, bottom);
            fit = Limit(fit, box.Height / 2, top);
            double style = VideoTextStyles.Number(Style, "FontSize", VideoTextStyles.DefaultFontSize);
            if (font > 0) {
                fit = Math.Min(fit, MaximumTextScale * style / font);
            }
            return Math.Max(fit, 1e-3);
        }

        /// <summary>
        /// Lowers a fit scale so an extent stays within the space available on its side of the box center.
        /// </summary>
        /// <param name="fit">Current fit scale.</param>
        /// <param name="space">Space from the box center to its edge, in pixels.</param>
        /// <param name="extent">Extent of the content on that side, in pixels.</param>
        /// <returns>Lowered fit scale.</returns>
        static double Limit(double fit, double space, double extent) {
            return extent > 1e-9 ? Math.Min(fit, space / extent) : fit;
        }

        /// <summary>
        /// Lists the instants an element's pose is checked at for the fit: its motion breakpoints, the midpoints between
        /// them and the overlay's visible edges, all inside the visible span.
        /// </summary>
        /// <param name="element">Element.</param>
        /// <returns>Instants in timeline seconds.</returns>
        List<double> Instants(VideoTimelineElement element) {
            List<double> breaks = element.Motion.Breakpoints(element.Motion.AllTracks(), true).Append(VisibleFrom).Append(VisibleTo)
                .Where(time => time >= VisibleFrom && time <= VisibleTo).Distinct().OrderBy(time => time).ToList();
            List<double> instants = new List<double>();
            for (int index = 0; index < breaks.Count; index++) {
                instants.Add(index + 1 < breaks.Count ? breaks[index] + 1e-6 : Math.Max(VisibleFrom, breaks[index] - 1e-6));
                if (index + 1 < breaks.Count) {
                    instants.Add((breaks[index] + breaks[index + 1]) / 2);
                }
            }
            return instants;
        }

        /// <summary>
        /// Builds the layer of one element over the part of the overlay where its slot is active.
        /// </summary>
        /// <param name="groupId">Owning group id, used as the layer id prefix.</param>
        /// <param name="element">Element.</param>
        /// <returns>Layer, or null when the slot is never active while the overlay is shown.</returns>
        VisualLayer Layer(string groupId, VideoTimelineElement element) {
            double from = VisibleFrom, to = VisibleTo;
            if (element.Motion.Intervals != null) {
                if (element.Motion.Intervals.Count == 0) {
                    return null;
                }
                from = Math.Max(from, element.Motion.Intervals[0].StartSeconds);
                to = Math.Min(to, element.Motion.Intervals[^1].EndSeconds);
            }
            if (to - from < 1e-6) {
                return null;
            }
            MediaTime start = Clamp(Origin + MediaTime.FromSeconds(from)), end = Clamp(Origin + MediaTime.FromSeconds(to));
            if (end <= start) {
                return null;
            }
            VisualLayer layer = new VisualLayer {
                Id = groupId + "-" + element.Slot,
                Order = element.Order,
                Start = start,
                End = end,
                ClipToViewport = false,
                Transform = new LayerTransform()
            };
            double frameWidth = State.Edit.Format.Width, frameHeight = State.Edit.Format.Height;
            double width = element.RestWidth(Box) * FitScale, height = element.RestHeight(Box) * FitScale;
            double unitX = frameWidth, unitY = frameHeight, baseScaleX = 1, baseScaleY = 1;
            bool rect = element.Kind == "rect";
            if (element.Kind == "text") {
                layer.Kind = "text";
                layer.MediaId = "";
                layer.Text = new CompositionText { Cues = [new CompositionTextCue { Text = element.Binding.Text, Start = start, End = end }], Style = WithFontSize(element.Style, Math.Round(element.FontSize(Box) * FitScale, 3)) };
            } else if (element.Kind == "media") {
                double viewportWidth = Math.Min(1, width / frameWidth), viewportHeight = Math.Min(1, height / frameHeight);
                layer.Kind = "media";
                layer.MediaId = element.Media.Id;
                layer.Fit = "contain";
                layer.Viewport = new LayerViewport { X = (1 - viewportWidth) / 2, Y = (1 - viewportHeight) / 2, Width = viewportWidth, Height = viewportHeight };
                unitX = viewportWidth * frameWidth;
                unitY = viewportHeight * frameHeight;
                if (element.Media.Kind == "video") {
                    layer.SourceOut = end - start;
                    if (element.Media.DurationSec > 0 && (end - start).ToSeconds() > element.Media.DurationSec) {
                        layer.SourceOut = MediaTime.FromSeconds(element.Media.DurationSec);
                        layer.HoldLastFrame = true;
                    }
                }
            } else {
                double fontSize = Math.Clamp((height - VideoGraphicCompiler.PanelVerticalPadding) / VideoTextStyles.LineHeight, PanelFontSize, 10 * PanelFontSize);
                JsonElement style = WithFontSize(element.Style, Math.Round(fontSize, 3));
                double advance = Measurer.Measure(style, "M", fontSize).Width;
                int count = (int)Math.Clamp(Math.Round((width - VideoGraphicCompiler.PanelHorizontalPadding) / Math.Max(1, advance)), 1, 48);
                string text = new string('M', count);
                double panelWidth = Measurer.Measure(style, text, fontSize).Width + VideoGraphicCompiler.PanelHorizontalPadding;
                double panelHeight = fontSize * VideoTextStyles.LineHeight + VideoGraphicCompiler.PanelVerticalPadding;
                baseScaleX = width / panelWidth;
                baseScaleY = height / panelHeight;
                layer.Kind = "text";
                layer.MediaId = "";
                layer.Text = new CompositionText { Cues = [new CompositionTextCue { Text = text, Start = start, End = end }], Style = style };
            }
            double centerX = Box.CenterX, centerY = (Box.Top + Box.Bottom) / 2;
            VideoTimelineSlotMotion motion = element.Motion;
            Func<double, VideoTimelinePose> pose = motion.Pose;
            AddProperty(layer, element, "position_x", from, to, 0.0005 * frameWidth / unitX, time => {
                VideoTimelinePose state = pose(time);
                double shift = rect ? (1 - state.Reveal) * width * state.ScaleX / 2 : 0;
                return (centerX + state.X * Box.Width * FitScale - shift - frameWidth / 2) / unitX;
            });
            AddProperty(layer, element, "position_y", from, to, 0.0005 * frameHeight / unitY, time => (centerY + pose(time).Y * Box.Height * FitScale - frameHeight / 2) / unitY);
            AddProperty(layer, element, "rotation_deg", from, to, 0.1, time => pose(time).Rotation);
            AddProperty(layer, element, "scale_x", from, to, 0.002, time => Math.Clamp(baseScaleX * pose(time).ScaleX * (rect ? pose(time).Reveal : 1), MinimumScale, MaximumScale));
            AddProperty(layer, element, "scale_y", from, to, 0.002, time => Math.Clamp(baseScaleY * pose(time).ScaleY, MinimumScale, MaximumScale));
            AddProperty(layer, element, "opacity", from, to, 0.004, time => {
                VideoTimelinePose state = pose(time);
                double scaleX = Math.Abs(baseScaleX * state.ScaleX * (rect ? state.Reveal : 1)), scaleY = Math.Abs(baseScaleY * state.ScaleY);
                return Math.Clamp(state.Opacity * Math.Min(1, scaleX / MinimumScale) * Math.Min(1, scaleY / MinimumScale), 0, 1);
            });
            return layer;
        }

        /// <summary>
        /// Converts one composition property of an element into its static transform value, or into a property animation
        /// when it changes while the layer is shown.
        /// </summary>
        /// <param name="layer">Layer being built.</param>
        /// <param name="element">Element.</param>
        /// <param name="property">Composition property.</param>
        /// <param name="from">Layer start in timeline seconds.</param>
        /// <param name="to">Layer end in timeline seconds.</param>
        /// <param name="tolerance">Largest accepted error in property units.</param>
        /// <param name="value">Property value at a timeline instant.</param>
        void AddProperty(VisualLayer layer, VideoTimelineElement element, string property, double from, double to, double tolerance, Func<double, double> value) {
            bool rect = element.Kind == "rect";
            List<FlattenedCurveTrack> tracks = element.Motion.Contributing(property, rect);
            if (property == "opacity") {
                tracks.AddRange(element.Motion.Contributing("scale_x", rect));
                tracks.AddRange(element.Motion.Contributing("scale_y", rect));
            }
            List<VideoTimelineSample> samples = VideoTimelineKeyframer.Build(value, tracks, element.Motion.Breakpoints(tracks, property == "opacity"), from, to, tolerance);
            layer.Transform.Set(property, samples[0].Value);
            if (samples.All(sample => Math.Abs(sample.Value - samples[0].Value) <= tolerance * 0.01)) {
                return;
            }
            MediaTime duration = layer.End - layer.Start;
            PropertyAnimation animation = new PropertyAnimation { Property = property };
            foreach (VideoTimelineSample sample in samples) {
                MediaTime local = Origin + MediaTime.FromSeconds(sample.Time) - layer.Start;
                if (local < MediaTime.Zero) {
                    local = MediaTime.Zero;
                } else if (local > duration) {
                    local = duration;
                }
                if (animation.Keyframes.Count > 0 && local <= animation.Keyframes[^1].Time) {
                    continue;
                }
                animation.Keyframes.Add(new AnimationKeyframe { Time = local, Value = sample.Value, Curve = sample.Curve });
            }
            if (animation.Keyframes.Count > 1) {
                layer.Animations.Add(animation);
            }
        }

        /// <summary>
        /// Keeps a global instant inside the overlay interval.
        /// </summary>
        /// <param name="time">Global instant.</param>
        /// <returns>Clamped instant.</returns>
        MediaTime Clamp(MediaTime time) {
            if (time < Start) {
                return Start;
            } else if (time > End) {
                return End;
            }
            return time;
        }

        /// <summary>
        /// Derives the style of a text element from the overlay style: drawn at the canvas center on one line without
        /// built-in caption motion, in the given color.
        /// </summary>
        /// <param name="color">Text color in the renderer's notation.</param>
        /// <returns>Style snapshot without its final font size.</returns>
        JsonElement TextStyle(string color) {
            Dictionary<string, JsonNode> overrides = new Dictionary<string, JsonNode>(StringComparer.Ordinal) {
                ["CenterX"] = 0.5,
                ["CenterY"] = 0.5,
                ["MaxWidth"] = 1.0,
                ["WordsPerLine"] = 64,
                ["Animation"] = "None",
                ["HighlightWords"] = false,
                ["TextColor"] = color
            };
            return VideoTextStyles.With(Style, overrides);
        }

        /// <summary>
        /// Derives the style of a rectangle drawn as a text panel: transparent glyphs without outline or shadow on a panel
        /// in the rectangle color.
        /// </summary>
        /// <param name="color">Fill color in the renderer's notation.</param>
        /// <returns>Style snapshot without its final font size.</returns>
        JsonElement PanelStyle(string color) {
            Dictionary<string, JsonNode> overrides = new Dictionary<string, JsonNode>(StringComparer.Ordinal) {
                ["CenterX"] = 0.5,
                ["CenterY"] = 0.5,
                ["MaxWidth"] = 1.0,
                ["WordsPerLine"] = 64,
                ["Animation"] = "None",
                ["HighlightWords"] = false,
                ["Uppercase"] = false,
                ["TextColor"] = "#00000000",
                ["PanelColor"] = color,
                ["OutlineWidth"] = 0,
                ["ShadowOffset"] = 0,
                ["ShadowColor"] = "#00000000"
            };
            return VideoTextStyles.With(Style, overrides);
        }

        /// <summary>
        /// Sets the font size of a style snapshot.
        /// </summary>
        /// <param name="style">Style snapshot.</param>
        /// <param name="fontSize">Font size in pixels.</param>
        /// <returns>Style snapshot with the font size.</returns>
        static JsonElement WithFontSize(JsonElement style, double fontSize) {
            return VideoTextStyles.With(style, new Dictionary<string, JsonNode>(StringComparer.Ordinal) { ["FontSize"] = fontSize });
        }
    }
}
