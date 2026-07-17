# PS2 Build Log Streaming Design

## Goal

Make the shared platform build wrapper forward editor publish and platform-build logs as they arrive, so long PS2 builds show progress continuously and retain the child process exit code.

## Root cause

`scripts/build-platform.ps1` currently invokes native `dotnet` commands with PowerShell's call operator. The wrapper has no explicit output-forwarding boundary, so callers that capture the wrapper's streams can receive the accumulated output only after the child process exits. The interrupted PS2 build was especially difficult to observe because its output was redirected and read only at the end by the caller.

## Design

Add one PowerShell helper that starts a native process through `System.Diagnostics.Process` with stdout and stderr redirected. `Register-ObjectEvent` subscriptions handle each `DataReceived` event, write immediately to the corresponding console stream, and flush it; this is compatible with Windows PowerShell 5.1 event execution. The helper waits for process completion, drains pending asynchronous callbacks, disposes the process, and returns its exact exit code.

Use the helper for editor restore, publish, and platform execution. Keep the existing wrapper validation, environment-variable lifetime, display messages, and exit-code mapping unchanged. Since the repository runs Windows PowerShell 5.1, arguments will be converted to a Windows command-line string rather than using the .NET Core-only `ArgumentList` property.

## Verification

Add a focused PowerShell regression harness using a delayed fake native executable. The harness will verify that an early output line is observable before the child exits and that both stdout and stderr are forwarded. Then run PowerShell parsing/static checks and the smallest available wrapper validation. A real PS2 build remains the end-to-end verification when the toolchain is available.
