using System.Globalization;
using System.Text.Json;
namespace helengine.recording.windows.tests;
/// <summary>Checks the actual encoded stream and decoded fixture pixels, independently from recorder counters.</summary>
static class RecordedVideoAssertions {
    /// <summary>Validates profile, color, geometry, duration, no audio and exact packet cadence.</summary>
    internal static async Task AssertStreamAsync(string path, int width, int height, long frames) {
        using var probe = await RecordedVideoProbe.ReadAsync(path); var streams = probe.RootElement.GetProperty("streams"); Assert.Equal(1, streams.GetArrayLength()); var stream = streams[0];
        Assert.Equal("video", stream.GetProperty("codec_type").GetString()); Assert.Equal("hevc", stream.GetProperty("codec_name").GetString()); Assert.Equal("hvc1", stream.GetProperty("codec_tag_string").GetString());
        Assert.Equal("Main", stream.GetProperty("profile").GetString()); Assert.Equal("yuv420p", stream.GetProperty("pix_fmt").GetString()); Assert.Equal("60/1", stream.GetProperty("avg_frame_rate").GetString());
        Assert.Equal("tv", stream.GetProperty("color_range").GetString()); Assert.Equal("bt709", stream.GetProperty("color_space").GetString()); Assert.Equal("bt709", stream.GetProperty("color_primaries").GetString());
        Assert.Equal(width, stream.GetProperty("width").GetInt32()); Assert.Equal(height, stream.GetProperty("height").GetInt32()); Assert.Equal(frames.ToString(CultureInfo.InvariantCulture), stream.GetProperty("nb_read_frames").GetString());
        double duration = double.Parse(stream.GetProperty("duration").GetString(), CultureInfo.InvariantCulture); Assert.InRange(duration, frames / 60.0 - 0.000002, frames / 60.0 + 0.000002);
        var packets = probe.RootElement.GetProperty("packets"); Assert.Equal(frames, packets.GetArrayLength());
        for (int index = 0; index < packets.GetArrayLength(); index++) {
            double pts = double.Parse(packets[index].GetProperty("pts_time").GetString(), CultureInfo.InvariantCulture); Assert.InRange(pts, index / 60.0 - 0.000002, index / 60.0 + 0.000002);
        }
    }
    /// <summary>Checks RGB channels in the stable upper bands and motion in the lower marker.</summary>
    internal static async Task AssertColorsAndMotionAsync(string path, int width, int height, int lastFrame) {
        byte[] bytes = await RecordedVideoProbe.DecodeAsync(path, lastFrame); int frameBytes = checked(width * height * 4); Assert.Equal(frameBytes * 2, bytes.Length);
        for (int frame = 0; frame < 2; frame++) {
            AssertPixel(bytes, frame * frameBytes, width, width / 6, height / 4, 0, 0, 255);
            AssertPixel(bytes, frame * frameBytes, width, width / 2, height / 4, 0, 255, 0);
            AssertPixel(bytes, frame * frameBytes, width, width * 5 / 6, height / 4, 255, 0, 0);
        }
        Assert.False(bytes.AsSpan(0, frameBytes).SequenceEqual(bytes.AsSpan(frameBytes, frameBytes)), "Animated fixture must change between first and final frame.");
    }
    /// <summary>Asserts lossy 4:2:0 interior RGB values without depending on exact compressed bytes.</summary>
    static void AssertPixel(byte[] bytes, int start, int width, int x, int y, int blue, int green, int red) {
        int offset = start + (y * width + x) * 4; Assert.InRange((int)bytes[offset], Math.Max(0, blue - 12), Math.Min(255, blue + 12));
        Assert.InRange((int)bytes[offset + 1], Math.Max(0, green - 12), Math.Min(255, green + 12)); Assert.InRange((int)bytes[offset + 2], Math.Max(0, red - 12), Math.Min(255, red + 12));
    }
}
