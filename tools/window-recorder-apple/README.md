# macOS window recorder

Reusable library: `/Users/distro/dev/helworks/helengine/engine/helengine.recording.apple`.
CLI consumer: `/Users/distro/dev/helworks/helengine/tools/window-recorder-apple`.

Build from the real main checkout:

```sh
bash /Users/distro/dev/helworks/helengine/scripts/build-window-recording-apple.sh
```

Set `HELENGINE_RECORDING_SIGN_IDENTITY` to an existing development signing identity to retain a stable designated requirement across updates. Without it, builds use an ad-hoc signature and macOS may require screen capture permission again after rebuilding. No certificates or permissions are created by the build script.

The fixed build output is `/private/tmp/helengine/macos/recording/Helengine Recorder.app`.
Install that completed app as `/Users/distro/Applications/Helengine Recorder.app` and launch through LaunchServices so macOS attributes capture permission to its own application identity. The installed HelenUI is independent and remains unchanged.

```sh
open -g -n '/Users/distro/Applications/Helengine Recorder.app' --args permission --request --result /private/tmp/helengine/macos/recording/permission.json
open -g -n '/Users/distro/Applications/Helengine Recorder.app' --args windows --result /private/tmp/helengine/macos/recording/windows.json
open -g -n '/Users/distro/Applications/Helengine Recorder.app' --args record --window-id 123 --output /private/tmp/helengine/macos/menu.mp4 --duration 30 --fps 60 --bitrate 40M --max-bitrate 60M --result /private/tmp/helengine/macos/recording-result.json
```

Select the exact window ID and verify its owner PID before recording. No implicit title match, desktop capture, software/codec fallback, scaling, cursor or audio is requested. Destination directories must already exist. Results are UTF-8 JSON; a LaunchServices exit code only describes launch, so automation must await the final operation receipt. During record, the result file first contains `phase: started` plus the recorder PID; this is ownership metadata, not success. A watchdog may terminate only that PID after rechecking its executable identity. The final JSON replaces that intermediate file atomically.

The output timeline is 60/1 FPS. Lower capture/game cadence repeats source observations; the receipt reports those repetitions. HEVC bitrate is a configured target/ceiling, not a promise of average size for static content. Never label 60 FPS video as proof of 60 distinct game frames.

`fixture --result /private/tmp/helengine/macos/recording/fixture.json` creates only the recorder's own animated test window, publishes its window ID and owner PID, and exits automatically after two minutes. It is for capture/encoder integration tests, not a replacement for an actual menu recording.

Parser unit tests use the same checkout and fixed scratch path:

```sh
xcrun swift test --package-path /Users/distro/dev/helworks/helengine/tools/window-recorder-apple --scratch-path /private/tmp/helengine/macos/recording/cli --jobs 2 --filter RecorderCommandTests
```

Exit 0 means a normal command completed, 2 invalid CLI input, and 3 permission or operational failure. A native failure receipt can report a playable shortened prefix; that is not a successful full recording. Do not bypass Gatekeeper or macOS screen recording permission.

Native async start/first-frame/stop/finalization deadlines are reported as failures, including whether cleanup remains pending. Synchronous `VTCompressionSessionCompleteFrames` can still block inside an unresponsive OS/GPU driver; the library cannot forcibly abort that call safely. Automation for these menu recordings uses an external 75-second watchdog and verifies the recorder PID, executable and exact invocation arguments before terminating only its own recorder instance. An incomplete partial after a watchdog kill is unverified and must not be presented as a successful recording.
