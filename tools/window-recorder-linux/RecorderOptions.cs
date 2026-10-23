using System.Globalization;
using helengine.media.linux;

namespace helengine.tools.windowrecorderlinux;

/// <summary>Parses only the owned-window identity, fixed output destination and duration needed by the Linux recorder.</summary>
public sealed class RecorderOptions {
    /// <summary>Identifies the supplied X11 client window; zero and implicit desktop capture are forbidden.</summary>
    public ulong WindowId { get; init; }
    /// <summary>Requires the window's declared process identity to match the caller's expected process.</summary>
    public int ProcessId { get; init; }
    /// <summary>Names the exclusive final menu.mp4 file; its fixed partial is managed by the encoder.</summary>
    public string Output { get; init; }
    /// <summary>Selects a trusted installed FFmpeg executable without installing or downloading software.</summary>
    public string Ffmpeg { get; init; }
    /// <summary>Selects one known software or hardware encoder explicitly; preflight failure never triggers fallback.</summary>
    public string Encoder { get; init; }
    /// <summary>Counts the exact CFR60 output frames, with thirty seconds as the standard invocation.</summary>
    public long FrameCount { get; init; }
    /// <summary>Exposes exact frame-aligned duration in seconds for scheduling and receipts.</summary>
    public double DurationSeconds => FrameCount / 60d;

    /// <summary>Rejects unknown, duplicate, missing, ambiguous and non-frame-aligned arguments before opening a window.</summary>
    public static RecorderOptions Parse(string[] args) {
        ArgumentNullException.ThrowIfNull(args);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index += 2) {
            string key = args[index];
            if (key is not ("--window" or "--pid" or "--output" or "--duration" or "--ffmpeg" or "--encoder")) {
                throw new ArgumentException("Unknown argument: " + key);
            }
            if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]) || args[index + 1].StartsWith("--", StringComparison.Ordinal)) {
                throw new ArgumentException("A value is required for " + key + ".");
            }
            if (!values.TryAdd(key, args[index + 1])) { throw new ArgumentException("Duplicate argument: " + key); }
        }
        if (!values.TryGetValue("--window", out string window)) { throw new ArgumentException("--window is required."); }
        bool hexadecimal = window.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        if (!ulong.TryParse(hexadecimal ? window.AsSpan(2) : window.AsSpan(), hexadecimal ? NumberStyles.AllowHexSpecifier : NumberStyles.None,
            CultureInfo.InvariantCulture, out ulong windowId) || windowId == 0) {
            throw new ArgumentException("--window requires a nonzero decimal or 0x-prefixed X11 client window ID.");
        }
        if (!values.TryGetValue("--pid", out string process) || !int.TryParse(process, NumberStyles.None, CultureInfo.InvariantCulture, out int processId) || processId <= 0) {
            throw new ArgumentException("--pid requires the positive PID of the supplied window's owner.");
        }
        string duration = values.GetValueOrDefault("--duration", "30");
        if (!decimal.TryParse(duration, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal seconds) || seconds <= 0 || seconds > 300) {
            throw new ArgumentException("--duration requires positive invariant seconds, at most 300.");
        }
        decimal frames = seconds * 60;
        if (frames != decimal.Truncate(frames)) { throw new ArgumentException("--duration must contain an exact whole number of CFR60 frames."); }
        string output = values.GetValueOrDefault("--output", OperatingSystem.IsWindows() ? @"C:\temp\helengine\linux\menu.mp4" : "/tmp/helengine/linux/menu.mp4");
        if (!Path.IsPathFullyQualified(output) || !string.Equals(Path.GetFileName(output), "menu.mp4", StringComparison.Ordinal)) {
            throw new ArgumentException("--output requires an absolute path whose final filename is menu.mp4.");
        }
        string encoder = values.GetValueOrDefault("--encoder", FfmpegEncodeArguments.EncoderName);
        FfmpegEncodeArguments.ValidateEncoder(encoder);
        return new RecorderOptions { WindowId = windowId, ProcessId = processId, Output = Path.GetFullPath(output),
            Ffmpeg = values.GetValueOrDefault("--ffmpeg", "ffmpeg"), Encoder = encoder, FrameCount = (long)frames };
    }
}
