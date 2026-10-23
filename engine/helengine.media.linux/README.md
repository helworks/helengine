# Linux RGBA HEVC encoder

`FfmpegCompositionEncoder` implements the existing `ICompositionEncoder` CPU
readback boundary. The host supplies a trusted FFmpeg executable. Existing
constructors default to software `libx265`; the `(executable, encoderName)`
overload explicitly selects `libx265` or NVIDIA `hevc_nvenc`. There is no
automatic fallback. The backend never downloads binaries or installs packages.
`SelectedEncoder` remains empty until an actual initialization preflight with
the requested resolution and codec parameters succeeds. `IsHardwareAccelerated`
becomes true only after successful NVIDIA preflight; capture remains CPU RGBA.
NVIDIA mode uses GPU 0, preset p4, low-latency tuning, VBR, zero lookahead and
output delay, and four encoding surfaces. It requires an installed working
NVIDIA driver and fails explicitly when initialization is unavailable.

Resolve `mp4-hevc-silent.v1` and pass a document with matching even dimensions,
60/1 frame rate, positive duration, disabled audio, and no audio clips. Supply
exactly `ceil(duration * 60)` frames in order, starting at index zero, each with
timestamp `index/60`, duration `1/60`, no unapplied rotation, and exactly
`width * height * 4` packed RGBA bytes. Audio block submission always throws.
The encoder owns no input frame or surface and never disposes them.

Output is Main HEVC, 8-bit YUV420, limited-range SDR BT.709, `hvc1` MP4,
CFR60, target 40 Mbps, maximum 60 Mbps, 120 Mbps VBV buffer, closed GOP120,
and no B frames. The rate-control target is not a promise that short or simple
recordings consume 40 Mbps. Very small dimensions may be rejected by the
selected encoder's conformance rules; the actual preflight reports this failure.

Writes go directly to bounded OS stdin buffering with one producer allowed in
flight. A concurrent producer is rejected, and there is no managed frame queue.
Writes, preflight, finalization and complete-file verification have a 30-second
deadline per operation; hosts may configure a positive timeout up to five
minutes. Cancellation kills the owned process tree. Dispose after any failure
or cancellation to release handles. No unrelated process is discovered or killed.

The passed output path must not exist. The fixed sibling `output.partial` must
also not exist; the backend reserves it exclusively and never deletes a stale
partial that it did not create. After EOF and successful muxing, FFmpeg decodes
the entire partial with error escalation and checks the exact frame count. Only
then does an exclusive rename publish the final output. Publication refuses a
destination created by another writer during recording. Failed recordings are
deleted, never advertised as a usable prefix. The caller should invoke the
encoder directly when it requires this exclusive publication policy; the shared
`CompositionExporter` has its own separate publication behavior.

Tests require an installed FFmpeg and matching FFprobe, discovered from the
host's PATH or explicitly selected by `HELENGINE_TEST_FFMPEG` and
`HELENGINE_TEST_FFPROBE`. On Linux, run from the real checkout:

```sh
/home/helena/.dotnet/dotnet test /home/helena/dev/helworks/helengine/engine/helengine.media.linux.tests/helengine.media.linux.tests.csproj -c Release --artifacts-path /tmp/helengine/linux -m:1 /nodeReuse:false
```

The tests independently decode ordered gray frames, inspect stream/frame
metadata, reject malformed frames and audio, exercise destination races and
cancellation, and on native Linux run owned helper children to verify stalled
stdin, preflight timeouts, concurrent-write rejection and process reaping.
Fixtures use fixed platform scratch paths and remove their scripts and outputs;
the build output directory is reused on every invocation.

Set `HELENGINE_TEST_NVENC=1` on a native Linux NVIDIA host to additionally run
`FfmpegCompositionEncoderTests.ExplicitNvidiaEncoderPublishesVerifiedMainHevc`.
This opt-in test initializes the real driver, verifies hardware identity and
independently checks decoded Main 8-bit, hvc1, BT.709, CFR60 and no-B-frame
output. Other hosts report a visible skip without requiring NVIDIA hardware.
