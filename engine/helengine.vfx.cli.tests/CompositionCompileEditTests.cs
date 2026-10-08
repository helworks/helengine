using System.Text.Json;
using helengine.video;

namespace helengine.vfx.cli.tests;

/// <summary>
/// Checks that the composition CLI publishes graphic templates and compiles edits with graphic overlays using the
/// renderer's own text measurement.
/// </summary>
public sealed class CompositionCompileEditTests {
    /// <summary>
    /// Visible build-owned directory for the fixtures these tests write.
    /// </summary>
    const string FixtureRoot = "C:/dev/helworks/builds/helengine/graphics-tests/cli";

    /// <summary>
    /// The capability document lists every built-in graphic template with its slots, parameters and layouts.
    /// </summary>
    [Fact]
    public void CapabilitiesListGraphicTemplates() {
        using JsonDocument json = JsonDocument.Parse(CompositionCliRunner.CapabilitiesJson());

        JsonElement templates = json.RootElement.GetProperty("graphic_templates");
        Assert.Equal(["contrast_chain", "list_build", "highlight_word", "strike_replace"], templates.EnumerateArray().Select(template => template.GetProperty("id").GetString()));
        JsonElement strike = templates.EnumerateArray().Single(template => template.GetProperty("id").GetString() == "strike_replace");
        Assert.Equal("color", strike.GetProperty("parameters").GetProperty("strike_color").GetProperty("type").GetString());
        Assert.Contains("ease_out_back.v1", json.RootElement.GetProperty("curves").EnumerateArray().Select(curve => curve.GetString()));
    }

    /// <summary>
    /// compile-edit writes a composition whose graphic layers were measured (no estimate diagnostic) and reports success.
    /// </summary>
    [Fact]
    public void CompileEditWritesMeasuredComposition() {
        string directory = Path.Combine(FixtureRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        VideoEdit edit = new VideoEdit {
            Id = "cli-graphic",
            Format = new VideoFormat { Width = 540, Height = 960, BackgroundColor = "#000000FF" },
            TextStyles = new(StringComparer.Ordinal) { ["graphic"] = JsonSerializer.SerializeToElement(new { FontFamily = "Arial", FontSize = 48, CenterY = 0.4 }) },
            Scenes = [
                new VideoScene {
                    Id = "s", Duration = new VideoSceneDuration { Mode = "fixed", Sec = 3 },
                    Overlays = [new VideoOverlay { Id = "g", Text = "A ≠ B", Graphic = new VideoGraphic { Template = "contrast_chain", Items = ["Legalizar", "Tratar"], At = [new VideoMoment { Sec = 0.2 }, new VideoMoment { Sec = 1 }] } }]
                }
            ]
        };
        string input = Path.Combine(directory, "edit.json"), output = Path.Combine(directory, "composition.json");
        File.WriteAllText(input, VideoEditJson.Serialize(edit));

        int exit = VfxCliRunner.Run(["composition", "compile-edit", "--input", input, "--assets-root", directory, "--out", output]);

        Assert.Equal(0, exit);
        string composition = File.ReadAllText(output);
        Assert.Contains("s-overlay-g-item-1", composition);
        Assert.Contains("s-overlay-g-separator-0", composition);
    }

    /// <summary>
    /// An edit naming an unknown template fails with a nonzero exit and writes nothing.
    /// </summary>
    [Fact]
    public void CompileEditRejectsUnknownTemplate() {
        string directory = Path.Combine(FixtureRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        VideoEdit edit = new VideoEdit {
            Id = "cli-graphic-bad",
            Format = new VideoFormat { Width = 540, Height = 960 },
            TextStyles = new(StringComparer.Ordinal) { ["graphic"] = JsonSerializer.SerializeToElement(new { FontFamily = "Arial" }) },
            Scenes = [
                new VideoScene {
                    Id = "s", Duration = new VideoSceneDuration { Mode = "fixed", Sec = 3 },
                    Overlays = [new VideoOverlay { Id = "g", Text = "x", Graphic = new VideoGraphic { Template = "spinning_cube", Items = ["x"], At = [new VideoMoment { Sec = 0.2 }] } }]
                }
            ]
        };
        string input = Path.Combine(directory, "edit.json"), output = Path.Combine(directory, "composition.json");
        File.WriteAllText(input, VideoEditJson.Serialize(edit));

        Assert.Equal(1, VfxCliRunner.Run(["composition", "compile-edit", "--input", input, "--assets-root", directory, "--out", output]));
        Assert.False(File.Exists(output));
    }
}
