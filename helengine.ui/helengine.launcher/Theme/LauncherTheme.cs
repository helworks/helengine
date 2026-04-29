using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace helengine.editor.launcher.Theme;

static class LauncherTheme {
    public static IBrush AppBackground { get; } = Brush("#0f0d16");
    public static IBrush PanelBackground { get; } = Brush("#161320");
    public static IBrush CardBackground { get; } = Brush("#1c1728");
    public static IBrush ProjectCardHoverBackground { get; } = Brush("#221c30");
    public static IBrush Frame { get; } = Brush("#2a2633");
    public static IBrush AccentLilac { get; } = Brush("#d9c3ff");
    public static IBrush AccentLilacDeep { get; } = Brush("#b7a1f2");
    public static IBrush AccentMint { get; } = Brush("#8bf6e1");
    public static IBrush AccentRose { get; } = Brush("#f6b1d0");
    public static IBrush AccentWarm { get; } = Brush("#ffd6a5");
    public static IBrush TextPrimary { get; } = Brush("#f4f2ff");
    public static IBrush TextSecondary { get; } = Brush("#b9b5c9");
    public static IBrush TextMuted { get; } = Brush("#9b95ae");
    public static IBrush Warning { get; } = Brush("#f6d38a");
    public static IBrush Danger { get; } = Brush("#ef8fa3");
    public static IBrush InputBackground { get; } = Brush("#151322");
    public static IBrush AccentTextOnLight { get; } = Brush("#0e0b17");

    public static IBrush TopBarBackground { get; } = new LinearGradientBrush {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops = new GradientStops {
            new GradientStop(Color.Parse("#1a1728"), 0),
            new GradientStop(Color.Parse("#141022"), 0.55),
            new GradientStop(Color.Parse("#0f0d1b"), 1)
        }
    };

    static IBrush Brush(string hex) => new ImmutableSolidColorBrush(Color.Parse(hex));
}
