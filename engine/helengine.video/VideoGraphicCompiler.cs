using System.Text.Json;
using System.Text.Json.Nodes;
using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Expands one overlay graphic into composition layers: a group spanning the graphic (which fades out at the end and
    /// carries the caption lift) owning one text layer per template element instance. Items are measured and laid out in
    /// the overlay's arrangement region when it claims one (fitted, allowed to grow, centered), otherwise in
    /// the part of the safe area the scene's pictures leave free, every element is drawn centered on its own layer and moved into place with the layer position, so
    /// its pop scales around its own center, and the template tracks are resolved against the item moments (usually the
    /// spoken words of the scene take) and merged into composition animation tracks.
    /// </summary>
    public sealed class VideoGraphicCompiler {
        /// <summary>
        /// Largest growth over the style font size a graphic placed in an arrangement region may take to fill it.
        /// </summary>
        public const double RegionMaximumScale = 2;

        /// <summary>
        /// Height the text renderer's panel adds around the line box, in pixels.
        /// </summary>
        const double PanelVerticalPadding = 8;

        /// <summary>
        /// Width the text renderer's panel adds around the text advance, in pixels.
        /// </summary>
        const double PanelHorizontalPadding = 16;

        /// <summary>
        /// Shared compilation state.
        /// </summary>
        readonly VideoCompileState State;

        /// <summary>
        /// Scene span owning the overlay.
        /// </summary>
        readonly VideoSceneSpan Span;

        /// <summary>
        /// Overlay being expanded.
        /// </summary>
        readonly VideoOverlay Overlay;

        /// <summary>
        /// JSON path of the overlay, used in diagnostics.
        /// </summary>
        readonly string OverlayPath;

        /// <summary>
        /// JSON path of the overlay graphic, used in diagnostics.
        /// </summary>
        readonly string Path;

        /// <summary>
        /// Template being expanded.
        /// </summary>
        readonly GraphicTemplateAsset Template;

        /// <summary>
        /// Resolved parameter values.
        /// </summary>
        readonly VideoGraphicParameters Parameters;

        /// <summary>
        /// Overlay text style snapshot.
        /// </summary>
        readonly JsonElement Style;

        /// <summary>
        /// Absolute moment of every item, clamped into the graphic interval.
        /// </summary>
        readonly List<MediaTime> Moments = new List<MediaTime>();

        /// <summary>
        /// Separator text; empty when the template or edit draws none.
        /// </summary>
        readonly string Separator;

        /// <summary>
        /// Elements whose enabling switch is on, in declaration order.
        /// </summary>
        readonly List<GraphicTemplateElementAsset> Elements;

        /// <summary>
        /// Laid-out blocks in reading order.
        /// </summary>
        readonly List<VideoGraphicBlock> Blocks = new List<VideoGraphicBlock>();

        /// <summary>
        /// Absolute start of the graphic.
        /// </summary>
        MediaTime Start;

        /// <summary>
        /// Absolute end of the graphic.
        /// </summary>
        MediaTime End;

        /// <summary>
        /// Prepares the expansion of one graphic whose template was already resolved.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        /// <param name="span">Scene span.</param>
        /// <param name="overlay">Overlay with a graphic.</param>
        /// <param name="path">Overlay JSON path; the graphic path is derived from it.</param>
        /// <param name="template">Resolved template.</param>
        VideoGraphicCompiler(VideoCompileState state, VideoSceneSpan span, VideoOverlay overlay, string path, GraphicTemplateAsset template) {
            State = state;
            Span = span;
            Overlay = overlay;
            OverlayPath = path;
            Path = path + ".graphic";
            Template = template;
            Parameters = new VideoGraphicParameters(template, overlay.Graphic.Parameters);
            Style = VideoSceneCompiler.TextStyle(state, overlay.Style, path + ".style");
            Elements = template.Elements.Where(element => Parameters.Flag(element.EnabledParameter)).ToList();
            string separator = overlay.Graphic.Separator ?? GraphicTemplateValidator.FindSlot(template, GraphicTemplateValidator.SeparatorSlot)?.DefaultText ?? "";
            Separator = Elements.Any(element => element.Content == GraphicElementContent.SeparatorText) ? separator : "";
        }

        /// <summary>
        /// Gets the chosen arrangement once <see cref="Layout"/> ran.
        /// </summary>
        VideoGraphicArrangement Arrangement { get; set; }

        /// <summary>
        /// Expands one overlay graphic, adding its element layers to the composition.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        /// <param name="span">Scene span.</param>
        /// <param name="overlay">Overlay with a graphic.</param>
        /// <param name="path">Overlay JSON path.</param>
        /// <returns>Group layer owning the element layers, or null when nothing can be shown.</returns>
        public static VisualLayer Compile(VideoCompileState state, VideoSceneSpan span, VideoOverlay overlay, string path) {
            string graphicPath = path + ".graphic";
            VideoGraphic graphic = overlay.Graphic;
            if (state.Context.GraphicTemplates == null) {
                state.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Error, "graphic_catalog_missing", span.Scene.Id, graphicPath, "The compile context has no graphic template catalog."));
                return null;
            }
            GraphicTemplateAsset template = state.Context.GraphicTemplates.Find(graphic.Template, graphic.Version);
            if (template == null) {
                state.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Error, "unknown_graphic_template", span.Scene.Id, graphicPath + ".template", $"'{graphic.Template}' v{graphic.Version} is not in the compile context's graphic templates."));
                return null;
            }
            return new VideoGraphicCompiler(state, span, overlay, path, template).Expand();
        }

        /// <summary>
        /// Resolves timing, lays out the blocks and builds the group with its element layers.
        /// </summary>
        /// <returns>Group layer, or null when nothing can be shown.</returns>
        VisualLayer Expand() {
            if (!Time()) {
                return null;
            }
            Layout();
            VisualLayer group = new VisualLayer { Id = Span.Scene.Id + "-overlay-" + Overlay.Id, Kind = "group", MediaId = "", Order = VideoSceneCompiler.OverlayOrder, Start = Start, End = End };
            foreach (GraphicTemplateElementAsset element in Elements.OrderBy(element => element.Order)) {
                for (int index = 0; index < Overlay.Graphic.Items.Count; index++) {
                    if (!Applies(element.Repeat, index)) {
                        continue;
                    }
                    VideoGraphicBlock block = Blocks.First(candidate => candidate.ItemIndex == index && candidate.IsSeparator == (element.Content == GraphicElementContent.SeparatorText));
                    VisualLayer layer = Element(group.Id, element, index, block);
                    if (layer != null) {
                        State.Document.Layers.Add(layer);
                        group.Members.Add(layer.Id);
                    }
                }
            }
            if (group.Members.Count == 0) {
                State.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Warning, "overlay_skipped", Span.Scene.Id, Path, "No graphic item appears before the overlay ends; the graphic was skipped."));
                return null;
            }
            double duration = (End - Start).ToSeconds(), exit = Math.Min(Template.ExitSeconds, duration * 0.3);
            if (exit > 0) {
                group.Animations.Add(VideoAnimationBuilder.Ramp("opacity", End - Start - MediaTime.FromSeconds(exit), 1, End - Start, 0, "smoothstep.v1"));
            }
            return group;
        }

        /// <summary>
        /// Resolves the graphic interval and the item moments.
        /// </summary>
        /// <returns>False when the graphic ends before it starts.</returns>
        bool Time() {
            VideoGraphic graphic = Overlay.Graphic;
            for (int index = 0; index < graphic.At.Count; index++) {
                Moments.Add(State.Global(Span, graphic.At[index], $"{Path}.at[{index}]"));
            }
            bool explicitStart = Overlay.At != null && (Overlay.At.Sec.HasValue || Overlay.At.FromEnd.HasValue || Overlay.At.Fraction.HasValue || !string.IsNullOrWhiteSpace(Overlay.At.Word));
            Start = explicitStart ? State.Global(Span, Overlay.At, OverlayPath + ".at") : Moments.Min();
            End = Overlay.Until == null ? Span.End : State.Global(Span, Overlay.Until, OverlayPath + ".until");
            if (End <= Start) {
                State.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Warning, "overlay_skipped", Span.Scene.Id, OverlayPath, "The overlay ends before it starts and was skipped."));
                return false;
            }
            for (int index = 0; index < Moments.Count; index++) {
                if (Moments[index] < Start) {
                    Moments[index] = Start;
                }
            }
            return true;
        }

        /// <summary>
        /// Builds the blocks (items and separators in reading order), measures them and arranges them in the part of the
        /// safe area the scene's pictures leave free.
        /// </summary>
        void Layout() {
            List<string> items = Overlay.Graphic.Items;
            for (int index = 0; index < items.Count; index++) {
                double scale = Elements.Where(element => element.Content != GraphicElementContent.SeparatorText && Applies(element.Repeat, index)).Select(element => (double)element.FontScale).DefaultIfEmpty(1).Max();
                Blocks.Add(new VideoGraphicBlock(index, false, items[index], scale));
                if (Separator.Length > 0 && index + 1 < items.Count) {
                    double separatorScale = Elements.Where(element => element.Content == GraphicElementContent.SeparatorText).Select(element => (double)element.FontScale).Max();
                    Blocks.Add(new VideoGraphicBlock(index, true, Separator, separatorScale));
                }
            }
            IVideoTextMeasurer measurer = State.Context.TextMeasurer;
            if (measurer == null) {
                measurer = new VideoTextEstimator();
                State.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Info, "text_measure_estimated", Span.Scene.Id, Path, "No text measurer was supplied; the graphic layout uses estimated text widths."));
            }
            string requested = Overlay.Graphic.Layout ?? "auto";
            if (Overlay.Region != null) {
                VideoGraphicRectangle region = VideoArrangementPresets.Region(State.Edit, Span.Scene, Overlay.Region).Rectangle(State.Edit.Format.Width, State.Edit.Format.Height);
                Arrangement = VideoGraphicLayout.Arrange(Blocks, Template, requested, Style, measurer, VideoGraphicSafeArea.ForRegion(State.Edit, region), RegionMaximumScale);
            } else {
                Arrangement = VideoGraphicFreeArea.Arrange(Blocks, Template, requested, Style, measurer, VideoGraphicSafeArea.ForStyle(State.Edit, Style), Pictures());
            }
        }

        /// <summary>
        /// Collects the frame rectangles the scene's pictures cover: every take and visual media layer as the compositor
        /// presents it (see <see cref="VideoPictureBounds"/>), or the take filling the frame when a take scene declares no
        /// layers. Text layers and audio media do not count.
        /// </summary>
        /// <returns>Picture rectangles in output pixels.</returns>
        List<VideoGraphicRectangle> Pictures() {
            double width = State.Edit.Format.Width, height = State.Edit.Format.Height;
            List<VideoGraphicRectangle> pictures = new List<VideoGraphicRectangle>();
            VideoScene scene = Span.Scene;
            if (scene.Take != null && scene.Layers.Count == 0) {
                pictures.Add(VideoPictureBounds.Resolve(new VideoLayer { Id = "take", Kind = "take", Fit = "cover" }, State.Media[scene.Take.Media], width, height));
            }
            foreach (VideoLayer layer in scene.Layers) {
                VideoMedia media = null;
                if (layer.Kind == "take" && scene.Take != null) {
                    media = State.Media[scene.Take.Media];
                } else if (layer.Kind == "media") {
                    media = State.Media[layer.Media];
                }
                if (media != null && media.Kind != "audio") {
                    pictures.Add(VideoPictureBounds.Resolve(layer, VideoLayoutPresets.Viewport(State.Edit, scene, layer), media, width, height));
                }
            }
            return pictures;
        }

        /// <summary>
        /// Reports whether an element repeat produces an instance for an item (or the gap after it).
        /// </summary>
        /// <param name="repeat">Element repeat.</param>
        /// <param name="index">Item index.</param>
        /// <returns>True when the element is instantiated for the item.</returns>
        bool Applies(GraphicElementRepeat repeat, int index) {
            int count = Overlay.Graphic.Items.Count;
            int? accent = Overlay.Graphic.AccentItem;
            return repeat switch {
                GraphicElementRepeat.EachItem => true,
                GraphicElementRepeat.BetweenItems => Separator.Length > 0 && index + 1 < count,
                GraphicElementRepeat.AccentItem => accent == index,
                GraphicElementRepeat.EachItemExceptAccent => accent != index,
                GraphicElementRepeat.FirstItem => index == 0,
                GraphicElementRepeat.LastItem => index == count - 1,
                GraphicElementRepeat.EachItemExceptLast => index < count - 1,
                _ => false
            };
        }

        /// <summary>
        /// Builds the layer of one element instance: its text or bar style, its position and its merged animation.
        /// </summary>
        /// <param name="groupId">Owning group id, used as the layer id prefix.</param>
        /// <param name="element">Element.</param>
        /// <param name="index">Item index (for a separator, the item before it).</param>
        /// <param name="block">Laid-out block the element is drawn on.</param>
        /// <returns>Text layer, or null when the element would appear after the graphic ends.</returns>
        VisualLayer Element(string groupId, GraphicTemplateElementAsset element, int index, VideoGraphicBlock block) {
            double width = State.Edit.Format.Width, height = State.Edit.Format.Height;
            double size = Arrangement.FontSize * element.FontScale;
            double x = block.CenterX + element.OffsetX * size, y = block.CenterY + element.OffsetY * size;
            double baseX = (x - width / 2) / width, baseY = (y - height / 2) / height;
            bool bar = element.Content == GraphicElementContent.Bar;
            string text = element.Content == GraphicElementContent.SeparatorText ? Separator : Overlay.Graphic.Items[index];
            double barWidth = block.Width * element.FontScale / block.FontScale + PanelHorizontalPadding;
            Dictionary<string, List<List<VideoGraphicKeyframe>>> segments = new Dictionary<string, List<List<VideoGraphicKeyframe>>>(StringComparer.Ordinal);
            foreach (GraphicTemplateTrackAsset track in element.Tracks.Where(track => Parameters.Flag(track.EnabledParameter))) {
                if (track.Anchor == GraphicTimeAnchor.NextItem && index + 1 >= Moments.Count) {
                    continue;
                }
                MediaTime anchor = track.Anchor == GraphicTimeAnchor.NextItem ? Moments[index + 1] : Moments[index];
                foreach (string property in Properties(track.Property)) {
                    List<VideoGraphicKeyframe> keyframes = track.Keyframes.Select(keyframe => new VideoGraphicKeyframe(
                        anchor + MediaTime.FromSeconds(keyframe.OffsetSeconds),
                        Value(track.Property, property, string.IsNullOrEmpty(keyframe.ValueParameter) ? keyframe.Value : Parameters.Number(keyframe.ValueParameter), size, baseX, baseY, barWidth),
                        keyframe.Curve)).ToList();
                    if (!segments.TryGetValue(property, out List<List<VideoGraphicKeyframe>> list)) {
                        list = new List<List<VideoGraphicKeyframe>>();
                        segments.Add(property, list);
                    }
                    list.Add(keyframes);
                }
            }
            MediaTime layerStart = segments.Count == 0 ? Moments[index] : segments.Values.SelectMany(list => list).Select(keyframes => keyframes[0].Time).Min();
            if (layerStart < Start) {
                layerStart = Start;
            }
            if (layerStart >= End) {
                return null;
            }
            VisualLayer layer = new VisualLayer {
                Id = groupId + "-" + element.Name + "-" + index,
                Kind = "text",
                MediaId = "",
                Order = element.Order,
                Start = layerStart,
                End = End,
                ClipToViewport = false,
                Transform = new LayerTransform { PositionX = baseX, PositionY = baseY, ScaleY = bar ? Math.Max(0.01, element.BarThickness * size / (size * VideoTextStyles.LineHeight + PanelVerticalPadding)) : 1 },
                Text = new CompositionText { Cues = [new CompositionTextCue { Text = text, Start = layerStart, End = End }], Style = ElementStyle(element, size, bar) }
            };
            foreach (KeyValuePair<string, List<List<VideoGraphicKeyframe>>> entry in segments) {
                PropertyAnimation animation = Merge(entry.Key, entry.Value, layerStart);
                if (animation != null) {
                    layer.Animations.Add(animation);
                }
            }
            return layer;
        }

        /// <summary>
        /// Lists the composition properties a template property drives.
        /// </summary>
        /// <param name="property">Template property.</param>
        /// <returns>Composition property names.</returns>
        static string[] Properties(GraphicAnimatedProperty property) {
            return property switch {
                GraphicAnimatedProperty.Opacity => ["opacity"],
                GraphicAnimatedProperty.Scale => ["scale_x", "scale_y"],
                GraphicAnimatedProperty.OffsetX => ["position_x"],
                GraphicAnimatedProperty.OffsetY => ["position_y"],
                GraphicAnimatedProperty.Reveal => ["scale_x", "position_x"],
                _ => throw new InvalidDataException($"Unknown graphic property {property}.")
            };
        }

        /// <summary>
        /// Converts one template value into the value of one composition property.
        /// </summary>
        /// <param name="property">Template property.</param>
        /// <param name="target">Composition property.</param>
        /// <param name="value">Template value.</param>
        /// <param name="size">Element font size in pixels.</param>
        /// <param name="baseX">Laid-out horizontal position as a viewport fraction.</param>
        /// <param name="baseY">Laid-out vertical position as a viewport fraction.</param>
        /// <param name="barWidth">Width of a bar in pixels.</param>
        /// <returns>Composition value.</returns>
        double Value(GraphicAnimatedProperty property, string target, double value, double size, double baseX, double baseY, double barWidth) {
            double width = State.Edit.Format.Width, height = State.Edit.Format.Height;
            return property switch {
                GraphicAnimatedProperty.OffsetX => baseX + value * size / width,
                GraphicAnimatedProperty.OffsetY => baseY + value * size / height,
                GraphicAnimatedProperty.Reveal => target == "scale_x" ? Math.Max(0.01, value) : baseX - (1 - Math.Max(0.01, value)) * barWidth / 2 / width,
                _ => value
            };
        }

        /// <summary>
        /// Merges the segments of one property into a single track in layer time: segments are applied in time order and
        /// a later segment cuts off the keyframes of an earlier one it overlaps; keyframes before the layer start are
        /// pulled onto it and keyframes after the graphic end are dropped.
        /// </summary>
        /// <param name="property">Composition property.</param>
        /// <param name="segments">Keyframe segments in absolute time.</param>
        /// <param name="layerStart">Absolute layer start.</param>
        /// <returns>Animation track, or null when no keyframe falls inside the layer.</returns>
        PropertyAnimation Merge(string property, List<List<VideoGraphicKeyframe>> segments, MediaTime layerStart) {
            List<VideoGraphicKeyframe> merged = new List<VideoGraphicKeyframe>();
            foreach (List<VideoGraphicKeyframe> segment in segments.OrderBy(segment => segment[0].Time)) {
                merged.RemoveAll(keyframe => keyframe.Time >= segment[0].Time);
                merged.AddRange(segment);
            }
            PropertyAnimation animation = new PropertyAnimation { Property = property };
            for (int index = 0; index < merged.Count; index++) {
                VideoGraphicKeyframe keyframe = merged[index];
                if (keyframe.Time > End) {
                    break;
                }
                bool superseded = index + 1 < merged.Count && merged[index + 1].Time <= layerStart;
                if (superseded) {
                    continue;
                }
                MediaTime local = keyframe.Time < layerStart ? MediaTime.Zero : keyframe.Time - layerStart;
                if (animation.Keyframes.Count > 0 && local <= animation.Keyframes[^1].Time) {
                    continue;
                }
                animation.Keyframes.Add(new AnimationKeyframe { Time = local, Value = keyframe.Value, Curve = keyframe.Curve });
            }
            return animation.Keyframes.Count == 0 ? null : animation;
        }

        /// <summary>
        /// Derives the text style of one element layer from the overlay style: drawn at the canvas center at the element
        /// font size without wrapping or built-in caption motion, in the element color; a bar keeps only its panel.
        /// </summary>
        /// <param name="element">Element.</param>
        /// <param name="size">Element font size in pixels.</param>
        /// <param name="bar">Whether the element is a bar.</param>
        /// <returns>Style snapshot.</returns>
        JsonElement ElementStyle(GraphicTemplateElementAsset element, double size, bool bar) {
            string color = Color(element);
            Dictionary<string, JsonNode> overrides = new Dictionary<string, JsonNode>(StringComparer.Ordinal) {
                ["CenterX"] = 0.5,
                ["CenterY"] = 0.5,
                ["FontSize"] = Math.Round(size, 3),
                ["MaxWidth"] = 1.0,
                ["WordsPerLine"] = 64,
                ["Animation"] = "None",
                ["HighlightWords"] = false,
                ["TextColor"] = bar ? "#00000000" : color
            };
            if (bar) {
                overrides["PanelColor"] = color;
                overrides["OutlineWidth"] = 0;
                overrides["ShadowOffset"] = 0;
                overrides["ShadowColor"] = "#00000000";
            }
            return VideoTextStyles.With(Style, overrides);
        }

        /// <summary>
        /// Resolves the fill color of an element.
        /// </summary>
        /// <param name="element">Element.</param>
        /// <returns>Color in the renderer's notation.</returns>
        string Color(GraphicTemplateElementAsset element) {
            string highlight = VideoTextStyles.Text(Style, "HighlightColor", "#FFE600");
            return element.Color switch {
                GraphicElementColor.Highlight => highlight,
                GraphicElementColor.Parameter => Parameters.Color(element.ColorParameter),
                GraphicElementColor.Accent => Parameters.IsSet(element.ColorParameter) ? Parameters.Color(element.ColorParameter) : highlight,
                _ => VideoTextStyles.Text(Style, "TextColor", "#FFFFFF")
            };
        }
    }
}
