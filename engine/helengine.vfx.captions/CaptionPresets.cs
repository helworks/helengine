namespace helengine.vfx.captions;

/// <summary>Creates independently editable short-form caption styles.</summary>
public static class CaptionPresets {
    /// <summary>Returns fresh presets so editing one cannot mutate later selections.</summary>
    public static IReadOnlyList<CaptionStyle> Create() => new CaptionStyle[] {
        new CaptionStyle(),
        new CaptionStyle { Name = "Neon cyan", TextColor = "#FFFFFF", HighlightColor = "#00F0FF", OutlineColor = "#002A45", OutlineWidth = 9, Animation = CaptionAnimation.Bounce },
        new CaptionStyle { Name = "Pink sticker", TextColor = "#FFFFFF", HighlightColor = "#FF82D4", PanelColor = "#CC251025", OutlineWidth = 3, Animation = CaptionAnimation.Pop },
        new CaptionStyle { Name = "Two-frame comic", TextColor = "#FFF5B5", HighlightColor = "#FF8A39", OutlineWidth = 8, Animation = CaptionAnimation.TwoFrame, TextureFps = 6 },
        new CaptionStyle { Name = "Clean lower third", FontSize = 52, Uppercase = false, WordsPerLine = 8, WordsPerGroup = 12, CenterY = 0.86, OutlineWidth = 2, PanelColor = "#A6000000", Animation = CaptionAnimation.None },
        new CaptionStyle { Name = "One word pop", WordsPerLine = 1, WordsPerGroup = 1, FontSize = 100, CenterY = 0.6, Animation = CaptionAnimation.Pop }
    };
}
