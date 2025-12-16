namespace helengine {
    /// <summary>
    /// Provides theme palettes and colors for UI elements.
    /// </summary>
    public static class ThemeManager {
        /// <summary>
        /// Raised when the active theme changes.
        /// </summary>
        public static event EventHandler? ThemeChanged;

        /// <summary>
        /// Gets the current theme palette.
        /// </summary>
        public static ThemePalette Current { get; private set; } = CreateNeon90s();

        /// <summary>
        /// Sets the active theme palette.
        /// </summary>
        /// <param name="palette">Theme palette to activate.</param>
        public static void SetTheme(ThemePalette palette) {
            Current = palette ?? throw new ArgumentNullException(nameof(palette));
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Creates a neon-inspired palette used as default.
        /// </summary>
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

        /// <summary>
        /// Creates a dark palette.
        /// </summary>
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

        /// <summary>
        /// Creates a light palette.
        /// </summary>
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

        /// <summary>
        /// Theme palette container.
        /// </summary>
        public sealed class ThemePalette {
            /// <summary>
            /// Gets the palette colors.
            /// </summary>
            public ThemeColors Colors { get; }

            /// <summary>
            /// Creates a palette from the provided colors.
            /// </summary>
            /// <param name="colors">Theme colors.</param>
            public ThemePalette(ThemeColors colors) {
                Colors = colors ?? throw new ArgumentNullException(nameof(colors));
            }
        }

        /// <summary>
        /// Theme color set.
        /// </summary>
        public sealed class ThemeColors {
            /// <summary>
            /// Background color for major UI surfaces.
            /// </summary>
            public byte4 BackgroundPrimary { get; set; }
            /// <summary>
            /// Secondary surface color for panels or cards.
            /// </summary>
            public byte4 SurfacePrimary { get; set; }
            /// <summary>
            /// Input background color for text boxes and fields.
            /// </summary>
            public byte4 SurfaceInput { get; set; }

            /// <summary>
            /// Accent color for active or hovered elements.
            /// </summary>
            public byte4 AccentPrimary { get; set; }
            /// <summary>
            /// Accent color for normal or inactive states.
            /// </summary>
            public byte4 AccentSecondary { get; set; }
            /// <summary>
            /// Accent color for borders and outlines.
            /// </summary>
            public byte4 AccentTertiary { get; set; }
            /// <summary>
            /// Accent color for inactive text or labels.
            /// </summary>
            public byte4 AccentQuaternary { get; set; }

            /// <summary>
            /// Color for danger or destructive actions.
            /// </summary>
            public byte4 StateDanger { get; set; }
            /// <summary>
            /// Color for warning or caution states.
            /// </summary>
            public byte4 StateWarning { get; set; }
            /// <summary>
            /// Color for success or confirmation states.
            /// </summary>
            public byte4 StateSuccess { get; set; }

            /// <summary>
            /// Primary input foreground (text) color.
            /// </summary>
            public byte4 InputForegroundPrimary { get; set; }
            /// <summary>
            /// Secondary input foreground (placeholder) color.
            /// </summary>
            public byte4 InputForegroundSecondary { get; set; }

            /// <summary>
            /// Primary text color.
            /// </summary>
            public byte4 TextPrimary { get; set; }
            /// <summary>
            /// Secondary or muted text color.
            /// </summary>
            public byte4 TextSecondary { get; set; }
            /// <summary>
            /// Text color to use on accented backgrounds.
            /// </summary>
            public byte4 TextOnAccent { get; set; }
        }

        /// <summary>
        /// Shortcuts for easy access to current theme colors.
        /// </summary>
        public static ThemeColors Colors => Current.Colors;
    }
}
