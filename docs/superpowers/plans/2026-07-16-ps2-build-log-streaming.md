# PS2 Build Log Streaming Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stream native editor and PS2 build output line-by-line from the shared PowerShell build wrapper while preserving exit codes.

**Architecture:** Add a private PowerShell process runner using `System.Diagnostics.Process`, `Register-ObjectEvent` stdout/stderr subscriptions, and Windows PowerShell-compatible argument quoting. Route restore, publish, and editor platform execution through that runner without changing validation or output paths.

**Tech Stack:** Windows PowerShell 5.1, .NET `System.Diagnostics.Process`, PowerShell test harness.

---

### Task 1: Add the failing streaming regression harness

**Files:**
- Create: `scripts/tests/build-platform-streaming.tests.ps1`

- [ ] **Step 1: Create a delayed fake native command and wrapper test.**

The test harness should create a temporary `dotnet.cmd` that emits `EARLY-OUT`, waits two seconds, emits `LATE-OUT`, writes `EARLY-ERR` to stderr, and exits with code 7. Invoke the wrapper with a valid temporary editor project and project file while placing the fake command first on `PATH`. Poll the wrapper output file during execution and assert that `EARLY-OUT` appears before the child exits; assert that the final output contains all four markers and exit code 7.

- [ ] **Step 2: Run the harness before implementation.**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\tests\build-platform-streaming.tests.ps1
```

Expected result: FAIL because the streaming process helper does not exist yet and the wrapper still invokes native commands directly.

### Task 2: Implement the streaming process runner

**Files:**
- Modify: `scripts/build-platform.ps1`

- [ ] **Step 1: Add Windows argument quoting.**

Add a helper that returns an empty quoted argument for empty values, leaves simple arguments unchanged, and wraps whitespace-containing arguments in quotes while escaping embedded quotes and trailing backslashes according to the Windows CRT command-line convention.

- [ ] **Step 2: Add asynchronous native-process execution.**

Add a helper that configures `ProcessStartInfo` with `UseShellExecute = false`, `CreateNoWindow = true`, and redirected stdout/stderr. Register `Register-ObjectEvent` actions for both data streams; each action writes non-null lines to `[Console]::Out` or `[Console]::Error` and flushes immediately. Start both readers, wait for process completion, call `WaitForExit()` again, allow queued event actions to drain, unregister subscriptions, dispose the process, and return the exit code.

- [ ] **Step 3: Route all native editor commands through the helper.**

Replace the direct `& dotnet @DotNetRestoreArguments`, `& dotnet @DotNetPublishArguments`, and editor CLI invocation with the helper. Preserve the existing non-zero exit handling and restore `HELENGINE_SOURCE_ROOT` in the existing `finally` block.

- [ ] **Step 4: Run the regression harness.**

Run the PowerShell test command from Task 1 and expect PASS, including early output visibility, stdout/stderr markers, and exit-code propagation.

### Task 3: Verify the real wrapper and PS2 path

**Files:**
- Modify: `scripts/build-platform.ps1`
- Verify: `scripts/tests/build-platform-streaming.tests.ps1`

- [ ] **Step 1: Parse the wrapper and test harness.**

Run:

```powershell
powershell -NoProfile -Command "[void][System.Management.Automation.Language.Parser]::ParseFile('C:\dev\helworks\helengine\scripts\build-platform.ps1',[ref]`$null,[ref]`$null); [void][System.Management.Automation.Language.Parser]::ParseFile('C:\dev\helworks\helengine\scripts\tests\build-platform-streaming.tests.ps1',[ref]`$null,[ref]`$null); Write-Output 'PARSE_OK'"
```

Expected result: `PARSE_OK`.

- [ ] **Step 2: Run the PS2 build and observe live phase output.**

Run the documented Release build:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\build-platform.ps1 -Project C:\dev\helprojs\demodisc\project.heproj -Platform ps2 -Output C:\dev\helprojs\demodisc\output\ps2 -Configuration Release
```

Expected result: build phase lines appear before the command exits, and a successful build returns exit code 0 with `output\ps2\game.iso` and `output\ps2\disc\HELENGIN.ELF` present.

- [ ] **Step 3: Review the focused diff.**

Run:

```powershell
git diff -- scripts/build-platform.ps1 scripts/tests/build-platform-streaming.tests.ps1
```

Confirm that the change is limited to log forwarding and its regression harness.
