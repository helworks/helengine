using Xunit;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the Play cook result requires the queue item's graphics option values and keeps its own copy of them, so a
/// later edit of the queue item cannot change a cook outcome that was already reported.
/// </summary>
public sealed class EditorPlayCookResultTests {
    /// <summary>A null graphics option map is rejected, naming the parameter.</summary>
    [Fact]
    public void Ctor_WhenGraphicsOptionValuesNull_Throws() {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => new EditorPlayCookResult(true, "Cooked.", @"C:\content", "scene", "scenes/scene.hasset", null));

        Assert.Equal("graphicsOptionValues", exception.ParamName);
    }

    /// <summary>Changing the source map after construction leaves the reported graphics option values untouched.</summary>
    [Fact]
    public void Ctor_CopiesGraphicsOptionValues() {
        Dictionary<string, string> source = new Dictionary<string, string> { ["video-mode"] = "ntsc-640x448i" };

        EditorPlayCookResult result = new EditorPlayCookResult(true, "Cooked.", @"C:\content", "scene", "scenes/scene.hasset", source);
        source["video-mode"] = "pal-640x512i";
        source["extra"] = "value";

        Assert.Equal("ntsc-640x448i", result.GraphicsOptionValues["video-mode"]);
        Assert.False(result.GraphicsOptionValues.ContainsKey("extra"));
    }

    /// <summary>A failed cook reports an empty graphics option map, never null.</summary>
    [Fact]
    public void Failure_ReportsEmptyGraphicsOptionValues() {
        Assert.Empty(EditorPlayCookResult.Failure("Cook broke.").GraphicsOptionValues);
    }
}
