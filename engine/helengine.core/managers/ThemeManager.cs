namespace helengine.editor {
    /// <summary>
    /// Theme manager
    /// </summary>
    public static class ThemeManager {
        public static event EventHandler? ThemeChanged;

        public static ThemePalette Current { get; private set; } = CreateNeon90s();

        public static void SetTheme(ThemePalette palette) {
            Current = palette ?? throw new ArgumentNullException(nameof(palette));
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        public static ThemePalette CreateNeon90s() {
            var colors = new ThemeColors {
                // Deep purple background like MainWindow
                BackgroundPrimary = new byte4(25, 15, 35, 255),
                SurfacePrimary = new byte4(40, 25, 50, 255),
                SurfaceInput = new byte4(15, 15, 15, 255),

                // Tab button colors
                AccentPrimary = new byte4(194, 49, 175, 255),      // Active tab background (pink/magenta)
                AccentSecondary = new byte4(141, 49, 194, 255),     // Inactive tab background (purple)
                AccentTertiary = new byte4(68, 49, 194, 255),       // Tab border color (dark purple)
                AccentQuaternary = new byte4(204, 204, 204, 255),   // Inactive tab text (light gray)

                // State colors
                StateWarning = new byte4(255, 178, 102, 255),
                StateDanger = new byte4(255, 80, 80, 255),
                StateSuccess = new byte4(102, 255, 153, 255),

                // Input colors
                InputForegroundPrimary = new byte4(0, 255, 0, 255),      // Bright green
                InputForegroundSecondary = new byte4(255, 255, 0, 255),  // Bright yellow

                // Text colors
                TextPrimary = new byte4(25, 15, 35, 255),
                TextSecondary = new byte4(40, 25, 50, 255),
                TextOnAccent = new byte4(25, 15, 35, 255)
            };

            return new ThemePalette(colors);
        }

        public static ThemePalette CreateDarkTheme() {
            var colors = new ThemeColors {
                BackgroundPrimary = new byte4(20, 20, 20, 255),
                SurfacePrimary = new byte4(35, 35, 35, 255),
                SurfaceInput = new byte4(10, 10, 10, 255),

                AccentPrimary = new byte4(0, 123, 255, 255),        // Blue
                AccentSecondary = new byte4(108, 117, 125, 255),    // Gray
                AccentTertiary = new byte4(52, 58, 64, 255),        // Dark gray
                AccentQuaternary = new byte4(173, 181, 189, 255),   // Light gray

                StateWarning = new byte4(255, 193, 7, 255),
                StateDanger = new byte4(220, 53, 69, 255),
                StateSuccess = new byte4(40, 167, 69, 255),

                InputForegroundPrimary = new byte4(0, 255, 0, 255),
                InputForegroundSecondary = new byte4(255, 255, 0, 255),

                TextPrimary = new byte4(255, 255, 255, 255),
                TextSecondary = new byte4(173, 181, 189, 255),
                TextOnAccent = new byte4(255, 255, 255, 255)
            };

            return new ThemePalette(colors);
        }

        public static ThemePalette CreateLightTheme() {
            var colors = new ThemeColors {
                BackgroundPrimary = new byte4(248, 249, 250, 255),
                SurfacePrimary = new byte4(255, 255, 255, 255),
                SurfaceInput = new byte4(233, 236, 239, 255),

                AccentPrimary = new byte4(0, 123, 255, 255),
                AccentSecondary = new byte4(108, 117, 125, 255),
                AccentTertiary = new byte4(52, 58, 64, 255),
                AccentQuaternary = new byte4(73, 80, 87, 255),

                StateWarning = new byte4(255, 193, 7, 255),
                StateDanger = new byte4(220, 53, 69, 255),
                StateSuccess = new byte4(40, 167, 69, 255),

                InputForegroundPrimary = new byte4(33, 37, 41, 255),
                InputForegroundSecondary = new byte4(52, 58, 64, 255),

                TextPrimary = new byte4(33, 37, 41, 255),
                TextSecondary = new byte4(73, 80, 87, 255),
                TextOnAccent = new byte4(255, 255, 255, 255)
            };

            return new ThemePalette(colors);
        }

        public sealed class ThemePalette {
            public ThemeColors Colors { get; }

            public ThemePalette(ThemeColors colors) {
                Colors = colors ?? throw new ArgumentNullException(nameof(colors));
            }
        }

        public sealed class ThemeColors {
            // Background colors
            public byte4 BackgroundPrimary { get; set; }
            public byte4 SurfacePrimary { get; set; }
            public byte4 SurfaceInput { get; set; }

            // Accent colors (matching tab system)
            public byte4 AccentPrimary { get; set; }      // Active/hover states
            public byte4 AccentSecondary { get; set; }    // Normal/inactive states
            public byte4 AccentTertiary { get; set; }     // Borders/outlines
            public byte4 AccentQuaternary { get; set; }   // Inactive text/labels

            // State colors
            public byte4 StateDanger { get; set; }
            public byte4 StateWarning { get; set; }
            public byte4 StateSuccess { get; set; }

            // Input colors
            public byte4 InputForegroundPrimary { get; set; }
            public byte4 InputForegroundSecondary { get; set; }

            // Text colors
            public byte4 TextPrimary { get; set; }
            public byte4 TextSecondary { get; set; }
            public byte4 TextOnAccent { get; set; }
        }

        // Shortcuts for easy access
        public static ThemeColors Colors => Current.Colors;
    }
}
