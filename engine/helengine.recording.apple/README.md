# HelengineRecordingApple

SwiftPM library for macOS 14+, Swift 5 language mode, with no external dependencies.
The CLI is maintained separately in tools/window-recorder-apple.

Record one exact ScreenCaptureKit window using native pixel dimensions from
SCContentFilter.contentRect multiplied by pointPixelScale. There is no desktop
fallback, audio, cursor, interpolation, padding, or upscaling. Odd HEVC dimensions
are rejected explicitly. Resizing the original point bounds or pixel buffer ends
the recording instead of stretching it.

SDR capture uses 8-bit BGRA in sRGB. VideoToolbox performs the native BGRA-to-HEVC
conversion with explicit BT.709 primaries, transfer, and YCbCr matrix. Hardware is
required and its actual use and nonempty encoder ID are verified. Defaults:
60/1 CFR, 30 seconds, 40,000,000 bits/s average target, 60,000,000 bits/s cap over
one second, Main profile, GOP120, and no reordered/B frames. The average bitrate
is an encoder target, not a claim that low-entropy source content consumes it.

The source queue holds three frames, application storage retains at most the
newest and previously selected pixel buffers, and compression permits at most
four pending frames. A future source observation is never inserted into an
earlier slot: a previously observed buffer is repeated instead, with explicit
duplicate and overwritten-source counts. CFR does not imply the application
rendered 60 distinct frames per second.

Existing destination and partial files are rejected. Recording uses one fixed
sibling menu.partial.mp4, then publishes using an exclusive hard link and removes
only that owned partial link. A late competing destination is never overwritten.
A successfully finalized failure prefix is identified by prefixPath; it is not
claimed playable if cleanupPending is true.

The first complete frame has a 10-second deadline. Asynchronous native start,
stop, and MP4 finalization callbacks have bounded deadlines (10, 5, and 5 seconds).
Completion gates resume exactly once and safely retain native callback context
after timeout. Timeout receipts may therefore report cleanupPending. Cleanup time
is additional to the first-frame deadline: this is not a total-operation SLA.
VideoToolbox CompleteFrames is synchronous; a hung GPU driver cannot be forcibly
aborted safely inside the process. The CLI orchestrator must use an external
watchdog that checks identity and terminates only its own recorder process.
No global service or other application is stopped.

Tests use synthetic CoreVideo buffers and the actual hardware HEVC encoder,
then read and decode the MP4, verify 60 timestamps and BT.709 tags, and exercise
existing-output and late-publication sentinels. They never start screen capture.

    swift test --package-path /Users/distro/dev/helworks/helengine/engine/helengine.recording.apple --scratch-path /private/tmp/helengine/macos/recording --jobs 1

Real-window ScreenCaptureKit behavior still requires the caller app's explicit
Screen Recording permission and an authorized target window. This library never
requests permission automatically and does not modify HelenUI or its TCC identity.

### Bounded encoder backpressure

The four-frame encoder capacity is released only after each VideoToolbox callback finishes appending its compressed sample. A due-slot batch waits for capacity using one shared monotonic 250 ms deadline; later frames do not renew that budget. Exhausting the deadline fails with `encoder_backpressure_timeout`, and an asynchronous callback failure wakes the waiter with the original error. No output frames are discarded and the native queue remains capped at four.

On the tested Apple hardware HEVC encoder (`com.apple.videotoolbox.videoencoder.ave.hevc`), `MaxFrameDelayCount` reads as zero but setting either zero or three is rejected with OSStatus -12900. Real-time pacing passed at 912 x 1944 and 3420 x 2146, whereas an eight-slot catch-up burst previously exhausted the application capacity. The callback wait fixes that burst without changing codec, resolution, GOP, bitrate, frame rate, or the installed app's permission identity.
