using System.Text.Json;
using helengine.media;

namespace helengine.video.tests {
    /// <summary>
    /// Verifies how overlay graphics expand into composition layers: one group per graphic owning one text layer per
    /// element instance, item timings taken from the spoken words, the vertical/horizontal choice, fit scaling, the
    /// estimate fallback, the muted copies under dimmed items and the strike bar.
    /// </summary>
    public class VideoGraphicCompilerTests {
        /// <summary>
        /// Three terms, two separators and the muted copies under the two terms that dim become seven element layers inside
        /// one group, and the result passes the engine's composition validator.
        /// </summary>
        [Fact]
        public void Compile_ContrastChain_ExpandsIntoGroupOfElementLayers() {
            VideoCompileResult result = VideoEditCompiler.Compile(GraphicEditSamples.ContrastChain(), GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.False(result.HasErrors, Describe(result));
            VisualLayer group = result.Composition.Layers.Single(layer => layer.Id == "contrast-overlay-chain");
            Assert.Equal("group", group.Kind);
            Assert.Equal(7, group.Members.Count);
            Assert.Equal(3, group.Members.Count(id => id.Contains("-item-")));
            Assert.Equal(2, group.Members.Count(id => id.Contains("-separator-")));
            Assert.Equal(2, group.Members.Count(id => id.Contains("-item_dimmed-")));
            Assert.Contains(group.Id, result.Composition.Layers.Single(layer => layer.Id == "scene-contrast").Members);
            Assert.Empty(CompositionValidator.Validate(result.Composition, GraphicEditSamples.Catalog()));
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "text_measure_estimated");
            Assert.Equal("≠", Layer(result, "separator-0").Text.Cues[0].Text);
        }

        /// <summary>
        /// Each item starts on its spoken word; separators pop just after the item before them; earlier items dim when the
        /// next item is spoken, and the graphic starts with its first item.
        /// </summary>
        [Fact]
        public void Compile_ContrastChain_TimesItemsFromWordAnchors() {
            VideoCompileResult result = VideoEditCompiler.Compile(GraphicEditSamples.ContrastChain(), GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.Equal(0.5, Layer(result, "item-0").Start.ToSeconds(), 6);
            Assert.Equal(1.5, Layer(result, "item-1").Start.ToSeconds(), 6);
            Assert.Equal(2.9, Layer(result, "item-2").Start.ToSeconds(), 6);
            Assert.Equal(0.62, Layer(result, "separator-0").Start.ToSeconds(), 6);
            Assert.Equal(0.5, result.Composition.Layers.Single(layer => layer.Id == "contrast-overlay-chain").Start.ToSeconds(), 6);
            PropertyAnimation opacity = Layer(result, "item-0").Animations.Single(animation => animation.Property == "opacity");
            Assert.Equal([0d, 0.18, 1.0, 1.25], opacity.Keyframes.Select(keyframe => Math.Round(keyframe.Time.ToSeconds(), 6)));
            Assert.Equal(0.45, opacity.Keyframes[^1].Value, 6);
            Assert.Equal("ease_out_back.v1", Layer(result, "item-1").Animations.Single(animation => animation.Property == "scale_x").Keyframes[0].Curve);
            Assert.Single(Layer(result, "item-2").Animations, animation => animation.Property == "opacity");
            Assert.Equal(2, Layer(result, "item-2").Animations.Single(animation => animation.Property == "opacity").Keyframes.Count);
        }

        /// <summary>
        /// Long terms in a 9:16 frame stack vertically, centered on the frame, in reading order with separators between.
        /// </summary>
        [Fact]
        public void Compile_LongTermsInPortrait_StackVertically() {
            VideoCompileResult result = VideoEditCompiler.Compile(GraphicEditSamples.ContrastChain(), GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            string[] names = ["item-0", "separator-0", "item-1", "separator-1", "item-2"];
            double[] y = names.Select(name => Layer(result, name).Transform.PositionY).ToArray();
            Assert.All(names, name => Assert.Equal(0, Layer(result, name).Transform.PositionX, 9));
            Assert.True(y.Zip(y.Skip(1)).All(pair => pair.First < pair.Second));
            Assert.Equal(96, Style(Layer(result, "item-1")).GetProperty("FontSize").GetDouble(), 3);
        }

        /// <summary>
        /// Short terms that fit at full size sit on one horizontal line.
        /// </summary>
        [Fact]
        public void Compile_ShortTerms_LineUpHorizontally() {
            VideoEdit edit = GraphicEditSamples.ContrastChain();
            edit.Scenes[0].Overlays[0].Graphic.Items = ["Lei", "Fato", "Voz"];

            VideoCompileResult result = VideoEditCompiler.Compile(edit, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            string[] names = ["item-0", "separator-0", "item-1", "separator-1", "item-2"];
            double[] x = names.Select(name => Layer(result, name).Transform.PositionX).ToArray();
            Assert.True(x.Zip(x.Skip(1)).All(pair => pair.First < pair.Second));
            Assert.All(["item-0", "item-1", "item-2"], name => Assert.Equal(Layer(result, "item-0").Transform.PositionY, Layer(result, name).Transform.PositionY, 9));
            Assert.Equal(0, Layer(result, "item-1").Transform.PositionX, 9);
        }

        /// <summary>
        /// An explicit vertical layout is honored even when the row would fit.
        /// </summary>
        [Fact]
        public void Compile_ExplicitVerticalLayout_IsHonored() {
            VideoEdit edit = GraphicEditSamples.ContrastChain();
            edit.Scenes[0].Overlays[0].Graphic.Items = ["Lei", "Fato", "Voto"];
            edit.Scenes[0].Overlays[0].Graphic.Layout = "vertical";

            VideoCompileResult result = VideoEditCompiler.Compile(edit, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.Equal(0, Layer(result, "item-2").Transform.PositionX, 9);
            Assert.True(Layer(result, "item-0").Transform.PositionY < Layer(result, "item-2").Transform.PositionY);
        }

        /// <summary>
        /// A term wider than the safe area shrinks every element uniformly until the widest one fits.
        /// </summary>
        [Fact]
        public void Compile_TooWideTerms_ScaleDownUniformly() {
            VideoEdit edit = GraphicEditSamples.ContrastChain();
            edit.Scenes[0].Overlays[0].Graphic.Items = ["Legalizar", "Descriminalização total", "Tratar"];

            VideoCompileResult result = VideoEditCompiler.Compile(edit, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            double size = Style(Layer(result, "item-1")).GetProperty("FontSize").GetDouble();
            Assert.InRange(size, 50, 95);
            Assert.Equal(size, Style(Layer(result, "item-0")).GetProperty("FontSize").GetDouble(), 3);
            Assert.Equal(size * 0.8, Style(Layer(result, "separator-0")).GetProperty("FontSize").GetDouble(), 1);
            Assert.True("Descriminalização total".Length * FixedAdvanceTextMeasurer.Advance * size <= 1080 * VideoTextStyles.DefaultMaxWidth);
        }

        /// <summary>
        /// Without a measurer the layout uses the estimate and says so with an info diagnostic that does not block a final render.
        /// </summary>
        [Fact]
        public void Compile_WithoutMeasurer_ReportsEstimate() {
            VideoCompileContext context = GraphicEditSamples.Context(null);
            context.Final = true;

            VideoCompileResult result = VideoEditCompiler.Compile(GraphicEditSamples.ContrastChain(), context);

            VideoDiagnostic estimate = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Code == "text_measure_estimated");
            Assert.Equal(VideoDiagnosticSeverity.Info, estimate.Severity);
            Assert.False(result.HasErrors, Describe(result));
            Assert.False(result.BlocksFinal, Describe(result));
        }

        /// <summary>
        /// The separator follows the accent color parameter when set, otherwise the style highlight color, and dimming can
        /// be switched off.
        /// </summary>
        [Fact]
        public void Compile_Parameters_DriveColorsAndDim() {
            VideoCompileResult plain = VideoEditCompiler.Compile(GraphicEditSamples.ContrastChain(), GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));
            VideoEdit edit = GraphicEditSamples.ContrastChain();
            edit.Scenes[0].Overlays[0].Graphic.Parameters["accent_color"] = JsonSerializer.SerializeToElement(new[] { 1.0, 0.0, 0.0 });
            edit.Scenes[0].Overlays[0].Graphic.Parameters["dim_previous"] = JsonSerializer.SerializeToElement(false);

            VideoCompileResult tuned = VideoEditCompiler.Compile(edit, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.Equal("#FFD400", Style(Layer(plain, "separator-0")).GetProperty("TextColor").GetString());
            Assert.Equal("#FFFF0000", Style(Layer(tuned, "separator-0")).GetProperty("TextColor").GetString());
            Assert.Equal(2, Layer(tuned, "item-0").Animations.Single(animation => animation.Property == "opacity").Keyframes.Count);
        }

        /// <summary>
        /// A dimmed item gets an opaque copy in the dim color under it from the moment the next item appears, at the same
        /// place and size and with the style's outline, while the item itself fades to the dim level; the last item gets
        /// no copy and switching dimming off removes the copies.
        /// </summary>
        [Fact]
        public void Compile_Dim_DrawsOpaqueMutedCopyUnderTheItem() {
            VideoCompileResult result = VideoEditCompiler.Compile(GraphicEditSamples.ContrastChain(), GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            VisualLayer copy = Layer(result, "item_dimmed-0");
            VisualLayer item = Layer(result, "item-0");
            Assert.Equal(1.5, copy.Start.ToSeconds(), 6);
            Assert.True(copy.Order < item.Order);
            Assert.Equal(item.Transform.PositionX, copy.Transform.PositionX, 9);
            Assert.Equal(item.Transform.PositionY, copy.Transform.PositionY, 9);
            Assert.Equal(Style(item).GetProperty("FontSize").GetDouble(), Style(copy).GetProperty("FontSize").GetDouble(), 6);
            Assert.All(copy.Animations.Single(animation => animation.Property == "opacity").Keyframes, keyframe => Assert.Equal(1, keyframe.Value, 9));
            Assert.NotEqual(Style(item).GetProperty("TextColor").GetString(), Style(copy).GetProperty("TextColor").GetString());
            Assert.StartsWith("#FF", Style(copy).GetProperty("TextColor").GetString());
            Assert.DoesNotContain(result.Composition.Layers, layer => layer.Id.EndsWith("-item_dimmed-2"));
            Assert.Equal(0.45, item.Animations.Single(animation => animation.Property == "opacity").Keyframes[^1].Value, 6);

            VideoEdit edit = GraphicEditSamples.ContrastChain();
            edit.Scenes[0].Overlays[0].Graphic.Parameters["dim_previous"] = JsonSerializer.SerializeToElement(false);
            VideoCompileResult plain = VideoEditCompiler.Compile(edit, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));
            Assert.DoesNotContain(plain.Composition.Layers, layer => layer.Id.Contains("-item_dimmed-"));
        }

        /// <summary>
        /// The strike bar reveals left to right: scale_x and a compensating position_x share keyframe times, and the bar
        /// keeps a thin static vertical scale.
        /// </summary>
        [Fact]
        public void Compile_StrikeReplace_RevealsBarFromTheLeft() {
            VideoEdit edit = GraphicEditSamples.ContrastChain();
            VideoGraphic graphic = edit.Scenes[0].Overlays[0].Graphic;
            graphic.Template = "strike_replace";
            graphic.Items = ["Legalizar", "Tratar"];
            graphic.At = [new VideoMoment { Word = "legalizar" }, new VideoMoment { Word = "tratar" }];

            VideoCompileResult result = VideoEditCompiler.Compile(edit, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.False(result.HasErrors, Describe(result));
            VisualLayer bar = Layer(result, "strike-0");
            PropertyAnimation scale = bar.Animations.Single(animation => animation.Property == "scale_x");
            PropertyAnimation position = bar.Animations.Single(animation => animation.Property == "position_x");
            Assert.Equal(scale.Keyframes.Select(keyframe => keyframe.Time), position.Keyframes.Select(keyframe => keyframe.Time));
            Assert.True(position.Keyframes[0].Value < position.Keyframes[^1].Value);
            Assert.Equal(1, scale.Keyframes[^1].Value, 6);
            Assert.InRange(bar.Transform.ScaleY, 0.01, 0.1);
            Assert.Equal(2.7, bar.Start.ToSeconds(), 6);
            Assert.Equal("#00000000", Style(bar).GetProperty("TextColor").GetString());
            Assert.Equal("#FFFF3B30", Style(bar).GetProperty("PanelColor").GetString());
        }

        /// <summary>
        /// The accent item of highlight_word gets its own larger accent layer and the other words keep the text color.
        /// </summary>
        [Fact]
        public void Compile_HighlightWord_EmphasizesTheAccentItem() {
            VideoEdit edit = GraphicEditSamples.ContrastChain();
            VideoGraphic graphic = edit.Scenes[0].Overlays[0].Graphic;
            graphic.Template = "highlight_word";
            graphic.AccentItem = 1;

            VideoCompileResult result = VideoEditCompiler.Compile(edit, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.False(result.HasErrors, Describe(result));
            Assert.Equal("#FFD400", Style(Layer(result, "accent-1")).GetProperty("TextColor").GetString());
            Assert.Equal(2, result.Composition.Layers.Count(layer => layer.Id.Contains("-word-")));
            Assert.True(Style(Layer(result, "accent-1")).GetProperty("FontSize").GetDouble() > Style(Layer(result, "word-0")).GetProperty("FontSize").GetDouble());
        }

        /// <summary>
        /// A graphic cannot compile without the template catalog in the context.
        /// </summary>
        [Fact]
        public void Compile_WithoutTemplateCatalog_ReportsError() {
            VideoCompileContext context = GraphicEditSamples.Context(new FixedAdvanceTextMeasurer());
            context.GraphicTemplates = null;

            VideoCompileResult result = VideoEditCompiler.Compile(GraphicEditSamples.ContrastChain(), context);

            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "graphic_catalog_missing" && diagnostic.Severity == VideoDiagnosticSeverity.Error);
            Assert.Null(result.Composition);
        }

        /// <summary>
        /// Finds the element layer with a name suffix such as <c>item-0</c>.
        /// </summary>
        /// <param name="result">Compile result.</param>
        /// <param name="name">Element name and index.</param>
        /// <returns>Layer.</returns>
        static VisualLayer Layer(VideoCompileResult result, string name) {
            Assert.NotNull(result.Composition);
            return result.Composition.Layers.Single(layer => layer.Id == "contrast-overlay-chain-" + name);
        }

        /// <summary>
        /// Returns the text style snapshot of a layer.
        /// </summary>
        /// <param name="layer">Text layer.</param>
        /// <returns>Style snapshot.</returns>
        static JsonElement Style(VisualLayer layer) {
            return layer.Text.Style;
        }

        /// <summary>
        /// Formats diagnostics for assertion messages.
        /// </summary>
        /// <param name="result">Compile result.</param>
        /// <returns>One line per diagnostic.</returns>
        static string Describe(VideoCompileResult result) {
            return string.Join("; ", result.Diagnostics.Select(item => item.Severity + " " + item.Code + " " + item.Path + " " + item.Message));
        }
    }
}
