using System.Globalization;

namespace helengine.baseplatform.Definitions {
    /// <summary>
    /// One video mode a console's video output really produces: the framebuffer the whole frame (3D, UI and text) renders
    /// into, its scan, the refresh the console outputs, the frame rate the desktop Play player paces to, and the display
    /// aspect the TV shows the framebuffer at. When the display aspect differs from the framebuffer's own shape, its pixels
    /// are not square (<see cref="PixelAspect"/>).
    /// </summary>
    public sealed class PlatformVideoMode {
        /// <summary>Largest accepted framebuffer width or height.</summary>
        public const int MaximumDimension = 4096;

        /// <summary>
        /// Initializes one video mode.
        /// </summary>
        /// <param name="id">Stable id stored in project settings (for example <c>ntsc-320x224p</c>).</param>
        /// <param name="width">Framebuffer width in pixels.</param>
        /// <param name="height">Framebuffer height in pixels.</param>
        /// <param name="isProgressive">True for progressive scan, false for interlaced.</param>
        /// <param name="refreshRateHz">Refresh the console outputs (59.94 NTSC, 50 PAL).</param>
        /// <param name="playerFrameRate">Frame rate the desktop player paces to (60 NTSC, 50 PAL).</param>
        /// <param name="displayAspectWidth">Width term of the aspect the display shows the whole framebuffer at (4 for 4:3).</param>
        /// <param name="displayAspectHeight">Height term of the aspect the display shows the whole framebuffer at (3 for 4:3).</param>
        /// <exception cref="ArgumentException">Thrown when the id is blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a dimension or aspect term outside 1..4096 or a non-positive or non-finite rate.</exception>
        public PlatformVideoMode(string id, int width, int height, bool isProgressive, double refreshRateHz, double playerFrameRate, int displayAspectWidth, int displayAspectHeight) {
            if (string.IsNullOrWhiteSpace(id)) {
                throw new ArgumentException("A video mode needs an id.", nameof(id));
            } else if (width < 1 || width > MaximumDimension) {
                throw new ArgumentOutOfRangeException(nameof(width), width, $"Video mode '{id}' needs a width between 1 and 4096.");
            } else if (height < 1 || height > MaximumDimension) {
                throw new ArgumentOutOfRangeException(nameof(height), height, $"Video mode '{id}' needs a height between 1 and 4096.");
            } else if (!(refreshRateHz > 0d && double.IsFinite(refreshRateHz))) {
                throw new ArgumentOutOfRangeException(nameof(refreshRateHz), refreshRateHz, $"Video mode '{id}' needs a positive, finite refresh rate.");
            } else if (!(playerFrameRate > 0d && double.IsFinite(playerFrameRate))) {
                throw new ArgumentOutOfRangeException(nameof(playerFrameRate), playerFrameRate, $"Video mode '{id}' needs a positive, finite player frame rate.");
            } else if (displayAspectWidth < 1 || displayAspectWidth > MaximumDimension) {
                throw new ArgumentOutOfRangeException(nameof(displayAspectWidth), displayAspectWidth, $"Video mode '{id}' needs a display aspect width between 1 and 4096.");
            } else if (displayAspectHeight < 1 || displayAspectHeight > MaximumDimension) {
                throw new ArgumentOutOfRangeException(nameof(displayAspectHeight), displayAspectHeight, $"Video mode '{id}' needs a display aspect height between 1 and 4096.");
            }

            Id = id;
            Width = width;
            Height = height;
            IsProgressive = isProgressive;
            RefreshRateHz = refreshRateHz;
            PlayerFrameRate = playerFrameRate;
            DisplayAspectWidth = displayAspectWidth;
            DisplayAspectHeight = displayAspectHeight;
        }

        /// <summary>Gets the stable id stored in project settings.</summary>
        public string Id { get; }

        /// <summary>Gets the framebuffer width.</summary>
        public int Width { get; }

        /// <summary>Gets the framebuffer height.</summary>
        public int Height { get; }

        /// <summary>Gets whether the mode scans progressively (false: interlaced).</summary>
        public bool IsProgressive { get; }

        /// <summary>Gets the refresh the console outputs.</summary>
        public double RefreshRateHz { get; }

        /// <summary>Gets the frame rate the desktop Play player paces to.</summary>
        public double PlayerFrameRate { get; }

        /// <summary>Gets the width term of the aspect the display shows the whole framebuffer at.</summary>
        public int DisplayAspectWidth { get; }

        /// <summary>Gets the height term of the aspect the display shows the whole framebuffer at.</summary>
        public int DisplayAspectHeight { get; }

        /// <summary>
        /// Gets the displayed width of one framebuffer pixel divided by its displayed height:
        /// <c>DisplayAspectWidth * Height / (DisplayAspectHeight * Width)</c>. It is exactly 1 when the framebuffer already
        /// has the display's shape, and 14/15 for a 640x448 framebuffer shown 4:3.
        /// </summary>
        public double PixelAspect => (double)DisplayAspectWidth * Height / ((double)DisplayAspectHeight * Width);

        /// <summary>Gets the short label shown in the Play window title, for example <c>320×224p</c>.</summary>
        public string Label => Width.ToString(CultureInfo.InvariantCulture) + "×" + Height.ToString(CultureInfo.InvariantCulture) + (IsProgressive ? "p" : "i");
    }
}
