using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Compiles one scene into a composition group: the take, media and text layers with their layout, transform, motion,
    /// animations, effects and masks, plus the scene's overlays (plain text, graphic templates and timelines). Layers and
    /// overlays that claim a region of the scene arrangement are placed inside it; everything else keeps its frame-relative
    /// placement.
    /// </summary>
    public static class VideoSceneCompiler {
        /// <summary>
        /// Draw order of overlays inside their scene group.
        /// </summary>
        public const int OverlayOrder = 30;

        /// <summary>
        /// Adds the group and member layers of one scene.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        /// <param name="span">Scene span.</param>
        public static void Compile(VideoCompileState state, VideoSceneSpan span) {
            VideoScene scene = span.Scene;
            VisualLayer group = new VisualLayer { Id = "scene-" + scene.Id, Kind = "group", MediaId = "", Start = span.Start, End = span.End };
            state.Document.Layers.Add(group);
            state.Groups[scene.Id] = group;
            List<VideoLayer> layers = new List<VideoLayer>(scene.Layers);
            if (scene.Take != null && layers.Count == 0) {
                layers.Insert(0, new VideoLayer { Id = "take", Kind = "take", Fit = "cover" });
            }
            string path = $"scenes[{span.Index}]";
            for (int index = 0; index < layers.Count; index++) {
                string layerPath = $"{path}.layers[{index}]";
                VisualLayer front = Layer(state, span, layers[index], layerPath);
                // The background is added first so it draws under the speaker: members of equal order keep their list order.
                VisualLayer behind = layers[index].Kind == "take" ? Background(state, span, layers[index], front, layerPath) : null;
                if (behind != null) {
                    state.AddMember(span, behind);
                }
                state.AddMember(span, front);
            }
            // Simultaneous plain overlays in one place become a stacked list instead of overprinting each other.
            foreach (VideoOverlayEntry entry in VideoOverlayStacking.Arrange(state, span, path)) {
                VisualLayer overlay = Overlay(state, span, entry.Overlay, entry.Path);
                if (overlay != null) {
                    state.AddMember(span, overlay);
                }
            }
            if (scene.Entry?.Effect == "appear") {
                MediaTime ramp = MediaTime.FromSeconds(Math.Min(scene.Entry.DurationSec, span.Duration.ToSeconds()));
                group.Animations.Add(VideoAnimationBuilder.Ramp("opacity", MediaTime.Zero, 0, ramp, 1, "linear.v1"));
            }
        }

        /// <summary>
        /// Compiles one visual layer.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        /// <param name="span">Scene span.</param>
        /// <param name="source">Edit layer.</param>
        /// <param name="path">JSON path used in diagnostics.</param>
        /// <returns>Composition layer spanning the scene.</returns>
        static VisualLayer Layer(VideoCompileState state, VideoSceneSpan span, VideoLayer source, string path) {
            VideoScene scene = span.Scene;
            VisualLayer layer = new VisualLayer {
                Id = scene.Id + "-" + source.Id,
                Start = span.Start,
                End = span.End,
                Order = source.Order,
                Fit = source.Fit,
                Viewport = VideoLayoutPresets.Viewport(state.Edit, scene, source),
                Transform = Transform(source.Transform),
                PaddingColor = source.PaddingColor ?? "#00000000",
                ClipToViewport = VideoLayoutPresets.ClipsToViewport(source)
            };
            if (source.Kind == "take") {
                layer.MediaId = scene.Take.Media;
                layer.SourceIn = MediaTime.FromSeconds(scene.Take.InSec);
                layer.SourceOut = MediaTime.FromSeconds(scene.Take.OutSec);
            } else if (source.Kind == "media") {
                VideoMedia media = state.Media[source.Media];
                layer.MediaId = media.Id;
                if (media.Kind == "video") {
                    layer.SourceOut = span.Duration;
                    if (media.DurationSec > 0 && span.Duration.ToSeconds() > media.DurationSec) {
                        layer.SourceOut = MediaTime.FromSeconds(media.DurationSec);
                        layer.HoldLastFrame = true;
                    }
                }
            } else {
                layer.Kind = "text";
                layer.MediaId = "";
                layer.Text = new CompositionText { Cues = [new CompositionTextCue { Text = source.Text, Start = span.Start, End = span.End }], Style = TextStyle(state, source.TextStyle, path) };
            }
            if (source.Motion?.Preset == "zoom_to_focus") {
                Motion(state, span, source, layer, path + ".motion");
            }
            for (int index = 0; index < source.Animations.Count; index++) {
                layer.Animations.Add(VideoAnimationBuilder.Raw(state, span, source.Animations[index], $"{path}.animations[{index}]"));
            }
            foreach (VideoEffect effect in source.Effects) {
                layer.Effects.Add(Effect(effect));
            }
            if (source.Mask != null) {
                layer.Mask = new MediaMask { MediaId = source.Mask.Media, Channel = source.Mask.Channel, Inverted = source.Mask.Inverted };
            }
            return layer;
        }

        /// <summary>
        /// Applies the scene background to a take layer. When the take media has a matte, the front layer becomes the matted
        /// speaker (over the despilled foreground when one exists) and the returned layer is drawn under it: a blurred copy of
        /// the take or the replacement media. Otherwise the layer is left untouched and a diagnostic says why.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        /// <param name="span">Scene span.</param>
        /// <param name="source">Take edit layer.</param>
        /// <param name="front">Compiled take layer, matted in place.</param>
        /// <param name="path">JSON path used in diagnostics.</param>
        /// <returns>Background layer to draw under the front layer, or null when the layer keeps its original background.</returns>
        static VisualLayer Background(VideoCompileState state, VideoSceneSpan span, VideoLayer source, VisualLayer front, string path) {
            VideoScene scene = span.Scene;
            VideoSceneBackground background = scene.Background;
            if (background == null || background.Mode is not ("blur" or "replace")) {
                return null;
            }
            if (source.Mask != null) {
                state.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Warning, "background_mask_conflict", scene.Id, path + ".mask", "The layer has its own mask, so the scene background was not applied."));
                return null;
            }
            if (!state.Media.TryGetValue(scene.Take.Media, out VideoMedia take) || string.IsNullOrEmpty(take.Matte)) {
                state.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Pending, "background_matte_pending", scene.Id, $"scenes[{span.Index}].background", "The take's background needs its matte; it renders with the original background until the matte is ready."));
                return null;
            }
            VisualLayer behind;
            if (background.Mode == "blur") {
                // Compile the layer a second time so animations, motion and effects are independent copies; the repeated
                // diagnostics are dropped because the front layer already reported them.
                int diagnostics = state.Diagnostics.Count;
                string fit = source.Fit;
                source.Fit = "cover";
                try {
                    behind = Layer(state, span, source, path);
                } finally {
                    source.Fit = fit;
                    state.Diagnostics.RemoveRange(diagnostics, state.Diagnostics.Count - diagnostics);
                }
                behind.Fit = "cover";
                behind.Effects.Add(new MediaEffect { Id = "gaussian-blur", Version = 1, Parameters = new(StringComparer.Ordinal) { ["Radius"] = System.Text.Json.JsonSerializer.SerializeToElement(background.Radius) } });
            } else {
                VideoMedia replacement = state.Media[background.Media];
                behind = new VisualLayer {
                    Id = front.Id, Start = span.Start, End = span.End, Order = front.Order, Fit = "cover", MediaId = replacement.Id,
                    Viewport = front.Viewport, PaddingColor = front.PaddingColor, ClipToViewport = front.ClipToViewport
                };
                if (replacement.Kind == "video") {
                    behind.SourceOut = span.Duration;
                    if (replacement.DurationSec > 0 && span.Duration.ToSeconds() > replacement.DurationSec) {
                        behind.SourceOut = MediaTime.FromSeconds(replacement.DurationSec);
                        behind.HoldLastFrame = true;
                    }
                }
            }
            behind.Id = front.Id + "-background";
            front.Mask = new MediaMask { MediaId = take.Matte, Channel = "luma" };
            if (!string.IsNullOrEmpty(take.Foreground)) {
                front.MediaId = take.Foreground;
            }
            return behind;
        }

        /// <summary>
        /// Expands the zoom-to-focus preset into a zoom track and a focus point mapped into the layer viewport.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        /// <param name="span">Scene span.</param>
        /// <param name="source">Edit layer.</param>
        /// <param name="layer">Composition layer, modified in place.</param>
        /// <param name="path">JSON path used in diagnostics.</param>
        static void Motion(VideoCompileState state, VideoSceneSpan span, VideoLayer source, VisualLayer layer, string path) {
            VideoMotion motion = source.Motion;
            double x = 0.5, y = 0.5;
            if (motion.Focus?.Type == "point") {
                x = motion.Focus.X;
                y = motion.Focus.Y;
            } else if (motion.Focus?.Type == "text_region") {
                state.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Pending, "focus_unresolved", span.Scene.Id, path + ".focus", "The text region must be resolved into a point on the image before rendering."));
            }
            VideoMedia media = source.Kind == "media" ? state.Media[source.Media] : state.Media.GetValueOrDefault(span.Scene.Take?.Media ?? "");
            if (media != null && media.Width > 0 && media.Height > 0) {
                double width = layer.Viewport.Width * state.Edit.Format.Width, height = layer.Viewport.Height * state.Edit.Format.Height;
                double scale = layer.Fit == "cover" ? Math.Max(width / media.Width, height / media.Height) : Math.Min(width / media.Width, height / media.Height);
                x = (x * media.Width * scale + (width - media.Width * scale) / 2) / width;
                y = (y * media.Height * scale + (height - media.Height * scale) / 2) / height;
            }
            layer.Transform.FocusX = Math.Clamp(x, 0, 1);
            layer.Transform.FocusY = Math.Clamp(y, 0, 1);
            MediaTime start = VideoMomentResolver.Resolve(state.Edit, span, motion.Start, path + ".start", state.Diagnostics);
            MediaTime end = motion.End == null ? span.Duration : VideoMomentResolver.Resolve(state.Edit, span, motion.End, path + ".end", state.Diagnostics);
            if (end <= start) {
                state.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Warning, "motion_skipped", span.Scene.Id, path, "The motion ends before it starts and was skipped."));
                return;
            }
            layer.Animations.Add(VideoAnimationBuilder.Ramp("zoom", start, motion.FromScale, end, motion.ToScale, motion.Curve));
        }

        /// <summary>
        /// Compiles one overlay into a timed text layer, or a graphic or timeline overlay into a group of animated layers.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        /// <param name="span">Scene span.</param>
        /// <param name="overlay">Overlay.</param>
        /// <param name="path">JSON path used in diagnostics.</param>
        /// <returns>Text or group layer, or null when the overlay has no time to show.</returns>
        static VisualLayer Overlay(VideoCompileState state, VideoSceneSpan span, VideoOverlay overlay, string path) {
            if (overlay.Graphic != null || overlay.Timeline != null) {
                VisualLayer group = overlay.Graphic != null ? VideoGraphicCompiler.Compile(state, span, overlay, path) : VideoTimelineCompiler.Compile(state, span, overlay, path);
                if (group != null) {
                    VideoAnimationBuilder.AddLift(group, state.Edit.Tracks.Captions);
                }
                return group;
            }
            MediaTime start = state.Global(span, overlay.At, path + ".at");
            MediaTime end = overlay.Until == null ? span.End : state.Global(span, overlay.Until, path + ".until");
            if (end <= start) {
                state.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Warning, "overlay_skipped", span.Scene.Id, path, "The overlay ends before it starts and was skipped."));
                return null;
            }
            System.Text.Json.JsonElement style = TextStyle(state, overlay.Style, path);
            if (overlay.Region != null) {
                style = RegionStyle(state, span, overlay, style, path);
            }
            VisualLayer layer = new VisualLayer {
                Id = span.Scene.Id + "-overlay-" + overlay.Id, Kind = "text", MediaId = "", Order = OverlayOrder, Start = start, End = end,
                Text = new CompositionText { Cues = [new CompositionTextCue { Text = overlay.Text, Start = start, End = end }], Style = style }
            };
            VideoAnimationBuilder.AddLift(layer, state.Edit.Tracks.Captions);
            return layer;
        }

        /// <summary>
        /// Fits a plain overlay into its arrangement region (see <see cref="VideoOverlayRegionLayout"/>).
        /// </summary>
        /// <param name="state">Compilation state.</param>
        /// <param name="span">Scene span.</param>
        /// <param name="overlay">Overlay claiming a region.</param>
        /// <param name="style">Overlay text style snapshot.</param>
        /// <param name="path">JSON path used in diagnostics.</param>
        /// <returns>Style snapshot placing the text in the region.</returns>
        static System.Text.Json.JsonElement RegionStyle(VideoCompileState state, VideoSceneSpan span, VideoOverlay overlay, System.Text.Json.JsonElement style, string path) {
            IVideoTextMeasurer measurer = state.Context.TextMeasurer;
            if (measurer == null) {
                measurer = new VideoTextEstimator();
                state.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Info, "text_measure_estimated", span.Scene.Id, path, "No text measurer was supplied; the overlay is fitted to its region with estimated text widths."));
            }
            VideoGraphicRectangle region = VideoArrangementPresets.Region(state.Edit, span.Scene, overlay.Region).Rectangle(state.Edit.Format.Width, state.Edit.Format.Height);
            return VideoOverlayRegionLayout.Fit(style, overlay.Text, VideoGraphicSafeArea.ForRegion(state.Edit, region), measurer);
        }

        /// <summary>
        /// Finds the text style snapshot for a style id.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        /// <param name="style">Name of the style in the edit text styles.</param>
        /// <param name="path">JSON path used in diagnostics.</param>
        /// <returns>Style snapshot.</returns>
        public static System.Text.Json.JsonElement TextStyle(VideoCompileState state, string style, string path) {
            if (style == null || !state.Edit.TextStyles.TryGetValue(style, out System.Text.Json.JsonElement snapshot) || snapshot.ValueKind != System.Text.Json.JsonValueKind.Object) {
                state.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Error, "missing_text_style", null, path, $"Text style '{style}' is not defined in text_styles."));
                return default;
            }
            return snapshot;
        }

        /// <summary>
        /// Converts the static transform.
        /// </summary>
        /// <param name="transform">Edit transform, or null.</param>
        /// <returns>Composition transform.</returns>
        static LayerTransform Transform(VideoTransform transform) {
            if (transform == null) {
                return new LayerTransform();
            }
            return new LayerTransform { PositionX = transform.PositionX, PositionY = transform.PositionY, ScaleX = transform.ScaleX, ScaleY = transform.ScaleY, RotationDegrees = transform.RotationDeg, Opacity = transform.Opacity };
        }

        /// <summary>
        /// Converts one catalog effect.
        /// </summary>
        /// <param name="effect">Edit effect.</param>
        /// <returns>Composition effect.</returns>
        public static MediaEffect Effect(VideoEffect effect) {
            return new MediaEffect { Id = effect.Id, Version = effect.Version, Parameters = new(effect.Parameters, StringComparer.Ordinal), Inputs = new(effect.Inputs, StringComparer.Ordinal) };
        }
    }
}
