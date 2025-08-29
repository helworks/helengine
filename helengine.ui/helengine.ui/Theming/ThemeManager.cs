using System;
using Avalonia.Media;

namespace helengine.ui.Theming {
    public static class ThemeManager {
        public static event EventHandler? ThemeChanged;

        public static ThemePalette Current { get; private set; } = CreateNeon90s();

        public static void SetTheme(ThemePalette palette) {
            Current = palette ?? throw new ArgumentNullException(nameof(palette));
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        public static ThemePalette CreateNeon90s() {
            var colors = new ThemeColors {
                BackgroundPrimary = Color.FromRgb(25, 15, 35),
                SurfacePrimary = Color.FromRgb(40, 25, 50),
                SurfaceInput = Color.FromRgb(15, 15, 15),

                AccentPrimary = Color.FromRgb(255, 102, 204),
                AccentSecondary = Color.FromRgb(102, 255, 255),
                AccentTertiary = Color.FromRgb(102, 255, 153),
                AccentQuaternary = Color.FromRgb(255, 255, 102),

                StateWarning = Color.FromRgb(255, 178, 102),
                StateDanger = Color.FromRgb(255, 80, 80),
                StateSuccess = Color.FromRgb(102, 255, 153),

                InputForegroundPrimary = Color.FromRgb(0, 255, 0),
                InputForegroundSecondary = Color.FromRgb(255, 255, 0),

                TextPrimary = Color.FromRgb(25, 15, 35),
                TextSecondary = Color.FromRgb(40, 25, 50),
                TextOnAccent = Color.FromRgb(25, 15, 35)
            };

            return new ThemePalette(colors);
        }

        public sealed class ThemePalette {
            public ThemeColors Colors { get; }
            public ThemeBrushes Brushes { get; }

            public ThemePalette(ThemeColors colors) {
                Colors = colors;
                Brushes = new ThemeBrushes(colors);
            }
        }

        public sealed class ThemeColors {
            public Color BackgroundPrimary { get; set; }
            public Color SurfacePrimary { get; set; }
            public Color SurfaceInput { get; set; }

            public Color AccentPrimary { get; set; }
            public Color AccentSecondary { get; set; }
            public Color AccentTertiary { get; set; }
            public Color AccentQuaternary { get; set; }

            public Color StateDanger { get; set; }
            public Color StateWarning { get; set; }
            public Color StateSuccess { get; set; }

            public Color InputForegroundPrimary { get; set; }
            public Color InputForegroundSecondary { get; set; }

            public Color TextPrimary { get; set; }
            public Color TextSecondary { get; set; }
            public Color TextOnAccent { get; set; }
        }

        public sealed class ThemeBrushes {
            public IBrush BackgroundPrimary { get; }
            public IBrush SurfacePrimary { get; }
            public IBrush SurfaceInput { get; }

            public IBrush AccentPrimary { get; }
            public IBrush AccentSecondary { get; }
            public IBrush AccentTertiary { get; }
            public IBrush AccentQuaternary { get; }

            public IBrush StateDanger { get; }
            public IBrush StateWarning { get; }
            public IBrush StateSuccess { get; }

            public IBrush InputForegroundPrimary { get; }
            public IBrush InputForegroundSecondary { get; }

            public IBrush TextPrimary { get; }
            public IBrush TextSecondary { get; }
            public IBrush TextOnAccent { get; }

            public ThemeBrushes(ThemeColors c) {
                BackgroundPrimary = new SolidColorBrush(c.BackgroundPrimary);
                SurfacePrimary = new SolidColorBrush(c.SurfacePrimary);
                SurfaceInput = new SolidColorBrush(c.SurfaceInput);

                AccentPrimary = new SolidColorBrush(c.AccentPrimary);
                AccentSecondary = new SolidColorBrush(c.AccentSecondary);
                AccentTertiary = new SolidColorBrush(c.AccentTertiary);
                AccentQuaternary = new SolidColorBrush(c.AccentQuaternary);

                StateDanger = new SolidColorBrush(c.StateDanger);
                StateWarning = new SolidColorBrush(c.StateWarning);
                StateSuccess = new SolidColorBrush(c.StateSuccess);

                InputForegroundPrimary = new SolidColorBrush(c.InputForegroundPrimary);
                InputForegroundSecondary = new SolidColorBrush(c.InputForegroundSecondary);

                TextPrimary = new SolidColorBrush(c.TextPrimary);
                TextSecondary = new SolidColorBrush(c.TextSecondary);
                TextOnAccent = new SolidColorBrush(c.TextOnAccent);
            }
        }

        // Shortcuts
        public static ThemeColors Colors => Current.Colors;
        public static ThemeBrushes Brushes => Current.Brushes;
    }
}


