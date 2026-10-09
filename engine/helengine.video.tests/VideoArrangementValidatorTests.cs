using System.Text.Json;

namespace helengine.video.tests {
    /// <summary>
    /// Verifies validation, schema, JSON round trip and re-planning of scene arrangements and regions.
    /// </summary>
    public class VideoArrangementValidatorTests {
        /// <summary>
        /// A stack scene with a picture in main and a graphic in top is valid.
        /// </summary>
        [Fact]
        public void Validate_ArrangedScene_IsValid() {
            Assert.Empty(VideoEditValidator.Validate(Arranged(), GraphicEditSamples.Catalog()));
        }

        /// <summary>
        /// Unknown presets, unknown regions for the preset and bad locks are errors at the offending path.
        /// </summary>
        /// <param name="mutation">Change applied to the arranged sample.</param>
        /// <param name="code">Expected diagnostic code.</param>
        /// <param name="path">Expected diagnostic path.</param>
        [Theory]
        [InlineData("unknown_preset", "unknown_arrangement", "scenes[0].arrangement.preset")]
        [InlineData("empty_preset", "unknown_arrangement", "scenes[0].arrangement.preset")]
        [InlineData("layer_region", "unknown_region", "scenes[0].layers[0].region")]
        [InlineData("overlay_region", "unknown_region", "scenes[0].overlays[0].region")]
        [InlineData("region_without_arrangement", "unknown_region", "scenes[0].overlays[0].region")]
        [InlineData("bad_lock", "invalid_lock", "scenes[0].arrangement.by")]
        public void Validate_ReportsArrangementErrors(string mutation, string code, string path) {
            VideoEdit edit = Arranged();
            VideoScene scene = edit.Scenes[0];
            switch (mutation) {
                case "unknown_preset": scene.Arrangement.Preset = "mosaic"; break;
                case "empty_preset": scene.Arrangement.Preset = ""; break;
                case "layer_region": scene.Layers[0].Region = "left"; break;
                case "overlay_region": scene.Overlays[0].Region = "band"; break;
                case "region_without_arrangement": scene.Arrangement = null; scene.Layers[0].Region = null; break;
                case "bad_lock": scene.Arrangement.By = "robot"; break;
            }

            VideoDiagnostic error = Assert.Single(VideoEditValidator.Validate(edit, GraphicEditSamples.Catalog()));

            Assert.Equal(VideoDiagnosticSeverity.Error, error.Severity);
            Assert.Equal(code, error.Code);
            Assert.Equal(path, error.Path);
        }

        /// <summary>
        /// A scene without an arrangement is the full arrangement: its main region is valid.
        /// </summary>
        [Fact]
        public void Validate_MainRegionWithoutArrangement_IsFull() {
            VideoEdit edit = Arranged();
            edit.Scenes[0].Arrangement = null;
            edit.Scenes[0].Overlays[0].Region = "main";

            Assert.Empty(VideoEditValidator.Validate(edit, GraphicEditSamples.Catalog()));
        }

        /// <summary>
        /// Two picture layers in one region are a warning, not an error: the edit still compiles.
        /// </summary>
        [Fact]
        public void Validate_TwoPicturesInOneRegion_Warns() {
            VideoEdit edit = Arranged();
            edit.Scenes[0].Layers.Add(new VideoLayer { Id = "second", Kind = "take", Fit = "cover", Region = "main" });

            VideoDiagnostic warning = Assert.Single(VideoEditValidator.Validate(edit, GraphicEditSamples.Catalog()));
            VideoCompileResult result = VideoEditCompiler.Compile(edit, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.Equal(VideoDiagnosticSeverity.Warning, warning.Severity);
            Assert.Equal("region_shared", warning.Code);
            Assert.Equal("scenes[0].layers[1].region", warning.Path);
            Assert.False(result.HasErrors);
            Assert.NotNull(result.Composition);
        }

        /// <summary>
        /// The arrangement and regions round-trip through the edit JSON in snake_case.
        /// </summary>
        [Fact]
        public void Json_RoundTripsArrangementAndRegions() {
            string json = VideoEditJson.Serialize(Arranged());

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement scene = document.RootElement.GetProperty("scenes")[0];
            Assert.Equal("stack", scene.GetProperty("arrangement").GetProperty("preset").GetString());
            Assert.Equal("main", scene.GetProperty("layers")[0].GetProperty("region").GetString());
            Assert.Equal("top", scene.GetProperty("overlays")[0].GetProperty("region").GetString());
            Assert.Equal("stack", VideoEditJson.Parse(json).Scenes[0].Arrangement.Preset);
        }

        /// <summary>
        /// The schema lists the presets and the region names as enums.
        /// </summary>
        [Fact]
        public void Schema_ListsPresetsAndRegions() {
            JsonElement scene = VideoEditSchema.Create().GetProperty("properties").GetProperty("scenes").GetProperty("items").GetProperty("properties");

            JsonElement preset = scene.GetProperty("arrangement").GetProperty("properties").GetProperty("preset");
            JsonElement region = scene.GetProperty("layers").GetProperty("items").GetProperty("properties").GetProperty("region");
            Assert.Equal(VideoArrangementPresets.Ids.Append(null), preset.GetProperty("enum").EnumerateArray().Select(value => value.ValueKind == JsonValueKind.Null ? null : value.GetString()));
            Assert.Contains("band", region.GetProperty("enum").EnumerateArray().Select(value => value.ValueKind == JsonValueKind.Null ? null : value.GetString()));
            Assert.Contains(null, region.GetProperty("enum").EnumerateArray().Select(value => value.ValueKind == JsonValueKind.Null ? null : value.GetString()));
        }

        /// <summary>
        /// A locked arrangement survives a proposal that changes it, and an unlocked one follows the proposal.
        /// </summary>
        [Fact]
        public void Replan_LockedArrangementSurvives() {
            VideoEdit current = Arranged();
            current.Scenes[0].Arrangement.By = "human";
            VideoEdit proposal = Arranged();
            proposal.Scenes[0].Arrangement = new VideoArrangement { Preset = "graphic_only" };

            Assert.Equal("stack", VideoEditMerge.Replan(current, proposal).Scenes[0].Arrangement.Preset);
            current.Scenes[0].Arrangement.By = null;
            Assert.Equal("graphic_only", VideoEditMerge.Replan(current, proposal).Scenes[0].Arrangement.Preset);
        }

        /// <summary>
        /// A scene the proposal dropped comes back when its arrangement is kept.
        /// </summary>
        [Fact]
        public void Replan_DroppedSceneWithKeptArrangementIsRestored() {
            VideoEdit current = Arranged();
            current.Scenes[0].Arrangement.By = "ai";
            VideoEdit proposal = Arranged();
            proposal.Scenes.Clear();

            VideoEdit merged = VideoEditMerge.Replan(current, proposal, [VideoEditMerge.Human, VideoEditMerge.Ai]);

            Assert.Equal("stack", Assert.Single(merged.Scenes).Arrangement.Preset);
        }

        /// <summary>
        /// Builds the portrait contrast-chain take scene arranged as a stack: the take in main, the graphic in top.
        /// </summary>
        /// <returns>Valid edit.</returns>
        static VideoEdit Arranged() {
            VideoEdit edit = GraphicEditSamples.ContrastChain();
            VideoScene scene = edit.Scenes[0];
            scene.Arrangement = new VideoArrangement { Preset = "stack" };
            scene.Layers = [new VideoLayer { Id = "take", Kind = "take", Fit = "cover", Region = "main" }];
            scene.Overlays[0].Region = "top";
            return edit;
        }
    }
}
