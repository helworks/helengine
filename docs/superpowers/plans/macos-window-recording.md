# macOS window recording implementation plan

Approved scope: record each DemoDisc Main Menu for 30 seconds as MP4 HEVC, 60/1 FPS, 40 Mbit/s target and 60 Mbit/s ceiling, without audio. A slower game produces repeated frames, which must be reported. The user approved ScreenCaptureKit plus VideoToolbox in the existing library family.

Architecture: a standalone SwiftPM library in engine/helengine.recording.apple owns window-only capture, bounded frame selection, hardware HEVC and MP4 finalization. tools/window-recorder-apple consumes its public API and is packaged as its own signed application for macOS capture permission. Keep the installed HelenUI untouched. No capture of the desktop and no fallback to another window, codec or software encoder.

## Constraints
- Work only in the real main checkout; never worktree, export, reset or overwrite unrelated changes.
- Fixed Mac build/test output: /private/tmp/helengine/macos/recording. Per-platform requested recording: /private/tmp/helengine/<platform>/menu.mp4. Retain receipts and requested videos, clean disposable probes.
- User reviews architectural changes; implementation of this backend is approved.
- One heavy build/emulator at a time on the 8 GiB Mac. Never automate mouse/keyboard or bypass Gatekeeper/TCC.

## Tasks
- [x] Library: pin validation, temporal selection (60 slots from slower observations), bounded ownership and failure/finalization with tests. Confirm real HEVC hardware and bitrate properties. Test native output metadata and decodability using platform APIs.
- [x] CLI: test malformed/duplicate/unknown arguments, required exact window ID, absolute output/result paths, fixed 60 FPS, positive duration/bitrates. Implement permission, windows and record commands with one JSON result. Add an owned animated fixture for integration only.
- [x] Build: build library plus CLI from this checkout into the fixed scratch path, package a separately identified app; do not rebuild HelenUI. Ask for capture permission only when the stable application is ready and the OS requires it.
- [ ] Integration: record only our animated fixture first. Check HEVC, 60/1 timestamps, exact frame count, dimensions, configured bitrate and hardware proof, valid trailer and decoded content. Review source/lifecycle before claiming completion.
- [ ] Actual menus: launch only verified packages/emulators with their private existing profiles. Select the exact owned process/window, record one at a time, inspect video frames and save a receipt. Keep build failures, permission blocks and rendering defects explicit; an error/boot screen is not a menu.

## Review focus
First-frame timeout; capture permission denial; closed/resized target; queue/encoder backpressure; incorrect timestamp normalization or hidden software fallback; interruption during MP4 finalization; preserving pre-existing outputs; correct ownership of sample/pixel buffers.

## Verification ledger
- Library validation/CFR RED: four tests failed against stubs; initial GREEN four tests passed.
- CLI parser RED: four tests executed, six assertions failed against explicit unimplemented parser; GREEN four tests, zero failures.
- Real hardware encoder fixture: 60 frames, HEVC, verified Apple hardware encoder, independent full AVAssetReader decode found hvc1, nominal 60/1 FPS, exact timestamps and one-second duration. This is a synthetic encoder fixture, not a menu capture.
- CLI release application compiles; not installed or granted screen capture yet. Integration stream remains pending. No user windows captured by this implementation.

- Final library suite:14/14 passed, zero warnings/errors. Covers initial-callback race, conservative temporal fallback, exclusive destination race, readable `.partial.mp4`, async deadline and late callback behavior.
- Final CLI parser suite:4/4 passed; release executable built without warnings after retaining the fixture owner explicitly.
- Apple Development signing in SSH returned errSecInternalComponent; no keychain or certificate settings were changed. The standard ad-hoc signing path passed strict verification. Installed `/Users/distro/Applications/Helengine Recorder.app`, SHA256 ae90d3e646119975b8ed72786e57dc3825b0bd50f363121a0000fe9120ed0719.
- macOS permission query and explicit request both reported granted=false. The user has been asked to enable this app through normal System Settings. No actual window capture or menu recording is claimed yet.
- Native synchronous VideoToolbox driver calls cannot be forcibly aborted safely in-process; the recording orchestrator has an external75s watchdog restricted to its own verified recorder PID.

- First real own-window integration attempt obtained capture permission but exposed SCK rectangle dictionary decoding before encoding any frame. The metadata decoder was corrected using CGRect(dictionaryRepresentation:), and the SDK-shape regression plus cleanup-state tests bring the library suite to18/18 passed. No menu video was claimed.
- Rebuilding changed the ad-hoc code requirement. TCC logs proved an old/new CDHash mismatch even after manual re-add. The official `tccutil reset ScreenCapture com.helworks.window-recorder` succeeded for this app only; a fresh normal request is pending the user's approval. No permission was granted through commands. Current installed executable SHA256 b8a7acc54e720af163671023549b14aa55a3fb567d6623934551809ca1a5ea1e.
