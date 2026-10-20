using System.Diagnostics;
using System.Text.Json;
namespace helengine.recording.windows.tests;
/// <summary>Uses diagnostic-only FFmpeg tools to inspect completed synthetic recordings.</summary>
static class RecordedVideoProbe {
    /// <summary>Returns streams, exact packet timestamps and frame count from a real completed MP4.</summary>
    internal static async Task<JsonDocument> ReadAsync(string path) {
        string json = await RunAsync("ffprobe.exe", ["-v", "error", "-count_frames", "-show_streams", "-show_packets", "-show_format", "-of", "json", path]);
        return JsonDocument.Parse(json);
    }
    /// <summary>Decodes only selected frames to an explicitly test-only raw pixel file under C:\temp.</summary>
    internal static async Task<byte[]> DecodeAsync(string path, int lastFrame, string format = "bgra") {
        string output = path + "." + format;
        string selection = lastFrame > 0 ? "select=eq(n\\,0)+eq(n\\," + lastFrame.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")" : "select=eq(n\\,0)";
        await RunAsync("ffmpeg.exe", ["-nostdin", "-v", "error", "-y", "-i", path, "-vf", selection, "-fps_mode", "passthrough", "-pix_fmt", format, "-f", "rawvideo", output]);
        return await File.ReadAllBytesAsync(output);
    }
    /// <summary>Runs a diagnostic tool with bounded lifetime and independent stdout/stderr drains.</summary>
    internal static async Task<string> RunAsync(string executable, IReadOnlyList<string> arguments) {
        string sdk = Environment.GetEnvironmentVariable("HELENGINE_RECORDING_FFMPEG_SDK") ?? "C:\\env\\ffmpeg-8.1.1-full_build-shared";
        var start = new ProcessStartInfo(Path.Combine(sdk, "bin", executable)) { WorkingDirectory = "C:\\temp\\helengine-window-recording", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
        using var process = Process.Start(start); var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45)); } catch { process.Kill(true); await process.WaitForExitAsync(); throw; }
        string error = await stderr; Assert.True(process.ExitCode == 0, executable + ": " + error); return await stdout;
    }
}
