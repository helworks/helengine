using System.Diagnostics;
using System.Text.Json;
using helengine.capture.linux;
using helengine.media;
using helengine.media.linux;

namespace helengine.tools.windowrecorderlinux;

/// <summary>Records one validated owned X11 window through sequential CPU readback and software libx265 encoding.</summary>
public sealed class RecorderApplication {
    /// <summary>Documents the fixed encoding contract and the explicit window/PID scope.</summary>
    public const string Usage = "window-recorder-linux --window <decimal|0xID> --pid <owner PID> [--output /tmp/helengine/linux/menu.mp4] [--duration 30] [--ffmpeg /absolute/path/ffmpeg]\n" +
        "Captures only the supplied X11/Xwayland client window on DISPLAY; no desktop, input or focus changes.\n" +
        "Output: silent MP4, software libx265 HEVC Main, CFR60, target 40M/max 60M, GOP120, no B frames, SDR BT.709.\n" +
        "Existing menu.mp4 or menu.mp4.partial is refused. Ctrl+C cancels and removes this invocation's unverified partial.\n" +
        "Capture samples and repeated output slots are reported separately; output FPS does not measure game FPS.";

    /// <summary>Publishes a machine-readable success receipt only after full encoder verification and exclusive final publication.</summary>
    public async Task<int> RunAsync(string[] args, TextWriter output, TextWriter errors, CancellationToken token) {
        if (args.Length == 1 && args[0] is "--help" or "-h") { await output.WriteLineAsync(Usage); return 0; }
        var options = RecorderOptions.Parse(args);
        if (!OperatingSystem.IsLinux()) { throw new PlatformNotSupportedException("Window recording requires Linux X11/Xwayland and an explicit DISPLAY."); }
        token.ThrowIfCancellationRequested();
        using var capture = new LinuxWindowCapture(options.WindowId, options.ProcessId);
        var profile = OutputProfile.Resolve(FfmpegEncodeArguments.ProfileId, capture.Width, capture.Height);
        var document = new CompositionDocument { Id = "linux-owned-window", Revision = 1, Width = capture.Width, Height = capture.Height,
            FrameRate = new MediaTime(60, 1), Duration = new MediaTime(options.FrameCount, 60), Audio = new CompositionAudioSettings { Enabled = false } };
        using var encoder = new FfmpegCompositionEncoder(options.Ffmpeg);
        await encoder.BeginAsync(document, profile, options.Output, token);
        await errors.WriteLineAsync("Recording supplied window with software " + encoder.SelectedEncoder + "; capture samples and CFR repeats will be measured separately.");
        var schedule = new FrameSchedule(options.FrameCount);
        var clock = Stopwatch.StartNew();
        long nextIndex = 0, captures = 0, repeats = 0;
        double lastCaptureSeconds = 0;
        MediaVideoFrame previous = null;
        try {
            while (nextIndex < schedule.FrameCount) {
                token.ThrowIfCancellationRequested();
                await WaitUntilAsync(clock, schedule.Deadline(nextIndex), token);
                long slot = schedule.SlotAt(clock.Elapsed);
                if (previous == null) { slot = 0; }
                if (slot >= schedule.FrameCount) {
                    while (nextIndex < schedule.FrameCount) {
                        await WriteAsync(encoder, previous, nextIndex++, token);
                        repeats++;
                    }
                    break;
                }
                var current = capture.Capture(new MediaTime(slot, 60), new MediaTime(1, 60));
                captures++;
                lastCaptureSeconds = clock.Elapsed.TotalSeconds;
                try {
                    while (nextIndex < slot) {
                        await WriteAsync(encoder, previous, nextIndex++, token);
                        repeats++;
                    }
                    await WriteAsync(encoder, current, nextIndex++, token);
                } catch {
                    current.Dispose();
                    throw;
                }
                previous?.Dispose();
                previous = current;
            }
            await WaitUntilAsync(clock, schedule.Deadline(schedule.FrameCount), token);
        } finally { previous?.Dispose(); }
        double captureAndDeliverySeconds = clock.Elapsed.TotalSeconds;
        await encoder.FinishVideoAsync(token);
        await encoder.CompleteAsync(token);
        await output.WriteLineAsync(JsonSerializer.Serialize(new { status = "verified", output = options.Output, encoder = encoder.SelectedEncoder,
            hardwareAccelerated = encoder.IsHardwareAccelerated, width = capture.Width, height = capture.Height, outputFrames = options.FrameCount,
            outputFps = 60, outputDurationSeconds = options.DurationSeconds, captureSamples = captures, repeatedOutputFrames = repeats,
            captureAndDeliverySeconds, lastCaptureSeconds, totalIncludingVerificationSeconds = clock.Elapsed.TotalSeconds,
            note = "Capture samples include unchanged images. CFR output rate does not measure game FPS." }));
        return 0;
    }

    /// <summary>Applies backpressure directly to one retained capture, with no queued frame producers or output temp naming.</summary>
    static async Task WriteAsync(ICompositionEncoder encoder, MediaVideoFrame source, long index, CancellationToken token) {
        if (source == null) { throw new InvalidOperationException("A capture is required before a repeated output slot."); }
        using var frame = new MediaVideoFrame(new MediaTime(index, 60), new MediaTime(1, 60), new BorrowedVideoSurface(source.Surface));
        await encoder.WriteVideoFrameAsync(frame, index, token);
    }

    /// <summary>Waits against the absolute monotonic schedule, avoiding accumulated per-frame delay drift.</summary>
    static async Task WaitUntilAsync(Stopwatch clock, TimeSpan deadline, CancellationToken token) {
        while (true) {
            TimeSpan remaining = deadline - clock.Elapsed;
            if (remaining <= TimeSpan.Zero) { return; }
            await Task.Delay(remaining > TimeSpan.FromMilliseconds(1) ? remaining : TimeSpan.FromMilliseconds(1), token);
        }
    }
}
