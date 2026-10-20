using System.Globalization;
using helengine.recording.windows;
namespace helengine.tools.windowrecorder;
/// <summary>Parses a small invariant CLI without accepting ambiguous windows or silently ignored options.</summary>
static class RecorderCommandLine {
    /// <summary>Parses and validates the entire command before any capture/device work begins.</summary>
    internal static RecorderCommand Parse(string[] args) {
        if (args.Length == 1 && args[0] == "windows") { return new() { IsWindowList = true }; }
        if (args.Length == 0 || args[0] != "record") { throw new ArgumentException("Use windows or record --hwnd <handle> --output <file.mp4> [--duration seconds] [--fps 60] [--bitrate 40M] [--max-bitrate 60M]."); }
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 1; index < args.Length; index += 2) {
            string name = args[index];
            if (name is not ("--hwnd" or "--output" or "--duration" or "--fps" or "--bitrate" or "--max-bitrate")) { throw new ArgumentException("Unknown option: " + name); }
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal)) { throw new ArgumentException("Missing value for " + name); }
            if (!values.TryAdd(name, args[index + 1])) { throw new ArgumentException("Repeated option: " + name); }
        }
        if (!values.TryGetValue("--hwnd", out var hwnd) || !values.TryGetValue("--output", out var output)) { throw new ArgumentException("record requires explicit --hwnd and --output."); }
        if (values.TryGetValue("--fps", out var fps) && fps != "60") { throw new ArgumentException("This recording profile accepts only --fps 60."); }
        var options = new WindowRecordingOptions {
            WindowHandle = ParseHandle(hwnd), OutputPath = Path.GetFullPath(output),
            Duration = values.TryGetValue("--duration", out var duration) ? ParseDuration(duration) : null,
            Bitrate = values.TryGetValue("--bitrate", out var bitrate) ? ParseBitrate(bitrate) : 40_000_000,
            MaxBitrate = values.TryGetValue("--max-bitrate", out var maximum) ? ParseBitrate(maximum) : 60_000_000
        };
        if (options.WindowHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(output) || !string.Equals(Path.GetExtension(output), ".mp4", StringComparison.OrdinalIgnoreCase) || options.Bitrate <= 0 || options.MaxBitrate < options.Bitrate) { throw new ArgumentException("A nonzero HWND, absent .mp4 destination and positive bitrates with max >= target are required."); }
        return new() { Options = options };
    }
    /// <summary>Accepts decimal or hexadecimal pointer bits without title-based heuristics.</summary>
    static IntPtr ParseHandle(string text) {
        bool hex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        if (!ulong.TryParse(hex ? text[2..] : text, hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None, CultureInfo.InvariantCulture, out ulong value) || value == 0) { throw new ArgumentException("--hwnd must be a nonzero decimal or 0x hexadecimal handle."); }
        return new IntPtr(unchecked((long)value));
    }
    /// <summary>Rounds sub-tick positive durations upward to the smallest representable 100 ns interval.</summary>
    static TimeSpan ParseDuration(string text) {
        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal seconds) || seconds <= 0 || seconds > (decimal)TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerSecond) { throw new ArgumentException("--duration must be positive invariant seconds, for example 1.5."); }
        return TimeSpan.FromTicks((long)decimal.Ceiling(seconds * TimeSpan.TicksPerSecond));
    }
    /// <summary>Accepts integral bit/s or invariant megabits per second with decimal M=1,000,000.</summary>
    static long ParseBitrate(string text) {
        bool mega = text.EndsWith("M", StringComparison.OrdinalIgnoreCase);
        if (!decimal.TryParse(mega ? text[..^1] : text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value)) { throw new ArgumentException("Bitrates must use invariant bit/s or M, for example 40M."); }
        if (mega && value > long.MaxValue / 1_000_000m) { throw new ArgumentException("Bitrate is too large."); }
        value *= mega ? 1_000_000 : 1;
        if (value <= 0 || value > long.MaxValue || decimal.Truncate(value) != value) { throw new ArgumentException("Bitrates must be positive integral bit/s."); }
        return (long)value;
    }
}
