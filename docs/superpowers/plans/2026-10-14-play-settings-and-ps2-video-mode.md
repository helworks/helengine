# Play Settings and PS2 Video Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give editor Play its own per-user settings (common plus per-platform, stored in `user_settings/play.json`, edited from the Play platform selector). Then use that foundation to let a PS2 project choose its console video mode, which the PS2 runtime boots into and the desktop player reproduces.

**Architecture:** Phase A adds the Play settings foundation. The engine declares five common Play settings, and `PlatformPlayerCapability` declares per-platform ones (both use the `PlatformSettingDefinition` shape). The editor loads `play.json`, validates it against those declarations, snapshots the values when Play is pressed and resolves them after the cook (`project` values come from the cooked queue item's graphics options). It writes one `--play-settings` JSON file that replaces `--target-fps`, `--shadows-enabled` and `--wait-for-debugger`. The player renders into an offscreen target of the adapter-declared render size and point-magnifies it into an integer-scaled window. Phase B adds the generic `PlatformVideoMode` contract, which PS2 implements with its 12 modes: the PS2 builder writes a versioned boot record, `Ps2BootHost` initialises gsKit from that record, and the PS2 Play `video-mode` setting (default `project`) picks the player's render size and frame rate.

**Tech Stack:** C# (.NET 9, xUnit, System.Text.Json, WinForms, SharpDX/DirectX 11), C++20 for the PS2 runtime (gsKit, ps2sdk, host g++ for native tests in the `helengine-ps2` Docker image).

**Spec:** `docs/superpowers/specs/2026-10-14-play-settings-design.md` (Phase A) and `docs/superpowers/specs/2026-10-14-console-video-mode-design.md` (Phase B; its player-window part is superseded by the common `window-scale` Play setting, and its editor→player CLI flags are superseded by the `--play-settings` file).

**Repositories touched (all on `main`, shared trees):**
- `C:\dev\helworks\helengine` (engine, editor, player)
- `C:\dev\helworks\helengine-ps2` (builder, player adapter, native runtime)
- `C:\dev\helprojs\demodisc` (Phase B Task B10 only)
- `C:\dev\helworks\helengine-windows` (Task A14 only, coordination-gated)

Phase B depends on Phase A. After Phase A every existing Play platform must still play. Today that is PS2 (committed) and Windows (in-flight, uncommitted work by another session; see Task A14).

## Global Constraints

- AGENTS.md conventions: one class per file, substantive XML docs on every class/member, no tuples, PascalCase fields, no redundant `private`, standard member order, opening braces on the same line, `if / else if` chains for mutually exclusive checks, nullable reference types disabled.
- No silent defaults: when a required value is missing or invalid, throw an exception that names it. Never construct a stand-in value.
- No best-effort masking: never catch-and-continue around a failure.
- Current-format only: no legacy, migrate, upgrade or version-range readers. Unknown versions, ids and fields fail and name the file and the id or field.
- No first-chance exceptions on normal paths, because Helena debugs with them enabled. Use `File.Exists`, `TryGetValue` and `TryParse`. Only an invalid input or a broken file may throw.
- Tests must be host-runnable and must never assert substrings of hand-written source files.
- Shared trees on `main`: stage only your own hunks (`git add -p` or explicit paths), never `git add -A`. After each Edit, check CRLF/LF drift with `git diff --numstat` and `git diff --ignore-all-space --stat`. If whole-file churn appears, restore the original line endings before committing.
- Commit trailer on every commit: `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Never push.
- Run dotnet through PowerShell from `C:\dev\helworks\helengine` as `.\dotnetw.cmd ...`. For PS2 builder tests run `C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj ...`.
- Native PS2 host tests run in Docker with `docker run --rm -v "C:\dev\helworks\helengine-ps2:/workspace:ro" -v "C:\dev\helworks\builds\helengine-ps2\native-tests\<name>:/build-output" -w /workspace helengine-ps2 make <target> BUILD_DIR=/build-output` (set `MSYS_NO_PATHCONV=1` when invoked from Git Bash).
- Build outputs and test scratch go under `C:\dev\helworks\builds\...`, never `%TEMP%`.
- Emulator validation (PCSX2) only after asking Helena explicitly.
- Editor test baseline: 28 tests are already red on main (list dated 2026-09-19). Compare failures by name, before and after, on the same branch.
- New UI text uses ASCII only (`Play Settings...`, not the ellipsis character). The shared editor font charset is off-limits.
- Play settings ids and values are exactly as specified:
  - `window-scale` Choice `1x`,`2x`,`3x`,`4x`,`fit`, default `fit`
  - `window-monitor` Choice `editor`,`primary`, default `editor`
  - `wait-for-debugger` Boolean, default `false`
  - `shadows` Boolean, default `true`
  - `frame-rate` Choice `platform`,`unlimited`, default `platform`
- `user_settings/play.json` is `{ "version": 1, "common": {...}, "platforms": { "<id>": {...} } }`. When the file or a key is missing, the declared defaults apply and nothing is written until the user saves the dialog.
- PS2 `video-mode` ids are exactly: `ntsc-640x448i`, `ntsc-512x448i`, `ntsc-640x224p`, `ntsc-512x224p`, `ntsc-320x224p`, `ntsc-256x224p`, `pal-640x512i`, `pal-512x512i`, `pal-640x256p`, `pal-512x256p`, `pal-320x256p`, `pal-256x256p`. The graphics setting defaults to `ntsc-640x448i` and the Play setting defaults to `project`. NTSC modes use `RefreshRateHz` 59.94 and `PlayerFrameRate` 60. PAL modes use 50 and 50.
- A PS2 boot with a missing or invalid boot record halts with a diagnostic. It never falls back to 640x448.

## Decisions this plan makes where the specs are silent (flagged for Helena)

1. **Shift+click still forces a debugger wait.** The effective wait is `wait-for-debugger || Shift held`. The setting replaces the hard-wired flag, and Shift remains a one-shot override.
2. **The `shadows` Play setting can only turn shadows off.** The effective value is `shadows && the project's runtime shadow policy`, so Play never shows shadows the console build would not.
3. **`project` means "the project graphics option with the same id".** It is resolved after the cook, from the queue item captured at press time. `PlatformDefinition` validates that every graphics profile declares that id.
4. **`frame-rate: unlimited` turns off both the pacer and vsync** (`PresentSyncInterval = 0`).
5. **An explicit scale that does not fit the monitor still opens at that scale.** The window is placed at the work-area origin and the player logs one line saying so. `fit` never goes below 1x.
6. **The offscreen render target and point magnification arrive in Phase A**, not B. Window scale is meaningless without a fixed render size, so every Play platform declares its render size through `IPlatformPlayerAdapter.ApplyPlaySettings`. PS2 renders at 640x448 until Phase B.
7. **The frame-rate source is explicit.** In Phase B a Play-capable platform declares exactly one source: a positive `PlatformPlayerCapability.TargetFrameRate` (Windows) or a `PlatformVideoModeCapability` with `TargetFrameRate` 0 (PS2). This departs from the video-mode spec's "must declare a video-mode capability", which Windows cannot satisfy.
8. **Press-time capture lives on `EditorPlayPreparation`.** The `EditorPlaySettingsSnapshot` is passed to `EditorPlaySessionService.Play` and stored there, not on the cook-only `EditorPlayPreparationInputs`, because the session (not the cook) consumes it.

## Review Focus

- A hand-edited `play.json` value that is no longer allowed (e.g. `"window-scale": "5x"`): Play refuses with one message naming the file, platform and id. The dialog opens with that row highlighted and saving fixes it. Pinned in Task A6 (evaluation test) and Task A11 (view-model test).
- A small monitor work area (1366x728 laptop) or an explicit scale bigger than the monitor: `fit` gives 1x, never 0. `4x` opens at 4x at the work-area origin and reports that it exceeds. Pinned in Task A9 (`PlayerWindowLayoutTests`).
- Editing Play settings while a preparation is still cooking: the running Play uses the press-time values and the next Play uses the edit. Pinned in Task A8 (`Play_UsesSettingsCapturedWhenPressed`).
- A `play.json` section for a platform that is not installed or not selected: it is never validated against another platform's declarations and is preserved untouched when the dialog saves a different platform. Pinned in Task A5 (round trip) and Task A11 (`CreateSavedDocument_PreservesOtherPlatformSections`).
- An old `platform.ps2.json` that still has `default-width`/`default-height`, or that lacks `video-mode`: Play and the PS2 build fail and name the field and the file. They never silently run at 640x448. Pinned in Task B5 (resolver test) and Task B7 (builder test).

---

## File Structure

### Phase A — helengine

| File | Responsibility |
|---|---|
| `engine/helengine.baseplatform/Definitions/PlayerCommonPlaySettings.cs` (new) | Ids, the `project` value and the five common `PlatformSettingDefinition`s |
| `engine/helengine.baseplatform/Definitions/PlatformPlaySettingDeclarationRules.cs` (new) | Shared declaration checks (choice default allowed, boolean default text) |
| `engine/helengine.baseplatform/Definitions/PlatformPlayerCapability.cs` (modify) | Gains `PlaySettings` plus validation |
| `engine/helengine.baseplatform/Definitions/PlatformDefinition.cs` (modify) | Validates `project` Play values against graphics profiles |
| `engine/helengine.baseplatform/Player/PlayerWindowScale.cs`, `PlayerWindowScaleText.cs`, `PlayerWindowMonitor.cs`, `PlayerWindowMonitorText.cs` (new) | Typed common choices and their text forms |
| `engine/helengine.baseplatform/Player/PlayerPlaySettings.cs` (new) | Resolved settings handed to the player |
| `engine/helengine.baseplatform/Player/PlayerPlaySettingsFile.cs` (new) | Strict JSON read/write of the `--play-settings` file |
| `engine/helengine.baseplatform/Player/PlayerRenderSize.cs` (new) | Render size an adapter declares |
| `engine/helengine.baseplatform/Player/IPlatformPlayerAdapter.cs` (modify) | Gains `ApplyPlaySettings` |
| `engine/helengine.editor/managers/play/EditorPlaySettingsDocument.cs`, `EditorPlaySettingsStore.cs` (new) | `user_settings/play.json` model and store |
| `engine/helengine.editor/managers/play/EditorPlaySettingScope.cs`, `EditorPlaySettingIssue.cs`, `EditorPlaySettingValueValidator.cs`, `EditorPlaySettingsEvaluation.cs`, `EditorPlaySettingsEvaluator.cs`, `EditorPlaySettingsSnapshot.cs` (new) | Validation against declarations plus the press-time snapshot |
| `engine/helengine.editor/managers/play/EditorPlayLaunchSettingsResolver.cs`, `EditorPlaySettingsFileWriter.cs` (new) | Snapshot + cook result → `PlayerPlaySettings` → file |
| `engine/helengine.editor/managers/project/EditorPlayCookResult.cs`, `EditorPlatformBuildGraphRunner.cs` (modify) | Cook result carries the graphics option values |
| `engine/helengine.editor/managers/play/EditorPlaySessionService.cs`, `EditorPlayPreparation.cs` (modify) | Takes the snapshot and launches with `--play-settings` |
| `engine/helengine.player/PlayerLaunchOptions.cs`, `PlayerHost.cs`, `PlayerWindow.cs` (modify) | Parses the file and applies the common settings |
| `engine/helengine.player/PlayerScreenArea.cs`, `PlayerWindowLayout.cs`, `PlayerWindowLayoutResult.cs`, `PlayerMonitorLocator.cs`, `IPlayerFrameLimiter.cs`, `PlayerUnpacedFrameLimiter.cs`, `PlayerFrameLimiterFactory.cs` (new) | Window scale and monitor, plus frame pacing choice |
| `engine/helengine.directx11/DirectX11PointMagnifyPass.cs` (new), `DirectX11SwapChainSurface.cs`, `DirectX11Renderer3D.cs` (modify) | Offscreen render-size surface plus point-sampled present |
| `engine/helengine.editor/managers/play/EditorPlaySettingsDialogModel.cs` (new) | Dialog view-model |
| `engine/helengine.editor/components/ui/EditorPlayPlatformMenu.cs` (new), `EditorViewport.cs` (modify) | `Play Settings...` item in the selector |
| `engine/helengine.editor/components/ui/PlaySettingsDialog.cs` (new), `EditorSession.cs` (modify) | Dialog plus wiring, press-time capture |

### Phase A — helengine-ps2

| File | Responsibility |
|---|---|
| `builder/Ps2PlatformDefinitionFactory.cs` (modify) | Passes `playSettings: []` |
| `builder/player/Ps2PlayerAdapter.cs` (modify) | `ApplyPlaySettings` returns 640x448 |

### Phase B — helengine

| File | Responsibility |
|---|---|
| `engine/helengine.baseplatform/Definitions/PlatformVideoMode.cs`, `PlatformVideoModeCapability.cs` (new) | Generic mode contract |
| `engine/helengine.baseplatform/Definitions/PlatformDefinition.cs`, `PlatformPlayerCapability.cs` (modify) | Video-mode validation; frame-rate source rule |
| `engine/helengine.baseplatform/Player/PlayerPlaySettings.cs`, `PlayerPlaySettingsFile.cs` (modify) | Carry the resolved `PlatformVideoMode` |
| `engine/helengine.editor/managers/play/EditorPlaySettingsSnapshot.cs`, `EditorPlayLaunchSettingsResolver.cs`, `EditorPlayPlatformOption.cs`, `EditorSession.cs` (modify) | Resolve the Play video mode |
| `engine/helengine.player/PlayerWindowTitle.cs`, `PlayerHost.cs` (modify) | Title shows the mode |

### Phase B — helengine-ps2

| File | Responsibility |
|---|---|
| `builder/Ps2VideoModes.cs` (new) | The 12 PS2 modes plus capability |
| `builder/Ps2VideoModeBootRecord.cs`, `builder/Ps2VideoModeBootRecordWriter.cs` (new) | Boot record format plus staging writer |
| `builder/Ps2PlatformDefinitionFactory.cs`, `builder/Ps2PlatformAssetBuilder.cs`, `builder/player/Ps2PlayerAdapter.cs` (modify) | Declare, write, apply |
| `src/platform/ps2/Ps2VideoModeBootRecordReader.hpp/.cpp` (new) | Native parse plus gsKit field mapping |
| `src/platform/ps2/Ps2BootHost.cpp/.hpp` (modify) | Read the record before `gsKit_init_screen` |
| `tests/native/Ps2VideoModeBootRecordTests.cpp` (new), `Makefile` (modify) | Native host test target `ps2-video-mode-tests` |

### Phase B — demodisc

| File | Responsibility |
|---|---|
| `C:\dev\helprojs\demodisc\settings\platform.ps2.json` (modify) | `video-mode` replaces `default-width`/`default-height` in `graphics.selectedOptionValues` |

---

# Phase A — Play settings foundation

### Task A1: Common Play settings and `PlatformPlayerCapability.PlaySettings`

**Files:**
- Create: `engine/helengine.baseplatform/Definitions/PlayerCommonPlaySettings.cs`
- Create: `engine/helengine.baseplatform/Definitions/PlatformPlaySettingDeclarationRules.cs`
- Modify: `engine/helengine.baseplatform/Definitions/PlatformPlayerCapability.cs` (constructor, properties, `CreateUnsupported`)
- Modify (constructor call sites, append `[]`): `engine/helengine.baseplatform.tests/Player/PlatformPlayerCapabilityTests.cs` (11 calls), `engine/helengine.editor.tests/managers/play/EditorPlayPlatformSelectionServiceTests.cs` (1), `engine/helengine.editor.tests/managers/play/EditorPlaySessionServiceTests.cs` (3), `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerAssetsOnlyTests.cs` (2)
- Modify: `C:\dev\helworks\helengine-ps2\builder\Ps2PlatformDefinitionFactory.cs:376-383`
- Test: `engine/helengine.baseplatform.tests/Player/PlayerCommonPlaySettingsTests.cs` (new), `engine/helengine.baseplatform.tests/Player/PlatformPlayerCapabilityTests.cs`

**Interfaces:**
- Produces:
  - `PlayerCommonPlaySettings.WindowScaleId/WindowMonitorId/WaitForDebuggerId/ShadowsId/FrameRateId/ProjectValue` (`const string`)
  - `PlayerCommonPlaySettings.CreateDefinitions(): PlatformSettingDefinition[]`
  - `PlayerCommonPlaySettings.IsCommonId(string): bool`
  - `PlatformPlaySettingDeclarationRules.Validate(PlatformSettingDefinition setting, string parameterName): void`
  - `PlatformPlayerCapability(bool supportsPlay, string adapterTypeName, string contentRootRelativePath, double targetFrameRate, PlayerInputDevices inputDevices, string runtimePlatformName, string runtimePlatformVersion, PlatformSettingDefinition[] playSettings)`
  - `PlatformPlayerCapability.PlaySettings: PlatformSettingDefinition[]`
  - `PlatformPlayerCapability.FindPlaySetting(string settingId): PlatformSettingDefinition` (null when not declared, no throw)

- [ ] **Step 1: Write the failing tests**

Create `engine/helengine.baseplatform.tests/Player/PlayerCommonPlaySettingsTests.cs`:

```csharp
using helengine.baseplatform.Definitions;

namespace helengine.baseplatform.tests.Player {
    /// <summary>
    /// Verifies the engine-declared common Play settings every platform's Play session offers.
    /// </summary>
    public sealed class PlayerCommonPlaySettingsTests {
        /// <summary>The common settings are exactly the five the Play settings spec lists, with their defaults and values.</summary>
        [Fact]
        public void CreateDefinitions_DeclaresTheFiveCommonSettings() {
            PlatformSettingDefinition[] definitions = PlayerCommonPlaySettings.CreateDefinitions();

            Assert.Equal(new[] { "window-scale", "window-monitor", "wait-for-debugger", "shadows", "frame-rate" }, definitions.Select(definition => definition.SettingId).ToArray());
            Assert.Equal("fit", definitions[0].DefaultValue);
            Assert.Equal(new[] { "1x", "2x", "3x", "4x", "fit" }, definitions[0].AllowedValues);
            Assert.Equal("editor", definitions[1].DefaultValue);
            Assert.Equal(new[] { "editor", "primary" }, definitions[1].AllowedValues);
            Assert.Equal(PlatformSettingKind.Boolean, definitions[2].SettingKind);
            Assert.Equal("false", definitions[2].DefaultValue);
            Assert.Equal(PlatformSettingKind.Boolean, definitions[3].SettingKind);
            Assert.Equal("true", definitions[3].DefaultValue);
            Assert.Equal("platform", definitions[4].DefaultValue);
            Assert.Equal(new[] { "platform", "unlimited" }, definitions[4].AllowedValues);
        }

        /// <summary>Every common declaration satisfies the same rules platform declarations must satisfy.</summary>
        [Fact]
        public void CreateDefinitions_PassDeclarationRules() {
            foreach (PlatformSettingDefinition definition in PlayerCommonPlaySettings.CreateDefinitions()) {
                PlatformPlaySettingDeclarationRules.Validate(definition, "definitions");
            }
        }

        /// <summary>Each call returns fresh instances, so no caller can change another caller's declarations.</summary>
        [Fact]
        public void CreateDefinitions_ReturnsFreshArrays() {
            Assert.NotSame(PlayerCommonPlaySettings.CreateDefinitions(), PlayerCommonPlaySettings.CreateDefinitions());
        }

        /// <summary>Only the five common ids are reported as common.</summary>
        [Theory]
        [InlineData("window-scale", true)]
        [InlineData("frame-rate", true)]
        [InlineData("video-mode", false)]
        [InlineData("Window-Scale", false)]
        public void IsCommonId_MatchesOrdinally(string settingId, bool expected) {
            Assert.Equal(expected, PlayerCommonPlaySettings.IsCommonId(settingId));
        }
    }
}
```

Append to `PlatformPlayerCapabilityTests.cs` (inside the class):

```csharp
        /// <summary>A supported capability exposes the Play settings it declares.</summary>
        [Fact]
        public void Ctor_WhenSupported_ExposesPlaySettings() {
            PlatformSettingDefinition gap = new PlatformSettingDefinition("screen-gap", "Screen Gap", PlatformSettingKind.Text, "0", true, []);

            PlatformPlayerCapability capability = new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "ds", "1.0.0", [gap]);

            Assert.Same(gap, Assert.Single(capability.PlaySettings));
            Assert.Same(gap, capability.FindPlaySetting("screen-gap"));
            Assert.Null(capability.FindPlaySetting("video-mode"));
        }

        /// <summary>A Play setting may not reuse a common Play setting id.</summary>
        [Fact]
        public void Ctor_WhenPlaySettingCollidesWithCommonId_Throws() {
            PlatformSettingDefinition collision = new PlatformSettingDefinition("window-scale", "Scale", PlatformSettingKind.Choice, "1x", true, ["1x"]);

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "ps2", "1.0.0", [collision]));

            Assert.Equal("playSettings", exception.ParamName);
            Assert.Contains("window-scale", exception.Message);
        }

        /// <summary>Two Play settings may not share an id.</summary>
        [Fact]
        public void Ctor_WhenPlaySettingIdsRepeat_Throws() {
            PlatformSettingDefinition first = new PlatformSettingDefinition("screen-gap", "Gap", PlatformSettingKind.Text, "0", true, []);
            PlatformSettingDefinition second = new PlatformSettingDefinition("screen-gap", "Gap 2", PlatformSettingKind.Text, "0", true, []);

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "ds", "1.0.0", [first, second]));

            Assert.Contains("screen-gap", exception.Message);
        }

        /// <summary>A choice Play setting's default must be one of its allowed values.</summary>
        [Fact]
        public void Ctor_WhenChoiceDefaultNotAllowed_Throws() {
            PlatformSettingDefinition layout = new PlatformSettingDefinition("screen-layout", "Layout", PlatformSettingKind.Choice, "side", true, ["stacked"]);

            Assert.Throws<ArgumentException>(
                () => new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "ds", "1.0.0", [layout]));
        }

        /// <summary>A boolean Play setting's default must be the text true or false.</summary>
        [Fact]
        public void Ctor_WhenBooleanDefaultIsNotBooleanText_Throws() {
            PlatformSettingDefinition flag = new PlatformSettingDefinition("fast", "Fast", PlatformSettingKind.Boolean, "yes", true, []);

            Assert.Throws<ArgumentException>(
                () => new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "ds", "1.0.0", [flag]));
        }

        /// <summary>The Play settings array is required, even when empty.</summary>
        [Fact]
        public void Ctor_WhenPlaySettingsNull_Throws() {
            Assert.Throws<ArgumentNullException>(
                () => new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "ps2", "1.0.0", null));
        }

        /// <summary>A platform that does not support Play cannot declare Play settings.</summary>
        [Fact]
        public void Ctor_WhenUnsupportedDeclaresPlaySettings_Throws() {
            PlatformSettingDefinition gap = new PlatformSettingDefinition("screen-gap", "Gap", PlatformSettingKind.Text, "0", true, []);

            Assert.Throws<ArgumentException>(
                () => new PlatformPlayerCapability(false, "none", "none", 0d, PlayerInputDevices.None, "none", "none", [gap]));
        }

        /// <summary>The unsupported capability declares no Play settings.</summary>
        [Fact]
        public void CreateUnsupported_DeclaresNoPlaySettings() {
            Assert.Empty(PlatformPlayerCapability.CreateUnsupported().PlaySettings);
        }
```

In the same file, append `, []` as the last argument of every existing `new PlatformPlayerCapability(...)` call (11 call sites).

- [ ] **Step 2: Run tests to verify they fail**

Run: `.\dotnetw.cmd test engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlayerCommonPlaySettingsTests|FullyQualifiedName~PlatformPlayerCapabilityTests"`
Expected: build FAIL, with `CS0103: The name 'PlayerCommonPlaySettings' does not exist` and `CS1729` (no 8-argument constructor).

- [ ] **Step 3: Implement the declarations and rules**

Create `engine/helengine.baseplatform/Definitions/PlayerCommonPlaySettings.cs`:

```csharp
namespace helengine.baseplatform.Definitions {
    /// <summary>
    /// Declares the Play settings the engine offers for every Play platform (window scale and monitor, debugger wait,
    /// shadows and frame rate). Platform-specific Play settings are declared on <see cref="PlatformPlayerCapability.PlaySettings"/>
    /// and may not reuse these ids.
    /// </summary>
    public static class PlayerCommonPlaySettings {
        /// <summary>Id of the window scale setting: an integer multiple of the platform render size, or the largest that fits.</summary>
        public const string WindowScaleId = "window-scale";

        /// <summary>Id of the monitor setting: the monitor the editor window is on, or the primary monitor.</summary>
        public const string WindowMonitorId = "window-monitor";

        /// <summary>Id of the setting that makes the player wait for a debugger before it starts the scene.</summary>
        public const string WaitForDebuggerId = "wait-for-debugger";

        /// <summary>Id of the setting that lets Play turn authored shadows off.</summary>
        public const string ShadowsId = "shadows";

        /// <summary>Id of the frame-rate setting: the platform's real refresh, or unpaced.</summary>
        public const string FrameRateId = "frame-rate";

        /// <summary>
        /// Value a platform Play setting may use to follow the project: the editor replaces it with the project graphics
        /// option of the same id when Play launches.
        /// </summary>
        public const string ProjectValue = "project";

        /// <summary>
        /// Creates fresh declarations of the five common Play settings, in the order the Play Settings dialog lists them.
        /// </summary>
        /// <returns>New array of common Play setting declarations.</returns>
        public static PlatformSettingDefinition[] CreateDefinitions() {
            return [
                new PlatformSettingDefinition(WindowScaleId, "Window Scale", PlatformSettingKind.Choice, "fit", true, ["1x", "2x", "3x", "4x", "fit"]),
                new PlatformSettingDefinition(WindowMonitorId, "Window Monitor", PlatformSettingKind.Choice, "editor", true, ["editor", "primary"]),
                new PlatformSettingDefinition(WaitForDebuggerId, "Wait For Debugger", PlatformSettingKind.Boolean, "false", true, []),
                new PlatformSettingDefinition(ShadowsId, "Shadows", PlatformSettingKind.Boolean, "true", true, []),
                new PlatformSettingDefinition(FrameRateId, "Frame Rate", PlatformSettingKind.Choice, "platform", true, ["platform", "unlimited"])
            ];
        }

        /// <summary>
        /// Reports whether an id names one of the common Play settings, compared ordinally.
        /// </summary>
        /// <param name="settingId">Setting id to test.</param>
        /// <returns>True when the id is a common Play setting id.</returns>
        public static bool IsCommonId(string settingId) {
            return string.Equals(settingId, WindowScaleId, StringComparison.Ordinal)
                || string.Equals(settingId, WindowMonitorId, StringComparison.Ordinal)
                || string.Equals(settingId, WaitForDebuggerId, StringComparison.Ordinal)
                || string.Equals(settingId, ShadowsId, StringComparison.Ordinal)
                || string.Equals(settingId, FrameRateId, StringComparison.Ordinal);
        }
    }
}
```

Create `engine/helengine.baseplatform/Definitions/PlatformPlaySettingDeclarationRules.cs`:

```csharp
namespace helengine.baseplatform.Definitions {
    /// <summary>
    /// Checks one Play setting declaration so the editor can always render and validate it: a choice offers values and
    /// defaults to one of them, and a boolean defaults to the text <c>true</c> or <c>false</c>.
    /// </summary>
    public static class PlatformPlaySettingDeclarationRules {
        /// <summary>
        /// Throws when the declaration cannot be rendered or validated by the Play Settings dialog.
        /// </summary>
        /// <param name="setting">Declaration to check.</param>
        /// <param name="parameterName">Parameter reported by the exception.</param>
        /// <exception cref="ArgumentException">Thrown when the declaration is null or inconsistent.</exception>
        public static void Validate(PlatformSettingDefinition setting, string parameterName) {
            if (setting == null) {
                throw new ArgumentException("Play setting declarations cannot contain null entries.", parameterName);
            } else if (setting.SettingKind == PlatformSettingKind.Choice && setting.AllowedValues.Length == 0) {
                throw new ArgumentException($"Play setting '{setting.SettingId}' is a choice but declares no allowed values.", parameterName);
            } else if (setting.SettingKind == PlatformSettingKind.Choice && Array.IndexOf(setting.AllowedValues, setting.DefaultValue) < 0) {
                throw new ArgumentException($"Play setting '{setting.SettingId}' defaults to '{setting.DefaultValue}', which is not one of its allowed values.", parameterName);
            } else if (setting.SettingKind == PlatformSettingKind.Boolean
                && !string.Equals(setting.DefaultValue, "true", StringComparison.Ordinal)
                && !string.Equals(setting.DefaultValue, "false", StringComparison.Ordinal)) {
                throw new ArgumentException($"Play setting '{setting.SettingId}' is a boolean but defaults to '{setting.DefaultValue}' instead of true or false.", parameterName);
            }
        }
    }
}
```

- [ ] **Step 4: Extend `PlatformPlayerCapability`**

In `PlatformPlayerCapability.cs`, add the parameter `PlatformSettingDefinition[] playSettings` after `runtimePlatformVersion`. Document it:

```csharp
        /// <param name="playSettings">
        /// Platform-specific Play settings (for example PS2 <c>video-mode</c>); may be empty, never null. Ids must be unique
        /// and must not reuse a <see cref="PlayerCommonPlaySettings"/> id. A platform that does not support Play declares none.
        /// </param>
```

Extend the guard chain by appending these branches after the `runtimePlatformVersion` branch:

```csharp
            } else if (playSettings == null) {
                throw new ArgumentNullException(nameof(playSettings), "Play settings are required; pass an empty array when the platform declares none.");
            } else if (!supportsPlay && playSettings.Length > 0) {
                throw new ArgumentException("A platform that does not support Play cannot declare Play settings.", nameof(playSettings));
            }

            HashSet<string> playSettingIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < playSettings.Length; index++) {
                PlatformPlaySettingDeclarationRules.Validate(playSettings[index], nameof(playSettings));
                string settingId = playSettings[index].SettingId;
                if (PlayerCommonPlaySettings.IsCommonId(settingId)) {
                    throw new ArgumentException($"Play setting '{settingId}' reuses a common Play setting id.", nameof(playSettings));
                } else if (!playSettingIds.Add(settingId)) {
                    throw new ArgumentException($"Play setting '{settingId}' is declared more than once.", nameof(playSettings));
                }
            }
```

Assign `PlaySettings = [.. playSettings];` with the other assignments, and add:

```csharp
        /// <summary>
        /// Gets the platform-specific Play settings, shown under the platform section of the Play Settings dialog and
        /// handed to the platform's player adapter; empty when the platform declares none.
        /// </summary>
        public PlatformSettingDefinition[] PlaySettings { get; }

        /// <summary>
        /// Finds one platform Play setting declaration by id, compared ordinally.
        /// </summary>
        /// <param name="settingId">Setting id to look up.</param>
        /// <returns>The declaration, or null when the platform declares no Play setting with that id.</returns>
        public PlatformSettingDefinition FindPlaySetting(string settingId) {
            for (int index = 0; index < PlaySettings.Length; index++) {
                if (string.Equals(PlaySettings[index].SettingId, settingId, StringComparison.Ordinal)) {
                    return PlaySettings[index];
                }
            }

            return null;
        }
```

In `CreateUnsupported`, pass `[]` as the last argument and add "and no Play settings" to its summary.

- [ ] **Step 5: Update the remaining helengine call sites**

Append `, []` as the final argument of each `new PlatformPlayerCapability(` in:
- `engine/helengine.editor.tests/managers/play/EditorPlayPlatformSelectionServiceTests.cs`
- `engine/helengine.editor.tests/managers/play/EditorPlaySessionServiceTests.cs`
- `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerAssetsOnlyTests.cs`

Verify none remain:
Run: Grep tool, pattern `new PlatformPlayerCapability\(` in `engine`. Confirm each hit ends with `[])` or `[...])`.

- [ ] **Step 6: Update the PS2 factory**

In `C:\dev\helworks\helengine-ps2\builder\Ps2PlatformDefinitionFactory.cs`, change the `playerCapability:` argument to:

```csharp
            playerCapability: new PlatformPlayerCapability(
                true,
                "helengine.ps2.builder.Ps2PlayerAdapter",
                "ps2-staging",
                60d,
                PlayerInputDevices.Gamepad,
                Ps2RuntimePlatformIdentity.Name,
                Ps2RuntimePlatformIdentity.Version,
                []));
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `.\dotnetw.cmd test engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlayerCommonPlaySettingsTests|FullyQualifiedName~PlatformPlayerCapabilityTests"`
Expected: PASS.
Run: `.\dotnetw.cmd build engine\helengine.editor.tests\helengine.editor.tests.csproj`
Expected: build succeeds.
Run: `C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Ps2PlatformDefinitionFactoryTests"`
Expected: PASS.

- [ ] **Step 8: Commit (two repos, own hunks only)**

```bash
cd /c/dev/helworks/helengine
git add engine/helengine.baseplatform/Definitions/PlayerCommonPlaySettings.cs engine/helengine.baseplatform/Definitions/PlatformPlaySettingDeclarationRules.cs engine/helengine.baseplatform/Definitions/PlatformPlayerCapability.cs engine/helengine.baseplatform.tests/Player/PlayerCommonPlaySettingsTests.cs engine/helengine.baseplatform.tests/Player/PlatformPlayerCapabilityTests.cs engine/helengine.editor.tests/managers/play/EditorPlayPlatformSelectionServiceTests.cs engine/helengine.editor.tests/managers/play/EditorPlaySessionServiceTests.cs engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerAssetsOnlyTests.cs
git diff --cached --numstat
git commit -m "feat(baseplatform): declare common and per-platform Play settings

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
cd /c/dev/helworks/helengine-ps2
git add builder/Ps2PlatformDefinitionFactory.cs
git commit -m "feat(ps2): declare no PS2-specific Play settings yet

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task A2: `PlatformDefinition` validates `project` Play values

**Files:**
- Modify: `engine/helengine.baseplatform/Definitions/PlatformDefinition.cs` (end of the primary constructor, after `PlayerCapability = ...`)
- Test: `engine/helengine.baseplatform.tests/Definitions/PlatformDefinitionPlaySettingsTests.cs` (new)

**Interfaces:**
- Consumes: `PlayerCommonPlaySettings.ProjectValue`, `PlatformPlayerCapability.PlaySettings` (Task A1)
- Produces: the construction rule "a Play setting that can hold `project` needs a same-id setting in every graphics profile". `ArgumentException` with `ParamName == "playerCapability"`.

- [ ] **Step 1: Write the failing tests**

```csharp
using helengine.baseplatform.Definitions;

namespace helengine.baseplatform.tests.Definitions {
    /// <summary>
    /// Verifies that a platform Play setting which may follow the project names a graphics option every graphics profile declares.
    /// </summary>
    public sealed class PlatformDefinitionPlaySettingsTests {
        /// <summary>A Play setting allowing <c>project</c> is accepted when every graphics profile declares the same id.</summary>
        [Fact]
        public void Ctor_WhenProjectSettingMatchesEveryGraphicsProfile_Succeeds() {
            PlatformDefinition definition = Create(
                [CreateProfile("a", "video-mode"), CreateProfile("b", "video-mode")],
                CreateCapability(new PlatformSettingDefinition("video-mode", "Video Mode", PlatformSettingKind.Choice, "project", true, ["project", "m1"])));

            Assert.Equal("video-mode", Assert.Single(definition.PlayerCapability.PlaySettings).SettingId);
        }

        /// <summary>A Play setting allowing <c>project</c> fails when one graphics profile lacks the id, naming the setting and profile.</summary>
        [Fact]
        public void Ctor_WhenAGraphicsProfileLacksTheProjectSetting_Throws() {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => Create(
                [CreateProfile("a", "video-mode"), CreateProfile("b", "other")],
                CreateCapability(new PlatformSettingDefinition("video-mode", "Video Mode", PlatformSettingKind.Choice, "project", true, ["project", "m1"]))));

            Assert.Equal("playerCapability", exception.ParamName);
            Assert.Contains("video-mode", exception.Message);
            Assert.Contains("'b'", exception.Message);
        }

        /// <summary>A text Play setting defaulting to <c>project</c> follows the same rule.</summary>
        [Fact]
        public void Ctor_WhenTextSettingDefaultsToProjectWithoutGraphicsSetting_Throws() {
            Assert.Throws<ArgumentException>(() => Create(
                [CreateProfile("a", "other")],
                CreateCapability(new PlatformSettingDefinition("resolution", "Resolution", PlatformSettingKind.Text, "project", true, []))));
        }

        /// <summary>A Play setting that cannot hold <c>project</c> needs no graphics counterpart.</summary>
        [Fact]
        public void Ctor_WhenSettingCannotFollowProject_NeedsNoGraphicsSetting() {
            PlatformDefinition definition = Create(
                [CreateProfile("a", "other")],
                CreateCapability(new PlatformSettingDefinition("screen-gap", "Gap", PlatformSettingKind.Text, "0", true, [])));

            Assert.Single(definition.PlayerCapability.PlaySettings);
        }

        /// <summary>Creates a platform definition with the supplied graphics profiles and Play capability.</summary>
        static PlatformDefinition Create(PlatformGraphicsProfileDefinition[] graphicsProfiles, PlatformPlayerCapability capability) {
            return new PlatformDefinition("ps2", "PlayStation 2", [], graphicsProfiles, [], [], [], [], [], [], playerCapability: capability);
        }

        /// <summary>Creates a graphics profile declaring one choice setting with the given id.</summary>
        static PlatformGraphicsProfileDefinition CreateProfile(string profileId, string settingId) {
            return new PlatformGraphicsProfileDefinition(profileId, profileId, profileId,
                [new PlatformSettingDefinition(settingId, settingId, PlatformSettingKind.Choice, "m1", true, ["m1"])]);
        }

        /// <summary>Creates a supported Play capability with one platform Play setting.</summary>
        static PlatformPlayerCapability CreateCapability(PlatformSettingDefinition playSetting) {
            return new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "ps2", "1.0.0", [playSetting]);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `.\dotnetw.cmd test engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlatformDefinitionPlaySettingsTests"`
Expected: FAIL. `Ctor_WhenAGraphicsProfileLacksTheProjectSetting_Throws` and `Ctor_WhenTextSettingDefaultsToProjectWithoutGraphicsSetting_Throws` report "No exception was thrown".

- [ ] **Step 3: Implement**

At the end of the primary `PlatformDefinition` constructor (after `PlayerCapability = ...`), add `ValidateProjectPlaySettings(PlayerCapability, GraphicsProfiles);` and this method:

```csharp
        /// <summary>
        /// Ensures every Play setting that may hold <see cref="PlayerCommonPlaySettings.ProjectValue"/> names a graphics option
        /// declared by every graphics profile, because the editor resolves <c>project</c> to that option when Play launches.
        /// </summary>
        /// <param name="playerCapability">Play capability whose settings are checked.</param>
        /// <param name="graphicsProfiles">Graphics profiles the project may select.</param>
        /// <exception cref="ArgumentException">Thrown when a graphics profile lacks the option a Play setting follows.</exception>
        static void ValidateProjectPlaySettings(PlatformPlayerCapability playerCapability, PlatformGraphicsProfileDefinition[] graphicsProfiles) {
            for (int settingIndex = 0; settingIndex < playerCapability.PlaySettings.Length; settingIndex++) {
                PlatformSettingDefinition playSetting = playerCapability.PlaySettings[settingIndex];
                bool canFollowProject = Array.IndexOf(playSetting.AllowedValues, PlayerCommonPlaySettings.ProjectValue) >= 0
                    || string.Equals(playSetting.DefaultValue, PlayerCommonPlaySettings.ProjectValue, StringComparison.Ordinal);
                if (!canFollowProject) {
                    continue;
                }

                for (int profileIndex = 0; profileIndex < graphicsProfiles.Length; profileIndex++) {
                    PlatformGraphicsProfileDefinition profile = graphicsProfiles[profileIndex];
                    if (!Array.Exists(profile.Settings, setting => string.Equals(setting.SettingId, playSetting.SettingId, StringComparison.Ordinal))) {
                        throw new ArgumentException(
                            $"Play setting '{playSetting.SettingId}' can follow the project, but graphics profile '{profile.ProfileId}' declares no '{playSetting.SettingId}' option.",
                            "playerCapability");
                    }
                }
            }
        }
```

(Check the profile-id property name in `PlatformGraphicsProfileDefinition.cs`. If it is not `ProfileId`, use the existing property.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `.\dotnetw.cmd test engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj`
Expected: PASS (whole project; no regressions).

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.baseplatform/Definitions/PlatformDefinition.cs engine/helengine.baseplatform.tests/Definitions/PlatformDefinitionPlaySettingsTests.cs
git commit -m "feat(baseplatform): require a graphics option behind every project-following Play setting

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task A3: The `--play-settings` file contract

**Files:**
- Create: `engine/helengine.baseplatform/Player/PlayerWindowScale.cs`, `PlayerWindowScaleText.cs`, `PlayerWindowMonitor.cs`, `PlayerWindowMonitorText.cs`, `PlayerPlaySettings.cs`, `PlayerPlaySettingsFile.cs`
- Test: `engine/helengine.baseplatform.tests/Player/PlayerPlaySettingsFileTests.cs` (new)

**Interfaces:**
- Produces:
  - `enum PlayerWindowScale { One, Two, Three, Four, Fit }`
  - `PlayerWindowScaleText.Parse(string): PlayerWindowScale`, `Format(PlayerWindowScale): string`, `ToMultiplier(PlayerWindowScale): int` (`Fit` throws)
  - `enum PlayerWindowMonitor { Editor, Primary }`
  - `PlayerWindowMonitorText.Parse(string)` and `Format(PlayerWindowMonitor)`
  - `PlayerPlaySettings(PlayerWindowScale windowScale, PlayerWindowMonitor windowMonitor, int editorProcessId, bool waitForDebugger, bool shadowsEnabled, bool frameRateUnlimited, double targetFrameRate, IReadOnlyDictionary<string, string> platformValues)` with same-named properties
  - `PlayerPlaySettingsFile.CurrentVersion = 1`
  - `PlayerPlaySettingsFile.Write(string path, PlayerPlaySettings settings): void`
  - `PlayerPlaySettingsFile.Read(string path): PlayerPlaySettings`

- [ ] **Step 1: Write the failing tests**

```csharp
using helengine.baseplatform.Player;

namespace helengine.baseplatform.tests.Player {
    /// <summary>
    /// Verifies the file the editor writes and the player reads for one Play session round-trips exactly and rejects
    /// anything outside the current format.
    /// </summary>
    public sealed class PlayerPlaySettingsFileTests : IDisposable {
        /// <summary>Workspace-owned scratch root (never %TEMP%).</summary>
        readonly string Root = Path.Combine(@"C:\dev\helworks\builds\helengine\tests\play-settings-file", Guid.NewGuid().ToString("N"));

        /// <summary>Creates the scratch root.</summary>
        public PlayerPlaySettingsFileTests() {
            Directory.CreateDirectory(Root);
        }

        /// <summary>Deletes the scratch root.</summary>
        public void Dispose() {
            Directory.Delete(Root, true);
        }

        /// <summary>Every field survives a write and read.</summary>
        [Fact]
        public void WriteThenRead_RoundTripsEveryField() {
            string path = Path.Combine(Root, "play-settings.json");
            PlayerPlaySettings written = new PlayerPlaySettings(
                PlayerWindowScale.Three, PlayerWindowMonitor.Primary, 4242, true, false, false, 59.94d,
                new Dictionary<string, string> { ["screen-gap"] = "8" });

            PlayerPlaySettingsFile.Write(path, written);
            PlayerPlaySettings read = PlayerPlaySettingsFile.Read(path);

            Assert.Equal(PlayerWindowScale.Three, read.WindowScale);
            Assert.Equal(PlayerWindowMonitor.Primary, read.WindowMonitor);
            Assert.Equal(4242, read.EditorProcessId);
            Assert.True(read.WaitForDebugger);
            Assert.False(read.ShadowsEnabled);
            Assert.False(read.FrameRateUnlimited);
            Assert.Equal(59.94d, read.TargetFrameRate);
            Assert.Equal("8", read.PlatformValues["screen-gap"]);
        }

        /// <summary>An unlimited frame rate round-trips with no target.</summary>
        [Fact]
        public void WriteThenRead_UnlimitedFrameRate() {
            string path = Path.Combine(Root, "unlimited.json");
            PlayerPlaySettingsFile.Write(path, new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, true, 0d, new Dictionary<string, string>()));

            PlayerPlaySettings read = PlayerPlaySettingsFile.Read(path);

            Assert.True(read.FrameRateUnlimited);
            Assert.Equal(0d, read.TargetFrameRate);
        }

        /// <summary>A file of another version is rejected, naming the file and the version.</summary>
        [Fact]
        public void Read_WhenVersionIsNotCurrent_ThrowsNamingFileAndVersion() {
            string path = WriteRaw("v2.json", "{\"version\":2}");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => PlayerPlaySettingsFile.Read(path));

            Assert.Contains(path, exception.Message);
            Assert.Contains("2", exception.Message);
        }

        /// <summary>An unknown property is rejected, naming it.</summary>
        [Fact]
        public void Read_WhenPropertyUnknown_ThrowsNamingProperty() {
            string path = Path.Combine(Root, "known.json");
            PlayerPlaySettingsFile.Write(path, new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, false, 60d, new Dictionary<string, string>()));
            string json = File.ReadAllText(path).Replace("\"version\": 1", "\"version\": 1, \"targetFps\": 60");
            File.WriteAllText(path, json);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => PlayerPlaySettingsFile.Read(path));

            Assert.Contains("targetFps", exception.Message);
        }

        /// <summary>A paced frame rate needs a positive target; an unlimited one must not carry one.</summary>
        [Fact]
        public void Ctor_RejectsInconsistentFrameRate() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, false, 0d, new Dictionary<string, string>()));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, true, 60d, new Dictionary<string, string>()));
        }

        /// <summary>The editor process id must be positive so the player can find the editor's monitor.</summary>
        [Fact]
        public void Ctor_RejectsNonPositiveEditorProcessId() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 0, false, true, false, 60d, new Dictionary<string, string>()));
        }

        /// <summary>Window scale text maps both ways and rejects unknown text.</summary>
        [Fact]
        public void WindowScaleText_MapsEveryValue() {
            foreach (string text in new[] { "1x", "2x", "3x", "4x", "fit" }) {
                Assert.Equal(text, PlayerWindowScaleText.Format(PlayerWindowScaleText.Parse(text)));
            }

            Assert.Equal(3, PlayerWindowScaleText.ToMultiplier(PlayerWindowScale.Three));
            Assert.Throws<ArgumentException>(() => PlayerWindowScaleText.Parse("5x"));
            Assert.Throws<ArgumentException>(() => PlayerWindowScaleText.ToMultiplier(PlayerWindowScale.Fit));
        }

        /// <summary>Writes raw JSON text into the scratch root.</summary>
        string WriteRaw(string name, string json) {
            string path = Path.Combine(Root, name);
            File.WriteAllText(path, json);
            return path;
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `.\dotnetw.cmd test engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlayerPlaySettingsFileTests"`
Expected: build FAIL (`PlayerPlaySettings` not found).

- [ ] **Step 3: Implement the enums and text maps**

`engine/helengine.baseplatform/Player/PlayerWindowScale.cs`:

```csharp
namespace helengine.baseplatform.Player {
    /// <summary>
    /// How large the Play window is relative to the platform render size: a fixed integer multiple, or the largest
    /// integer multiple that fits the chosen monitor's work area.
    /// </summary>
    public enum PlayerWindowScale {
        /// <summary>The window client equals the render size.</summary>
        One,

        /// <summary>The window client is twice the render size.</summary>
        Two,

        /// <summary>The window client is three times the render size.</summary>
        Three,

        /// <summary>The window client is four times the render size.</summary>
        Four,

        /// <summary>The largest integer multiple (at least one) whose window fits the monitor work area.</summary>
        Fit
    }
}
```

`engine/helengine.baseplatform/Player/PlayerWindowScaleText.cs`:

```csharp
namespace helengine.baseplatform.Player {
    /// <summary>
    /// Converts <see cref="PlayerWindowScale"/> to and from the <c>window-scale</c> Play setting text (<c>1x</c> to <c>4x</c>, <c>fit</c>).
    /// </summary>
    public static class PlayerWindowScaleText {
        /// <summary>
        /// Parses one validated <c>window-scale</c> value.
        /// </summary>
        /// <param name="text">Setting text.</param>
        /// <returns>The window scale.</returns>
        /// <exception cref="ArgumentException">Thrown when the text is not a window scale.</exception>
        public static PlayerWindowScale Parse(string text) {
            if (string.Equals(text, "1x", StringComparison.Ordinal)) {
                return PlayerWindowScale.One;
            } else if (string.Equals(text, "2x", StringComparison.Ordinal)) {
                return PlayerWindowScale.Two;
            } else if (string.Equals(text, "3x", StringComparison.Ordinal)) {
                return PlayerWindowScale.Three;
            } else if (string.Equals(text, "4x", StringComparison.Ordinal)) {
                return PlayerWindowScale.Four;
            } else if (string.Equals(text, "fit", StringComparison.Ordinal)) {
                return PlayerWindowScale.Fit;
            }

            throw new ArgumentException($"'{text}' is not a window scale; expected 1x, 2x, 3x, 4x or fit.", nameof(text));
        }

        /// <summary>
        /// Formats one window scale as its setting text.
        /// </summary>
        /// <param name="scale">Window scale.</param>
        /// <returns>The setting text.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for an undefined enum value.</exception>
        public static string Format(PlayerWindowScale scale) {
            switch (scale) {
                case PlayerWindowScale.One: return "1x";
                case PlayerWindowScale.Two: return "2x";
                case PlayerWindowScale.Three: return "3x";
                case PlayerWindowScale.Four: return "4x";
                case PlayerWindowScale.Fit: return "fit";
                default: throw new ArgumentOutOfRangeException(nameof(scale), scale, "Unknown window scale.");
            }
        }

        /// <summary>
        /// Returns the integer multiplier of a fixed window scale.
        /// </summary>
        /// <param name="scale">Fixed window scale.</param>
        /// <returns>1 to 4.</returns>
        /// <exception cref="ArgumentException">Thrown for <see cref="PlayerWindowScale.Fit"/>, whose multiplier depends on the monitor.</exception>
        public static int ToMultiplier(PlayerWindowScale scale) {
            if (scale == PlayerWindowScale.Fit) {
                throw new ArgumentException("The fit window scale has no fixed multiplier; compute it from the monitor work area.", nameof(scale));
            }

            return (int)scale + 1;
        }
    }
}
```

`engine/helengine.baseplatform/Player/PlayerWindowMonitor.cs`:

```csharp
namespace helengine.baseplatform.Player {
    /// <summary>
    /// Which monitor the Play window opens on.
    /// </summary>
    public enum PlayerWindowMonitor {
        /// <summary>The monitor that shows the editor's main window.</summary>
        Editor,

        /// <summary>The primary monitor.</summary>
        Primary
    }
}
```

`engine/helengine.baseplatform/Player/PlayerWindowMonitorText.cs`:

```csharp
namespace helengine.baseplatform.Player {
    /// <summary>
    /// Converts <see cref="PlayerWindowMonitor"/> to and from the <c>window-monitor</c> Play setting text.
    /// </summary>
    public static class PlayerWindowMonitorText {
        /// <summary>
        /// Parses one validated <c>window-monitor</c> value.
        /// </summary>
        /// <param name="text">Setting text (<c>editor</c> or <c>primary</c>).</param>
        /// <returns>The monitor choice.</returns>
        /// <exception cref="ArgumentException">Thrown when the text is not a monitor choice.</exception>
        public static PlayerWindowMonitor Parse(string text) {
            if (string.Equals(text, "editor", StringComparison.Ordinal)) {
                return PlayerWindowMonitor.Editor;
            } else if (string.Equals(text, "primary", StringComparison.Ordinal)) {
                return PlayerWindowMonitor.Primary;
            }

            throw new ArgumentException($"'{text}' is not a window monitor; expected editor or primary.", nameof(text));
        }

        /// <summary>
        /// Formats one monitor choice as its setting text.
        /// </summary>
        /// <param name="monitor">Monitor choice.</param>
        /// <returns>The setting text.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for an undefined enum value.</exception>
        public static string Format(PlayerWindowMonitor monitor) {
            switch (monitor) {
                case PlayerWindowMonitor.Editor: return "editor";
                case PlayerWindowMonitor.Primary: return "primary";
                default: throw new ArgumentOutOfRangeException(nameof(monitor), monitor, "Unknown window monitor.");
            }
        }
    }
}
```

- [ ] **Step 4: Implement `PlayerPlaySettings`**

```csharp
namespace helengine.baseplatform.Player {
    /// <summary>
    /// The Play settings of one Play session, fully resolved by the editor (no <c>project</c> placeholders remain) and
    /// handed to the player through the <c>--play-settings</c> file.
    /// </summary>
    public sealed class PlayerPlaySettings {
        /// <summary>
        /// Initializes resolved Play settings.
        /// </summary>
        /// <param name="windowScale">Window scale relative to the platform render size.</param>
        /// <param name="windowMonitor">Monitor the window opens on.</param>
        /// <param name="editorProcessId">Process id of the editor, used to find the monitor its main window is on.</param>
        /// <param name="waitForDebugger">Whether the player waits for a debugger before starting the scene.</param>
        /// <param name="shadowsEnabled">Whether authored shadows render.</param>
        /// <param name="frameRateUnlimited">Whether the frame loop runs unpaced and without vsync.</param>
        /// <param name="targetFrameRate">Frames per second the loop is paced to; 0 exactly when unlimited.</param>
        /// <param name="platformValues">Resolved platform Play setting values keyed by setting id.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a non-positive process id or an inconsistent frame rate.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the platform values are null.</exception>
        /// <exception cref="ArgumentException">Thrown when a platform value or key is blank or null.</exception>
        public PlayerPlaySettings(
            PlayerWindowScale windowScale,
            PlayerWindowMonitor windowMonitor,
            int editorProcessId,
            bool waitForDebugger,
            bool shadowsEnabled,
            bool frameRateUnlimited,
            double targetFrameRate,
            IReadOnlyDictionary<string, string> platformValues) {
            if (editorProcessId <= 0) {
                throw new ArgumentOutOfRangeException(nameof(editorProcessId), editorProcessId, "The editor process id must be positive.");
            } else if (frameRateUnlimited && targetFrameRate != 0d) {
                throw new ArgumentOutOfRangeException(nameof(targetFrameRate), targetFrameRate, "An unlimited frame rate carries no target frame rate.");
            } else if (!frameRateUnlimited && !(targetFrameRate > 0d && double.IsFinite(targetFrameRate))) {
                throw new ArgumentOutOfRangeException(nameof(targetFrameRate), targetFrameRate, "A paced frame rate needs a positive, finite target frame rate.");
            } else if (platformValues == null) {
                throw new ArgumentNullException(nameof(platformValues));
            }

            Dictionary<string, string> copy = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> entry in platformValues) {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null) {
                    throw new ArgumentException("Platform Play values need non-blank ids and non-null values.", nameof(platformValues));
                }
                copy.Add(entry.Key, entry.Value);
            }

            WindowScale = windowScale;
            WindowMonitor = windowMonitor;
            EditorProcessId = editorProcessId;
            WaitForDebugger = waitForDebugger;
            ShadowsEnabled = shadowsEnabled;
            FrameRateUnlimited = frameRateUnlimited;
            TargetFrameRate = targetFrameRate;
            PlatformValues = copy;
        }

        /// <summary>Gets the window scale relative to the platform render size.</summary>
        public PlayerWindowScale WindowScale { get; }

        /// <summary>Gets the monitor the window opens on.</summary>
        public PlayerWindowMonitor WindowMonitor { get; }

        /// <summary>Gets the editor process id used to find the editor's monitor.</summary>
        public int EditorProcessId { get; }

        /// <summary>Gets whether the player waits for a debugger before starting the scene.</summary>
        public bool WaitForDebugger { get; }

        /// <summary>Gets whether authored shadows render in this Play session.</summary>
        public bool ShadowsEnabled { get; }

        /// <summary>Gets whether the frame loop runs unpaced and without vsync.</summary>
        public bool FrameRateUnlimited { get; }

        /// <summary>Gets the paced frames per second; 0 when <see cref="FrameRateUnlimited"/>.</summary>
        public double TargetFrameRate { get; }

        /// <summary>Gets the resolved platform Play setting values keyed by setting id.</summary>
        public IReadOnlyDictionary<string, string> PlatformValues { get; }
    }
}
```

- [ ] **Step 5: Implement `PlayerPlaySettingsFile`**

```csharp
using System.Globalization;
using System.Text.Json;

namespace helengine.baseplatform.Player {
    /// <summary>
    /// Reads and writes the <c>--play-settings</c> file. The format is the current one only: version 1, a fixed set of
    /// properties, every property required. Anything else fails and names the file.
    /// </summary>
    public static class PlayerPlaySettingsFile {
        /// <summary>The only format version this build reads and writes.</summary>
        public const int CurrentVersion = 1;

        /// <summary>Property names, in write order.</summary>
        static readonly string[] PropertyNames = [
            "version", "windowScale", "windowMonitor", "editorProcessId", "waitForDebugger",
            "shadowsEnabled", "frameRateUnlimited", "targetFrameRate", "platform"
        ];

        /// <summary>
        /// Writes one resolved settings file, replacing any previous one.
        /// </summary>
        /// <param name="path">Absolute file path.</param>
        /// <param name="settings">Resolved settings.</param>
        /// <exception cref="ArgumentException">Thrown when the path is blank.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the settings are null.</exception>
        public static void Write(string path, PlayerPlaySettings settings) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("The Play settings file path is required.", nameof(path));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using FileStream stream = File.Create(path);
            using Utf8JsonWriter writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            writer.WriteStartObject();
            writer.WriteNumber("version", CurrentVersion);
            writer.WriteString("windowScale", PlayerWindowScaleText.Format(settings.WindowScale));
            writer.WriteString("windowMonitor", PlayerWindowMonitorText.Format(settings.WindowMonitor));
            writer.WriteNumber("editorProcessId", settings.EditorProcessId);
            writer.WriteBoolean("waitForDebugger", settings.WaitForDebugger);
            writer.WriteBoolean("shadowsEnabled", settings.ShadowsEnabled);
            writer.WriteBoolean("frameRateUnlimited", settings.FrameRateUnlimited);
            writer.WriteNumber("targetFrameRate", settings.TargetFrameRate);
            writer.WriteStartObject("platform");
            foreach (KeyValuePair<string, string> entry in settings.PlatformValues) {
                writer.WriteString(entry.Key, entry.Value);
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        /// <summary>
        /// Reads one settings file written by <see cref="Write"/>.
        /// </summary>
        /// <param name="path">Absolute file path.</param>
        /// <returns>The resolved settings.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
        /// <exception cref="InvalidOperationException">Thrown for another version, an unknown or missing property, or a wrongly typed value.</exception>
        public static PlayerPlaySettings Read(string path) {
            if (!File.Exists(path)) {
                throw new FileNotFoundException("The Play settings file does not exist.", path);
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) {
                throw new InvalidOperationException($"Play settings file '{path}' must hold one JSON object.");
            }

            foreach (JsonProperty property in root.EnumerateObject()) {
                if (Array.IndexOf(PropertyNames, property.Name) < 0) {
                    throw new InvalidOperationException($"Play settings file '{path}' has unknown property '{property.Name}'.");
                }
            }

            int version = RequireProperty(root, "version", JsonValueKind.Number, path).GetInt32();
            if (version != CurrentVersion) {
                throw new InvalidOperationException($"Play settings file '{path}' has version {version}; this player reads version {CurrentVersion} only.");
            }

            Dictionary<string, string> platformValues = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JsonProperty entry in RequireProperty(root, "platform", JsonValueKind.Object, path).EnumerateObject()) {
                if (entry.Value.ValueKind != JsonValueKind.String) {
                    throw new InvalidOperationException($"Play settings file '{path}' platform value '{entry.Name}' must be a string.");
                }
                platformValues.Add(entry.Name, entry.Value.GetString());
            }

            return new PlayerPlaySettings(
                PlayerWindowScaleText.Parse(RequireProperty(root, "windowScale", JsonValueKind.String, path).GetString()),
                PlayerWindowMonitorText.Parse(RequireProperty(root, "windowMonitor", JsonValueKind.String, path).GetString()),
                RequireProperty(root, "editorProcessId", JsonValueKind.Number, path).GetInt32(),
                RequireBoolean(root, "waitForDebugger", path),
                RequireBoolean(root, "shadowsEnabled", path),
                RequireBoolean(root, "frameRateUnlimited", path),
                RequireProperty(root, "targetFrameRate", JsonValueKind.Number, path).GetDouble(),
                platformValues);
        }

        /// <summary>
        /// Returns one required property of the expected JSON kind.
        /// </summary>
        /// <param name="root">Root object.</param>
        /// <param name="name">Property name.</param>
        /// <param name="kind">Expected value kind.</param>
        /// <param name="path">File path reported on failure.</param>
        /// <returns>The property value.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the property is missing or of another kind.</exception>
        static JsonElement RequireProperty(JsonElement root, string name, JsonValueKind kind, string path) {
            if (!root.TryGetProperty(name, out JsonElement value)) {
                throw new InvalidOperationException($"Play settings file '{path}' is missing property '{name}'.");
            } else if (value.ValueKind != kind) {
                throw new InvalidOperationException($"Play settings file '{path}' property '{name}' must be a JSON {kind.ToString().ToLower(CultureInfo.InvariantCulture)}.");
            }

            return value;
        }

        /// <summary>
        /// Returns one required boolean property.
        /// </summary>
        /// <param name="root">Root object.</param>
        /// <param name="name">Property name.</param>
        /// <param name="path">File path reported on failure.</param>
        /// <returns>The boolean value.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the property is missing or not a boolean.</exception>
        static bool RequireBoolean(JsonElement root, string name, string path) {
            if (!root.TryGetProperty(name, out JsonElement value)) {
                throw new InvalidOperationException($"Play settings file '{path}' is missing property '{name}'.");
            } else if (value.ValueKind == JsonValueKind.True) {
                return true;
            } else if (value.ValueKind == JsonValueKind.False) {
                return false;
            }

            throw new InvalidOperationException($"Play settings file '{path}' property '{name}' must be true or false.");
        }
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `.\dotnetw.cmd test engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlayerPlaySettingsFileTests"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add engine/helengine.baseplatform/Player/PlayerWindowScale.cs engine/helengine.baseplatform/Player/PlayerWindowScaleText.cs engine/helengine.baseplatform/Player/PlayerWindowMonitor.cs engine/helengine.baseplatform/Player/PlayerWindowMonitorText.cs engine/helengine.baseplatform/Player/PlayerPlaySettings.cs engine/helengine.baseplatform/Player/PlayerPlaySettingsFile.cs engine/helengine.baseplatform.tests/Player/PlayerPlaySettingsFileTests.cs
git commit -m "feat(baseplatform): define the resolved Play settings file

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task A4: Adapters declare their render size from the Play settings

**Files:**
- Create: `engine/helengine.baseplatform/Player/PlayerRenderSize.cs`
- Modify: `engine/helengine.baseplatform/Player/IPlatformPlayerAdapter.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder\player\Ps2PlayerAdapter.cs`
- Test: `engine/helengine.baseplatform.tests/Player/PlayerRenderSizeTests.cs` (new), `C:\dev\helworks\helengine-ps2\builder.tests\Ps2PlayerAdapterTests.cs`

**Interfaces:**
- Consumes: `PlayerPlaySettings` (Task A3)
- Produces:
  - `PlayerRenderSize(int width, int height)` with `Width`/`Height` (1..8192)
  - `IPlatformPlayerAdapter.ApplyPlaySettings(PlayerPlaySettings playSettings): PlayerRenderSize`, called once after `Initialize` and before content loads
  - `Ps2PlayerAdapter.Phase A behaviour`: returns 640x448

- [ ] **Step 1: Write the failing tests**

`engine/helengine.baseplatform.tests/Player/PlayerRenderSizeTests.cs`:

```csharp
using helengine.baseplatform.Player;

namespace helengine.baseplatform.tests.Player {
    /// <summary>
    /// Verifies the render size an adapter declares is validated.
    /// </summary>
    public sealed class PlayerRenderSizeTests {
        /// <summary>A positive size is kept.</summary>
        [Fact]
        public void Ctor_KeepsPositiveSize() {
            PlayerRenderSize size = new PlayerRenderSize(320, 224);

            Assert.Equal(320, size.Width);
            Assert.Equal(224, size.Height);
        }

        /// <summary>Zero, negative and absurd sizes are rejected.</summary>
        [Theory]
        [InlineData(0, 224)]
        [InlineData(320, -1)]
        [InlineData(8193, 224)]
        public void Ctor_RejectsInvalidSize(int width, int height) {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerRenderSize(width, height));
        }
    }
}
```

Append to `Ps2PlayerAdapterTests.cs`:

```csharp
    /// <summary>
    /// The PS2 adapter declares the PS2 framebuffer (640x448) as the Play render size until the video mode is configurable.
    /// </summary>
    [Fact]
    public void ApplyPlaySettings_DeclaresThePs2FramebufferSize() {
        Ps2PlayerAdapter adapter = new Ps2PlayerAdapter();
        PlayerPlaySettings settings = new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, false, 60d, new Dictionary<string, string>());

        PlayerRenderSize size = adapter.ApplyPlaySettings(settings);

        Assert.Equal(640, size.Width);
        Assert.Equal(448, size.Height);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `.\dotnetw.cmd test engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlayerRenderSizeTests"`
Expected: build FAIL (`PlayerRenderSize` not found).

- [ ] **Step 3: Implement**

`engine/helengine.baseplatform/Player/PlayerRenderSize.cs`:

```csharp
namespace helengine.baseplatform.Player {
    /// <summary>
    /// The size, in pixels, of the frame a platform renders during Play. The player renders into an offscreen target of
    /// exactly this size and magnifies it into the window by an integer scale.
    /// </summary>
    public sealed class PlayerRenderSize {
        /// <summary>Largest accepted width or height.</summary>
        public const int MaximumDimension = 8192;

        /// <summary>
        /// Initializes one render size.
        /// </summary>
        /// <param name="width">Width in pixels, 1 to <see cref="MaximumDimension"/>.</param>
        /// <param name="height">Height in pixels, 1 to <see cref="MaximumDimension"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension is outside the accepted range.</exception>
        public PlayerRenderSize(int width, int height) {
            if (width < 1 || width > MaximumDimension) {
                throw new ArgumentOutOfRangeException(nameof(width), width, "The Play render width must be between 1 and 8192 pixels.");
            } else if (height < 1 || height > MaximumDimension) {
                throw new ArgumentOutOfRangeException(nameof(height), height, "The Play render height must be between 1 and 8192 pixels.");
            }

            Width = width;
            Height = height;
        }

        /// <summary>Gets the render width in pixels.</summary>
        public int Width { get; }

        /// <summary>Gets the render height in pixels.</summary>
        public int Height { get; }
    }
}
```

Add to `IPlatformPlayerAdapter` after `Initialize`:

```csharp
        /// <summary>
        /// Applies the resolved Play settings of this session (including the platform's own Play settings in
        /// <see cref="PlayerPlaySettings.PlatformValues"/>) and declares the size of the frame the platform renders. Called
        /// once, after <see cref="Initialize"/> and before any content is loaded.
        /// </summary>
        /// <param name="playSettings">Resolved Play settings.</param>
        /// <returns>The render size the player renders at before magnifying into the window.</returns>
        PlayerRenderSize ApplyPlaySettings(PlayerPlaySettings playSettings);
```

In `Ps2PlayerAdapter.cs` add:

```csharp
    /// <summary>
    /// Width of the PS2 framebuffer the native runtime initializes gsKit with (Ps2BootHost).
    /// </summary>
    public const int FramebufferWidth = 640;

    /// <summary>
    /// Height of the PS2 framebuffer the native runtime initializes gsKit with (Ps2BootHost).
    /// </summary>
    public const int FramebufferHeight = 448;

    /// <summary>
    /// Declares the PS2 framebuffer size as the Play render size. The PS2 declares no platform Play settings yet.
    /// </summary>
    /// <param name="playSettings">Resolved Play settings of this session.</param>
    /// <returns>The 640x448 PS2 framebuffer size.</returns>
    public PlayerRenderSize ApplyPlaySettings(PlayerPlaySettings playSettings) {
        if (playSettings == null) {
            throw new ArgumentNullException(nameof(playSettings));
        }

        return new PlayerRenderSize(FramebufferWidth, FramebufferHeight);
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `.\dotnetw.cmd test engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlayerRenderSizeTests"` and `C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Ps2PlayerAdapterTests"`
Expected: PASS for both.

- [ ] **Step 5: Commit**

```bash
cd /c/dev/helworks/helengine
git add engine/helengine.baseplatform/Player/PlayerRenderSize.cs engine/helengine.baseplatform/Player/IPlatformPlayerAdapter.cs engine/helengine.baseplatform.tests/Player/PlayerRenderSizeTests.cs
git commit -m "feat(baseplatform): let the player adapter declare its Play render size

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
cd /c/dev/helworks/helengine-ps2
git add builder/player/Ps2PlayerAdapter.cs builder.tests/Ps2PlayerAdapterTests.cs
git commit -m "feat(ps2): declare the 640x448 framebuffer as the Play render size

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task A5: `user_settings/play.json` store

**Files:**
- Create: `engine/helengine.editor/managers/play/EditorPlaySettingsDocument.cs`
- Create: `engine/helengine.editor/managers/play/EditorPlaySettingsStore.cs`
- Test: `engine/helengine.editor.tests/managers/play/EditorPlaySettingsStoreTests.cs` (new)

**Interfaces:**
- Consumes: `PlayerCommonPlaySettings.IsCommonId` (Task A1)
- Produces:
  - `EditorPlaySettingsDocument()` with `CommonValues: Dictionary<string,string>`, `PlatformValues: Dictionary<string, Dictionary<string,string>>`, `Clone(): EditorPlaySettingsDocument`
  - `EditorPlaySettingsStore(string projectRootPath)` with `FilePath: string`, `Load(): EditorPlaySettingsDocument`, `Save(EditorPlaySettingsDocument): void`, `const int CurrentVersion = 1`

- [ ] **Step 1: Write the failing tests**

```csharp
using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Verifies the per-user Play settings store in <c>user_settings/play.json</c>.
/// </summary>
public sealed class EditorPlaySettingsStoreTests : IDisposable {
    /// <summary>Workspace-owned scratch project root.</summary>
    readonly string ProjectRoot = Path.Combine(@"C:\dev\helworks\builds\helengine\tests\play-settings-store", Guid.NewGuid().ToString("N"));

    /// <summary>Creates the scratch project root.</summary>
    public EditorPlaySettingsStoreTests() {
        Directory.CreateDirectory(ProjectRoot);
    }

    /// <summary>Deletes the scratch project root.</summary>
    public void Dispose() {
        Directory.Delete(ProjectRoot, true);
    }

    /// <summary>A missing file loads as an empty document and is not created.</summary>
    [Fact]
    public void Load_WhenFileMissing_ReturnsEmptyDocumentWithoutWriting() {
        EditorPlaySettingsStore store = new EditorPlaySettingsStore(ProjectRoot);

        EditorPlaySettingsDocument document = store.Load();

        Assert.Empty(document.CommonValues);
        Assert.Empty(document.PlatformValues);
        Assert.False(File.Exists(store.FilePath));
        Assert.Equal(Path.Combine(ProjectRoot, "user_settings", "play.json"), store.FilePath);
    }

    /// <summary>Common values, booleans and every platform section (including ones for other platforms) round-trip.</summary>
    [Fact]
    public void SaveThenLoad_RoundTripsAllSections() {
        EditorPlaySettingsStore store = new EditorPlaySettingsStore(ProjectRoot);
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.CommonValues["window-scale"] = "2x";
        document.CommonValues["wait-for-debugger"] = "true";
        document.PlatformValues["ps2"] = new Dictionary<string, string> { ["video-mode"] = "project" };
        document.PlatformValues["ds"] = new Dictionary<string, string> { ["screen-gap"] = "8" };

        store.Save(document);
        EditorPlaySettingsDocument loaded = store.Load();

        Assert.Equal("2x", loaded.CommonValues["window-scale"]);
        Assert.Equal("true", loaded.CommonValues["wait-for-debugger"]);
        Assert.Equal("project", loaded.PlatformValues["ps2"]["video-mode"]);
        Assert.Equal("8", loaded.PlatformValues["ds"]["screen-gap"]);
        Assert.Contains("\"wait-for-debugger\": true", File.ReadAllText(store.FilePath));
    }

    /// <summary>Another version fails, naming the file and the version.</summary>
    [Fact]
    public void Load_WhenVersionUnknown_ThrowsNamingFileAndVersion() {
        EditorPlaySettingsStore store = WriteRaw("{ \"version\": 7, \"common\": {} }");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.Load());

        Assert.Contains(store.FilePath, exception.Message);
        Assert.Contains("7", exception.Message);
    }

    /// <summary>A common id the engine does not declare fails, naming the file and the id.</summary>
    [Fact]
    public void Load_WhenCommonIdUnknown_ThrowsNamingFileAndId() {
        EditorPlaySettingsStore store = WriteRaw("{ \"version\": 1, \"common\": { \"target-fps\": \"60\" } }");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.Load());

        Assert.Contains(store.FilePath, exception.Message);
        Assert.Contains("target-fps", exception.Message);
    }

    /// <summary>A value that is neither a string nor a boolean fails, naming the id.</summary>
    [Fact]
    public void Load_WhenValueIsNumber_ThrowsNamingId() {
        EditorPlaySettingsStore store = WriteRaw("{ \"version\": 1, \"platforms\": { \"ds\": { \"screen-gap\": 8 } } }");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.Load());

        Assert.Contains("screen-gap", exception.Message);
        Assert.Contains("ds", exception.Message);
    }

    /// <summary>An unknown top-level property fails, naming it.</summary>
    [Fact]
    public void Load_WhenTopLevelPropertyUnknown_ThrowsNamingProperty() {
        EditorPlaySettingsStore store = WriteRaw("{ \"version\": 1, \"lastPlatform\": \"ps2\" }");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.Load());

        Assert.Contains("lastPlatform", exception.Message);
    }

    /// <summary>Writes raw text to play.json and returns a store over it.</summary>
    EditorPlaySettingsStore WriteRaw(string json) {
        EditorPlaySettingsStore store = new EditorPlaySettingsStore(ProjectRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath));
        File.WriteAllText(store.FilePath, json);
        return store;
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlaySettingsStoreTests"`
Expected: build FAIL (`EditorPlaySettingsStore` not found).

- [ ] **Step 3: Implement the document**

```csharp
namespace helengine.editor {
    /// <summary>
    /// The raw contents of <c>user_settings/play.json</c>: stored common Play setting values and one section of stored values
    /// per platform id. Values are text (<c>true</c>/<c>false</c> for booleans). Keys absent here fall back to the declared
    /// defaults; validation against declarations happens in <see cref="EditorPlaySettingsEvaluator"/>.
    /// </summary>
    public sealed class EditorPlaySettingsDocument {
        /// <summary>
        /// Initializes an empty document (no stored values).
        /// </summary>
        public EditorPlaySettingsDocument() {
            CommonValues = new Dictionary<string, string>(StringComparer.Ordinal);
            PlatformValues = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        }

        /// <summary>Gets the stored common Play setting values keyed by setting id.</summary>
        public Dictionary<string, string> CommonValues { get; }

        /// <summary>Gets the stored platform Play setting values, keyed by platform id and then by setting id.</summary>
        public Dictionary<string, Dictionary<string, string>> PlatformValues { get; }

        /// <summary>
        /// Creates a deep copy, so a dialog can edit values without changing the loaded document.
        /// </summary>
        /// <returns>Independent copy of this document.</returns>
        public EditorPlaySettingsDocument Clone() {
            EditorPlaySettingsDocument copy = new EditorPlaySettingsDocument();
            foreach (KeyValuePair<string, string> entry in CommonValues) {
                copy.CommonValues.Add(entry.Key, entry.Value);
            }
            foreach (KeyValuePair<string, Dictionary<string, string>> section in PlatformValues) {
                copy.PlatformValues.Add(section.Key, new Dictionary<string, string>(section.Value, StringComparer.Ordinal));
            }

            return copy;
        }
    }
}
```

- [ ] **Step 4: Implement the store**

```csharp
using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Loads and saves the per-user Play settings in <c>user_settings/play.json</c> (git-ignored). The format is version 1
    /// only: <c>{ "version": 1, "common": {...}, "platforms": { "&lt;id&gt;": {...} } }</c>. A missing file is an empty
    /// document and is never created by loading; unknown versions, properties and common ids fail naming the file.
    /// </summary>
    public sealed class EditorPlaySettingsStore {
        /// <summary>The only format version this editor reads and writes.</summary>
        public const int CurrentVersion = 1;

        /// <summary>
        /// Initializes the store for one project.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root.</param>
        /// <exception cref="ArgumentException">Thrown when the project root is blank.</exception>
        public EditorPlaySettingsStore(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            FilePath = Path.Combine(Path.GetFullPath(projectRootPath), "user_settings", "play.json");
        }

        /// <summary>Gets the absolute path of <c>user_settings/play.json</c>.</summary>
        public string FilePath { get; }

        /// <summary>
        /// Loads the stored Play settings.
        /// </summary>
        /// <returns>The stored document, or an empty one when the file does not exist.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the file is malformed or not in the current format.</exception>
        public EditorPlaySettingsDocument Load() {
            EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
            if (!File.Exists(FilePath)) {
                return document;
            }

            JsonDocument json;
            try {
                json = JsonDocument.Parse(File.ReadAllText(FilePath));
            } catch (JsonException exception) {
                throw new InvalidOperationException($"Play settings file '{FilePath}' is not valid JSON: {exception.Message}", exception);
            }

            using (json) {
                JsonElement root = json.RootElement;
                if (root.ValueKind != JsonValueKind.Object) {
                    throw new InvalidOperationException($"Play settings file '{FilePath}' must hold one JSON object.");
                }

                bool sawVersion = false;
                foreach (JsonProperty property in root.EnumerateObject()) {
                    if (string.Equals(property.Name, "version", StringComparison.Ordinal)) {
                        sawVersion = true;
                        if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt32(out int version) || version != CurrentVersion) {
                            throw new InvalidOperationException($"Play settings file '{FilePath}' has version {property.Value.GetRawText()}; this editor reads version {CurrentVersion} only.");
                        }
                    } else if (string.Equals(property.Name, "common", StringComparison.Ordinal)) {
                        ReadSection(property.Value, "common", document.CommonValues);
                        foreach (string settingId in document.CommonValues.Keys) {
                            if (!PlayerCommonPlaySettings.IsCommonId(settingId)) {
                                throw new InvalidOperationException($"Play settings file '{FilePath}' names unknown common setting '{settingId}'.");
                            }
                        }
                    } else if (string.Equals(property.Name, "platforms", StringComparison.Ordinal)) {
                        if (property.Value.ValueKind != JsonValueKind.Object) {
                            throw new InvalidOperationException($"Play settings file '{FilePath}' property 'platforms' must be an object.");
                        }
                        foreach (JsonProperty platform in property.Value.EnumerateObject()) {
                            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
                            ReadSection(platform.Value, "platform '" + platform.Name + "'", values);
                            document.PlatformValues.Add(platform.Name, values);
                        }
                    } else {
                        throw new InvalidOperationException($"Play settings file '{FilePath}' has unknown property '{property.Name}'.");
                    }
                }

                if (!sawVersion) {
                    throw new InvalidOperationException($"Play settings file '{FilePath}' has no version.");
                }
            }

            return document;
        }

        /// <summary>
        /// Saves the document, replacing the file. Boolean text is written as JSON booleans.
        /// </summary>
        /// <param name="document">Document to save.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public void Save(EditorPlaySettingsDocument document) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            using FileStream stream = File.Create(FilePath);
            using Utf8JsonWriter writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            writer.WriteStartObject();
            writer.WriteNumber("version", CurrentVersion);
            writer.WriteStartObject("common");
            WriteValues(writer, document.CommonValues);
            writer.WriteEndObject();
            writer.WriteStartObject("platforms");
            foreach (KeyValuePair<string, Dictionary<string, string>> section in document.PlatformValues) {
                writer.WriteStartObject(section.Key);
                WriteValues(writer, section.Value);
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        /// <summary>
        /// Reads one object of setting values; strings are kept and JSON booleans become <c>true</c>/<c>false</c>.
        /// </summary>
        /// <param name="section">JSON object to read.</param>
        /// <param name="sectionName">Section name reported on failure.</param>
        /// <param name="values">Receives the values.</param>
        /// <exception cref="InvalidOperationException">Thrown when the section is not an object or a value is another kind.</exception>
        void ReadSection(JsonElement section, string sectionName, Dictionary<string, string> values) {
            if (section.ValueKind != JsonValueKind.Object) {
                throw new InvalidOperationException($"Play settings file '{FilePath}' section {sectionName} must be an object.");
            }

            foreach (JsonProperty entry in section.EnumerateObject()) {
                if (entry.Value.ValueKind == JsonValueKind.String) {
                    values.Add(entry.Name, entry.Value.GetString());
                } else if (entry.Value.ValueKind == JsonValueKind.True) {
                    values.Add(entry.Name, "true");
                } else if (entry.Value.ValueKind == JsonValueKind.False) {
                    values.Add(entry.Name, "false");
                } else {
                    throw new InvalidOperationException($"Play settings file '{FilePath}' value '{entry.Name}' in {sectionName} must be a string or a boolean.");
                }
            }
        }

        /// <summary>
        /// Writes one section's values, turning the text <c>true</c>/<c>false</c> into JSON booleans.
        /// </summary>
        /// <param name="writer">JSON writer positioned inside the section object.</param>
        /// <param name="values">Values to write.</param>
        static void WriteValues(Utf8JsonWriter writer, Dictionary<string, string> values) {
            foreach (KeyValuePair<string, string> entry in values) {
                if (string.Equals(entry.Value, "true", StringComparison.Ordinal)) {
                    writer.WriteBoolean(entry.Key, true);
                } else if (string.Equals(entry.Value, "false", StringComparison.Ordinal)) {
                    writer.WriteBoolean(entry.Key, false);
                } else {
                    writer.WriteString(entry.Key, entry.Value);
                }
            }
        }
    }
}
```

Note: the `try/catch (JsonException)` adds the file path to a real failure and rethrows. It does not mask anything. A valid file raises no exception.

- [ ] **Step 5: Run tests to verify they pass**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlaySettingsStoreTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.editor/managers/play/EditorPlaySettingsDocument.cs engine/helengine.editor/managers/play/EditorPlaySettingsStore.cs engine/helengine.editor.tests/managers/play/EditorPlaySettingsStoreTests.cs
git commit -m "feat(editor): store per-user Play settings in user_settings/play.json

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task A6: Validate stored values and capture the press-time snapshot

**Files:**
- Create: `engine/helengine.editor/managers/play/EditorPlaySettingScope.cs`, `EditorPlaySettingIssue.cs`, `EditorPlaySettingValueValidator.cs`, `EditorPlaySettingsEvaluation.cs`, `EditorPlaySettingsEvaluator.cs`, `EditorPlaySettingsSnapshot.cs`
- Test: `engine/helengine.editor.tests/managers/play/EditorPlaySettingsEvaluatorTests.cs` (new)

**Interfaces:**
- Consumes: `EditorPlaySettingsDocument` (A5), `PlatformPlayerCapability.PlaySettings` / `FindPlaySetting` (A1), `PlayerCommonPlaySettings` (A1)
- Produces:
  - `enum EditorPlaySettingScope { Common, Platform }`
  - `EditorPlaySettingIssue(EditorPlaySettingScope scope, string settingId, string storedValue, string reason)` with `Describe(): string`
  - `EditorPlaySettingValueValidator.TryValidate(PlatformSettingDefinition definition, string value, out string reason): bool`
  - `EditorPlaySettingsEvaluation`: `FilePath`, `PlatformId`, `CommonValues`, `PlatformValues` (`IReadOnlyDictionary<string,string>`, effective = stored or default), `Issues: IReadOnlyList<EditorPlaySettingIssue>`, `IsValid: bool`, `FormatFailure(): string`
  - `EditorPlaySettingsEvaluator.Evaluate(EditorPlaySettingsDocument document, string filePath, string platformId, PlatformPlayerCapability capability): EditorPlaySettingsEvaluation`. An unknown platform setting id throws `InvalidOperationException` naming the file, platform and id.
  - `EditorPlaySettingsSnapshot(EditorPlaySettingsEvaluation evaluation, bool waitForDebuggerRequested, PlatformPlayerCapability capability, int editorProcessId)` with `PlatformId`, `CommonValues`, `PlatformValues`, `WaitForDebuggerRequested`, `Capability`, `EditorProcessId`, `EffectiveWaitForDebugger`

- [ ] **Step 1: Write the failing tests**

```csharp
using helengine.baseplatform.Definitions;
using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Verifies stored Play settings are checked against the current declarations and frozen into a press-time snapshot.
/// </summary>
public sealed class EditorPlaySettingsEvaluatorTests {
    /// <summary>File path reported in messages.</summary>
    const string FilePath = @"C:\project\user_settings\play.json";

    /// <summary>An empty document yields every declared default and no issues.</summary>
    [Fact]
    public void Evaluate_WhenNothingStored_UsesDeclaredDefaults() {
        EditorPlaySettingsEvaluation evaluation = EditorPlaySettingsEvaluator.Evaluate(new EditorPlaySettingsDocument(), FilePath, "ds", CreateCapability());

        Assert.True(evaluation.IsValid);
        Assert.Equal("fit", evaluation.CommonValues["window-scale"]);
        Assert.Equal("editor", evaluation.CommonValues["window-monitor"]);
        Assert.Equal("false", evaluation.CommonValues["wait-for-debugger"]);
        Assert.Equal("true", evaluation.CommonValues["shadows"]);
        Assert.Equal("platform", evaluation.CommonValues["frame-rate"]);
        Assert.Equal("stacked", evaluation.PlatformValues["screen-layout"]);
    }

    /// <summary>A stored value outside a choice is an issue (not an exception) and the failure names file, platform and id.</summary>
    [Fact]
    public void Evaluate_WhenStoredChoiceNotAllowed_ReportsIssueNamingFilePlatformAndId() {
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.CommonValues["window-scale"] = "5x";

        EditorPlaySettingsEvaluation evaluation = EditorPlaySettingsEvaluator.Evaluate(document, FilePath, "ds", CreateCapability());

        Assert.False(evaluation.IsValid);
        EditorPlaySettingIssue issue = Assert.Single(evaluation.Issues);
        Assert.Equal(EditorPlaySettingScope.Common, issue.Scope);
        Assert.Equal("window-scale", issue.SettingId);
        Assert.Equal("5x", issue.StoredValue);
        Assert.Equal("5x", evaluation.CommonValues["window-scale"]);
        string failure = evaluation.FormatFailure();
        Assert.Contains(FilePath, failure);
        Assert.Contains("'ds'", failure);
        Assert.Contains("window-scale", failure);
    }

    /// <summary>A stored boolean that is not true/false is an issue.</summary>
    [Fact]
    public void Evaluate_WhenStoredBooleanInvalid_ReportsIssue() {
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.CommonValues["shadows"] = "True";

        Assert.False(EditorPlaySettingsEvaluator.Evaluate(document, FilePath, "ds", CreateCapability()).IsValid);
    }

    /// <summary>A stored platform id the platform does not declare throws, naming file, platform and id.</summary>
    [Fact]
    public void Evaluate_WhenPlatformIdUnknown_Throws() {
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.PlatformValues["ds"] = new Dictionary<string, string> { ["screen-mode"] = "x" };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => EditorPlaySettingsEvaluator.Evaluate(document, FilePath, "ds", CreateCapability()));

        Assert.Contains(FilePath, exception.Message);
        Assert.Contains("'ds'", exception.Message);
        Assert.Contains("screen-mode", exception.Message);
    }

    /// <summary>Another platform's section is neither validated nor reported.</summary>
    [Fact]
    public void Evaluate_IgnoresOtherPlatformSections() {
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.PlatformValues["ps2"] = new Dictionary<string, string> { ["video-mode"] = "not-a-mode" };

        Assert.True(EditorPlaySettingsEvaluator.Evaluate(document, FilePath, "ds", CreateCapability()).IsValid);
    }

    /// <summary>The snapshot copies the values, so later edits to the document or evaluation never reach it.</summary>
    [Fact]
    public void Snapshot_IsIndependentOfLaterEdits() {
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.CommonValues["window-scale"] = "2x";
        EditorPlaySettingsEvaluation evaluation = EditorPlaySettingsEvaluator.Evaluate(document, FilePath, "ds", CreateCapability());
        EditorPlaySettingsSnapshot snapshot = new EditorPlaySettingsSnapshot(evaluation, false, CreateCapability(), 77);

        document.CommonValues["window-scale"] = "4x";

        Assert.Equal("2x", snapshot.CommonValues["window-scale"]);
        Assert.Equal("ds", snapshot.PlatformId);
        Assert.Equal(77, snapshot.EditorProcessId);
    }

    /// <summary>A snapshot refuses an invalid evaluation, because Play must not start from invalid settings.</summary>
    [Fact]
    public void Snapshot_RejectsInvalidEvaluation() {
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.CommonValues["window-scale"] = "5x";
        EditorPlaySettingsEvaluation evaluation = EditorPlaySettingsEvaluator.Evaluate(document, FilePath, "ds", CreateCapability());

        Assert.Throws<ArgumentException>(() => new EditorPlaySettingsSnapshot(evaluation, false, CreateCapability(), 77));
    }

    /// <summary>The debugger wait is the stored setting or the Shift+click request.</summary>
    [Theory]
    [InlineData("false", false, false)]
    [InlineData("true", false, true)]
    [InlineData("false", true, true)]
    public void Snapshot_EffectiveWaitForDebugger(string stored, bool shiftRequested, bool expected) {
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.CommonValues["wait-for-debugger"] = stored;
        EditorPlaySettingsEvaluation evaluation = EditorPlaySettingsEvaluator.Evaluate(document, FilePath, "ds", CreateCapability());

        Assert.Equal(expected, new EditorPlaySettingsSnapshot(evaluation, shiftRequested, CreateCapability(), 77).EffectiveWaitForDebugger);
    }

    /// <summary>Creates a DS-like capability with one choice Play setting.</summary>
    static PlatformPlayerCapability CreateCapability() {
        return new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "ds", "1.0.0",
            [new PlatformSettingDefinition("screen-layout", "Screen Layout", PlatformSettingKind.Choice, "stacked", true, ["stacked"])]);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlaySettingsEvaluatorTests"`
Expected: build FAIL (`EditorPlaySettingsEvaluator` not found).

- [ ] **Step 3: Implement scope, issue and validator**

`EditorPlaySettingScope.cs`:

```csharp
namespace helengine.editor {
    /// <summary>
    /// Which section of the Play settings a value belongs to.
    /// </summary>
    public enum EditorPlaySettingScope {
        /// <summary>A common Play setting the engine declares for every platform.</summary>
        Common,

        /// <summary>A Play setting the selected platform declares on its Play capability.</summary>
        Platform
    }
}
```

`EditorPlaySettingIssue.cs`:

```csharp
namespace helengine.editor {
    /// <summary>
    /// One stored Play setting value that the current declarations do not accept. The dialog highlights it, and Play refuses
    /// to start while any issue exists.
    /// </summary>
    public sealed class EditorPlaySettingIssue {
        /// <summary>
        /// Initializes one issue.
        /// </summary>
        /// <param name="scope">Section the value belongs to.</param>
        /// <param name="settingId">Setting id.</param>
        /// <param name="storedValue">Stored value that was rejected.</param>
        /// <param name="reason">Why the value was rejected.</param>
        public EditorPlaySettingIssue(EditorPlaySettingScope scope, string settingId, string storedValue, string reason) {
            Scope = scope;
            SettingId = settingId ?? throw new ArgumentNullException(nameof(settingId));
            StoredValue = storedValue ?? throw new ArgumentNullException(nameof(storedValue));
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        }

        /// <summary>Gets the section the value belongs to.</summary>
        public EditorPlaySettingScope Scope { get; }

        /// <summary>Gets the setting id.</summary>
        public string SettingId { get; }

        /// <summary>Gets the stored value that was rejected.</summary>
        public string StoredValue { get; }

        /// <summary>Gets why the value was rejected.</summary>
        public string Reason { get; }

        /// <summary>
        /// Describes the issue in one clause, for example <c>'window-scale' = '5x' (not one of 1x, 2x, 3x, 4x, fit)</c>.
        /// </summary>
        /// <returns>Readable description.</returns>
        public string Describe() {
            return $"'{SettingId}' = '{StoredValue}' ({Reason})";
        }
    }
}
```

`EditorPlaySettingValueValidator.cs`:

```csharp
using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Checks one Play setting value against its declaration without throwing: a boolean must be the text <c>true</c> or
    /// <c>false</c>, a choice one of its allowed values (ordinal), and a required text non-blank.
    /// </summary>
    public static class EditorPlaySettingValueValidator {
        /// <summary>
        /// Reports whether a value is acceptable for a declaration.
        /// </summary>
        /// <param name="definition">Setting declaration.</param>
        /// <param name="value">Value to check.</param>
        /// <param name="reason">Why the value is rejected; empty when accepted.</param>
        /// <returns>True when the value is acceptable.</returns>
        public static bool TryValidate(PlatformSettingDefinition definition, string value, out string reason) {
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            } else if (value == null) {
                reason = "no value";
                return false;
            } else if (definition.SettingKind == PlatformSettingKind.Boolean
                && !string.Equals(value, "true", StringComparison.Ordinal)
                && !string.Equals(value, "false", StringComparison.Ordinal)) {
                reason = "must be true or false";
                return false;
            } else if (definition.SettingKind == PlatformSettingKind.Choice && Array.IndexOf(definition.AllowedValues, value) < 0) {
                reason = "not one of " + string.Join(", ", definition.AllowedValues);
                return false;
            } else if (definition.SettingKind == PlatformSettingKind.Text && definition.Required && string.IsNullOrWhiteSpace(value)) {
                reason = "a value is required";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
```

- [ ] **Step 4: Implement the evaluation and evaluator**

`EditorPlaySettingsEvaluation.cs`:

```csharp
namespace helengine.editor {
    /// <summary>
    /// The effective Play settings for one platform: each declared setting's stored value, or its declared default when
    /// nothing is stored, plus every stored value the declarations reject.
    /// </summary>
    public sealed class EditorPlaySettingsEvaluation {
        /// <summary>
        /// Initializes one evaluation.
        /// </summary>
        /// <param name="filePath">Play settings file the values came from.</param>
        /// <param name="platformId">Platform the values were evaluated for.</param>
        /// <param name="commonValues">Effective common values keyed by id.</param>
        /// <param name="platformValues">Effective platform values keyed by id.</param>
        /// <param name="issues">Rejected stored values.</param>
        public EditorPlaySettingsEvaluation(
            string filePath,
            string platformId,
            IReadOnlyDictionary<string, string> commonValues,
            IReadOnlyDictionary<string, string> platformValues,
            IReadOnlyList<EditorPlaySettingIssue> issues) {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            PlatformId = platformId ?? throw new ArgumentNullException(nameof(platformId));
            CommonValues = commonValues ?? throw new ArgumentNullException(nameof(commonValues));
            PlatformValues = platformValues ?? throw new ArgumentNullException(nameof(platformValues));
            Issues = issues ?? throw new ArgumentNullException(nameof(issues));
        }

        /// <summary>Gets the Play settings file the values came from.</summary>
        public string FilePath { get; }

        /// <summary>Gets the platform the values were evaluated for.</summary>
        public string PlatformId { get; }

        /// <summary>Gets the effective common values keyed by id.</summary>
        public IReadOnlyDictionary<string, string> CommonValues { get; }

        /// <summary>Gets the effective platform values keyed by id.</summary>
        public IReadOnlyDictionary<string, string> PlatformValues { get; }

        /// <summary>Gets the stored values the declarations reject.</summary>
        public IReadOnlyList<EditorPlaySettingIssue> Issues { get; }

        /// <summary>Gets whether every effective value is accepted.</summary>
        public bool IsValid => Issues.Count == 0;

        /// <summary>
        /// Formats the message Play fails with while issues exist, naming the file, the platform and every rejected id.
        /// </summary>
        /// <returns>One-line failure message.</returns>
        public string FormatFailure() {
            List<string> descriptions = new List<string>();
            for (int index = 0; index < Issues.Count; index++) {
                descriptions.Add(Issues[index].Describe());
            }

            return $"Play settings in '{FilePath}' are invalid for platform '{PlatformId}': {string.Join("; ", descriptions)}. Fix them under Play Settings... in the Play platform selector.";
        }
    }
}
```

`EditorPlaySettingsEvaluator.cs`:

```csharp
using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Evaluates the stored Play settings for one platform against the current declarations (engine common settings plus
    /// the platform's <see cref="PlatformPlayerCapability.PlaySettings"/>). Invalid values become issues so the dialog can
    /// highlight them; an id the platform does not declare is a broken file and throws.
    /// </summary>
    public static class EditorPlaySettingsEvaluator {
        /// <summary>
        /// Evaluates the stored settings for one platform.
        /// </summary>
        /// <param name="document">Stored Play settings.</param>
        /// <param name="filePath">File the document came from, named in messages.</param>
        /// <param name="platformId">Platform being played.</param>
        /// <param name="capability">Play capability of that platform.</param>
        /// <returns>Effective values and issues.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the platform section names an undeclared setting.</exception>
        public static EditorPlaySettingsEvaluation Evaluate(EditorPlaySettingsDocument document, string filePath, string platformId, PlatformPlayerCapability capability) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            } else if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("The Play settings file path is required.", nameof(filePath));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("The platform id is required.", nameof(platformId));
            } else if (capability == null) {
                throw new ArgumentNullException(nameof(capability));
            }

            List<EditorPlaySettingIssue> issues = new List<EditorPlaySettingIssue>();
            Dictionary<string, string> commonValues = ResolveSection(PlayerCommonPlaySettings.CreateDefinitions(), document.CommonValues, EditorPlaySettingScope.Common, issues);
            if (!document.PlatformValues.TryGetValue(platformId, out Dictionary<string, string> storedPlatformValues)) {
                storedPlatformValues = new Dictionary<string, string>(StringComparer.Ordinal);
            }
            foreach (string settingId in storedPlatformValues.Keys) {
                if (capability.FindPlaySetting(settingId) == null) {
                    throw new InvalidOperationException($"Play settings file '{filePath}' names setting '{settingId}' for platform '{platformId}', which the platform does not declare.");
                }
            }

            Dictionary<string, string> platformValues = ResolveSection(capability.PlaySettings, storedPlatformValues, EditorPlaySettingScope.Platform, issues);
            return new EditorPlaySettingsEvaluation(filePath, platformId, commonValues, platformValues, issues);
        }

        /// <summary>
        /// Resolves one section: the stored value when present (validated), otherwise the declared default.
        /// </summary>
        /// <param name="definitions">Declarations of the section.</param>
        /// <param name="stored">Stored values of the section.</param>
        /// <param name="scope">Section scope, recorded on issues.</param>
        /// <param name="issues">Receives rejected stored values.</param>
        /// <returns>Effective values keyed by id.</returns>
        static Dictionary<string, string> ResolveSection(
            PlatformSettingDefinition[] definitions,
            Dictionary<string, string> stored,
            EditorPlaySettingScope scope,
            List<EditorPlaySettingIssue> issues) {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < definitions.Length; index++) {
                PlatformSettingDefinition definition = definitions[index];
                if (stored.TryGetValue(definition.SettingId, out string storedValue)) {
                    values.Add(definition.SettingId, storedValue);
                    if (!EditorPlaySettingValueValidator.TryValidate(definition, storedValue, out string reason)) {
                        issues.Add(new EditorPlaySettingIssue(scope, definition.SettingId, storedValue ?? string.Empty, reason));
                    }
                } else {
                    values.Add(definition.SettingId, definition.DefaultValue);
                }
            }

            return values;
        }
    }
}
```

- [ ] **Step 5: Implement the snapshot**

```csharp
using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// The Play settings captured on the editor frame thread when Play was pressed: private copies of the effective common
    /// and platform values, the Shift+click debugger request, the platform's Play capability and the editor process id.
    /// Editing Play settings while this Play prepares changes only the next Play.
    /// </summary>
    public sealed class EditorPlaySettingsSnapshot {
        /// <summary>
        /// Captures one snapshot.
        /// </summary>
        /// <param name="evaluation">Valid evaluation of the stored settings for the platform.</param>
        /// <param name="waitForDebuggerRequested">Whether Shift was held on the Play button.</param>
        /// <param name="capability">Play capability of the platform (read-only after the catalog loads it).</param>
        /// <param name="editorProcessId">Process id of this editor, used by the player to find the editor's monitor.</param>
        /// <exception cref="ArgumentException">Thrown when the evaluation has issues or the process id is not positive.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the evaluation or capability is null.</exception>
        public EditorPlaySettingsSnapshot(EditorPlaySettingsEvaluation evaluation, bool waitForDebuggerRequested, PlatformPlayerCapability capability, int editorProcessId) {
            if (evaluation == null) {
                throw new ArgumentNullException(nameof(evaluation));
            } else if (!evaluation.IsValid) {
                throw new ArgumentException(evaluation.FormatFailure(), nameof(evaluation));
            } else if (capability == null) {
                throw new ArgumentNullException(nameof(capability));
            } else if (editorProcessId <= 0) {
                throw new ArgumentException("The editor process id must be positive.", nameof(editorProcessId));
            }

            PlatformId = evaluation.PlatformId;
            CommonValues = new Dictionary<string, string>(evaluation.CommonValues, StringComparer.Ordinal);
            PlatformValues = new Dictionary<string, string>(evaluation.PlatformValues, StringComparer.Ordinal);
            WaitForDebuggerRequested = waitForDebuggerRequested;
            Capability = capability;
            EditorProcessId = editorProcessId;
        }

        /// <summary>Gets the platform being played.</summary>
        public string PlatformId { get; }

        /// <summary>Gets the effective common values captured at press time.</summary>
        public IReadOnlyDictionary<string, string> CommonValues { get; }

        /// <summary>Gets the effective platform values captured at press time; <c>project</c> is still unresolved.</summary>
        public IReadOnlyDictionary<string, string> PlatformValues { get; }

        /// <summary>Gets whether Shift was held on the Play button.</summary>
        public bool WaitForDebuggerRequested { get; }

        /// <summary>Gets the Play capability of the platform.</summary>
        public PlatformPlayerCapability Capability { get; }

        /// <summary>Gets the editor process id.</summary>
        public int EditorProcessId { get; }

        /// <summary>Gets whether the player waits for a debugger: the stored setting or the Shift+click request.</summary>
        public bool EffectiveWaitForDebugger =>
            WaitForDebuggerRequested || string.Equals(CommonValues[PlayerCommonPlaySettings.WaitForDebuggerId], "true", StringComparison.Ordinal);
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlaySettingsEvaluatorTests"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add engine/helengine.editor/managers/play/EditorPlaySettingScope.cs engine/helengine.editor/managers/play/EditorPlaySettingIssue.cs engine/helengine.editor/managers/play/EditorPlaySettingValueValidator.cs engine/helengine.editor/managers/play/EditorPlaySettingsEvaluation.cs engine/helengine.editor/managers/play/EditorPlaySettingsEvaluator.cs engine/helengine.editor/managers/play/EditorPlaySettingsSnapshot.cs engine/helengine.editor.tests/managers/play/EditorPlaySettingsEvaluatorTests.cs
git commit -m "feat(editor): validate Play settings and snapshot them at press time

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task A7: Resolve the snapshot into player settings after the cook

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorPlayCookResult.cs` (add `graphicsOptionValues` after `startupSceneCookedRelativePath`; update `Failure`)
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs:376-383` (pass the queue item's graphics option values)
- Modify (cook-result call sites; insert `new Dictionary<string, string>()` as the 6th argument): `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs:422`, `engine/helengine.editor.tests/managers/play/EditorPlaySessionPreparationTests.cs`, `engine/helengine.editor.tests/managers/play/EditorPlaySessionServiceTests.cs` (2), `engine/helengine.editor.tests/managers/play/EditorPlaySessionStartupTimingTests.cs`, `engine/helengine.editor.tests/testing/RecordingPlayAssetCookExecutor.cs`, `engine/helengine.player.tests/ContractPlayCookRunner.cs`
- Create: `engine/helengine.editor/managers/play/EditorPlayLaunchSettingsResolver.cs`
- Test: `engine/helengine.editor.tests/managers/play/EditorPlayLaunchSettingsResolverTests.cs` (new), `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerAssetsOnlyTests.cs`

**Interfaces:**
- Consumes: `EditorPlaySettingsSnapshot` (A6), `PlayerPlaySettings`, `PlayerWindowScaleText`, `PlayerWindowMonitorText` (A3)
- Produces:
  - `EditorPlayCookResult(bool succeeded, string message, string contentRootPath, string startupSceneId, string startupSceneCookedRelativePath, IReadOnlyDictionary<string,string> graphicsOptionValues, bool shadowsEnabled = true, string runtimeSettingsPath = "")`
  - `EditorPlayCookResult.GraphicsOptionValues: IReadOnlyDictionary<string,string>`
  - `EditorPlayLaunchSettingsResolver.Resolve(EditorPlaySettingsSnapshot snapshot, EditorPlayCookResult cookResult): PlayerPlaySettings`

- [ ] **Step 1: Write the failing tests**

```csharp
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Player;
using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Verifies the press-time Play settings become the player's resolved settings once the cook reported the project's
/// graphics options and shadow policy.
/// </summary>
public sealed class EditorPlayLaunchSettingsResolverTests {
    /// <summary>Common settings map onto the typed player settings, with the capability's frame rate for <c>platform</c>.</summary>
    [Fact]
    public void Resolve_MapsCommonSettings() {
        EditorPlaySettingsSnapshot snapshot = CreateSnapshot(new Dictionary<string, string> { ["window-scale"] = "3x", ["window-monitor"] = "primary" }, new Dictionary<string, string>(), false);

        PlayerPlaySettings settings = EditorPlayLaunchSettingsResolver.Resolve(snapshot, CreateCookResult(true, new Dictionary<string, string>()));

        Assert.Equal(PlayerWindowScale.Three, settings.WindowScale);
        Assert.Equal(PlayerWindowMonitor.Primary, settings.WindowMonitor);
        Assert.Equal(55, settings.EditorProcessId);
        Assert.False(settings.FrameRateUnlimited);
        Assert.Equal(60d, settings.TargetFrameRate);
        Assert.True(settings.ShadowsEnabled);
        Assert.False(settings.WaitForDebugger);
    }

    /// <summary><c>frame-rate: unlimited</c> turns pacing off.</summary>
    [Fact]
    public void Resolve_UnlimitedFrameRate() {
        EditorPlaySettingsSnapshot snapshot = CreateSnapshot(new Dictionary<string, string> { ["frame-rate"] = "unlimited" }, new Dictionary<string, string>(), false);

        PlayerPlaySettings settings = EditorPlayLaunchSettingsResolver.Resolve(snapshot, CreateCookResult(true, new Dictionary<string, string>()));

        Assert.True(settings.FrameRateUnlimited);
        Assert.Equal(0d, settings.TargetFrameRate);
    }

    /// <summary>The Play shadows setting can only turn shadows off; the project's policy still applies.</summary>
    [Theory]
    [InlineData("true", true, true)]
    [InlineData("false", true, false)]
    [InlineData("true", false, false)]
    public void Resolve_ShadowsAreSettingAndProjectPolicy(string playShadows, bool projectShadows, bool expected) {
        EditorPlaySettingsSnapshot snapshot = CreateSnapshot(new Dictionary<string, string> { ["shadows"] = playShadows }, new Dictionary<string, string>(), false);

        Assert.Equal(expected, EditorPlayLaunchSettingsResolver.Resolve(snapshot, CreateCookResult(projectShadows, new Dictionary<string, string>())).ShadowsEnabled);
    }

    /// <summary>Shift+click forces the debugger wait even when the setting is off.</summary>
    [Fact]
    public void Resolve_ShiftClickForcesDebuggerWait() {
        EditorPlaySettingsSnapshot snapshot = CreateSnapshot(new Dictionary<string, string>(), new Dictionary<string, string>(), true);

        Assert.True(EditorPlayLaunchSettingsResolver.Resolve(snapshot, CreateCookResult(true, new Dictionary<string, string>())).WaitForDebugger);
    }

    /// <summary>A platform value of <c>project</c> takes the project graphics option with the same id.</summary>
    [Fact]
    public void Resolve_ProjectValueTakesGraphicsOption() {
        EditorPlaySettingsSnapshot snapshot = CreateSnapshot(new Dictionary<string, string>(), new Dictionary<string, string> { ["video-mode"] = "project" }, false);

        PlayerPlaySettings settings = EditorPlayLaunchSettingsResolver.Resolve(snapshot, CreateCookResult(true, new Dictionary<string, string> { ["video-mode"] = "m2" }));

        Assert.Equal("m2", settings.PlatformValues["video-mode"]);
    }

    /// <summary>A <c>project</c> value whose graphics option is missing fails, naming the id and the platform settings file.</summary>
    [Fact]
    public void Resolve_WhenProjectOptionMissing_ThrowsNamingIdAndFile() {
        EditorPlaySettingsSnapshot snapshot = CreateSnapshot(new Dictionary<string, string>(), new Dictionary<string, string> { ["video-mode"] = "project" }, false);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => EditorPlayLaunchSettingsResolver.Resolve(snapshot, CreateCookResult(true, new Dictionary<string, string>())));

        Assert.Contains("video-mode", exception.Message);
        Assert.Contains("settings/platform.tst.json", exception.Message);
    }

    /// <summary>A project graphics value the Play setting does not allow fails, naming the value.</summary>
    [Fact]
    public void Resolve_WhenProjectOptionNotAllowed_ThrowsNamingValue() {
        EditorPlaySettingsSnapshot snapshot = CreateSnapshot(new Dictionary<string, string>(), new Dictionary<string, string> { ["video-mode"] = "project" }, false);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => EditorPlayLaunchSettingsResolver.Resolve(snapshot, CreateCookResult(true, new Dictionary<string, string> { ["video-mode"] = "m9" })));

        Assert.Contains("m9", exception.Message);
    }

    /// <summary>Creates a snapshot for a test platform whose Play setting may follow the project.</summary>
    static EditorPlaySettingsSnapshot CreateSnapshot(Dictionary<string, string> common, Dictionary<string, string> platform, bool shift) {
        PlatformPlayerCapability capability = new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "tst", "1.0.0",
            [new PlatformSettingDefinition("video-mode", "Video Mode", PlatformSettingKind.Choice, "project", true, ["project", "m1", "m2"])]);
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        foreach (KeyValuePair<string, string> entry in common) {
            document.CommonValues[entry.Key] = entry.Value;
        }
        document.PlatformValues["tst"] = platform;
        EditorPlaySettingsEvaluation evaluation = EditorPlaySettingsEvaluator.Evaluate(document, @"C:\p\user_settings\play.json", "tst", capability);
        return new EditorPlaySettingsSnapshot(evaluation, shift, capability, 55);
    }

    /// <summary>Creates a successful cook result with the given shadow policy and graphics options.</summary>
    static EditorPlayCookResult CreateCookResult(bool shadows, Dictionary<string, string> graphics) {
        return new EditorPlayCookResult(true, "Cooked.", @"C:\content", "scene", "scenes/scene.hasset", graphics, shadows);
    }
}
```

Append to `EditorPlatformBuildGraphRunnerAssetsOnlyTests.cs` a test that runs the existing successful assets-only fixture (reuse that file's own success-path setup). Set `queueItem.SelectedGraphicsOptionValues["video-mode"] = "ntsc-320x224p"` before `ExecuteAssetsOnly`, then assert:

```csharp
        Assert.Equal("ntsc-320x224p", result.GraphicsOptionValues["video-mode"]);
```

(Copy the arrange block of the existing test in that file that asserts `result.Succeeded` for a supported platform, then add the line above. The new test must not share state with it.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlayLaunchSettingsResolverTests|FullyQualifiedName~EditorPlatformBuildGraphRunnerAssetsOnlyTests"`
Expected: build FAIL (`EditorPlayLaunchSettingsResolver` not found; no 6-argument `EditorPlayCookResult` taking a dictionary).

- [ ] **Step 3: Extend `EditorPlayCookResult`**

Insert the parameter after `startupSceneCookedRelativePath`:

```csharp
        /// <param name="graphicsOptionValues">
        /// Graphics option values of the cooked queue item (the project's selected graphics settings captured at press time);
        /// Play settings that follow the project resolve against them. Empty on failure.
        /// </param>
```

```csharp
            IReadOnlyDictionary<string, string> graphicsOptionValues,
```

In the body add `GraphicsOptionValues = graphicsOptionValues == null ? throw new ArgumentNullException(nameof(graphicsOptionValues)) : new Dictionary<string, string>(graphicsOptionValues, StringComparer.Ordinal);`. Add the property:

```csharp
        /// <summary>
        /// Gets the graphics option values of the cooked queue item; Play settings whose value is <c>project</c> resolve
        /// against them.
        /// </summary>
        public IReadOnlyDictionary<string, string> GraphicsOptionValues { get; }
```

`Failure` becomes `return new EditorPlayCookResult(false, message, string.Empty, string.Empty, string.Empty, new Dictionary<string, string>());`.

In `EditorPlatformBuildGraphRunner.ExecuteAssetsOnly`, insert `queueItem.SelectedGraphicsOptionValues,` after `ResolveStartupSceneCookedRelativePath(cookedManifest),` in the `new EditorPlayCookResult(` call.

Update every test call site listed under **Files** by inserting `new Dictionary<string, string>()` after the startup scene path argument (the 5th positional argument).

- [ ] **Step 4: Implement the resolver**

```csharp
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Player;

namespace helengine.editor {
    /// <summary>
    /// Turns the Play settings captured at press time into the fully resolved settings the player receives, once the cook
    /// has reported the project's graphics option values and shadow policy (both also captured at press time).
    /// </summary>
    public static class EditorPlayLaunchSettingsResolver {
        /// <summary>
        /// Resolves the player settings for one Play.
        /// </summary>
        /// <param name="snapshot">Settings captured when Play was pressed.</param>
        /// <param name="cookResult">Successful cook outcome.</param>
        /// <returns>Resolved player settings.</returns>
        /// <exception cref="InvalidOperationException">Thrown when a setting follows the project but the project has no acceptable value.</exception>
        public static PlayerPlaySettings Resolve(EditorPlaySettingsSnapshot snapshot, EditorPlayCookResult cookResult) {
            if (snapshot == null) {
                throw new ArgumentNullException(nameof(snapshot));
            } else if (cookResult == null) {
                throw new ArgumentNullException(nameof(cookResult));
            }

            Dictionary<string, string> platformValues = new Dictionary<string, string>(StringComparer.Ordinal);
            PlatformSettingDefinition[] declarations = snapshot.Capability.PlaySettings;
            for (int index = 0; index < declarations.Length; index++) {
                PlatformSettingDefinition declaration = declarations[index];
                string value = snapshot.PlatformValues[declaration.SettingId];
                if (string.Equals(value, PlayerCommonPlaySettings.ProjectValue, StringComparison.Ordinal)) {
                    value = ResolveProjectValue(snapshot.PlatformId, declaration, cookResult.GraphicsOptionValues);
                }
                platformValues.Add(declaration.SettingId, value);
            }

            bool unlimited = string.Equals(snapshot.CommonValues[PlayerCommonPlaySettings.FrameRateId], "unlimited", StringComparison.Ordinal);
            bool playShadows = string.Equals(snapshot.CommonValues[PlayerCommonPlaySettings.ShadowsId], "true", StringComparison.Ordinal);
            return new PlayerPlaySettings(
                PlayerWindowScaleText.Parse(snapshot.CommonValues[PlayerCommonPlaySettings.WindowScaleId]),
                PlayerWindowMonitorText.Parse(snapshot.CommonValues[PlayerCommonPlaySettings.WindowMonitorId]),
                snapshot.EditorProcessId,
                snapshot.EffectiveWaitForDebugger,
                playShadows && cookResult.ShadowsEnabled,
                unlimited,
                unlimited ? 0d : snapshot.Capability.TargetFrameRate,
                platformValues);
        }

        /// <summary>
        /// Resolves a <c>project</c> value to the project graphics option with the same id and checks the Play setting accepts it.
        /// </summary>
        /// <param name="platformId">Platform being played, named in messages.</param>
        /// <param name="declaration">Play setting declaration.</param>
        /// <param name="graphicsOptionValues">Project graphics option values captured at press time.</param>
        /// <returns>The concrete project value.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the project has no value or one the Play setting rejects.</exception>
        static string ResolveProjectValue(string platformId, PlatformSettingDefinition declaration, IReadOnlyDictionary<string, string> graphicsOptionValues) {
            string settingsFile = "settings/platform." + platformId + ".json";
            if (!graphicsOptionValues.TryGetValue(declaration.SettingId, out string projectValue) || string.IsNullOrWhiteSpace(projectValue)) {
                throw new InvalidOperationException($"Play setting '{declaration.SettingId}' follows the project, but the selected graphics settings in {settingsFile} have no '{declaration.SettingId}' value.");
            } else if (string.Equals(projectValue, PlayerCommonPlaySettings.ProjectValue, StringComparison.Ordinal)
                || !EditorPlaySettingValueValidator.TryValidate(declaration, projectValue, out string reason)) {
                throw new InvalidOperationException($"Play setting '{declaration.SettingId}' follows the project, but {settingsFile} selects '{projectValue}', which Play does not accept.");
            }

            return projectValue;
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlayLaunchSettingsResolverTests|FullyQualifiedName~EditorPlatformBuildGraphRunnerAssetsOnlyTests"` and `.\dotnetw.cmd build engine\helengine.player.tests\helengine.player.tests.csproj`
Expected: PASS and build success.

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.editor/managers/project/EditorPlayCookResult.cs engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs engine/helengine.editor/managers/play/EditorPlayLaunchSettingsResolver.cs engine/helengine.editor.tests/managers/play/EditorPlayLaunchSettingsResolverTests.cs engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerAssetsOnlyTests.cs engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs engine/helengine.editor.tests/managers/play/EditorPlaySessionPreparationTests.cs engine/helengine.editor.tests/managers/play/EditorPlaySessionServiceTests.cs engine/helengine.editor.tests/managers/play/EditorPlaySessionStartupTimingTests.cs engine/helengine.editor.tests/testing/RecordingPlayAssetCookExecutor.cs engine/helengine.player.tests/ContractPlayCookRunner.cs
git diff --cached --numstat
git commit -m "feat(editor): resolve Play settings against the cooked project graphics options

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task A8: Launch the player with `--play-settings` (folding the old flags)

**Files:**
- Create: `engine/helengine.editor/managers/play/EditorPlaySettingsFileWriter.cs`
- Modify: `engine/helengine.editor/managers/play/EditorPlaySessionService.cs` (constants 54-81, both constructors, `Play`, `LaunchPreparedPlayer`, `BuildPlayerArguments`)
- Modify: `engine/helengine.editor/managers/play/EditorPlayPreparation.cs` (replace `targetFrameRate`/`waitForDebugger` with `playSettings`)
- Modify: `engine/helengine.player/PlayerLaunchOptions.cs`
- Create (test helpers): `engine/helengine.editor.tests/managers/play/TestPlaySettings.cs`, `engine/helengine.player.tests/ContractPlaySettings.cs`
- Modify (tests): `engine/helengine.editor.tests/managers/play/EditorPlaySessionServiceTests.cs`, `EditorPlaySessionPreparationTests.cs`, `EditorPlaySessionStartupTimingTests.cs`, `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`, `engine/helengine.player.tests/EditorPlayArgumentContractTests.cs`, `engine/helengine.player.tests/PlayerLaunchOptionsTests.cs`
- Modify: `engine/helengine.editor/EditorSession.cs:956-966` (construct the writer; temporary capture in `HandlePlayRequested`, replaced in Task A13)

**Interfaces:**
- Consumes: `EditorPlaySettingsSnapshot` (A6), `EditorPlayLaunchSettingsResolver` (A7), `PlayerPlaySettingsFile` (A3)
- Produces:
  - `EditorPlaySettingsFileWriter(string projectRootPath)` with `ResolvePath(string platformId): string` (`<project>/cache/play/<id>/play-settings.json`) and `Write(string platformId, PlayerPlaySettings settings): string`
  - `EditorPlaySessionService.PlaySettingsOption = "--play-settings"`. `TargetFpsOption` and `WaitForDebuggerOption` are removed.
  - `EditorPlaySessionService(IEditorPlayCookRunner, IEditorPlayScriptBuilder, IEditorPlayProcessLauncher, string playerExecutablePath, EditorMainThreadActionQueue, IEditorPlayPreparationScheduler, EditorPlaySettingsFileWriter)`. The internal overload adds `IEditorPlayClock clock` last.
  - `EditorPlaySessionService.Play(string platformId, string platformDisplayName, string currentScenePath, Func<bool> saveScene, string adapterAssemblyPath, string adapterTypeName, PlayerInputDevices inputDevices, string runtimePlatformName, string runtimePlatformVersion, EditorPlaySettingsSnapshot playSettings, Action<EditorBuildExecutionResult> completed)`
  - `EditorPlayPreparation.PlaySettings: EditorPlaySettingsSnapshot`
  - `PlayerLaunchOptions.PlaySettingsOption = "--play-settings"` and `PlayerLaunchOptions.PlaySettings: PlayerPlaySettings` (removed: `TargetFpsOption`, `ShadowsEnabledOption`, `WaitForDebuggerOption`, `TargetFrameRate`, `ShadowsEnabled`, `WaitForDebugger`)

- [ ] **Step 1: Add the shared test helpers**

`engine/helengine.editor.tests/managers/play/TestPlaySettings.cs`:

```csharp
using helengine.baseplatform.Definitions;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Builds Play settings snapshots and a workspace-owned settings file writer for Play session tests.
/// </summary>
public static class TestPlaySettings {
    /// <summary>Workspace-owned root that holds the Play settings files tests write.</summary>
    public const string ProjectRoot = @"C:\dev\helworks\builds\helengine\tests\play-session-project";

    /// <summary>
    /// Creates a snapshot of the declared defaults for a platform whose capability paces at the given frame rate.
    /// </summary>
    /// <param name="platformId">Platform id.</param>
    /// <param name="targetFrameRate">Capability frame rate.</param>
    /// <param name="waitForDebuggerRequested">Whether Shift was held.</param>
    /// <returns>Snapshot of default Play settings.</returns>
    public static EditorPlaySettingsSnapshot CreateDefaults(string platformId, double targetFrameRate, bool waitForDebuggerRequested) {
        PlatformPlayerCapability capability = new PlatformPlayerCapability(true, "a.B", "staging", targetFrameRate, PlayerInputDevices.Gamepad, platformId, "1.0.0", []);
        EditorPlaySettingsEvaluation evaluation = EditorPlaySettingsEvaluator.Evaluate(new EditorPlaySettingsDocument(), ProjectRoot + @"\user_settings\play.json", platformId, capability);
        return new EditorPlaySettingsSnapshot(evaluation, waitForDebuggerRequested, capability, 4242);
    }

    /// <summary>
    /// Creates the settings file writer rooted at <see cref="ProjectRoot"/>.
    /// </summary>
    /// <returns>Settings file writer.</returns>
    public static EditorPlaySettingsFileWriter CreateWriter() {
        return new EditorPlaySettingsFileWriter(ProjectRoot);
    }
}
```

`engine/helengine.player.tests/ContractPlaySettings.cs` has the same body in namespace `helengine.player.tests` with class name `ContractPlaySettings` and `ProjectRoot = @"C:\dev\helworks\builds\helengine\tests\play-contract-project"`.

- [ ] **Step 2: Write the failing tests**

In `EditorPlaySessionServiceTests.cs`:
- `CreateService` becomes `new EditorPlaySessionService(cookRunner, scriptBuilder, launcher, PlayerExecutablePath, Queue, new InlinePlayPreparationScheduler(), TestPlaySettings.CreateWriter())`.
- `PlayToCompletion` loses its `double targetFrameRate` and `bool waitForDebugger` parameters and gains `EditorPlaySettingsSnapshot playSettings` after `inputDevices`. Its `service.Play(...)` call becomes `service.Play(platformId, platformDisplayName, scenePath, saveScene, adapterAssemblyPath, adapterTypeName, inputDevices, RuntimePlatformName, RuntimePlatformVersion, playSettings, result => launchResult = result)`.
- Every call `..., TargetFrameRate, InputDevices, false)` becomes `..., InputDevices, TestPlaySettings.CreateDefaults(PlatformId, TargetFrameRate, false))`, and `..., TargetFrameRate, InputDevices, true)` becomes `..., InputDevices, TestPlaySettings.CreateDefaults(PlatformId, TargetFrameRate, true))`. Calls passing `capability.TargetFrameRate, capability.InputDevices, false` become `capability.InputDevices, TestPlaySettings.CreateDefaults(PlatformId, capability.TargetFrameRate, false)`.
- Delete `Play_PassesCapabilityTargetFrameRateAsInvariantTargetFpsArgument` and `Play_WhenPlatformDisablesShadows_PassesSwitchToPlayer`. Replace `Play_WhenAllSucceed_LaunchesWithExpectedArgumentsAndIsRunning` with:

```csharp
    /// <summary>
    /// Ensures a fully successful Play launches the player with the exact argument list (one --play-settings file instead
    /// of the old frame-rate, shadow and debugger flags) and leaves the session running.
    /// </summary>
    [Fact]
    public void Play_WhenAllSucceed_LaunchesWithExpectedArgumentsAndIsRunning() {
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = CreateService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), launcher);
        List<EditorPlaySessionState> states = new List<EditorPlaySessionState>();
        service.StateChanged += states.Add;

        EditorBuildExecutionResult result = PlayToCompletion(service, PlatformId, PlatformDisplayName, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, InputDevices, TestPlaySettings.CreateDefaults(PlatformId, TargetFrameRate, false));

        Assert.True(result.Succeeded);
        Assert.Equal(EditorPlaySessionState.Running, service.State);
        Assert.Equal(new[] { EditorPlaySessionState.Preparing, EditorPlaySessionState.Running }, states);
        EditorPlayLaunchRequest request = Assert.Single(launcher.Requests);
        string settingsPath = TestPlaySettings.CreateWriter().ResolvePath(PlatformId);
        string[] expectedArguments = {
            "--content-root", @"C:\project\cache\play\ps2-staging",
            "--scene-id", "scene-cube-test",
            "--platform-name", PlatformDisplayName,
            "--scene-path", "scenes/cube_test.hscene",
            "--adapter-assembly", AdapterAssemblyPath,
            "--adapter-type", AdapterTypeName,
            "--script-modules", RuntimeModulesRootPath,
            "--input-devices", "gamepad",
            "--runtime-platform-name", RuntimePlatformName,
            "--runtime-platform-version", RuntimePlatformVersion,
            "--play-settings", settingsPath
        };
        Assert.Equal(expectedArguments, request.Arguments);
        PlayerPlaySettings written = PlayerPlaySettingsFile.Read(settingsPath);
        Assert.Equal(TargetFrameRate, written.TargetFrameRate);
        Assert.False(written.WaitForDebugger);
    }

    /// <summary>
    /// The settings captured when Play was pressed are the ones the player receives, even when a newer snapshot exists by
    /// the time the preparation launches (an edit during preparation applies to the next Play).
    /// </summary>
    [Fact]
    public void Play_UsesSettingsCapturedWhenPressed() {
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = CreateService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), launcher);
        EditorPlaySettingsSnapshot pressed = TestPlaySettings.CreateDefaults(PlatformId, 60d, true);
        EditorBuildExecutionResult launchResult = null;

        service.Play(PlatformId, PlatformDisplayName, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, InputDevices, RuntimePlatformName, RuntimePlatformVersion, pressed, result => launchResult = result);
        EditorPlaySettingsSnapshot edited = TestPlaySettings.CreateDefaults(PlatformId, 30d, false);
        Queue.ProcessPending();

        Assert.True(launchResult.Succeeded, launchResult.Message);
        PlayerPlaySettings written = PlayerPlaySettingsFile.Read(TestPlaySettings.CreateWriter().ResolvePath(PlatformId));
        Assert.True(written.WaitForDebugger);
        Assert.Equal(60d, written.TargetFrameRate);
        Assert.NotSame(pressed, edited);
    }

    /// <summary>A settings snapshot taken for another platform is rejected before anything runs.</summary>
    [Fact]
    public void Play_WhenSnapshotIsForAnotherPlatform_Throws() {
        EditorPlaySessionService service = CreateService(CreateSucceedingCookRunner(), CreateSucceedingScriptBuilder(), new FakePlayProcessLauncher());

        Assert.Throws<ArgumentException>(() => service.Play(PlatformId, PlatformDisplayName, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, InputDevices, RuntimePlatformName, RuntimePlatformVersion, TestPlaySettings.CreateDefaults("ds", 60d, false), result => { }));
    }
```

Add `using helengine.baseplatform.Player;` to the file. In `EditorPlaySessionPreparationTests.cs`, `EditorPlaySessionStartupTimingTests.cs` and `EditorSessionSceneSaveTests.cs`, append the writer argument to each `new EditorPlaySessionService(...)`. Use `TestPlaySettings.CreateWriter()`; in `EditorSessionSceneSaveTests` it is `new managers.play.TestPlaySettings` qualified as `managers.play.TestPlaySettings.CreateWriter()`. In the `service.Play(` calls, replace `60d, PlayerInputDevices.Gamepad, "ps2", "1.0.0", false,` with `PlayerInputDevices.Gamepad, "ps2", "1.0.0", TestPlaySettings.CreateDefaults("ps2", 60d, false),`.

In `engine/helengine.player.tests/EditorPlayArgumentContractTests.cs`, construct the service with `ContractPlaySettings.CreateWriter()` and call:

```csharp
            EditorBuildExecutionResult started = service.Play(PlatformId, PlatformDisplayName, ScenePath, () => true, AdapterAssemblyPath, AdapterTypeName, InputDevices, RuntimePlatformName, RuntimePlatformVersion, ContractPlaySettings.CreateDefaults(PlatformId, TargetFrameRate, waitForDebugger), completed => result = completed);
```

Replace the three assertions on `options.TargetFrameRate` and `options.WaitForDebugger` with:

```csharp
            Assert.Equal(TargetFrameRate, options.PlaySettings.TargetFrameRate);
            Assert.Equal(waitForDebugger, options.PlaySettings.WaitForDebugger);
            Assert.Equal(PlayerWindowScale.Fit, options.PlaySettings.WindowScale);
```

In `PlayerLaunchOptionsTests.cs`, delete the tests that exercise `--target-fps`, `--shadows-enabled` and `--wait-for-debugger`. The arguments helper of that file must now include `"--play-settings", <path>`, where the path is written once per test with `PlayerPlaySettingsFile.Write` under `C:\dev\helworks\builds\helengine\tests\player-launch-options\<guid>\play-settings.json`. Add:

```csharp
        /// <summary>The play settings file is read into the options.</summary>
        [Fact]
        public void Parse_ReadsPlaySettingsFile() {
            PlayerLaunchOptions options = PlayerLaunchOptions.Parse(CreateArguments());

            Assert.Equal(PlayerWindowScale.Fit, options.PlaySettings.WindowScale);
            Assert.Equal(60d, options.PlaySettings.TargetFrameRate);
        }

        /// <summary>The removed flags are now unknown options.</summary>
        [Theory]
        [InlineData("--target-fps")]
        [InlineData("--shadows-enabled")]
        [InlineData("--wait-for-debugger")]
        public void Parse_RejectsFoldedFlags(string removedOption) {
            List<string> arguments = CreateArguments().ToList();
            arguments.Add(removedOption);
            arguments.Add("60");

            Assert.Throws<ArgumentException>(() => PlayerLaunchOptions.Parse(arguments.ToArray()));
        }

        /// <summary>The play settings option is required.</summary>
        [Fact]
        public void Parse_WhenPlaySettingsMissing_Throws() {
            List<string> arguments = CreateArguments().ToList();
            int index = arguments.IndexOf("--play-settings");
            arguments.RemoveRange(index, 2);

            ArgumentException exception = Assert.Throws<ArgumentException>(() => PlayerLaunchOptions.Parse(arguments.ToArray()));
            Assert.Contains("--play-settings", exception.Message);
        }
```

(`CreateArguments()` is the file's existing complete-argument helper. Rename it to match whatever the file already calls it, and add the `--play-settings` pair there.)

- [ ] **Step 3: Run tests to verify they fail**

Run: `.\dotnetw.cmd test engine\helengine.player.tests\helengine.player.tests.csproj`
Expected: build FAIL (no `EditorPlaySettingsFileWriter`, no `PlaySettings` member).

- [ ] **Step 4: Implement the writer**

```csharp
using helengine.baseplatform.Player;

namespace helengine.editor {
    /// <summary>
    /// Writes the resolved Play settings of one Play to <c>&lt;project&gt;/cache/play/&lt;platformId&gt;/play-settings.json</c>,
    /// the file the player reads through <c>--play-settings</c>. The cache folder stays deletable: the file is rewritten
    /// for every Play.
    /// </summary>
    public sealed class EditorPlaySettingsFileWriter {
        /// <summary>File name of the Play settings file inside one platform's Play cache folder.</summary>
        public const string FileName = "play-settings.json";

        /// <summary>Absolute project root.</summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes the writer for one project.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root.</param>
        /// <exception cref="ArgumentException">Thrown when the project root is blank.</exception>
        public EditorPlaySettingsFileWriter(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
        }

        /// <summary>
        /// Resolves the settings file path of one platform.
        /// </summary>
        /// <param name="platformId">Platform being played.</param>
        /// <returns>Absolute file path.</returns>
        public string ResolvePath(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            return Path.Combine(ProjectRootPath, "cache", "play", platformId, FileName);
        }

        /// <summary>
        /// Writes the resolved settings of one Play.
        /// </summary>
        /// <param name="platformId">Platform being played.</param>
        /// <param name="settings">Resolved settings.</param>
        /// <returns>Absolute path of the written file.</returns>
        public string Write(string platformId, PlayerPlaySettings settings) {
            string path = ResolvePath(platformId);
            PlayerPlaySettingsFile.Write(path, settings);
            return path;
        }
    }
}
```

- [ ] **Step 5: Rework `EditorPlayPreparation`**

Replace the `double targetFrameRate` and `bool waitForDebugger` constructor parameters (and the `TargetFrameRate`/`WaitForDebugger` properties) with one parameter placed after `runtimePlatformVersion`:

```csharp
        /// <param name="playSettings">Play settings captured when Play was pressed.</param>
```

```csharp
            EditorPlaySettingsSnapshot playSettings,
```

```csharp
            PlaySettings = playSettings ?? throw new ArgumentNullException(nameof(playSettings));
```

```csharp
        /// <summary>
        /// Gets the Play settings captured when Play was pressed; the launch resolves them after the cook.
        /// </summary>
        public EditorPlaySettingsSnapshot PlaySettings { get; }
```

- [ ] **Step 6: Rework `EditorPlaySessionService`**

1. Delete the `TargetFpsOption` and `WaitForDebuggerOption` constants. Add:

```csharp
        /// <summary>
        /// Player flag naming the resolved Play settings file (window scale and monitor, debugger wait, shadows, frame
        /// pacing and the platform's own Play settings).
        /// </summary>
        public const string PlaySettingsOption = "--play-settings";
```

2. Add the field `readonly EditorPlaySettingsFileWriter PlaySettingsFileWriter;` (XML doc: "Writes the resolved Play settings file the player reads."). Add `EditorPlaySettingsFileWriter playSettingsFileWriter` as the last parameter of the public constructor (forwarded) and before `clock` in the internal one. Assign it with `?? throw new ArgumentNullException(nameof(playSettingsFileWriter))`.

3. In `Play`, remove the `double targetFrameRate` and `bool waitForDebugger` parameters, their XML docs and the `targetFrameRate` guard. Add `EditorPlaySettingsSnapshot playSettings` after `runtimePlatformVersion`, with doc `<param name="playSettings">Play settings captured when Play was pressed; edits made afterwards apply to the next Play.</param>`, and extend the guard chain:

```csharp
            } else if (playSettings == null) {
                throw new ArgumentNullException(nameof(playSettings));
            } else if (!string.Equals(playSettings.PlatformId, platformId, StringComparison.Ordinal)) {
                throw new ArgumentException($"The Play settings were captured for platform '{playSettings.PlatformId}', not '{platformId}'.", nameof(playSettings));
            }
```

Pass `playSettings` to `new EditorPlayPreparation(...)` in place of the two removed arguments.

4. In `LaunchPreparedPlayer`, replace the `BuildPlayerArguments(...)` call with:

```csharp
            PlayerPlaySettings playerSettings = EditorPlayLaunchSettingsResolver.Resolve(preparation.PlaySettings, cookResult);
            string playSettingsPath = PlaySettingsFileWriter.Write(preparation.PlatformId, playerSettings);
            List<string> arguments = BuildPlayerArguments(
                cookResult,
                preparation.PlatformDisplayName,
                preparation.AdapterAssemblyPath,
                preparation.AdapterTypeName,
                preparation.RuntimeModulesRootPath,
                preparation.InputDevices,
                preparation.RuntimePlatformName,
                preparation.RuntimePlatformVersion,
                playSettingsPath);
```

(A resolver failure throws inside `CompletePreparation`'s existing try/catch and is reported as `Play failed: ...`. That path is a real failure, never a normal one.)

5. Replace `BuildPlayerArguments` with:

```csharp
        /// <summary>
        /// Builds the ordered player command line from the cook outcome, the platform adapter, the script modules and the
        /// resolved Play settings file.
        /// </summary>
        /// <param name="cookResult">Successful cook outcome naming the content root and startup scene.</param>
        /// <param name="platformDisplayName">Display name of the platform, shown in the player window title.</param>
        /// <param name="adapterAssemblyPath">Path of the assembly holding the platform's player adapter.</param>
        /// <param name="adapterTypeName">Type name of the platform's player adapter.</param>
        /// <param name="runtimeModulesRootPath">Directory holding the built runtime script modules.</param>
        /// <param name="inputDevices">Input devices the player forwards to the game.</param>
        /// <param name="runtimePlatformName">Platform name the player reports to the core.</param>
        /// <param name="runtimePlatformVersion">Platform version the player reports to the core.</param>
        /// <param name="playSettingsPath">Resolved Play settings file.</param>
        /// <returns>Player arguments, one entry per argument, in order.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the script builder reported no module root.</exception>
        static List<string> BuildPlayerArguments(
            EditorPlayCookResult cookResult,
            string platformDisplayName,
            string adapterAssemblyPath,
            string adapterTypeName,
            string runtimeModulesRootPath,
            PlayerInputDevices inputDevices,
            string runtimePlatformName,
            string runtimePlatformVersion,
            string playSettingsPath) {
            if (string.IsNullOrWhiteSpace(runtimeModulesRootPath)) {
                throw new InvalidOperationException("The Play script build succeeded without reporting a runtime module root.");
            }

            List<string> arguments = new List<string> {
                ContentRootOption, cookResult.ContentRootPath,
                SceneIdOption, cookResult.StartupSceneId,
                PlatformNameOption, platformDisplayName,
                ScenePathOption, cookResult.StartupSceneCookedRelativePath,
                AdapterAssemblyOption, adapterAssemblyPath,
                AdapterTypeOption, adapterTypeName,
                ScriptModulesOption, runtimeModulesRootPath,
                InputDevicesOption, PlayerInputDevicesText.Format(inputDevices),
                RuntimePlatformNameOption, runtimePlatformName,
                RuntimePlatformVersionOption, runtimePlatformVersion,
                PlaySettingsOption, playSettingsPath
            };
            if (cookResult.RuntimeSettingsPath.Length > 0) {
                arguments.Add("--runtime-settings");
                arguments.Add(cookResult.RuntimeSettingsPath);
            }

            return arguments;
        }
```

Add `using helengine.baseplatform.Player;` at the top.

- [ ] **Step 7: Rework `PlayerLaunchOptions`**

- Delete the `TargetFpsOption`, `ShadowsEnabledOption` and `WaitForDebuggerOption` constants, the `TargetFrameRate`/`WaitForDebugger`/`ShadowsEnabled` properties and their constructor parameters, `ParseTargetFrameRate`, and the switch handling at the top of the loop.
- Add:

```csharp
        /// <summary>Option naming the resolved Play settings file the editor wrote for this Play.</summary>
        public const string PlaySettingsOption = "--play-settings";
```

```csharp
        /// <summary>Gets the resolved Play settings of this Play (window, debugger wait, shadows, pacing, platform values).</summary>
        public PlayerPlaySettings PlaySettings { get; }
```

- In `Parse`: add `string playSettingsPath = null;` and `else if (string.Equals(option, PlaySettingsOption, StringComparison.Ordinal)) { playSettingsPath = ReadValue(args, i, playSettingsPath); }`. After the existing `RequireOption` calls, add `RequireOption(PlaySettingsOption, playSettingsPath);` and pass `PlayerPlaySettingsFile.Read(playSettingsPath)` to the constructor.
- Update the class summary command line to `... --runtime-platform-version <version> --play-settings <file> [--runtime-settings <file>]`.
- Add `using helengine.baseplatform.Player;`.

- [ ] **Step 8: Keep `EditorSession` compiling**

In `EditorSession.cs` construction (lines 956-966), pass `new EditorPlaySettingsFileWriter(this.projectPath)` as the last argument of `new EditorPlaySessionService(...)`.

In `HandlePlayRequested`, replace the `PlaySession.Play(...)` call with the version below. Task A13 replaces this temporary defaults capture with the store-backed one.

```csharp
                EditorPlaySettingsEvaluation defaults = EditorPlaySettingsEvaluator.Evaluate(new EditorPlaySettingsDocument(), "(defaults)", playPlatform.PlatformId, playPlatform.PlayerCapability);
                EditorPlaySettingsSnapshot playSettings = new EditorPlaySettingsSnapshot(defaults, waitForDebugger, playPlatform.PlayerCapability, Environment.ProcessId);
                EditorBuildExecutionResult result = PlaySession.Play(
                    playPlatform.PlatformId,
                    playPlatform.DisplayName,
                    CurrentScenePath,
                    SaveCurrentSceneForPlay,
                    playPlatform.BuilderAssemblyPath,
                    playPlatform.PlayerCapability.AdapterTypeName,
                    playPlatform.PlayerCapability.InputDevices,
                    playPlatform.PlayerCapability.RuntimePlatformName,
                    playPlatform.PlayerCapability.RuntimePlatformVersion,
                    playSettings,
                    launchResult => HandlePlayLaunchCompleted(launchResult, playSettings.EffectiveWaitForDebugger));
```

`PlayerHost` still reads `options.TargetFrameRate`, `options.WaitForDebugger` and `options.ShadowsEnabled`. Temporarily point them at `options.PlaySettings.TargetFrameRate`, `options.PlaySettings.WaitForDebugger` and `options.PlaySettings.ShadowsEnabled`. Task A9 rewrites these lines. An unlimited frame rate cannot reach the player yet, because the stored settings are not read until A13.

- [ ] **Step 9: Run tests to verify they pass**

Run: `.\dotnetw.cmd test engine\helengine.player.tests\helengine.player.tests.csproj`
Expected: PASS.
Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~managers.play|FullyQualifiedName~EditorSessionSceneSaveTests"`
Expected: PASS except names already on the 2026-09-19 baseline list.

- [ ] **Step 10: Commit**

```bash
git add engine/helengine.editor/managers/play/EditorPlaySettingsFileWriter.cs engine/helengine.editor/managers/play/EditorPlaySessionService.cs engine/helengine.editor/managers/play/EditorPlayPreparation.cs engine/helengine.editor/EditorSession.cs engine/helengine.player/PlayerLaunchOptions.cs engine/helengine.player/PlayerHost.cs engine/helengine.editor.tests/managers/play/TestPlaySettings.cs engine/helengine.editor.tests/managers/play/EditorPlaySessionServiceTests.cs engine/helengine.editor.tests/managers/play/EditorPlaySessionPreparationTests.cs engine/helengine.editor.tests/managers/play/EditorPlaySessionStartupTimingTests.cs engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs engine/helengine.player.tests/ContractPlaySettings.cs engine/helengine.player.tests/EditorPlayArgumentContractTests.cs engine/helengine.player.tests/PlayerLaunchOptionsTests.cs
git diff --cached --numstat
git commit -m "feat(play): hand the player one --play-settings file instead of separate flags

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task A9: Window scale, monitor and frame pacing in the player

**Files:**
- Create: `engine/helengine.player/PlayerScreenArea.cs`, `PlayerWindowLayout.cs`, `PlayerWindowLayoutResult.cs`, `PlayerMonitorLocator.cs`, `IPlayerFrameLimiter.cs`, `PlayerUnpacedFrameLimiter.cs`, `PlayerFrameLimiterFactory.cs`
- Modify: `engine/helengine.player/PlayerFramePacer.cs` (implements `IPlayerFrameLimiter`)
- Modify: `engine/helengine.player/PlayerWindow.cs` (size from the layout; manual position)
- Modify: `engine/helengine.player/PlayerHost.cs`
- Test: `engine/helengine.player.tests/PlayerWindowLayoutTests.cs`, `engine/helengine.player.tests/PlayerFrameLimiterFactoryTests.cs` (new)

**Interfaces:**
- Consumes: `PlayerPlaySettings`, `PlayerWindowScale`, `PlayerRenderSize`, `IPlatformPlayerAdapter.ApplyPlaySettings` (A3/A4)
- Produces:
  - `PlayerScreenArea(int left, int top, int width, int height)`
  - `PlayerWindowLayout.Compute(PlayerRenderSize renderSize, PlayerWindowScale scale, PlayerScreenArea workArea, int nonClientWidth, int nonClientHeight): PlayerWindowLayoutResult`
  - `PlayerWindowLayoutResult`: `Multiplier`, `ClientWidth`, `ClientHeight`, `Left`, `Top`, `ExceedsWorkArea`
  - `PlayerMonitorLocator.ResolveWorkArea(PlayerWindowMonitor monitor, int editorProcessId): PlayerScreenArea`
  - `IPlayerFrameLimiter { void Start(); void WaitForNextFrame(); }`
  - `PlayerFrameLimiterFactory.Create(PlayerPlaySettings settings, IPlayerFrameClock clock): IPlayerFrameLimiter`
  - `PlayerWindow(string platformName, string sceneId, int clientWidth, int clientHeight)` (constants `ClientWidth`/`ClientHeight` removed)

- [ ] **Step 1: Write the failing tests**

`engine/helengine.player.tests/PlayerWindowLayoutTests.cs`:

```csharp
using helengine.baseplatform.Player;

namespace helengine.player.tests {
    /// <summary>
    /// Verifies the Play window size and position for several monitor work areas.
    /// </summary>
    public sealed class PlayerWindowLayoutTests {
        /// <summary>PS2 framebuffer used as the render size.</summary>
        static readonly PlayerRenderSize Ps2 = new PlayerRenderSize(640, 448);

        /// <summary>Non-client border width of a fixed-single window.</summary>
        const int FrameWidth = 16;

        /// <summary>Non-client border plus caption height of a fixed-single window.</summary>
        const int FrameHeight = 39;

        /// <summary>Fit picks the largest integer multiple whose whole window fits.</summary>
        [Theory]
        [InlineData(1920, 1040, 2)]
        [InlineData(2560, 1400, 2)]
        [InlineData(3840, 2120, 4)]
        [InlineData(1366, 728, 1)]
        public void Compute_Fit_PicksLargestFittingMultiplier(int workWidth, int workHeight, int expected) {
            PlayerWindowLayoutResult layout = PlayerWindowLayout.Compute(Ps2, PlayerWindowScale.Fit, new PlayerScreenArea(0, 0, workWidth, workHeight), FrameWidth, FrameHeight);

            Assert.Equal(expected, layout.Multiplier);
            Assert.Equal(640 * expected, layout.ClientWidth);
            Assert.Equal(448 * expected, layout.ClientHeight);
            Assert.False(layout.ExceedsWorkArea);
        }

        /// <summary>Fit never goes below 1x, even when 1x does not fit.</summary>
        [Fact]
        public void Compute_Fit_OnTinyMonitor_IsOneAndReportsOverflow() {
            PlayerWindowLayoutResult layout = PlayerWindowLayout.Compute(Ps2, PlayerWindowScale.Fit, new PlayerScreenArea(0, 0, 640, 400), FrameWidth, FrameHeight);

            Assert.Equal(1, layout.Multiplier);
            Assert.True(layout.ExceedsWorkArea);
            Assert.Equal(0, layout.Left);
            Assert.Equal(0, layout.Top);
        }

        /// <summary>A fixed scale is honoured; the window is centred inside the work area, which may start off-origin.</summary>
        [Fact]
        public void Compute_FixedScale_CentresInWorkArea() {
            PlayerWindowLayoutResult layout = PlayerWindowLayout.Compute(Ps2, PlayerWindowScale.Two, new PlayerScreenArea(1920, 0, 1920, 1040), FrameWidth, FrameHeight);

            Assert.Equal(1280, layout.ClientWidth);
            Assert.Equal(896, layout.ClientHeight);
            Assert.Equal(1920 + (1920 - 1296) / 2, layout.Left);
            Assert.Equal((1040 - 935) / 2, layout.Top);
        }

        /// <summary>A fixed scale larger than the monitor still opens at that scale, at the work-area origin.</summary>
        [Fact]
        public void Compute_FixedScaleLargerThanMonitor_OpensAtOriginAndReportsOverflow() {
            PlayerWindowLayoutResult layout = PlayerWindowLayout.Compute(Ps2, PlayerWindowScale.Four, new PlayerScreenArea(100, 50, 1920, 1040), FrameWidth, FrameHeight);

            Assert.Equal(2560, layout.ClientWidth);
            Assert.True(layout.ExceedsWorkArea);
            Assert.Equal(100, layout.Left);
            Assert.Equal(50, layout.Top);
        }
    }
}
```

`engine/helengine.player.tests/PlayerFrameLimiterFactoryTests.cs`:

```csharp
using helengine.baseplatform.Player;

namespace helengine.player.tests {
    /// <summary>
    /// Verifies the frame-rate Play setting selects paced or unpaced frame loops.
    /// </summary>
    public sealed class PlayerFrameLimiterFactoryTests {
        /// <summary>A paced setting creates a pacer at the target frame rate.</summary>
        [Fact]
        public void Create_WhenPaced_ReturnsPacerAtTargetRate() {
            PlayerPlaySettings settings = new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, false, 50d, new Dictionary<string, string>());

            PlayerFramePacer pacer = Assert.IsType<PlayerFramePacer>(PlayerFrameLimiterFactory.Create(settings, new SimulatedPlayerFrameClock()));

            Assert.Equal(1d / 50d, pacer.FrameSeconds);
        }

        /// <summary>An unlimited setting creates a limiter that never waits.</summary>
        [Fact]
        public void Create_WhenUnlimited_ReturnsUnpacedLimiter() {
            PlayerPlaySettings settings = new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, true, 0d, new Dictionary<string, string>());

            IPlayerFrameLimiter limiter = PlayerFrameLimiterFactory.Create(settings, new SimulatedPlayerFrameClock());

            Assert.IsType<PlayerUnpacedFrameLimiter>(limiter);
            limiter.Start();
            limiter.WaitForNextFrame();
        }
    }
}
```

(Check `SimulatedPlayerFrameClock`'s constructor in `engine/helengine.player.tests/SimulatedPlayerFrameClock.cs` and pass whatever it requires.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `.\dotnetw.cmd test engine\helengine.player.tests\helengine.player.tests.csproj --filter "FullyQualifiedName~PlayerWindowLayoutTests|FullyQualifiedName~PlayerFrameLimiterFactoryTests"`
Expected: build FAIL (types not found).

- [ ] **Step 3: Implement the pure layout**

`PlayerScreenArea.cs`:

```csharp
namespace helengine.player {
    /// <summary>
    /// A monitor work area in virtual-screen pixels (the monitor minus the taskbar).
    /// </summary>
    public sealed class PlayerScreenArea {
        /// <summary>
        /// Initializes one work area.
        /// </summary>
        /// <param name="left">Left edge in virtual-screen pixels.</param>
        /// <param name="top">Top edge in virtual-screen pixels.</param>
        /// <param name="width">Positive width.</param>
        /// <param name="height">Positive height.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the size is not positive.</exception>
        public PlayerScreenArea(int left, int top, int width, int height) {
            if (width <= 0) {
                throw new ArgumentOutOfRangeException(nameof(width), width, "A work area needs a positive width.");
            } else if (height <= 0) {
                throw new ArgumentOutOfRangeException(nameof(height), height, "A work area needs a positive height.");
            }

            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }

        /// <summary>Gets the left edge.</summary>
        public int Left { get; }

        /// <summary>Gets the top edge.</summary>
        public int Top { get; }

        /// <summary>Gets the width.</summary>
        public int Width { get; }

        /// <summary>Gets the height.</summary>
        public int Height { get; }
    }
}
```

`PlayerWindowLayoutResult.cs`:

```csharp
namespace helengine.player {
    /// <summary>
    /// The computed Play window: integer multiplier, client size and outer-window position.
    /// </summary>
    public sealed class PlayerWindowLayoutResult {
        /// <summary>
        /// Initializes one layout.
        /// </summary>
        /// <param name="multiplier">Integer magnification of the render size.</param>
        /// <param name="clientWidth">Client width in pixels.</param>
        /// <param name="clientHeight">Client height in pixels.</param>
        /// <param name="left">Outer window left edge.</param>
        /// <param name="top">Outer window top edge.</param>
        /// <param name="exceedsWorkArea">Whether the outer window is larger than the work area.</param>
        public PlayerWindowLayoutResult(int multiplier, int clientWidth, int clientHeight, int left, int top, bool exceedsWorkArea) {
            Multiplier = multiplier;
            ClientWidth = clientWidth;
            ClientHeight = clientHeight;
            Left = left;
            Top = top;
            ExceedsWorkArea = exceedsWorkArea;
        }

        /// <summary>Gets the integer magnification of the render size.</summary>
        public int Multiplier { get; }

        /// <summary>Gets the client width.</summary>
        public int ClientWidth { get; }

        /// <summary>Gets the client height.</summary>
        public int ClientHeight { get; }

        /// <summary>Gets the outer window left edge.</summary>
        public int Left { get; }

        /// <summary>Gets the outer window top edge.</summary>
        public int Top { get; }

        /// <summary>Gets whether the outer window is larger than the work area.</summary>
        public bool ExceedsWorkArea { get; }
    }
}
```

`PlayerWindowLayout.cs`:

```csharp
using helengine.baseplatform.Player;

namespace helengine.player {
    /// <summary>
    /// Computes the Play window from the render size, the <c>window-scale</c> setting and the chosen monitor's work area.
    /// <c>fit</c> picks the largest integer multiple whose whole window (client plus borders) fits, never below 1x. A fixed
    /// scale is honoured even when it does not fit; such a window opens at the work-area origin.
    /// </summary>
    public static class PlayerWindowLayout {
        /// <summary>Largest multiplier fit considers.</summary>
        public const int MaximumMultiplier = 16;

        /// <summary>
        /// Computes the window layout.
        /// </summary>
        /// <param name="renderSize">Platform render size.</param>
        /// <param name="scale">Window scale setting.</param>
        /// <param name="workArea">Work area of the chosen monitor.</param>
        /// <param name="nonClientWidth">Horizontal border size the window adds around the client.</param>
        /// <param name="nonClientHeight">Vertical border and caption size the window adds around the client.</param>
        /// <returns>The layout.</returns>
        public static PlayerWindowLayoutResult Compute(PlayerRenderSize renderSize, PlayerWindowScale scale, PlayerScreenArea workArea, int nonClientWidth, int nonClientHeight) {
            if (renderSize == null) {
                throw new ArgumentNullException(nameof(renderSize));
            } else if (workArea == null) {
                throw new ArgumentNullException(nameof(workArea));
            } else if (nonClientWidth < 0 || nonClientHeight < 0) {
                throw new ArgumentOutOfRangeException(nameof(nonClientWidth), "Window borders cannot be negative.");
            }

            int multiplier;
            if (scale == PlayerWindowScale.Fit) {
                multiplier = 1;
                for (int candidate = 2; candidate <= MaximumMultiplier; candidate++) {
                    if (!Fits(renderSize, candidate, workArea, nonClientWidth, nonClientHeight)) {
                        break;
                    }
                    multiplier = candidate;
                }
            } else {
                multiplier = PlayerWindowScaleText.ToMultiplier(scale);
            }

            int clientWidth = renderSize.Width * multiplier;
            int clientHeight = renderSize.Height * multiplier;
            int outerWidth = clientWidth + nonClientWidth;
            int outerHeight = clientHeight + nonClientHeight;
            bool exceeds = !Fits(renderSize, multiplier, workArea, nonClientWidth, nonClientHeight);
            int left = exceeds ? workArea.Left : workArea.Left + (workArea.Width - outerWidth) / 2;
            int top = exceeds ? workArea.Top : workArea.Top + (workArea.Height - outerHeight) / 2;
            return new PlayerWindowLayoutResult(multiplier, clientWidth, clientHeight, left, top, exceeds);
        }

        /// <summary>
        /// Reports whether the outer window at one multiplier fits the work area.
        /// </summary>
        /// <param name="renderSize">Render size.</param>
        /// <param name="multiplier">Candidate multiplier.</param>
        /// <param name="workArea">Work area.</param>
        /// <param name="nonClientWidth">Horizontal border size.</param>
        /// <param name="nonClientHeight">Vertical border and caption size.</param>
        /// <returns>True when the window fits.</returns>
        static bool Fits(PlayerRenderSize renderSize, int multiplier, PlayerScreenArea workArea, int nonClientWidth, int nonClientHeight) {
            return renderSize.Width * multiplier + nonClientWidth <= workArea.Width
                && renderSize.Height * multiplier + nonClientHeight <= workArea.Height;
        }
    }
}
```

- [ ] **Step 4: Implement the limiters**

`IPlayerFrameLimiter.cs`:

```csharp
namespace helengine.player {
    /// <summary>
    /// Limits how fast the player's frame loop runs.
    /// </summary>
    public interface IPlayerFrameLimiter {
        /// <summary>Begins limiting; called once before the first frame.</summary>
        void Start();

        /// <summary>Waits, if needed, until the next frame may start.</summary>
        void WaitForNextFrame();
    }
}
```

Make `PlayerFramePacer` declare `: IPlayerFrameLimiter`; its `Start`/`WaitForNextFrame` already match.

`PlayerUnpacedFrameLimiter.cs`:

```csharp
namespace helengine.player {
    /// <summary>
    /// Frame limiter for <c>frame-rate: unlimited</c>: never waits, so the loop runs as fast as the machine allows.
    /// </summary>
    public sealed class PlayerUnpacedFrameLimiter : IPlayerFrameLimiter {
        /// <summary>Does nothing; an unpaced loop needs no start time.</summary>
        public void Start() {
        }

        /// <summary>Returns immediately.</summary>
        public void WaitForNextFrame() {
        }
    }
}
```

`PlayerFrameLimiterFactory.cs`:

```csharp
using helengine.baseplatform.Player;

namespace helengine.player {
    /// <summary>
    /// Chooses the frame limiter for the <c>frame-rate</c> Play setting.
    /// </summary>
    public static class PlayerFrameLimiterFactory {
        /// <summary>
        /// Creates the limiter for one Play.
        /// </summary>
        /// <param name="settings">Resolved Play settings.</param>
        /// <param name="clock">Clock a pacer uses.</param>
        /// <returns>A pacer at the target frame rate, or an unpaced limiter.</returns>
        public static IPlayerFrameLimiter Create(PlayerPlaySettings settings, IPlayerFrameClock clock) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (settings.FrameRateUnlimited) {
                return new PlayerUnpacedFrameLimiter();
            }

            return new PlayerFramePacer(settings.TargetFrameRate, clock);
        }
    }
}
```

- [ ] **Step 5: Implement the monitor locator**

`PlayerMonitorLocator.cs`:

```csharp
using System.Diagnostics;
using helengine.baseplatform.Player;

namespace helengine.player {
    /// <summary>
    /// Finds the work area of the monitor the Play window opens on: the monitor showing the editor's main window, or the
    /// primary monitor.
    /// </summary>
    public static class PlayerMonitorLocator {
        /// <summary>
        /// Resolves the work area for the <c>window-monitor</c> setting.
        /// </summary>
        /// <param name="monitor">Monitor choice.</param>
        /// <param name="editorProcessId">Process id of the editor that launched the player.</param>
        /// <returns>Work area of the chosen monitor.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the editor has no main window to locate.</exception>
        public static PlayerScreenArea ResolveWorkArea(PlayerWindowMonitor monitor, int editorProcessId) {
            Screen screen;
            if (monitor == PlayerWindowMonitor.Primary) {
                screen = Screen.PrimaryScreen;
            } else {
                using Process editor = Process.GetProcessById(editorProcessId);
                IntPtr editorWindow = editor.MainWindowHandle;
                if (editorWindow == IntPtr.Zero) {
                    throw new InvalidOperationException($"Editor process {editorProcessId} has no main window, so the player cannot open on the editor's monitor; choose window-monitor 'primary'.");
                }
                screen = Screen.FromHandle(editorWindow);
            }

            Rectangle area = screen.WorkingArea;
            return new PlayerScreenArea(area.Left, area.Top, area.Width, area.Height);
        }
    }
}
```

- [ ] **Step 6: Size the window from the layout**

In `PlayerWindow.cs`, delete `ClientWidth`/`ClientHeight` and change the constructor:

```csharp
        /// <summary>
        /// Creates the player window titled after the platform and scene being played, with the given client size. The
        /// host positions it explicitly.
        /// </summary>
        /// <param name="platformName">Display name of the platform shown in square brackets in the title bar.</param>
        /// <param name="sceneId">Stable id of the scene shown in the title bar.</param>
        /// <param name="clientWidth">Client width in pixels.</param>
        /// <param name="clientHeight">Client height in pixels.</param>
        /// <exception cref="ArgumentException">The platform name or scene id is blank.</exception>
        public PlayerWindow(string platformName, string sceneId, int clientWidth, int clientHeight) {
            string title = PlayerWindowTitle.Format(platformName, sceneId);
            ClientSize = new Size(clientWidth, clientHeight);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = false;
            Text = title;
        }
```

- [ ] **Step 7: Apply the settings in `PlayerHost.Run`**

Replace the section from the debugger wait down to `renderer3D.AddWindow(...)` so it reads:

```csharp
                PlayerPlaySettings playSettings = options.PlaySettings;
                if (playSettings.WaitForDebugger) {
                    Console.WriteLine("[player] waiting for debugger, pid " + Environment.ProcessId);
                    while (!Debugger.IsAttached) {
                        Thread.Sleep(DebuggerPollMilliseconds);
                    }
                }

                IPlatformPlayerAdapter adapter = PlayerPlatformAdapterLoader.Load(options.AdapterAssemblyPath, options.AdapterTypeName);
                if (options.RuntimeSettingsPath.Length > 0) { runtimeSettings = PlatformSettingsSerializer.ReadBytes(File.ReadAllBytes(options.RuntimeSettingsPath)); }
                adapter.Initialize(options.ContentRootPath);
                PlayerRenderSize renderSize = adapter.ApplyPlaySettings(playSettings);
                PlayerScriptModuleLoader.LoadAll(options.ScriptModulesRootPath);

                ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
                shaderBackendRegistry.Register(new DirectX11ShaderBackend());
                shaders = new EditorBuiltInShaderAssetLibrary(shaderBackendRegistry);

                renderer3D = new PlayerRenderManager3D(adapter, shaders);
                renderer3D.ShadowsEnabled = playSettings.ShadowsEnabled;
                // Vsync only prevents tearing; the frame limiter holds the loop to the platform's rate. An unlimited Play
                // turns vsync off too, so it really runs unpaced.
                renderer3D.PresentSyncInterval = playSettings.FrameRateUnlimited ? 0 : 1;
                window = new PlayerWindow(options.PlatformName, options.SceneId, renderSize.Width, renderSize.Height);
                PlayerScreenArea workArea = PlayerMonitorLocator.ResolveWorkArea(playSettings.WindowMonitor, playSettings.EditorProcessId);
                PlayerWindowLayoutResult layout = PlayerWindowLayout.Compute(
                    renderSize,
                    playSettings.WindowScale,
                    workArea,
                    window.Width - window.ClientSize.Width,
                    window.Height - window.ClientSize.Height);
                window.ClientSize = new Size(layout.ClientWidth, layout.ClientHeight);
                window.Location = new Point(layout.Left, layout.Top);
                Console.WriteLine("[player] render " + renderSize.Width + "x" + renderSize.Height + " at " + layout.Multiplier + "x");
                if (layout.ExceedsWorkArea) {
                    Console.WriteLine("[player] window-scale " + layout.Multiplier + "x is larger than the monitor work area " + workArea.Width + "x" + workArea.Height + "; the window opens at the work-area origin");
                }
                window.Show();
```

Change `renderer3D.AddWindow(window.Handle, PlayerWindow.ClientWidth, PlayerWindow.ClientHeight);` to `renderer3D.AddWindow(window.Handle, layout.ClientWidth, layout.ClientHeight);`. Task A10 replaces this call with the fixed-render-size call. Change `RunFrameLoop(core, window, options.TargetFrameRate, startupStopwatch);` to `RunFrameLoop(core, window, playSettings, startupStopwatch);`. Rework `RunFrameLoop` so that:
- its parameter is `PlayerPlaySettings playSettings` (XML doc updated);
- its first line logs `"[player] frame loop target: " + (playSettings.FrameRateUnlimited ? "unlimited" : playSettings.TargetFrameRate.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " FPS")`;
- it creates `IPlayerFrameLimiter limiter = PlayerFrameLimiterFactory.Create(playSettings, clock);`, using `limiter.Start()` and `limiter.WaitForNextFrame()` where `pacer` was used.

Add `using helengine.baseplatform.Player;`.

- [ ] **Step 8: Run tests to verify they pass**

Run: `.\dotnetw.cmd test engine\helengine.player.tests\helengine.player.tests.csproj`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add engine/helengine.player/PlayerScreenArea.cs engine/helengine.player/PlayerWindowLayout.cs engine/helengine.player/PlayerWindowLayoutResult.cs engine/helengine.player/PlayerMonitorLocator.cs engine/helengine.player/IPlayerFrameLimiter.cs engine/helengine.player/PlayerUnpacedFrameLimiter.cs engine/helengine.player/PlayerFrameLimiterFactory.cs engine/helengine.player/PlayerFramePacer.cs engine/helengine.player/PlayerWindow.cs engine/helengine.player/PlayerHost.cs engine/helengine.player.tests/PlayerWindowLayoutTests.cs engine/helengine.player.tests/PlayerFrameLimiterFactoryTests.cs
git commit -m "feat(player): size, place and pace the Play window from the Play settings

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task A10: Render at the platform size and point-magnify into the window

**Files:**
- Create: `engine/helengine.directx11/DirectX11PointMagnifyPass.cs`
- Create: `engine/helengine.directx11/DirectX11FixedRenderSize.cs`
- Modify: `engine/helengine.directx11/DirectX11SwapChainSurface.cs` (new members plus `Dispose`)
- Modify: `engine/helengine.directx11/DirectX11Renderer3D.cs` (`AddWindow` at 398-434 split, new `AddWindowWithFixedRenderSize`, `OnWindowResized` at 451, `Draw` at 1765-1797, `Dispose`)
- Modify: `engine/helengine.player/PlayerHost.cs` (the `AddWindow` call)
- Test: `engine/helengine.player.tests/DirectX11FixedRenderSizeTests.cs` (new)

**Interfaces:**
- Consumes: `PlayerWindowLayoutResult` (A9), `PlayerRenderSize` (A4)
- Produces:
  - `DirectX11FixedRenderSize.ValidateMagnification(int clientWidth, int clientHeight, int renderWidth, int renderHeight): int` returns the integer multiplier and throws `ArgumentException` unless the client is the same integer multiple of the render size on both axes
  - `DirectX11Renderer3D.AddWindowWithFixedRenderSize(IntPtr handle, int clientWidth, int clientHeight, int renderWidth, int renderHeight): void`. The core sees the window as `renderWidth x renderHeight`, the scene renders into an offscreen target of that size, and Present point-magnifies it into the client-sized back buffer.

- [ ] **Step 1: Write the failing test**

```csharp
using helengine.directx11;

namespace helengine.player.tests {
    /// <summary>
    /// Verifies the fixed-render-size surface only accepts exact integer magnification, which is what makes point
    /// sampling reproduce every source pixel as an identical block.
    /// </summary>
    public sealed class DirectX11FixedRenderSizeTests {
        /// <summary>Equal integer multiples on both axes are accepted and reported.</summary>
        [Theory]
        [InlineData(640, 448, 640, 448, 1)]
        [InlineData(1280, 896, 640, 448, 2)]
        [InlineData(1280, 896, 320, 224, 4)]
        public void ValidateMagnification_AcceptsIntegerMultiples(int clientWidth, int clientHeight, int renderWidth, int renderHeight, int expected) {
            Assert.Equal(expected, DirectX11FixedRenderSize.ValidateMagnification(clientWidth, clientHeight, renderWidth, renderHeight));
        }

        /// <summary>Fractional or mismatched magnification is rejected.</summary>
        [Theory]
        [InlineData(1000, 700, 640, 448)]
        [InlineData(1280, 448, 640, 448)]
        [InlineData(320, 224, 640, 448)]
        public void ValidateMagnification_RejectsNonIntegerOrUnequalMultiples(int clientWidth, int clientHeight, int renderWidth, int renderHeight) {
            Assert.Throws<ArgumentException>(() => DirectX11FixedRenderSize.ValidateMagnification(clientWidth, clientHeight, renderWidth, renderHeight));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `.\dotnetw.cmd test engine\helengine.player.tests\helengine.player.tests.csproj --filter "FullyQualifiedName~DirectX11FixedRenderSizeTests"`
Expected: build FAIL (`DirectX11FixedRenderSize` not found).

- [ ] **Step 3: Implement the rule**

```csharp
namespace helengine.directx11 {
    /// <summary>
    /// Rules for a window that renders at a fixed size and is magnified on present: the client must be the same integer
    /// multiple of the render size on both axes, so point sampling turns every rendered pixel into an identical square block.
    /// </summary>
    public static class DirectX11FixedRenderSize {
        /// <summary>
        /// Validates the magnification and returns its integer multiplier.
        /// </summary>
        /// <param name="clientWidth">Window client width.</param>
        /// <param name="clientHeight">Window client height.</param>
        /// <param name="renderWidth">Fixed render width.</param>
        /// <param name="renderHeight">Fixed render height.</param>
        /// <returns>The integer multiplier (at least 1).</returns>
        /// <exception cref="ArgumentException">Thrown when the sizes are not positive or not an equal integer multiple.</exception>
        public static int ValidateMagnification(int clientWidth, int clientHeight, int renderWidth, int renderHeight) {
            if (clientWidth <= 0 || clientHeight <= 0 || renderWidth <= 0 || renderHeight <= 0) {
                throw new ArgumentException("Client and render sizes must be positive.");
            } else if (clientWidth % renderWidth != 0 || clientHeight % renderHeight != 0) {
                throw new ArgumentException($"Client {clientWidth}x{clientHeight} is not an integer multiple of render size {renderWidth}x{renderHeight}.");
            } else if (clientWidth / renderWidth != clientHeight / renderHeight) {
                throw new ArgumentException($"Client {clientWidth}x{clientHeight} magnifies render size {renderWidth}x{renderHeight} by different amounts horizontally and vertically.");
            }

            return clientWidth / renderWidth;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `.\dotnetw.cmd test engine\helengine.player.tests\helengine.player.tests.csproj --filter "FullyQualifiedName~DirectX11FixedRenderSizeTests"`
Expected: PASS.

- [ ] **Step 5: Implement the magnify pass**

```csharp
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.Mathematics.Interop;

namespace helengine.directx11 {
    /// <summary>
    /// Copies a rendered frame into a larger back buffer with point sampling (nearest texel), the desktop equivalent of a
    /// console's video output magnifying its framebuffer. Draws one full-screen triangle generated from the vertex id, so
    /// it needs no vertex buffer or input layout.
    /// </summary>
    public sealed class DirectX11PointMagnifyPass : IDisposable {
        /// <summary>HLSL source of the full-screen triangle and the point-sampled copy.</summary>
        const string ShaderSource = @"
Texture2D SourceTexture : register(t0);
SamplerState PointSampler : register(s0);
struct VertexOutput { float4 Position : SV_POSITION; float2 TexCoord : TEXCOORD0; };
VertexOutput VSMain(uint vertexId : SV_VertexID) {
    VertexOutput output;
    float2 uv = float2((vertexId << 1) & 2, vertexId & 2);
    output.TexCoord = uv;
    output.Position = float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
    return output;
}
float4 PSMain(VertexOutput input) : SV_TARGET {
    return SourceTexture.Sample(PointSampler, input.TexCoord);
}";

        /// <summary>Vertex shader generating the full-screen triangle.</summary>
        readonly VertexShader VertexShader;

        /// <summary>Pixel shader sampling the source with point filtering.</summary>
        readonly PixelShader PixelShader;

        /// <summary>Point, clamp sampler.</summary>
        readonly SamplerState PointSampler;

        /// <summary>
        /// Compiles the pass for one device.
        /// </summary>
        /// <param name="device">Device that owns the pass resources.</param>
        public DirectX11PointMagnifyPass(SharpDX.Direct3D11.Device device) {
            if (device == null) {
                throw new ArgumentNullException(nameof(device));
            }

            using (CompilationResult vertex = ShaderBytecode.Compile(ShaderSource, "VSMain", "vs_4_0")) {
                VertexShader = new VertexShader(device, vertex.Bytecode);
            }
            using (CompilationResult pixel = ShaderBytecode.Compile(ShaderSource, "PSMain", "ps_4_0")) {
                PixelShader = new PixelShader(device, pixel.Bytecode);
            }
            PointSampler = new SamplerState(device, new SamplerStateDescription {
                Filter = Filter.MinMagMipPoint,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                MinimumLod = 0f,
                MaximumLod = float.MaxValue
            });
        }

        /// <summary>
        /// Draws the source into the whole target with point sampling.
        /// </summary>
        /// <param name="context">Immediate context.</param>
        /// <param name="source">Rendered frame.</param>
        /// <param name="target">Back buffer view.</param>
        /// <param name="targetWidth">Back buffer width.</param>
        /// <param name="targetHeight">Back buffer height.</param>
        public void Execute(DeviceContext context, ShaderResourceView source, RenderTargetView target, int targetWidth, int targetHeight) {
            context.OutputMerger.SetRenderTargets((DepthStencilView)null, target);
            context.OutputMerger.SetBlendState(null);
            context.OutputMerger.SetDepthStencilState(null);
            context.Rasterizer.State = null;
            context.Rasterizer.SetViewport(new RawViewportF { X = 0f, Y = 0f, Width = targetWidth, Height = targetHeight, MinDepth = 0f, MaxDepth = 1f });
            context.InputAssembler.InputLayout = null;
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.VertexShader.Set(VertexShader);
            context.PixelShader.Set(PixelShader);
            context.PixelShader.SetShaderResource(0, source);
            context.PixelShader.SetSampler(0, PointSampler);
            context.Draw(3, 0);
            context.PixelShader.SetShaderResource(0, null);
        }

        /// <summary>Releases the shaders and sampler.</summary>
        public void Dispose() {
            PointSampler.Dispose();
            PixelShader.Dispose();
            VertexShader.Dispose();
        }
    }
}
```

- [ ] **Step 6: Extend the surface and the renderer**

In `DirectX11SwapChainSurface.cs` add (with XML docs) `public bool UsesFixedRenderSize { get; set; }`, `public RenderTargetView BackBufferRenderTargetView { get; set; }`, `public Texture2D RenderColorTexture { get; set; }`, `public ShaderResourceView RenderColorShaderResourceView { get; set; }`, `public int BackBufferWidth { get; set; }`, `public int BackBufferHeight { get; set; }`. Prefix `Dispose` with `RenderColorShaderResourceView?.Dispose(); RenderColorTexture?.Dispose(); BackBufferRenderTargetView?.Dispose();`.

In `DirectX11Renderer3D.cs`:
1. Move everything in `AddWindow` after `base.AddWindow(handle, width, height);` into `DirectX11SwapChainSurface CreateSwapChainSurface(IntPtr handle, int width, int height)`, which returns the surface. `AddWindow` becomes `base.AddWindow(handle, width, height); CreateSwapChainSurface(handle, width, height);`.
2. Add the field `DirectX11PointMagnifyPass PointMagnifyPass;` (XML: "Point magnification used by fixed-render-size surfaces; created on first use.") and dispose it in `Dispose` before `Device?.Dispose()`: `PointMagnifyPass?.Dispose();`.
3. Add:

```csharp
        /// <summary>
        /// Adds a window that renders at a fixed size and is magnified on present: the core and every camera see the
        /// window as <paramref name="renderWidth"/> x <paramref name="renderHeight"/>, the scene renders into an offscreen
        /// target of that size, and Present copies it into the client-sized back buffer with point sampling.
        /// </summary>
        /// <param name="handle">Native window handle.</param>
        /// <param name="clientWidth">Window client width; an integer multiple of the render width.</param>
        /// <param name="clientHeight">Window client height; the same integer multiple of the render height.</param>
        /// <param name="renderWidth">Fixed render width.</param>
        /// <param name="renderHeight">Fixed render height.</param>
        /// <exception cref="ArgumentException">Thrown when the client is not an equal integer multiple of the render size.</exception>
        public void AddWindowWithFixedRenderSize(IntPtr handle, int clientWidth, int clientHeight, int renderWidth, int renderHeight) {
            DirectX11FixedRenderSize.ValidateMagnification(clientWidth, clientHeight, renderWidth, renderHeight);
            base.AddWindow(handle, renderWidth, renderHeight);
            DirectX11SwapChainSurface surface = CreateSwapChainSurface(handle, clientWidth, clientHeight);

            surface.BackBufferRenderTargetView = surface.RenderTargetView;
            surface.BackBufferWidth = clientWidth;
            surface.BackBufferHeight = clientHeight;
            surface.RenderColorTexture = new Texture2D(Device, new Texture2DDescription {
                Format = Format.B8G8R8A8_UNorm,
                ArraySize = 1,
                MipLevels = 1,
                Width = renderWidth,
                Height = renderHeight,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });
            surface.RenderTargetView = new RenderTargetView(Device, surface.RenderColorTexture);
            surface.RenderColorShaderResourceView = new ShaderResourceView(Device, surface.RenderColorTexture);
            surface.DepthStencilView.Dispose();
            surface.DepthBuffer.Dispose();
            surface.DepthBuffer = new Texture2D(Device, new Texture2DDescription {
                Format = Format.D32_Float_S8X24_UInt,
                ArraySize = 1,
                MipLevels = 1,
                Width = renderWidth,
                Height = renderHeight,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });
            surface.DepthStencilView = new DepthStencilView(Device, surface.DepthBuffer);
            surface.Width = renderWidth;
            surface.Height = renderHeight;
            surface.UsesFixedRenderSize = true;
            PointMagnifyPass ??= new DirectX11PointMagnifyPass(Device);
        }
```

4. In `OnWindowResized`, after the surface lookup, add the guard below. The player window is fixed-size, so a fixed-render-size surface only ever sees its own size again (ignored). Any other size is a real error, and only the editor's resizable windows reach the existing code.

```csharp
            if (surface.UsesFixedRenderSize) {
                if (newWidth == surface.BackBufferWidth && newHeight == surface.BackBufferHeight) {
                    return;
                }

                throw new InvalidOperationException($"A fixed-render-size surface cannot be resized from {surface.BackBufferWidth}x{surface.BackBufferHeight} to {newWidth}x{newHeight}; its window must keep the client size it was added with.");
            }
```

5. In `Draw`, immediately before `surface.SwapChain.Present(...)`:

```csharp
                    if (surface.UsesFixedRenderSize) {
                        PointMagnifyPass.Execute(Device.ImmediateContext, surface.RenderColorShaderResourceView, surface.BackBufferRenderTargetView, surface.BackBufferWidth, surface.BackBufferHeight);
                    }
```

6. Check that the 2D renderer writes the main window's UI through `context.Surface.RenderTargetView` (or the surface it is handed) inside the camera passes, before Present. Run Grep with pattern `RenderTargetView` in `engine/helengine.directx11/DirectX11Renderer2D.cs`. If the 2D renderer resolves the swap chain's back buffer on its own (for example `SwapChain.GetBackBuffer`), route it to `surface.RenderTargetView` the same way. UI must land in the offscreen target, otherwise it would be drawn unmagnified at the top-left of the window.

- [ ] **Step 7: Use it in the player**

In `PlayerHost.Run`, replace `renderer3D.AddWindow(window.Handle, layout.ClientWidth, layout.ClientHeight);` with:

```csharp
                renderer3D.AddWindowWithFixedRenderSize(window.Handle, layout.ClientWidth, layout.ClientHeight, renderSize.Width, renderSize.Height);
```

- [ ] **Step 8: Build and run the player tests**

Run: `.\dotnetw.cmd test engine\helengine.player.tests\helengine.player.tests.csproj` and `.\dotnetw.cmd build engine\helengine.editor\helengine.editor.csproj`
Expected: PASS and build success.

- [ ] **Step 9: Manual Play check (announce first; no screenshots unless Helena allows them)**

Tell Helena the editor will be launched for a Play check, then follow the memory "helengine editor launch context": a real engine version, the installed windows platform, and the log at `%TEMP%\helengine.editor.log`. Ask her to open demodisc, choose PS2 and press Play. Expected player console lines: `[player] render 640x448 at 2x` on a 1080p monitor, then the first-frame marker. Helena confirms the menu (3D and UI text) fills the window with crisp, blocky pixels.

- [ ] **Step 10: Commit**

```bash
git add engine/helengine.directx11/DirectX11PointMagnifyPass.cs engine/helengine.directx11/DirectX11FixedRenderSize.cs engine/helengine.directx11/DirectX11SwapChainSurface.cs engine/helengine.directx11/DirectX11Renderer3D.cs engine/helengine.player/PlayerHost.cs engine/helengine.player.tests/DirectX11FixedRenderSizeTests.cs
git diff --cached --numstat
git commit -m "feat(player): render Play at the platform size and point-magnify it into the window

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task A11: Play Settings dialog view-model

**Files:**
- Create: `engine/helengine.editor/managers/play/EditorPlaySettingsDialogModel.cs`
- Test: `engine/helengine.editor.tests/managers/play/EditorPlaySettingsDialogModelTests.cs` (new)

**Interfaces:**
- Consumes: `EditorPlaySettingsDocument`, `EditorPlaySettingsEvaluator`, `EditorPlaySettingIssue`, `EditorPlaySettingScope`, `EditorPlaySettingValueValidator` (A5/A6)
- Produces:
  - `EditorPlaySettingsDialogModel(string platformId, string platformDisplayName, PlatformPlayerCapability capability, EditorPlaySettingsDocument document, string filePath)`
  - Properties `PlatformId`, `PlatformDisplayName`, `CommonDefinitions`, `PlatformDefinitions`, `StoredIssues: IReadOnlyList<EditorPlaySettingIssue>`
  - `IsStoredValueInvalid(EditorPlaySettingScope scope, string settingId): bool`
  - `CreateCommonEditValues(): Dictionary<string,string>` and `CreatePlatformEditValues(): Dictionary<string,string>`
  - `ApplyEdits(IReadOnlyDictionary<string,string> commonEdits, IReadOnlyDictionary<string,string> platformEdits): IReadOnlyList<EditorPlaySettingIssue>`
  - `CreateSavedDocument(): EditorPlaySettingsDocument`

- [ ] **Step 1: Write the failing tests**

```csharp
using helengine.baseplatform.Definitions;
using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Verifies the Play Settings dialog's view-model without any GUI: initial values, highlighting of stored values the
/// declarations reject, edit validation and the document it saves.
/// </summary>
public sealed class EditorPlaySettingsDialogModelTests {
    /// <summary>File path reported in messages.</summary>
    const string FilePath = @"C:\project\user_settings\play.json";

    /// <summary>Edit values start from the effective values: stored where present, declared default otherwise.</summary>
    [Fact]
    public void CreateEditValues_StartFromEffectiveValues() {
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.CommonValues["window-scale"] = "3x";

        EditorPlaySettingsDialogModel model = new EditorPlaySettingsDialogModel("ds", "Nintendo DS", CreateCapability(), document, FilePath);

        Assert.Equal("3x", model.CreateCommonEditValues()["window-scale"]);
        Assert.Equal("editor", model.CreateCommonEditValues()["window-monitor"]);
        Assert.Equal("stacked", model.CreatePlatformEditValues()["screen-layout"]);
        Assert.Empty(model.StoredIssues);
    }

    /// <summary>A stored value the declarations reject is flagged so the dialog can highlight its row.</summary>
    [Fact]
    public void StoredInvalidValue_IsFlagged() {
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.CommonValues["window-scale"] = "5x";

        EditorPlaySettingsDialogModel model = new EditorPlaySettingsDialogModel("ds", "Nintendo DS", CreateCapability(), document, FilePath);

        Assert.True(model.IsStoredValueInvalid(EditorPlaySettingScope.Common, "window-scale"));
        Assert.False(model.IsStoredValueInvalid(EditorPlaySettingScope.Common, "window-monitor"));
        Assert.Equal("5x", Assert.Single(model.StoredIssues).StoredValue);
    }

    /// <summary>Valid edits are saved for the common section and the selected platform only.</summary>
    [Fact]
    public void CreateSavedDocument_PreservesOtherPlatformSections() {
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.CommonValues["window-scale"] = "5x";
        document.PlatformValues["ps2"] = new Dictionary<string, string> { ["video-mode"] = "pal-320x256p" };
        EditorPlaySettingsDialogModel model = new EditorPlaySettingsDialogModel("ds", "Nintendo DS", CreateCapability(), document, FilePath);
        Dictionary<string, string> common = model.CreateCommonEditValues();
        common["window-scale"] = "2x";

        IReadOnlyList<EditorPlaySettingIssue> issues = model.ApplyEdits(common, model.CreatePlatformEditValues());
        EditorPlaySettingsDocument saved = model.CreateSavedDocument();

        Assert.Empty(issues);
        Assert.Equal("2x", saved.CommonValues["window-scale"]);
        Assert.Equal("stacked", saved.PlatformValues["ds"]["screen-layout"]);
        Assert.Equal("pal-320x256p", saved.PlatformValues["ps2"]["video-mode"]);
        Assert.Equal("5x", document.CommonValues["window-scale"]);
    }

    /// <summary>Invalid edits are reported and cannot be saved.</summary>
    [Fact]
    public void ApplyEdits_WhenInvalid_ReportsAndBlocksSave() {
        EditorPlaySettingsDialogModel model = new EditorPlaySettingsDialogModel("ds", "Nintendo DS", CreateCapability(), new EditorPlaySettingsDocument(), FilePath);
        Dictionary<string, string> common = model.CreateCommonEditValues();
        common["shadows"] = "True";

        IReadOnlyList<EditorPlaySettingIssue> issues = model.ApplyEdits(common, model.CreatePlatformEditValues());

        Assert.Equal("shadows", Assert.Single(issues).SettingId);
        Assert.Throws<InvalidOperationException>(() => model.CreateSavedDocument());
    }

    /// <summary>Edits naming an undeclared id are a programming error.</summary>
    [Fact]
    public void ApplyEdits_WhenIdUndeclared_Throws() {
        EditorPlaySettingsDialogModel model = new EditorPlaySettingsDialogModel("ds", "Nintendo DS", CreateCapability(), new EditorPlaySettingsDocument(), FilePath);
        Dictionary<string, string> platform = model.CreatePlatformEditValues();
        platform["video-mode"] = "x";

        Assert.Throws<ArgumentException>(() => model.ApplyEdits(model.CreateCommonEditValues(), platform));
    }

    /// <summary>Creates a DS-like capability with one choice Play setting.</summary>
    static PlatformPlayerCapability CreateCapability() {
        return new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "ds", "1.0.0",
            [new PlatformSettingDefinition("screen-layout", "Screen Layout", PlatformSettingKind.Choice, "stacked", true, ["stacked"])]);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlaySettingsDialogModelTests"`
Expected: build FAIL (`EditorPlaySettingsDialogModel` not found).

- [ ] **Step 3: Implement**

```csharp
using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// View-model of the Play Settings dialog for one platform. It holds the declarations, the effective values, the stored
    /// values the declarations reject (so the dialog highlights them), validates edits and produces the document to save:
    /// the common section plus this platform's section, with every other platform's section untouched.
    /// </summary>
    public sealed class EditorPlaySettingsDialogModel {
        /// <summary>Stored document the dialog was opened on; never modified.</summary>
        readonly EditorPlaySettingsDocument Document;

        /// <summary>Current common values (stored, default, or applied edits).</summary>
        Dictionary<string, string> CommonValues;

        /// <summary>Current platform values (stored, default, or applied edits).</summary>
        Dictionary<string, string> PlatformValues;

        /// <summary>Issues of the latest applied edits; empty when they were valid or none were applied.</summary>
        IReadOnlyList<EditorPlaySettingIssue> EditIssues;

        /// <summary>
        /// Opens the model for one platform.
        /// </summary>
        /// <param name="platformId">Platform whose Play settings are edited.</param>
        /// <param name="platformDisplayName">Platform name shown as the platform section title.</param>
        /// <param name="capability">Play capability of the platform.</param>
        /// <param name="document">Stored Play settings.</param>
        /// <param name="filePath">File the document came from.</param>
        /// <exception cref="InvalidOperationException">Thrown when the stored platform section names an undeclared setting.</exception>
        public EditorPlaySettingsDialogModel(string platformId, string platformDisplayName, PlatformPlayerCapability capability, EditorPlaySettingsDocument document, string filePath) {
            if (string.IsNullOrWhiteSpace(platformDisplayName)) {
                throw new ArgumentException("The platform display name is required.", nameof(platformDisplayName));
            }

            EditorPlaySettingsEvaluation evaluation = EditorPlaySettingsEvaluator.Evaluate(document, filePath, platformId, capability);
            Document = document;
            PlatformId = platformId;
            PlatformDisplayName = platformDisplayName;
            CommonDefinitions = PlayerCommonPlaySettings.CreateDefinitions();
            PlatformDefinitions = capability.PlaySettings;
            CommonValues = new Dictionary<string, string>(evaluation.CommonValues, StringComparer.Ordinal);
            PlatformValues = new Dictionary<string, string>(evaluation.PlatformValues, StringComparer.Ordinal);
            StoredIssues = evaluation.Issues;
            EditIssues = evaluation.Issues;
        }

        /// <summary>Gets the platform whose Play settings are edited.</summary>
        public string PlatformId { get; }

        /// <summary>Gets the platform name shown as the platform section title.</summary>
        public string PlatformDisplayName { get; }

        /// <summary>Gets the common Play setting declarations.</summary>
        public PlatformSettingDefinition[] CommonDefinitions { get; }

        /// <summary>Gets the platform's Play setting declarations (may be empty).</summary>
        public PlatformSettingDefinition[] PlatformDefinitions { get; }

        /// <summary>Gets the stored values the declarations rejected when the dialog opened.</summary>
        public IReadOnlyList<EditorPlaySettingIssue> StoredIssues { get; }

        /// <summary>
        /// Reports whether a stored value was rejected when the dialog opened, so its row is highlighted.
        /// </summary>
        /// <param name="scope">Section of the setting.</param>
        /// <param name="settingId">Setting id.</param>
        /// <returns>True when the stored value was rejected.</returns>
        public bool IsStoredValueInvalid(EditorPlaySettingScope scope, string settingId) {
            for (int index = 0; index < StoredIssues.Count; index++) {
                if (StoredIssues[index].Scope == scope && string.Equals(StoredIssues[index].SettingId, settingId, StringComparison.Ordinal)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Creates a private copy of the current common values for the dialog's controls to edit.</summary>
        /// <returns>Editable copy.</returns>
        public Dictionary<string, string> CreateCommonEditValues() {
            return new Dictionary<string, string>(CommonValues, StringComparer.Ordinal);
        }

        /// <summary>Creates a private copy of the current platform values for the dialog's controls to edit.</summary>
        /// <returns>Editable copy.</returns>
        public Dictionary<string, string> CreatePlatformEditValues() {
            return new Dictionary<string, string>(PlatformValues, StringComparer.Ordinal);
        }

        /// <summary>
        /// Applies the dialog's edited values and validates them against the declarations.
        /// </summary>
        /// <param name="commonEdits">Edited common values.</param>
        /// <param name="platformEdits">Edited platform values.</param>
        /// <returns>Rejected values; empty when every value is accepted.</returns>
        /// <exception cref="ArgumentException">Thrown when an edit names an undeclared setting.</exception>
        public IReadOnlyList<EditorPlaySettingIssue> ApplyEdits(IReadOnlyDictionary<string, string> commonEdits, IReadOnlyDictionary<string, string> platformEdits) {
            if (commonEdits == null) {
                throw new ArgumentNullException(nameof(commonEdits));
            } else if (platformEdits == null) {
                throw new ArgumentNullException(nameof(platformEdits));
            }

            List<EditorPlaySettingIssue> issues = new List<EditorPlaySettingIssue>();
            Dictionary<string, string> common = ValidateSection(CommonDefinitions, commonEdits, EditorPlaySettingScope.Common, issues);
            Dictionary<string, string> platform = ValidateSection(PlatformDefinitions, platformEdits, EditorPlaySettingScope.Platform, issues);
            CommonValues = common;
            PlatformValues = platform;
            EditIssues = issues;
            return issues;
        }

        /// <summary>
        /// Creates the document to save: a copy of the stored document with the common section and this platform's
        /// section replaced by the current values.
        /// </summary>
        /// <returns>Document to save.</returns>
        /// <exception cref="InvalidOperationException">Thrown while any current value is rejected.</exception>
        public EditorPlaySettingsDocument CreateSavedDocument() {
            if (EditIssues.Count > 0) {
                throw new InvalidOperationException("Play settings cannot be saved while a value is invalid: " + EditIssues[0].Describe() + ".");
            }

            EditorPlaySettingsDocument saved = Document.Clone();
            saved.CommonValues.Clear();
            foreach (KeyValuePair<string, string> entry in CommonValues) {
                saved.CommonValues.Add(entry.Key, entry.Value);
            }
            saved.PlatformValues[PlatformId] = new Dictionary<string, string>(PlatformValues, StringComparer.Ordinal);
            return saved;
        }

        /// <summary>
        /// Validates one edited section: every edit must name a declared setting, and every declared setting must have an
        /// accepted value.
        /// </summary>
        /// <param name="definitions">Declarations of the section.</param>
        /// <param name="edits">Edited values.</param>
        /// <param name="scope">Section scope.</param>
        /// <param name="issues">Receives rejected values.</param>
        /// <returns>The section values.</returns>
        /// <exception cref="ArgumentException">Thrown when an edit names an undeclared setting.</exception>
        static Dictionary<string, string> ValidateSection(
            PlatformSettingDefinition[] definitions,
            IReadOnlyDictionary<string, string> edits,
            EditorPlaySettingScope scope,
            List<EditorPlaySettingIssue> issues) {
            foreach (string settingId in edits.Keys) {
                if (!Array.Exists(definitions, definition => string.Equals(definition.SettingId, settingId, StringComparison.Ordinal))) {
                    throw new ArgumentException($"Play setting '{settingId}' is not declared in the {scope} section.", nameof(edits));
                }
            }

            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < definitions.Length; index++) {
                PlatformSettingDefinition definition = definitions[index];
                edits.TryGetValue(definition.SettingId, out string value);
                values.Add(definition.SettingId, value ?? string.Empty);
                if (!EditorPlaySettingValueValidator.TryValidate(definition, value, out string reason)) {
                    issues.Add(new EditorPlaySettingIssue(scope, definition.SettingId, value ?? string.Empty, reason));
                }
            }

            return values;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlaySettingsDialogModelTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/play/EditorPlaySettingsDialogModel.cs engine/helengine.editor.tests/managers/play/EditorPlaySettingsDialogModelTests.cs
git commit -m "feat(editor): add the Play Settings dialog view-model

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task A12: `Play Settings...` in the Play platform selector

**Files:**
- Create: `engine/helengine.editor/components/ui/EditorPlayPlatformMenu.cs`
- Modify: `engine/helengine.editor/components/ui/EditorViewport.cs` (fields near 298-310, events near 441-452, `UpdatePlayPlatformSelectorWidth` 1234, `SetPlayPlatformOptions` 1255, `HandlePlayPlatformComboSelectionChanged` 1290)
- Test: `engine/helengine.editor.tests/EditorPlayPlatformMenuTests.cs` (new), `engine/helengine.editor.tests/EditorViewportKeyboardFocusTests.cs` (one new test)

**Interfaces:**
- Produces:
  - `EditorPlayPlatformMenu.PlaySettingsItemText = "Play Settings..."`
  - `EditorPlayPlatformMenu.BuildItems(IReadOnlyList<string> displayNames): string[]`
  - `EditorPlayPlatformMenu.IsPlaySettingsItem(int index, int platformCount): bool`
  - `EditorViewport.PlaySettingsRequested: event Action`

- [ ] **Step 1: Write the failing tests**

`engine/helengine.editor.tests/EditorPlayPlatformMenuTests.cs`:

```csharp
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the items of the viewport Play platform selector.
    /// </summary>
    public sealed class EditorPlayPlatformMenuTests {
        /// <summary>The settings item follows the platforms.</summary>
        [Fact]
        public void BuildItems_AppendsPlaySettingsAfterPlatforms() {
            Assert.Equal(new[] { "PlayStation 2", "Windows", "Play Settings..." }, EditorPlayPlatformMenu.BuildItems(new[] { "PlayStation 2", "Windows" }));
        }

        /// <summary>With no platform there is nothing to configure, so no settings item.</summary>
        [Fact]
        public void BuildItems_WithoutPlatforms_IsEmpty() {
            Assert.Empty(EditorPlayPlatformMenu.BuildItems(Array.Empty<string>()));
        }

        /// <summary>Only the index after the platforms is the settings item.</summary>
        [Theory]
        [InlineData(0, 2, false)]
        [InlineData(1, 2, false)]
        [InlineData(2, 2, true)]
        public void IsPlaySettingsItem_MatchesIndexAfterPlatforms(int index, int platformCount, bool expected) {
            Assert.Equal(expected, EditorPlayPlatformMenu.IsPlaySettingsItem(index, platformCount));
        }
    }
}
```

Append to `EditorViewportKeyboardFocusTests.cs` (it already has `InitializeCore`, `CreateViewport` and `GetPrivateField`):

```csharp
        /// <summary>
        /// Picking Play Settings... raises the settings request, keeps the selected Play platform and is not a platform pick.
        /// </summary>
        [Fact]
        public void EditorViewport_WhenPlaySettingsItemPicked_RaisesRequestAndKeepsPlatform() {
            InitializeCore();
            EditorViewport viewport = CreateViewport();
            viewport.SetPlayPlatformOptions(new[] { "ps2", "windows" }, new[] { "PlayStation 2", "Windows" }, "ps2");
            ComboBoxComponent combo = GetPrivateField<ComboBoxComponent>(viewport, "PlayPlatformComboBox");
            int settingsRequests = 0;
            List<string> platformPicks = new List<string>();
            viewport.PlaySettingsRequested += () => settingsRequests++;
            viewport.PlayPlatformSelected += platformPicks.Add;

            combo.SelectedIndex = 2;

            Assert.Equal(new[] { "PlayStation 2", "Windows", "Play Settings..." }, combo.Items);
            Assert.Equal(1, settingsRequests);
            Assert.Empty(platformPicks);
            Assert.Equal(0, combo.SelectedIndex);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlayPlatformMenuTests|FullyQualifiedName~EditorViewport_WhenPlaySettingsItemPicked"`
Expected: build FAIL (`EditorPlayPlatformMenu` not found).

- [ ] **Step 3: Implement the menu helper**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Builds the items of the viewport Play platform selector: one item per playable platform followed by
    /// <see cref="PlaySettingsItemText"/>, which opens the Play Settings dialog instead of selecting a platform.
    /// </summary>
    public static class EditorPlayPlatformMenu {
        /// <summary>Text of the item that opens the Play Settings dialog (ASCII only; the shared font charset is fixed).</summary>
        public const string PlaySettingsItemText = "Play Settings...";

        /// <summary>
        /// Builds the selector items.
        /// </summary>
        /// <param name="displayNames">Display names of the playable platforms, in order.</param>
        /// <returns>The platform names followed by the settings item, or no items when no platform is playable.</returns>
        public static string[] BuildItems(IReadOnlyList<string> displayNames) {
            if (displayNames == null) {
                throw new ArgumentNullException(nameof(displayNames));
            } else if (displayNames.Count == 0) {
                return [];
            }

            string[] items = new string[displayNames.Count + 1];
            for (int index = 0; index < displayNames.Count; index++) {
                items[index] = displayNames[index];
            }
            items[displayNames.Count] = PlaySettingsItemText;
            return items;
        }

        /// <summary>
        /// Reports whether a selector index is the settings item.
        /// </summary>
        /// <param name="index">Selected index.</param>
        /// <param name="platformCount">Number of platform items before it.</param>
        /// <returns>True for the settings item.</returns>
        public static bool IsPlaySettingsItem(int index, int platformCount) {
            return platformCount > 0 && index == platformCount;
        }
    }
}
```

- [ ] **Step 4: Wire the viewport**

In `EditorViewport.cs`:
1. Add the fields `readonly List<string> PresentedPlayPlatformNames = new List<string>();` ("Display names of the presented platforms; the selector is sized to these, not to the settings item.") and `int PresentedPlayPlatformIndex = -1;` ("Selector index of the presented platform, restored after the settings item is picked.").
2. Add the event:

```csharp
        /// <summary>
        /// Raised when the user picks <see cref="EditorPlayPlatformMenu.PlaySettingsItemText"/> in the Play platform
        /// selector; the session opens the Play Settings dialog for the selected Play platform.
        /// </summary>
        public event Action PlaySettingsRequested;
```

3. In `SetPlayPlatformOptions`, replace `PlayPlatformComboBox.SetItems(displayNames, selectedIndex);` with:

```csharp
            PresentedPlayPlatformNames.Clear();
            PresentedPlayPlatformNames.AddRange(displayNames);
            PresentedPlayPlatformIndex = selectedIndex;
            PlayPlatformComboBox.SetItems(EditorPlayPlatformMenu.BuildItems(displayNames), selectedIndex);
```

4. In `UpdatePlayPlatformSelectorWidth`, measure `PresentedPlayPlatformNames` instead of `PlayPlatformComboBox.Items`.
5. Replace the start of `HandlePlayPlatformComboSelectionChanged`:

```csharp
        void HandlePlayPlatformComboSelectionChanged(int selectedIndex, string selectedDisplayName) {
            if (EditorPlayPlatformMenu.IsPlaySettingsItem(selectedIndex, PresentedPlayPlatformIds.Count)) {
                // Restoring the platform raises this handler again for the presented platform, which it ignores.
                PlayPlatformComboBox.SelectedIndex = PresentedPlayPlatformIndex;
                PlaySettingsRequested?.Invoke();
                return;
            }

            string platformId = PresentedPlayPlatformIds[selectedIndex];
            if (string.Equals(platformId, PresentedPlayPlatformId, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            PresentedPlayPlatformId = platformId;
            PresentedPlayPlatformIndex = selectedIndex;
            PlayPlatformSelected?.Invoke(platformId);
        }
```

Update the XML doc of `HandlePlayPlatformComboSelectionChanged` to mention the settings item.

- [ ] **Step 5: Run tests to verify they pass**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlayPlatformMenuTests|FullyQualifiedName~EditorViewportKeyboardFocusTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.editor/components/ui/EditorPlayPlatformMenu.cs engine/helengine.editor/components/ui/EditorViewport.cs engine/helengine.editor.tests/EditorPlayPlatformMenuTests.cs engine/helengine.editor.tests/EditorViewportKeyboardFocusTests.cs
git diff --cached --numstat
git commit -m "feat(editor): offer Play Settings... in the Play platform selector

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task A13: Play Settings dialog and press-time capture in the session

**Files:**
- Create: `engine/helengine.editor/managers/play/EditorPlaySettingsCapture.cs`
- Create: `engine/helengine.editor/components/ui/PlaySettingsDialog.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`. Fields are near 474-493. `ProjectLocalSettingsService` is constructed at 741. Dialog creation is at 991 and 2035. Cleanup registration is at 1838. Handler attach is at 1914. `UpdateLayout` is at 1675. The open-dialog check is at 3284. Viewport wiring is at 2548 and 2586. `HandlePlayRequested` is at 3627.
- Test: `engine/helengine.editor.tests/managers/play/EditorPlaySettingsCaptureTests.cs` (new)

**Interfaces:**
- Consumes: `EditorPlaySettingsStore` (A5), `EditorPlaySettingsEvaluator`, `EditorPlaySettingsSnapshot` (A6), `EditorPlaySettingsDialogModel` (A11), `EditorViewport.PlaySettingsRequested` (A12), `EditorPlatformSettingsSection` (existing)
- Produces:
  - `EditorPlaySettingsCapture.TryCapture(EditorPlaySettingsStore store, string platformId, PlatformPlayerCapability capability, bool waitForDebuggerRequested, int editorProcessId, out EditorPlaySettingsSnapshot snapshot, out string failureMessage): bool`
  - `PlaySettingsDialog(Core, EditorSessionInteractionServices, FontAsset, EditorUiMetrics)` with `Show(EditorPlaySettingsDialogModel)`, `Hide()`, `UpdateLayout(int,int)`, `event Action<EditorPlaySettingsDialogModel> SaveRequested`, `event Action CancelRequested`

- [ ] **Step 1: Write the failing tests**

```csharp
using helengine.baseplatform.Definitions;
using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Verifies the press-time capture reads the store, refuses invalid settings with a message naming the file, platform
/// and id, and otherwise returns a snapshot.
/// </summary>
public sealed class EditorPlaySettingsCaptureTests : IDisposable {
    /// <summary>Workspace-owned scratch project root.</summary>
    readonly string ProjectRoot = Path.Combine(@"C:\dev\helworks\builds\helengine\tests\play-settings-capture", Guid.NewGuid().ToString("N"));

    /// <summary>Creates the scratch project root.</summary>
    public EditorPlaySettingsCaptureTests() {
        Directory.CreateDirectory(ProjectRoot);
    }

    /// <summary>Deletes the scratch project root.</summary>
    public void Dispose() {
        Directory.Delete(ProjectRoot, true);
    }

    /// <summary>Without a file, the capture succeeds with the defaults.</summary>
    [Fact]
    public void TryCapture_WithoutFile_ReturnsDefaults() {
        EditorPlaySettingsStore store = new EditorPlaySettingsStore(ProjectRoot);

        bool captured = EditorPlaySettingsCapture.TryCapture(store, "ps2", CreateCapability(), false, 99, out EditorPlaySettingsSnapshot snapshot, out string failure);

        Assert.True(captured);
        Assert.Equal(string.Empty, failure);
        Assert.Equal("fit", snapshot.CommonValues["window-scale"]);
    }

    /// <summary>An invalid stored value refuses Play with a message naming file, platform and id.</summary>
    [Fact]
    public void TryCapture_WithInvalidValue_RefusesNamingFilePlatformAndId() {
        EditorPlaySettingsStore store = new EditorPlaySettingsStore(ProjectRoot);
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.CommonValues["frame-rate"] = "120";
        store.Save(document);

        bool captured = EditorPlaySettingsCapture.TryCapture(store, "ps2", CreateCapability(), false, 99, out EditorPlaySettingsSnapshot snapshot, out string failure);

        Assert.False(captured);
        Assert.Null(snapshot);
        Assert.Contains(store.FilePath, failure);
        Assert.Contains("'ps2'", failure);
        Assert.Contains("frame-rate", failure);
    }

    /// <summary>Creates a PS2-like capability without platform Play settings.</summary>
    static PlatformPlayerCapability CreateCapability() {
        return new PlatformPlayerCapability(true, "a.B", "ps2-staging", 60d, PlayerInputDevices.Gamepad, "ps2", "1.0.0", []);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlaySettingsCaptureTests"`
Expected: build FAIL (`EditorPlaySettingsCapture` not found).

- [ ] **Step 3: Implement the capture**

```csharp
using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Captures the Play settings on the editor frame thread when Play is pressed: loads <c>user_settings/play.json</c>,
    /// evaluates it for the selected platform and freezes it into a snapshot. Invalid values refuse Play with one message;
    /// a broken file throws (it is not a normal path).
    /// </summary>
    public static class EditorPlaySettingsCapture {
        /// <summary>
        /// Tries to capture the Play settings for one Play.
        /// </summary>
        /// <param name="store">Per-user Play settings store.</param>
        /// <param name="platformId">Platform being played.</param>
        /// <param name="capability">Play capability of the platform.</param>
        /// <param name="waitForDebuggerRequested">Whether Shift was held on the Play button.</param>
        /// <param name="editorProcessId">Process id of this editor.</param>
        /// <param name="snapshot">The snapshot when captured; null otherwise.</param>
        /// <param name="failureMessage">Why Play is refused; empty when captured.</param>
        /// <returns>True when the settings are valid and captured.</returns>
        public static bool TryCapture(
            EditorPlaySettingsStore store,
            string platformId,
            PlatformPlayerCapability capability,
            bool waitForDebuggerRequested,
            int editorProcessId,
            out EditorPlaySettingsSnapshot snapshot,
            out string failureMessage) {
            if (store == null) {
                throw new ArgumentNullException(nameof(store));
            }

            EditorPlaySettingsEvaluation evaluation = EditorPlaySettingsEvaluator.Evaluate(store.Load(), store.FilePath, platformId, capability);
            if (!evaluation.IsValid) {
                snapshot = null;
                failureMessage = evaluation.FormatFailure();
                return false;
            }

            snapshot = new EditorPlaySettingsSnapshot(evaluation, waitForDebuggerRequested, capability, editorProcessId);
            failureMessage = string.Empty;
            return true;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlaySettingsCaptureTests"`
Expected: PASS.

- [ ] **Step 5: Implement the dialog**

`engine/helengine.editor/components/ui/PlaySettingsDialog.cs`:

```csharp
namespace helengine.editor {
    /// <summary>
    /// Modal dialog that edits the Play settings of the selected Play platform: a Common section and a section for the
    /// platform's own Play settings, rendered with the same rows and controls as the profiles dialog. Stored values the
    /// declarations reject are highlighted and described until the user picks a valid value and saves. Presentation only:
    /// validation and the saved document come from <see cref="EditorPlaySettingsDialogModel"/>.
    /// </summary>
    public class PlaySettingsDialog : EditorDialogBase {
        /// <summary>Unscaled panel width.</summary>
        public const int PanelWidth = 540;

        /// <summary>Unscaled minimum panel height.</summary>
        public const int PanelHeight = 440;

        /// <summary>Unscaled padding inside the panel.</summary>
        public const int PanelPadding = 16;

        /// <summary>Unscaled height of the draggable title bar.</summary>
        public const int HeaderHeight = 32;

        /// <summary>Unscaled height reserved for the footer buttons.</summary>
        public const int FooterHeight = 28;

        /// <summary>Unscaled spacing between the sections, the status line and the footer.</summary>
        public const int SectionSpacing = 12;

        /// <summary>Unscaled height of the status text area.</summary>
        public const int StatusHeight = 36;

        /// <summary>Unscaled label column width of the setting rows.</summary>
        const int LabelColumnWidth = 200;

        /// <summary>Unscaled value column width of the setting rows.</summary>
        const int ValueColumnWidth = 280;

        /// <summary>Unscaled size of the Save and Cancel buttons.</summary>
        static readonly int2 ButtonSize = new int2(88, 22);

        /// <summary>Host of the common section.</summary>
        readonly EditorEntity CommonContentHost;

        /// <summary>Rows of the common Play settings.</summary>
        readonly EditorPlatformSettingsSection CommonSection;

        /// <summary>Host of the platform section.</summary>
        readonly EditorEntity PlatformContentHost;

        /// <summary>Rows of the platform's own Play settings.</summary>
        readonly EditorPlatformSettingsSection PlatformSection;

        /// <summary>Host of the status text.</summary>
        readonly EditorEntity StatusHost;

        /// <summary>Status text describing rejected stored or edited values.</summary>
        readonly TextComponent StatusText;

        /// <summary>Host of the Save button.</summary>
        readonly EditorEntity SaveButtonHost;

        /// <summary>Button that validates and saves the settings.</summary>
        readonly ButtonComponent SaveButton;

        /// <summary>Host of the Cancel button.</summary>
        readonly EditorEntity CancelButtonHost;

        /// <summary>Button that closes the dialog without saving.</summary>
        readonly ButtonComponent CancelButton;

        /// <summary>View-model of the open dialog; null while hidden.</summary>
        EditorPlaySettingsDialogModel Model;

        /// <summary>Values the common rows edit; a private copy the model never sees until Save.</summary>
        Dictionary<string, string> CommonEditValues;

        /// <summary>Values the platform rows edit; a private copy the model never sees until Save.</summary>
        Dictionary<string, string> PlatformEditValues;

        /// <summary>Whether construction finished, so layout requests are honoured.</summary>
        bool IsInitialized;

        /// <summary>Raised with the model when the user saves valid settings.</summary>
        public event Action<EditorPlaySettingsDialogModel> SaveRequested;

        /// <summary>Raised when the user cancels or closes the dialog.</summary>
        public event Action CancelRequested;

        /// <summary>
        /// Creates the hidden dialog.
        /// </summary>
        /// <param name="ownerCore">Core that owns the dialog entities.</param>
        /// <param name="interactionServices">Editor interaction services.</param>
        /// <param name="font">Dialog font.</param>
        /// <param name="metrics">Scaled editor UI metrics.</param>
        public PlaySettingsDialog(Core ownerCore, EditorSessionInteractionServices interactionServices, FontAsset font, EditorUiMetrics metrics)
            : base(ownerCore, interactionServices, "PlaySettingsDialog", "Play Settings", font, metrics, PanelWidth, PanelHeight, HeaderHeight) {
            SetDialogMinimumSize(PanelWidth, PanelHeight);

            CommonContentHost = CreateDialogHost();
            DialogPanelRoot.AddChild(CommonContentHost);
            CommonSection = new EditorPlatformSettingsSection(CommonContentHost, LayerMask, DialogFont, DialogMetrics.ScalePixels(LabelColumnWidth), DialogMetrics.ScalePixels(ValueColumnWidth));

            PlatformContentHost = CreateDialogHost();
            DialogPanelRoot.AddChild(PlatformContentHost);
            PlatformSection = new EditorPlatformSettingsSection(PlatformContentHost, LayerMask, DialogFont, DialogMetrics.ScalePixels(LabelColumnWidth), DialogMetrics.ScalePixels(ValueColumnWidth));

            StatusHost = CreateDialogHost();
            DialogPanelRoot.AddChild(StatusHost);
            StatusText = new TextComponent {
                Font = DialogFont,
                Text = string.Empty,
                Color = ThemeManager.Colors.StateWarning,
                Size = new int2(1, DialogMetrics.ScalePixels(StatusHeight))
            };
            StatusHost.AddComponent(StatusText);

            SaveButtonHost = CreateDialogHost();
            DialogPanelRoot.AddChild(SaveButtonHost);
            SaveButton = new ButtonComponent("Save", GetButtonSize(), DialogFont, HandleSaveClicked, 0f);
            SaveButtonHost.AddComponent(SaveButton);

            CancelButtonHost = CreateDialogHost();
            DialogPanelRoot.AddChild(CancelButtonHost);
            CancelButton = new ButtonComponent("Cancel", GetButtonSize(), DialogFont, HandleCancelClicked, 0f);
            CancelButtonHost.AddComponent(CancelButton);

            Enabled = false;
            IsInitialized = true;
        }

        /// <summary>
        /// Shows the dialog for one platform's Play settings, highlighting stored values the declarations reject.
        /// </summary>
        /// <param name="model">View-model of the platform's Play settings.</param>
        public void Show(EditorPlaySettingsDialogModel model) {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            CommonEditValues = model.CreateCommonEditValues();
            PlatformEditValues = model.CreatePlatformEditValues();
            CommonSection.Rebuild("Common", model.CommonDefinitions, CommonEditValues);
            PlatformSection.Rebuild(
                model.PlatformDefinitions.Length == 0 ? model.PlatformDisplayName + " (no platform Play settings)" : model.PlatformDisplayName,
                model.PlatformDefinitions,
                PlatformEditValues);
            HighlightStoredIssues(CommonSection, EditorPlaySettingScope.Common);
            HighlightStoredIssues(PlatformSection, EditorPlaySettingScope.Platform);
            StatusText.Text = DescribeIssues("Stored values are no longer allowed", model.StoredIssues);
            SetDialogSize(DialogWidth, ComputePanelHeightPixels());
            ResetDialogPositioning();
            Enabled = true;
            ShowDialogImmediately();
        }

        /// <summary>
        /// Hides the dialog and forgets the model.
        /// </summary>
        public void Hide() {
            Model = null;
            ResetDialogPositioning();
            Enabled = false;
        }

        /// <summary>
        /// Updates sizing and layout for the current editor window size.
        /// </summary>
        /// <param name="windowWidth">Editor window width.</param>
        /// <param name="windowHeight">Editor window height.</param>
        public void UpdateLayout(int windowWidth, int windowHeight) {
            if (!IsInitialized) {
                return;
            }

            UpdateDialogFrame(windowWidth, windowHeight);
        }

        /// <summary>Treats the close button as Cancel.</summary>
        protected override void OnCloseRequested() {
            HandleCancelClicked();
        }

        /// <summary>Repositions the content when the shell moves or resizes.</summary>
        protected override void HandleDialogLayoutChanged() {
            LayoutContent();
        }

        /// <summary>
        /// Positions the two sections, the status text and the footer buttons.
        /// </summary>
        void LayoutContent() {
            int padding = DialogMetrics.ScalePixels(PanelPadding);
            int spacing = DialogMetrics.ScalePixels(SectionSpacing);
            float commonTop = DialogMetrics.ScalePixels(HeaderHeight) + padding;
            CommonContentHost.Position = new float3(padding, commonTop, 0.1f);
            CommonSection.Root.Position = float3.Zero;
            CommonSection.Layout();
            float platformTop = commonTop + CommonSection.ContentHeight + spacing;
            PlatformContentHost.Position = new float3(padding, platformTop, 0.1f);
            PlatformSection.Root.Position = float3.Zero;
            PlatformSection.Layout();
            float statusTop = platformTop + PlatformSection.ContentHeight + spacing;
            StatusHost.Position = new float3(padding, statusTop, 0f);
            StatusText.Size = new int2(Math.Max(1, AnchorBounds.X - padding * 2), DialogMetrics.ScalePixels(StatusHeight));
            int footerY = AnchorBounds.Y - padding - DialogMetrics.ScalePixels(FooterHeight);
            int cancelX = AnchorBounds.X - padding - GetButtonSize().X;
            int saveX = cancelX - DialogMetrics.ScalePixels(8) - GetButtonSize().X;
            SaveButtonHost.Position = new float3(saveX, footerY, 0f);
            CancelButtonHost.Position = new float3(cancelX, footerY, 0f);
        }

        /// <summary>
        /// Computes the panel height that fits both sections, the status text and the footer.
        /// </summary>
        /// <returns>Panel height in pixels.</returns>
        int ComputePanelHeightPixels() {
            float height = DialogMetrics.ScalePixels(HeaderHeight)
                + DialogMetrics.ScalePixels(PanelPadding) * 2
                + CommonSection.ContentHeight
                + PlatformSection.ContentHeight
                + DialogMetrics.ScalePixels(SectionSpacing) * 3
                + DialogMetrics.ScalePixels(StatusHeight)
                + DialogMetrics.ScalePixels(FooterHeight);
            return (int)Math.Ceiling(height);
        }

        /// <summary>
        /// Colours the label of every row whose stored value the declarations rejected.
        /// </summary>
        /// <param name="section">Section whose rows are checked.</param>
        /// <param name="scope">Scope of the section.</param>
        void HighlightStoredIssues(EditorPlatformSettingsSection section, EditorPlaySettingScope scope) {
            for (int index = 0; index < section.Items.Count; index++) {
                EditorPlatformSettingBinding row = section.Items[index];
                if (Model.IsStoredValueInvalid(scope, row.Setting.SettingId)) {
                    row.LabelText.Color = ThemeManager.Colors.StateWarning;
                }
            }
        }

        /// <summary>
        /// Formats issues for the status text.
        /// </summary>
        /// <param name="prefix">Leading sentence.</param>
        /// <param name="issues">Issues to describe.</param>
        /// <returns>Status text, empty when there are no issues.</returns>
        static string DescribeIssues(string prefix, IReadOnlyList<EditorPlaySettingIssue> issues) {
            if (issues.Count == 0) {
                return string.Empty;
            }

            List<string> descriptions = new List<string>();
            for (int index = 0; index < issues.Count; index++) {
                descriptions.Add(issues[index].Describe());
            }

            return prefix + ": " + string.Join("; ", descriptions) + ". Pick a value and Save.";
        }

        /// <summary>
        /// Validates the rows, applies the edits to the model and raises <see cref="SaveRequested"/> when every value is accepted.
        /// </summary>
        void HandleSaveClicked() {
            if (Model == null) {
                return;
            }
            // TryValidate also syncs each control into its values dictionary (and normalises booleans to true/false).
            if (!CommonSection.TryValidate(out string commonError)) {
                StatusText.Text = commonError;
                return;
            } else if (!PlatformSection.TryValidate(out string platformError)) {
                StatusText.Text = platformError;
                return;
            }

            IReadOnlyList<EditorPlaySettingIssue> issues = Model.ApplyEdits(CommonEditValues, PlatformEditValues);
            if (issues.Count > 0) {
                StatusText.Text = DescribeIssues("Invalid values", issues);
                return;
            }

            SaveRequested?.Invoke(Model);
        }

        /// <summary>Raises <see cref="CancelRequested"/>.</summary>
        void HandleCancelClicked() {
            CancelRequested?.Invoke();
        }

        /// <summary>
        /// Creates one dialog content host.
        /// </summary>
        /// <returns>Host entity.</returns>
        EditorEntity CreateDialogHost() {
            return new EditorEntity(OwnerCore, InteractionServices) {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
        }

        /// <summary>
        /// Gets the scaled footer button size.
        /// </summary>
        /// <returns>Button size in pixels.</returns>
        int2 GetButtonSize() {
            return new int2(DialogMetrics.ScalePixels(ButtonSize.X), DialogMetrics.ScalePixels(ButtonSize.Y));
        }
    }
}
```

- [ ] **Step 6: Wire the session**

In `EditorSession.cs`, in each place listed below, add the Play Settings counterpart next to the matching `sceneSettingsDialog` line:
1. Fields: `PlaySettingsDialog playSettingsDialog;` (XML: "Dialog that edits the selected Play platform's Play settings.") and `EditorPlaySettingsStore PlaySettingsStore;` (XML: "Per-user Play settings in user_settings/play.json.").
2. After `ProjectLocalSettingsService = new EditorProjectLocalSettingsService(this.projectPath, ProjectSupportedPlatforms);` (line 741): `PlaySettingsStore = new EditorPlaySettingsStore(this.projectPath);`.
3. Both dialog-construction blocks (991 and 2035): `playSettingsDialog = new PlaySettingsDialog(core, interactionServices, uiFont, CurrentUiMetrics);` plus `RegisterScaleSensitiveDialogCleanup(<ledger>, playSettingsDialog.Dispose, hide: playSettingsDialog.Hide);`, using the same ledger variable as the neighbouring lines.
4. The cleanup re-registration block (1838): `if (playSettingsDialog != null) { RegisterScaleSensitiveDialogCleanup(ledger, playSettingsDialog.Dispose, hide: playSettingsDialog.Hide); }`.
5. `AttachScaleSensitiveDialogHandlers` (1914):

```csharp
            if (playSettingsDialog != null) {
                RegisterDetacher(ConstructionLedger, () => playSettingsDialog.SaveRequested -= HandlePlaySettingsDialogSaveRequested);
                playSettingsDialog.SaveRequested += HandlePlaySettingsDialogSaveRequested;
                RegisterDetacher(ConstructionLedger, () => playSettingsDialog.CancelRequested -= HandlePlaySettingsDialogCancelRequested);
                playSettingsDialog.CancelRequested += HandlePlaySettingsDialogCancelRequested;
            }
```

6. Layout (1675): `playSettingsDialog?.UpdateLayout(width, height);`.
7. Modal check (3284): `if (playSettingsDialog != null && playSettingsDialog.Enabled) { return true; }`.
8. Viewport wiring (2548): `RegisterDetacher(ConstructionLedger, () => viewport.PlaySettingsRequested -= HandlePlaySettingsRequested); viewport.PlaySettingsRequested += HandlePlaySettingsRequested;`. Unwire (2586): `viewport.PlaySettingsRequested -= HandlePlaySettingsRequested;`.
9. New handlers (place after `HandlePlayPlatformSelected`):

```csharp
        /// <summary>
        /// Opens the Play Settings dialog for the platform selected in the Play platform selector. A Play settings file
        /// that is broken (unknown version or id) cannot be edited here and is reported instead.
        /// </summary>
        void HandlePlaySettingsRequested() {
            if (!PlayPlatformSelection.HasSelection) {
                Logger.WriteError("No installed project platform supports Play.");
                return;
            }

            EditorPlayPlatformOption option = PlayPlatformSelection.SelectedOption;
            EditorPlaySettingsDialogModel model;
            try {
                model = new EditorPlaySettingsDialogModel(option.PlatformId, option.DisplayName, option.PlayerCapability, PlaySettingsStore.Load(), PlaySettingsStore.FilePath);
            } catch (InvalidOperationException exception) {
                Logger.WriteError("Play Settings cannot open: " + exception.Message);
                return;
            }

            playSettingsDialog.Show(model);
        }

        /// <summary>
        /// Saves the dialog's Play settings to user_settings/play.json and closes the dialog. The next Play uses them; a
        /// Play already preparing keeps the settings captured when it was pressed.
        /// </summary>
        /// <param name="model">Model holding the validated values.</param>
        void HandlePlaySettingsDialogSaveRequested(EditorPlaySettingsDialogModel model) {
            PlaySettingsStore.Save(model.CreateSavedDocument());
            playSettingsDialog.Hide();
            Logger.WriteLine("[play] Play settings saved to " + PlaySettingsStore.FilePath);
        }

        /// <summary>Closes the Play Settings dialog without saving.</summary>
        void HandlePlaySettingsDialogCancelRequested() {
            playSettingsDialog.Hide();
        }
```

10. In `HandlePlayRequested`, replace the two temporary lines from Task A8 (`EditorPlaySettingsEvaluation defaults = ...` and `EditorPlaySettingsSnapshot playSettings = ...`) with:

```csharp
                if (!EditorPlaySettingsCapture.TryCapture(PlaySettingsStore, playPlatform.PlatformId, playPlatform.PlayerCapability, waitForDebugger, Environment.ProcessId, out EditorPlaySettingsSnapshot playSettings, out string playSettingsFailure)) {
                    Logger.WriteError(playSettingsFailure);
                    return;
                }
```

Update the method's XML summary: Play uses the Play settings captured at press time, and Shift+click forces the debugger wait.

- [ ] **Step 7: Build and run the editor play tests**

Run: `.\dotnetw.cmd build engine\helengine.editor\helengine.editor.csproj` then `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~managers.play|FullyQualifiedName~EditorViewport|FullyQualifiedName~EditorSessionSceneSaveTests"`
Expected: build success. Tests PASS except names on the 2026-09-19 baseline.

- [ ] **Step 8: Manual dialog check (announce first; no screenshots unless Helena allows them)**

Ask Helena to open demodisc and pick `Play Settings...` in the selector next to Play. Check that:
- the dialog lists the five common settings and a "PlayStation 2 (no platform Play settings)" section;
- Save writes `C:\dev\helprojs\demodisc\user_settings\play.json`;
- hand-editing `"window-scale": "5x"` makes the next Play fail with the file, platform and id;
- reopening the dialog shows Window Scale highlighted.

- [ ] **Step 9: Commit**

```bash
git add engine/helengine.editor/managers/play/EditorPlaySettingsCapture.cs engine/helengine.editor/components/ui/PlaySettingsDialog.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/managers/play/EditorPlaySettingsCaptureTests.cs
git diff --cached --numstat
git commit -m "feat(editor): edit Play settings from the selector and capture them when Play is pressed

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task A14: Keep the in-flight Windows Play rollout compiling (coordination-gated)

**Files:**
- Modify (only with Helena's go-ahead): `C:\dev\helworks\helengine-windows\builder\WindowsPlatformDefinitionFactory.cs:359-366`

**Interfaces:**
- Consumes: the `PlatformPlayerCapability` constructor with `playSettings` (A1) and `IPlatformPlayerAdapter.ApplyPlaySettings` (A4)

Context: `helengine-windows` has uncommitted Play work from another session. `WindowsPlatformDefinitionFactory.cs` is modified and references `WindowsPlayerAdapter`, which does not exist in the tree yet. Tasks A1 and A4 change the contract that work compiles against.

- [ ] **Step 1: Check the Windows tree state**

Run: `git -C C:\dev\helworks\helengine-windows status --short` and Grep for `class WindowsPlayerAdapter` under `C:\dev\helworks\helengine-windows`.

- [ ] **Step 2: Stop and ask Helena**

Report the state to Helena and ask whether the Windows session or this one adapts it. Never edit or stage another session's uncommitted hunks without her explicit answer.

- [ ] **Step 3: If Helena says this session adapts it**

Add `[]` as the last argument of the `new PlatformPlayerCapability(` call in `WindowsPlatformDefinitionFactory.cs`. If `WindowsPlayerAdapter` exists by then, also add `ApplyPlaySettings`, returning the render size Helena specifies (the Windows `resolution` Play setting is that rollout's job and is out of scope here). Build with `C:\dev\helworks\helengine\dotnetw.cmd build C:\dev\helworks\helengine-windows\builder\helengine.windows.builder.csproj`. Stage only these hunks with `git add -p`.

- [ ] **Step 4: Commit (only if Step 3 ran)**

```bash
cd /c/dev/helworks/helengine-windows
git add -p builder/WindowsPlatformDefinitionFactory.cs
git commit -m "feat(windows): declare no Windows-specific Play settings yet

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Phase A exit check

- [ ] Run `.\dotnetw.cmd test engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj`, `.\dotnetw.cmd test engine\helengine.player.tests\helengine.player.tests.csproj`, `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj` and `C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj`. Compare editor failures by name with the 2026-09-19 baseline; any new name is a regression to fix before Phase B.
- [ ] Grep `engine` for `--target-fps`, `--shadows-enabled` and `"--wait-for-debugger"`. None may remain outside the `Parse_RejectsFoldedFlags` test data.
- [ ] Run `git -C C:\dev\helworks\helengine status --short` and `git -C C:\dev\helworks\helengine-ps2 status --short`. Only other sessions' files may remain modified.

---
# Phase B — PS2 console video mode

Phase B starts only after the Phase A exit check passes.

**Execution order:** B1, B2, B3, **B5, B4**, B6, B7, B8, **B10, B9**, B11.
- B5 (the editor resolves video modes) depends only on B3, so it runs before B4 declares the PS2 modes. Otherwise PS2 Play would break between the B4 and B5 commits: B4 sets the capability frame rate to 0, and only B5 teaches the resolver to take it from the mode.
- B9's disc-build check needs demodisc's B10 settings.
- PS2 Play still renders 640x448 between B4 and B6. Do B4-B6 in one sitting.

### Task B1: Audit every 640/448 assumption in the PS2 runtime

**Files:**
- Read only: `C:\dev\helworks\helengine-ps2\src\platform\ps2\**`, `C:\dev\helworks\helengine\engine\helengine.core\utils\CameraProjectionUtils.cs`
- Modify: this plan file, section "B1 findings" below (record the confirmed table)

**Interfaces:**
- Produces: the confirmed findings table. Task B9 implements the "replace" rows. Any row marked "open" is reported to Helena and is not fixed in this plan.

- [ ] **Step 1: Search the runtime**

Run these Grep tool searches over `C:\dev\helworks\helengine-ps2\src` with output mode "content", one at a time:
- `\b(640|448|480|512|224|256)\b`
- `GS_MODE_|GS_INTERLACED|GS_NONINTERLACED|GS_FIELD|GS_FRAME|GS_ASPECT`
- `GsGlobal->(Width|Height)|Ps2DefaultFramebuffer`
- `CurrentPointer|gsKit_vram_alloc|vram`
- `2048\.0f \+ viewport|2048\.0f \+ screen`
- `AddWindow\(|ScissorRect|SCISSOR|clipRect`

For each hit that concerns the screen (ignore texture sizes, audio buffers and packet sizes), record the file:line, what it assumes, and whether it already derives from `GsGlobal->Width/Height` or the core's window size.

- [ ] **Step 2: Check projection and VRAM**

Read `CameraProjectionUtils.cs` around line 79-100 to confirm the projection aspect is viewport width / height (square pixels). Then compute GS VRAM use per mode. The calculation is two CT16 framebuffers plus a Z16 buffer: `3 * W * H * 2` bytes. Compare 640x448 (1,720,320 bytes) with 640x512 (1,966,080 bytes); the GS has 4 MiB. Record how much less VRAM the largest PAL mode leaves for textures than today's mode.

- [ ] **Step 3: Record the findings**

Replace the table below with the confirmed rows (keep the pre-filled rows that the search confirms):

#### B1 findings

| Location | Assumption | Status |
|---|---|---|
| `src/platform/ps2/Ps2BootHost.cpp:155-156` | `Ps2DefaultFramebufferWidth/Height = 640/448` | replace (B9) |
| `src/platform/ps2/Ps2BootHost.cpp:3289-3291` | `Mode = GS_MODE_NTSC`, `Interlace = GS_INTERLACED`, `Field = GS_FIELD` | replace (B9) |
| `src/platform/ps2/Ps2BootHost.cpp:3317-3320` | `AddWindow(0, GsGlobal->Width, GsGlobal->Height)` | already derived |
| `src/platform/ps2/rendering/vu/Ps2VuVifPacketBuilder.cpp:453,2427`, `Ps2VuOpaqueUntexturedSetupBuilder.cpp:137,225` | GS offset centre `2048 + viewport` | already derived |
| `src/platform/ps2/Ps2BootHost.cpp:2147-3052` (2D quad clipping) | clip rectangles come from the core canvas | already derived (core window size) |
| `src/platform/ps2/rendering/Ps2RenderManager3D.cpp:362` | texture VRAM starts at `CurrentPointer` after `gsKit_init_screen` | already derived; PAL 640x512 leaves 245,760 fewer bytes for textures |
| `helengine.core/utils/CameraProjectionUtils.cs:79-100` | projection aspect = viewport width / height (square pixels) | **open**: 224p/256p/512-wide modes are displayed 4:3 by the TV but rendered with a 2.86:1 or 1.14:1 aspect; Helena decides the pixel-aspect policy |

- [ ] **Step 4: Commit nothing; report**

Tell Helena the "open" rows. B9 replaces only the "replace" rows. Do not invent a pixel-aspect fix.

---

### Task B2: `PlatformVideoMode` and `PlatformVideoModeCapability`

**Files:**
- Create: `engine/helengine.baseplatform/Definitions/PlatformVideoMode.cs`, `engine/helengine.baseplatform/Definitions/PlatformVideoModeCapability.cs`
- Test: `engine/helengine.baseplatform.tests/Definitions/PlatformVideoModeTests.cs` (new)

**Interfaces:**
- Produces:
  - `PlatformVideoMode(string id, int width, int height, bool isProgressive, double refreshRateHz, double playerFrameRate)` with same-named properties plus `Label` (for example `320×224p`)
  - `PlatformVideoModeCapability(string settingId, PlatformVideoMode[] modes)` with `SettingId`, `Modes`, `GetModeIds(): string[]`, `Contains(string modeId): bool`, `Resolve(string modeId, string platformId): PlatformVideoMode`

- [ ] **Step 1: Write the failing tests**

```csharp
using helengine.baseplatform.Definitions;

namespace helengine.baseplatform.tests.Definitions {
    /// <summary>
    /// Verifies console video modes and the closed mode list a platform declares.
    /// </summary>
    public sealed class PlatformVideoModeTests {
        /// <summary>A valid mode keeps its fields and labels itself by size and scan.</summary>
        [Fact]
        public void Ctor_KeepsFieldsAndLabels() {
            PlatformVideoMode mode = new PlatformVideoMode("ntsc-320x224p", 320, 224, true, 59.94d, 60d);

            Assert.Equal("ntsc-320x224p", mode.Id);
            Assert.Equal(320, mode.Width);
            Assert.Equal(224, mode.Height);
            Assert.True(mode.IsProgressive);
            Assert.Equal(59.94d, mode.RefreshRateHz);
            Assert.Equal(60d, mode.PlayerFrameRate);
            Assert.Equal("320×224p", mode.Label);
            Assert.Equal("640×448i", new PlatformVideoMode("ntsc-640x448i", 640, 448, false, 59.94d, 60d).Label);
        }

        /// <summary>Every invalid field is rejected.</summary>
        [Fact]
        public void Ctor_RejectsInvalidFields() {
            Assert.Throws<ArgumentException>(() => new PlatformVideoMode(" ", 320, 224, true, 59.94d, 60d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformVideoMode("m", 0, 224, true, 59.94d, 60d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformVideoMode("m", 320, 4097, true, 59.94d, 60d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformVideoMode("m", 320, 224, true, double.NaN, 60d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlatformVideoMode("m", 320, 224, true, 59.94d, 0d));
        }

        /// <summary>The capability resolves each id and fails naming an unknown id and the platform.</summary>
        [Fact]
        public void Capability_ResolvesKnownAndRejectsUnknown() {
            PlatformVideoModeCapability capability = new PlatformVideoModeCapability("video-mode", [
                new PlatformVideoMode("a", 640, 448, false, 59.94d, 60d),
                new PlatformVideoMode("b", 320, 224, true, 59.94d, 60d)
            ]);

            Assert.Equal(new[] { "a", "b" }, capability.GetModeIds());
            Assert.Equal(320, capability.Resolve("b", "ps2").Width);
            Assert.True(capability.Contains("a"));
            Assert.False(capability.Contains("A"));
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => capability.Resolve("c", "ps2"));
            Assert.Contains("'c'", exception.Message);
            Assert.Contains("'ps2'", exception.Message);
        }

        /// <summary>The capability needs a setting id, at least one mode and unique ids.</summary>
        [Fact]
        public void Capability_RejectsInvalidDeclarations() {
            PlatformVideoMode mode = new PlatformVideoMode("a", 640, 448, false, 59.94d, 60d);

            Assert.Throws<ArgumentException>(() => new PlatformVideoModeCapability(" ", [mode]));
            Assert.Throws<ArgumentException>(() => new PlatformVideoModeCapability("video-mode", []));
            Assert.Throws<ArgumentException>(() => new PlatformVideoModeCapability("video-mode", [mode, mode]));
            Assert.Throws<ArgumentException>(() => new PlatformVideoModeCapability("video-mode", [mode, null]));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `.\dotnetw.cmd test engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlatformVideoModeTests"`
Expected: build FAIL (`PlatformVideoMode` not found).

- [ ] **Step 3: Implement**

`PlatformVideoMode.cs`:

```csharp
using System.Globalization;

namespace helengine.baseplatform.Definitions {
    /// <summary>
    /// One video mode a console's video output really produces: the framebuffer the whole frame (3D, UI and text) renders
    /// into, its scan, the refresh the console outputs and the frame rate the desktop Play player paces to.
    /// </summary>
    public sealed class PlatformVideoMode {
        /// <summary>Largest accepted framebuffer width or height.</summary>
        public const int MaximumDimension = 4096;

        /// <summary>
        /// Initializes one video mode.
        /// </summary>
        /// <param name="id">Stable id stored in project settings (for example <c>ntsc-320x224p</c>).</param>
        /// <param name="width">Framebuffer width in pixels.</param>
        /// <param name="height">Framebuffer height in pixels.</param>
        /// <param name="isProgressive">True for progressive scan, false for interlaced.</param>
        /// <param name="refreshRateHz">Refresh the console outputs (59.94 NTSC, 50 PAL).</param>
        /// <param name="playerFrameRate">Frame rate the desktop player paces to (60 NTSC, 50 PAL).</param>
        /// <exception cref="ArgumentException">Thrown when the id is blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a dimension outside 1..4096 or a non-positive or non-finite rate.</exception>
        public PlatformVideoMode(string id, int width, int height, bool isProgressive, double refreshRateHz, double playerFrameRate) {
            if (string.IsNullOrWhiteSpace(id)) {
                throw new ArgumentException("A video mode needs an id.", nameof(id));
            } else if (width < 1 || width > MaximumDimension) {
                throw new ArgumentOutOfRangeException(nameof(width), width, $"Video mode '{id}' needs a width between 1 and 4096.");
            } else if (height < 1 || height > MaximumDimension) {
                throw new ArgumentOutOfRangeException(nameof(height), height, $"Video mode '{id}' needs a height between 1 and 4096.");
            } else if (!(refreshRateHz > 0d && double.IsFinite(refreshRateHz))) {
                throw new ArgumentOutOfRangeException(nameof(refreshRateHz), refreshRateHz, $"Video mode '{id}' needs a positive, finite refresh rate.");
            } else if (!(playerFrameRate > 0d && double.IsFinite(playerFrameRate))) {
                throw new ArgumentOutOfRangeException(nameof(playerFrameRate), playerFrameRate, $"Video mode '{id}' needs a positive, finite player frame rate.");
            }

            Id = id;
            Width = width;
            Height = height;
            IsProgressive = isProgressive;
            RefreshRateHz = refreshRateHz;
            PlayerFrameRate = playerFrameRate;
        }

        /// <summary>Gets the stable id stored in project settings.</summary>
        public string Id { get; }

        /// <summary>Gets the framebuffer width.</summary>
        public int Width { get; }

        /// <summary>Gets the framebuffer height.</summary>
        public int Height { get; }

        /// <summary>Gets whether the mode scans progressively (false: interlaced).</summary>
        public bool IsProgressive { get; }

        /// <summary>Gets the refresh the console outputs.</summary>
        public double RefreshRateHz { get; }

        /// <summary>Gets the frame rate the desktop Play player paces to.</summary>
        public double PlayerFrameRate { get; }

        /// <summary>Gets the short label shown in the Play window title, for example <c>320×224p</c>.</summary>
        public string Label => Width.ToString(CultureInfo.InvariantCulture) + "×" + Height.ToString(CultureInfo.InvariantCulture) + (IsProgressive ? "p" : "i");
    }
}
```

`PlatformVideoModeCapability.cs`:

```csharp
namespace helengine.baseplatform.Definitions {
    /// <summary>
    /// The closed list of video modes a console platform offers, selected by one graphics-profile choice setting. The
    /// mode stays fixed from boot to shutdown.
    /// </summary>
    public sealed class PlatformVideoModeCapability {
        /// <summary>
        /// Initializes the capability.
        /// </summary>
        /// <param name="settingId">Graphics setting id that selects the mode (and the platform's Play setting id).</param>
        /// <param name="modes">Every mode the hardware produces, in display order.</param>
        /// <exception cref="ArgumentException">Thrown for a blank id, an empty list, a null entry or a repeated mode id.</exception>
        public PlatformVideoModeCapability(string settingId, PlatformVideoMode[] modes) {
            if (string.IsNullOrWhiteSpace(settingId)) {
                throw new ArgumentException("A video-mode capability needs the setting id that selects the mode.", nameof(settingId));
            } else if (modes == null || modes.Length == 0) {
                throw new ArgumentException("A video-mode capability needs at least one mode.", nameof(modes));
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < modes.Length; index++) {
                if (modes[index] == null) {
                    throw new ArgumentException("Video modes cannot contain null entries.", nameof(modes));
                } else if (!ids.Add(modes[index].Id)) {
                    throw new ArgumentException($"Video mode '{modes[index].Id}' is declared more than once.", nameof(modes));
                }
            }

            SettingId = settingId;
            Modes = [.. modes];
        }

        /// <summary>Gets the graphics (and Play) setting id that selects the mode.</summary>
        public string SettingId { get; }

        /// <summary>Gets every mode, in display order.</summary>
        public PlatformVideoMode[] Modes { get; }

        /// <summary>
        /// Gets the mode ids in display order, the exact allowed values of the selecting choice setting.
        /// </summary>
        /// <returns>New array of mode ids.</returns>
        public string[] GetModeIds() {
            string[] ids = new string[Modes.Length];
            for (int index = 0; index < Modes.Length; index++) {
                ids[index] = Modes[index].Id;
            }

            return ids;
        }

        /// <summary>
        /// Reports whether a mode id is declared, compared ordinally.
        /// </summary>
        /// <param name="modeId">Mode id.</param>
        /// <returns>True when declared.</returns>
        public bool Contains(string modeId) {
            return Array.Exists(Modes, mode => string.Equals(mode.Id, modeId, StringComparison.Ordinal));
        }

        /// <summary>
        /// Returns the mode for an id.
        /// </summary>
        /// <param name="modeId">Mode id.</param>
        /// <param name="platformId">Platform named in the failure.</param>
        /// <returns>The mode.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the id is not declared.</exception>
        public PlatformVideoMode Resolve(string modeId, string platformId) {
            for (int index = 0; index < Modes.Length; index++) {
                if (string.Equals(Modes[index].Id, modeId, StringComparison.Ordinal)) {
                    return Modes[index];
                }
            }

            throw new InvalidOperationException($"Video mode '{modeId}' is not one of platform '{platformId}''s modes: {string.Join(", ", GetModeIds())}.");
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `.\dotnetw.cmd test engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlatformVideoModeTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.baseplatform/Definitions/PlatformVideoMode.cs engine/helengine.baseplatform/Definitions/PlatformVideoModeCapability.cs engine/helengine.baseplatform.tests/Definitions/PlatformVideoModeTests.cs
git commit -m "feat(baseplatform): describe console video modes

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task B3: Platform definitions declare and validate video modes

**Files:**
- Modify: `engine/helengine.baseplatform/Definitions/PlatformDefinition.cs` (new optional constructor parameter `videoModeCapability` after `playerCapability`, property, validation)
- Modify: `engine/helengine.baseplatform/Definitions/PlatformPlayerCapability.cs` (a supported capability may declare frame rate 0 = "from the video mode")
- Modify: `engine/helengine.baseplatform.tests/Player/PlatformPlayerCapabilityTests.cs` (drop `[InlineData(0d)]` from the rejection theory; add the zero test)
- Test: `engine/helengine.baseplatform.tests/Definitions/PlatformDefinitionVideoModeTests.cs` (new)

**Interfaces:**
- Consumes: `PlatformVideoModeCapability` (B2), `PlatformPlayerCapability.FindPlaySetting` (A1)
- Produces:
  - `PlatformDefinition(..., PlatformPlayerCapability playerCapability = null, PlatformVideoModeCapability videoModeCapability = null)`
  - `PlatformDefinition.VideoModeCapability` (null when not declared) and `HasVideoModes: bool`
  - `PlatformPlayerCapability.FrameRateFromVideoMode: bool` (`SupportsPlay && TargetFrameRate == 0`)
  - Rules:
    - a Play-capable platform has exactly one frame-rate source;
    - every graphics profile declares the selecting choice with `AllowedValues` equal to `GetModeIds()` (same order) and a default among them;
    - a Play-capable platform with modes declares the same-id Play setting as a choice of `["project", ..ids]` defaulting to `project`.

- [ ] **Step 1: Write the failing tests**

```csharp
using helengine.baseplatform.Definitions;

namespace helengine.baseplatform.tests.Definitions {
    /// <summary>
    /// Verifies a platform's video modes agree with its graphics profiles and Play declarations.
    /// </summary>
    public sealed class PlatformDefinitionVideoModeTests {
        /// <summary>A consistent declaration is accepted and exposed.</summary>
        [Fact]
        public void Ctor_WhenConsistent_ExposesVideoModes() {
            PlatformDefinition definition = Create(CreateGraphicsSetting(["a", "b"], "a"), CreatePlayCapability(0d, CreatePlaySetting(["project", "a", "b"])), CreateModes());

            Assert.True(definition.HasVideoModes);
            Assert.True(definition.PlayerCapability.FrameRateFromVideoMode);
            Assert.Equal("b", definition.VideoModeCapability.Resolve("b", "ps2").Id);
        }

        /// <summary>The graphics choice must list exactly the mode ids, in order.</summary>
        [Fact]
        public void Ctor_WhenGraphicsChoiceDiffers_Throws() {
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => Create(CreateGraphicsSetting(["b", "a"], "a"), CreatePlayCapability(0d, CreatePlaySetting(["project", "a", "b"])), CreateModes()));

            Assert.Equal("videoModeCapability", exception.ParamName);
        }

        /// <summary>The Play setting must offer project plus every mode.</summary>
        [Fact]
        public void Ctor_WhenPlaySettingDiffers_Throws() {
            Assert.Throws<ArgumentException>(
                () => Create(CreateGraphicsSetting(["a", "b"], "a"), CreatePlayCapability(0d, CreatePlaySetting(["project", "a"])), CreateModes()));
        }

        /// <summary>A Play platform with modes may not also declare a fixed frame rate.</summary>
        [Fact]
        public void Ctor_WhenModesAndFixedFrameRate_Throws() {
            Assert.Throws<ArgumentException>(
                () => Create(CreateGraphicsSetting(["a", "b"], "a"), CreatePlayCapability(60d, CreatePlaySetting(["project", "a", "b"])), CreateModes()));
        }

        /// <summary>A Play platform without modes must declare a fixed frame rate.</summary>
        [Fact]
        public void Ctor_WhenNoModesAndNoFrameRate_Throws() {
            Assert.Throws<ArgumentException>(
                () => Create(CreateGraphicsSetting(["a", "b"], "a"), new PlatformPlayerCapability(true, "a.B", "s", 0d, PlayerInputDevices.Gamepad, "ps2", "1.0.0", []), null));
        }

        /// <summary>Creates the definition under test.</summary>
        static PlatformDefinition Create(PlatformSettingDefinition graphicsSetting, PlatformPlayerCapability capability, PlatformVideoModeCapability modes) {
            return new PlatformDefinition("ps2", "PlayStation 2", [],
                [new PlatformGraphicsProfileDefinition("g", "G", "G", [graphicsSetting])],
                [], [], [], [], [], [], playerCapability: capability, videoModeCapability: modes);
        }

        /// <summary>Creates the graphics choice selecting the mode.</summary>
        static PlatformSettingDefinition CreateGraphicsSetting(string[] allowed, string defaultValue) {
            return new PlatformSettingDefinition("video-mode", "Video Mode", PlatformSettingKind.Choice, defaultValue, true, allowed);
        }

        /// <summary>Creates the Play setting choosing how Play renders.</summary>
        static PlatformSettingDefinition CreatePlaySetting(string[] allowed) {
            return new PlatformSettingDefinition("video-mode", "Video Mode", PlatformSettingKind.Choice, "project", true, allowed);
        }

        /// <summary>Creates a supported Play capability.</summary>
        static PlatformPlayerCapability CreatePlayCapability(double frameRate, PlatformSettingDefinition playSetting) {
            return new PlatformPlayerCapability(true, "a.B", "s", frameRate, PlayerInputDevices.Gamepad, "ps2", "1.0.0", [playSetting]);
        }

        /// <summary>Creates two modes.</summary>
        static PlatformVideoModeCapability CreateModes() {
            return new PlatformVideoModeCapability("video-mode", [
                new PlatformVideoMode("a", 640, 448, false, 59.94d, 60d),
                new PlatformVideoMode("b", 320, 224, true, 59.94d, 60d)
            ]);
        }
    }
}
```

In `PlatformPlayerCapabilityTests.cs`, remove `[InlineData(0d)]` from `Ctor_WhenSupportedWithoutPositiveTargetFrameRate_Throws`, rename that test to `Ctor_WhenSupportedWithNegativeOrNonFiniteTargetFrameRate_Throws`, and add:

```csharp
        /// <summary>A supported capability may take its frame rate from the platform's video mode (declared as 0).</summary>
        [Fact]
        public void Ctor_WhenSupportedWithZeroFrameRate_TakesFrameRateFromVideoMode() {
            Assert.True(new PlatformPlayerCapability(true, "a.B", "staging", 0d, PlayerInputDevices.Gamepad, "ps2", "1.0.0", []).FrameRateFromVideoMode);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `.\dotnetw.cmd test engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj --filter "FullyQualifiedName~PlatformDefinitionVideoModeTests|FullyQualifiedName~PlatformPlayerCapabilityTests"`
Expected: build FAIL (no `videoModeCapability` parameter, no `FrameRateFromVideoMode`).

- [ ] **Step 3: Relax the capability frame-rate guard**

In `PlatformPlayerCapability`, change the frame-rate branch to:

```csharp
            } else if (supportsPlay && !(targetFrameRate >= 0d && double.IsFinite(targetFrameRate))) {
                throw new ArgumentOutOfRangeException(
                    nameof(targetFrameRate),
                    targetFrameRate,
                    "A platform that supports Play must declare a non-negative, finite player target frame rate (0 = taken from the platform's video mode).");
```

Update the `targetFrameRate` param doc to say that 0 means the selected video mode supplies the rate, and that `PlatformDefinition` checks exactly one source exists. Add:

```csharp
        /// <summary>
        /// Gets whether the player frame rate comes from the platform's selected video mode instead of
        /// <see cref="TargetFrameRate"/>.
        /// </summary>
        public bool FrameRateFromVideoMode => SupportsPlay && TargetFrameRate == 0d;
```

- [ ] **Step 4: Validate in `PlatformDefinition`**

Add the optional constructor parameter `PlatformVideoModeCapability videoModeCapability = null` after `playerCapability`:

```csharp
        /// <param name="videoModeCapability">Video modes the console produces; null when the platform has none (its output is not mode-based).</param>
```

After the Task A2 call, assign `VideoModeCapability = videoModeCapability;` and call `ValidateVideoModes(PlayerCapability, GraphicsProfiles, videoModeCapability);`. Add:

```csharp
        /// <summary>
        /// Gets the video modes the console produces, or null when the platform declares none.
        /// </summary>
        public PlatformVideoModeCapability VideoModeCapability { get; }

        /// <summary>
        /// Gets whether the platform declares video modes.
        /// </summary>
        public bool HasVideoModes => VideoModeCapability != null;

        /// <summary>
        /// Checks that video modes agree with the graphics profiles and the Play declarations, and that a Play platform has
        /// exactly one frame-rate source.
        /// </summary>
        /// <param name="playerCapability">Play capability.</param>
        /// <param name="graphicsProfiles">Graphics profiles.</param>
        /// <param name="videoModes">Declared video modes, or null.</param>
        /// <exception cref="ArgumentException">Thrown when the declarations disagree.</exception>
        static void ValidateVideoModes(PlatformPlayerCapability playerCapability, PlatformGraphicsProfileDefinition[] graphicsProfiles, PlatformVideoModeCapability videoModes) {
            bool hasModes = videoModes != null;
            if (playerCapability.SupportsPlay && hasModes == (playerCapability.TargetFrameRate > 0d)) {
                throw new ArgumentException(
                    "A platform that supports Play takes its player frame rate from exactly one source: a positive Play capability frame rate, or video modes with a Play capability frame rate of 0.",
                    "playerCapability");
            } else if (!hasModes) {
                return;
            }

            string[] modeIds = videoModes.GetModeIds();
            for (int profileIndex = 0; profileIndex < graphicsProfiles.Length; profileIndex++) {
                PlatformGraphicsProfileDefinition profile = graphicsProfiles[profileIndex];
                PlatformSettingDefinition setting = Array.Find(profile.Settings, candidate => string.Equals(candidate.SettingId, videoModes.SettingId, StringComparison.Ordinal));
                if (setting == null) {
                    throw new ArgumentException($"Graphics profile '{profile.ProfileId}' declares no '{videoModes.SettingId}' setting to select the video mode.", "videoModeCapability");
                } else if (setting.SettingKind != PlatformSettingKind.Choice || !setting.AllowedValues.SequenceEqual(modeIds, StringComparer.Ordinal)) {
                    throw new ArgumentException($"Graphics profile '{profile.ProfileId}' setting '{videoModes.SettingId}' must be a choice of exactly: {string.Join(", ", modeIds)}.", "videoModeCapability");
                } else if (Array.IndexOf(modeIds, setting.DefaultValue) < 0) {
                    throw new ArgumentException($"Graphics profile '{profile.ProfileId}' setting '{videoModes.SettingId}' defaults to '{setting.DefaultValue}', which is not a video mode.", "videoModeCapability");
                }
            }

            if (!playerCapability.SupportsPlay) {
                return;
            }

            string[] playValues = new string[modeIds.Length + 1];
            playValues[0] = PlayerCommonPlaySettings.ProjectValue;
            Array.Copy(modeIds, 0, playValues, 1, modeIds.Length);
            PlatformSettingDefinition playSetting = playerCapability.FindPlaySetting(videoModes.SettingId);
            if (playSetting == null
                || playSetting.SettingKind != PlatformSettingKind.Choice
                || !playSetting.AllowedValues.SequenceEqual(playValues, StringComparer.Ordinal)
                || !string.Equals(playSetting.DefaultValue, PlayerCommonPlaySettings.ProjectValue, StringComparison.Ordinal)) {
                throw new ArgumentException(
                    $"A Play platform with video modes must declare Play setting '{videoModes.SettingId}' as a choice of project plus every mode, defaulting to project.",
                    "videoModeCapability");
            }
        }
```

Pass `null` for `videoModeCapability` from the secondary constructor overload(s) if they forward to the primary positional constructor.

- [ ] **Step 5: Run tests to verify they pass**

Run: `.\dotnetw.cmd test engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj`
Expected: PASS (whole project).

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.baseplatform/Definitions/PlatformDefinition.cs engine/helengine.baseplatform/Definitions/PlatformPlayerCapability.cs engine/helengine.baseplatform.tests/Definitions/PlatformDefinitionVideoModeTests.cs engine/helengine.baseplatform.tests/Player/PlatformPlayerCapabilityTests.cs
git commit -m "feat(baseplatform): validate a platform's video modes against its graphics and Play settings

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task B4: PS2 declares its 12 video modes

**Files:**
- Create: `C:\dev\helworks\helengine-ps2\builder\Ps2VideoModes.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder\Ps2PlatformDefinitionFactory.cs` (both graphics profiles, lines 41-54 and 82-95; player capability 376-384; new `videoModeCapability:` argument)
- Test: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2PlatformDefinitionFactoryTests.cs`

**Interfaces:**
- Consumes: `PlatformVideoMode`, `PlatformVideoModeCapability` (B2), the `PlatformDefinition` `videoModeCapability` parameter (B3)
- Produces:
  - `Ps2VideoModes.SettingId = "video-mode"` and `Ps2VideoModes.DefaultModeId = "ntsc-640x448i"`
  - `Ps2VideoModes.CreateCapability(): PlatformVideoModeCapability`
  - `Ps2VideoModes.CreateGraphicsSetting(): PlatformSettingDefinition`
  - `Ps2VideoModes.CreatePlaySetting(): PlatformSettingDefinition`
  - `Ps2VideoModes.IsPal(PlatformVideoMode): bool`

- [ ] **Step 1: Write the failing tests**

Append to `Ps2PlatformDefinitionFactoryTests.cs`:

```csharp
    /// <summary>
    /// The PS2 offers exactly the 12 NTSC and PAL modes, selected by a graphics choice whose values equal the capability
    /// ids and default to today's 640x448 interlaced NTSC mode.
    /// </summary>
    [Fact]
    public void Create_DeclaresTheTwelvePs2VideoModes() {
        PlatformDefinition definition = Ps2PlatformDefinitionFactory.Create();
        string[] expectedIds = [
            "ntsc-640x448i", "ntsc-512x448i", "ntsc-640x224p", "ntsc-512x224p", "ntsc-320x224p", "ntsc-256x224p",
            "pal-640x512i", "pal-512x512i", "pal-640x256p", "pal-512x256p", "pal-320x256p", "pal-256x256p"
        ];

        Assert.Equal(expectedIds, definition.VideoModeCapability.GetModeIds());
        foreach (PlatformGraphicsProfileDefinition profile in definition.GraphicsProfiles) {
            PlatformSettingDefinition setting = Assert.Single(profile.Settings, candidate => candidate.SettingId == "video-mode");
            Assert.Equal(expectedIds, setting.AllowedValues);
            Assert.Equal("ntsc-640x448i", setting.DefaultValue);
            Assert.DoesNotContain(profile.Settings, candidate => candidate.SettingId == "default-width" || candidate.SettingId == "default-height");
        }
    }

    /// <summary>Every mode resolves to its size, scan and rates; an unknown id is rejected naming it.</summary>
    [Theory]
    [InlineData("ntsc-640x448i", 640, 448, false, 59.94d, 60d)]
    [InlineData("ntsc-512x448i", 512, 448, false, 59.94d, 60d)]
    [InlineData("ntsc-640x224p", 640, 224, true, 59.94d, 60d)]
    [InlineData("ntsc-512x224p", 512, 224, true, 59.94d, 60d)]
    [InlineData("ntsc-320x224p", 320, 224, true, 59.94d, 60d)]
    [InlineData("ntsc-256x224p", 256, 224, true, 59.94d, 60d)]
    [InlineData("pal-640x512i", 640, 512, false, 50d, 50d)]
    [InlineData("pal-512x512i", 512, 512, false, 50d, 50d)]
    [InlineData("pal-640x256p", 640, 256, true, 50d, 50d)]
    [InlineData("pal-512x256p", 512, 256, true, 50d, 50d)]
    [InlineData("pal-320x256p", 320, 256, true, 50d, 50d)]
    [InlineData("pal-256x256p", 256, 256, true, 50d, 50d)]
    public void VideoModes_ResolveEveryId(string id, int width, int height, bool progressive, double refresh, double playerRate) {
        PlatformVideoMode mode = Ps2PlatformDefinitionFactory.Create().VideoModeCapability.Resolve(id, "ps2");

        Assert.Equal(width, mode.Width);
        Assert.Equal(height, mode.Height);
        Assert.Equal(progressive, mode.IsProgressive);
        Assert.Equal(refresh, mode.RefreshRateHz);
        Assert.Equal(playerRate, mode.PlayerFrameRate);
        Assert.Equal(id.StartsWith("pal-", StringComparison.Ordinal), Ps2VideoModes.IsPal(mode));
    }

    /// <summary>An unknown mode id is rejected naming it.</summary>
    [Fact]
    public void VideoModes_RejectUnknownId() {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Ps2PlatformDefinitionFactory.Create().VideoModeCapability.Resolve("ntsc-640x480p", "ps2"));

        Assert.Contains("ntsc-640x480p", exception.Message);
    }

    /// <summary>Play follows the project's mode by default and takes its frame rate from the mode.</summary>
    [Fact]
    public void Create_PlayVideoModeDefaultsToProject() {
        PlatformPlayerCapability capability = Ps2PlatformDefinitionFactory.Create().PlayerCapability;

        PlatformSettingDefinition playSetting = capability.FindPlaySetting("video-mode");
        Assert.Equal("project", playSetting.DefaultValue);
        Assert.Equal("project", playSetting.AllowedValues[0]);
        Assert.Equal(13, playSetting.AllowedValues.Length);
        Assert.True(capability.FrameRateFromVideoMode);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Ps2PlatformDefinitionFactoryTests"`
Expected: build FAIL (`Ps2VideoModes` not found).

- [ ] **Step 3: Implement `Ps2VideoModes`**

```csharp
using helengine.baseplatform.Definitions;

namespace helengine.ps2.builder;

/// <summary>
/// The PS2 video modes: NTSC (59.94 Hz, Play paced at 60) and PAL (50 Hz) framebuffers that the GS renders into and the
/// PCRTC magnifies on output. A single id carries region, size and scan, so impossible combinations cannot be expressed.
/// </summary>
public static class Ps2VideoModes {
    /// <summary>Graphics setting (and Play setting) id that selects the mode.</summary>
    public const string SettingId = "video-mode";

    /// <summary>Default mode: today's 640x448 interlaced NTSC output.</summary>
    public const string DefaultModeId = "ntsc-640x448i";

    /// <summary>Refresh of NTSC output.</summary>
    const double NtscRefreshHz = 59.94d;

    /// <summary>Desktop Play frame rate for NTSC modes (kept at 60 so it divides desktop refresh rates evenly).</summary>
    const double NtscPlayerFrameRate = 60d;

    /// <summary>Refresh of PAL output and its Play frame rate.</summary>
    const double PalRate = 50d;

    /// <summary>
    /// Creates the capability listing all 12 modes in display order.
    /// </summary>
    /// <returns>New PS2 video-mode capability.</returns>
    public static PlatformVideoModeCapability CreateCapability() {
        return new PlatformVideoModeCapability(SettingId, [
            new PlatformVideoMode("ntsc-640x448i", 640, 448, false, NtscRefreshHz, NtscPlayerFrameRate),
            new PlatformVideoMode("ntsc-512x448i", 512, 448, false, NtscRefreshHz, NtscPlayerFrameRate),
            new PlatformVideoMode("ntsc-640x224p", 640, 224, true, NtscRefreshHz, NtscPlayerFrameRate),
            new PlatformVideoMode("ntsc-512x224p", 512, 224, true, NtscRefreshHz, NtscPlayerFrameRate),
            new PlatformVideoMode("ntsc-320x224p", 320, 224, true, NtscRefreshHz, NtscPlayerFrameRate),
            new PlatformVideoMode("ntsc-256x224p", 256, 224, true, NtscRefreshHz, NtscPlayerFrameRate),
            new PlatformVideoMode("pal-640x512i", 640, 512, false, PalRate, PalRate),
            new PlatformVideoMode("pal-512x512i", 512, 512, false, PalRate, PalRate),
            new PlatformVideoMode("pal-640x256p", 640, 256, true, PalRate, PalRate),
            new PlatformVideoMode("pal-512x256p", 512, 256, true, PalRate, PalRate),
            new PlatformVideoMode("pal-320x256p", 320, 256, true, PalRate, PalRate),
            new PlatformVideoMode("pal-256x256p", 256, 256, true, PalRate, PalRate)
        ]);
    }

    /// <summary>
    /// Creates the graphics-profile choice that selects the console's mode.
    /// </summary>
    /// <returns>New setting declaration.</returns>
    public static PlatformSettingDefinition CreateGraphicsSetting() {
        return new PlatformSettingDefinition(SettingId, "Video Mode", PlatformSettingKind.Choice, DefaultModeId, true, CreateCapability().GetModeIds());
    }

    /// <summary>
    /// Creates the Play setting that chooses how Play renders: the project's mode (default) or an explicit one.
    /// </summary>
    /// <returns>New Play setting declaration.</returns>
    public static PlatformSettingDefinition CreatePlaySetting() {
        string[] modeIds = CreateCapability().GetModeIds();
        string[] values = new string[modeIds.Length + 1];
        values[0] = PlayerCommonPlaySettings.ProjectValue;
        Array.Copy(modeIds, 0, values, 1, modeIds.Length);
        return new PlatformSettingDefinition(SettingId, "Video Mode", PlatformSettingKind.Choice, PlayerCommonPlaySettings.ProjectValue, true, values);
    }

    /// <summary>
    /// Reports whether a PS2 mode outputs PAL (50 Hz) rather than NTSC.
    /// </summary>
    /// <param name="mode">PS2 video mode.</param>
    /// <returns>True for PAL.</returns>
    /// <exception cref="ArgumentException">Thrown when the refresh is neither PS2 rate.</exception>
    public static bool IsPal(PlatformVideoMode mode) {
        if (mode == null) {
            throw new ArgumentNullException(nameof(mode));
        } else if (mode.RefreshRateHz == PalRate) {
            return true;
        } else if (mode.RefreshRateHz == NtscRefreshHz) {
            return false;
        }

        throw new ArgumentException($"Video mode '{mode.Id}' has refresh {mode.RefreshRateHz}, which is neither PS2 NTSC nor PAL.", nameof(mode));
    }
}
```

- [ ] **Step 4: Update the factory**

In both graphics profiles, replace the `default-width` and `default-height` `PlatformSettingDefinition`s with `Ps2VideoModes.CreateGraphicsSetting(),`. Change the player capability's frame rate from `60d` to `0d` and its play settings from `[]` to `[Ps2VideoModes.CreatePlaySetting()]`. Add `videoModeCapability: Ps2VideoModes.CreateCapability()` after `playerCapability:`. Rewrite the comment above the capability: the desktop player now takes its frame rate from the selected video mode (60 for NTSC modes, 50 for PAL).

- [ ] **Step 5: Run tests to verify they pass**

Run: `C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Ps2PlatformDefinitionFactoryTests|FullyQualifiedName~Ps2PlayerAdapterTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
cd /c/dev/helworks/helengine-ps2
git add builder/Ps2VideoModes.cs builder/Ps2PlatformDefinitionFactory.cs builder.tests/Ps2PlatformDefinitionFactoryTests.cs
git commit -m "feat(ps2): declare the PS2 NTSC and PAL video modes

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task B5: The editor resolves the Play video mode

**Files:**
- Modify: `engine/helengine.baseplatform/Player/PlayerPlaySettings.cs` (new last parameter `PlatformVideoMode videoMode`, null when the platform has no modes; property `VideoMode`, `HasVideoMode`)
- Modify: `engine/helengine.baseplatform/Player/PlayerPlaySettingsFile.cs` (`videoMode` object or JSON null)
- Modify: `engine/helengine.editor/managers/play/EditorPlaySettingsSnapshot.cs` (new parameter `PlatformVideoModeCapability videoModeCapability`, may be null)
- Modify: `engine/helengine.editor/managers/play/EditorPlaySettingsCapture.cs` (pass it through)
- Modify: `engine/helengine.editor/managers/play/EditorPlayPlatformOption.cs` (`VideoModeCapability` from `loadedBuilder.Definition.VideoModeCapability`)
- Modify: `engine/helengine.editor/managers/play/EditorPlayLaunchSettingsResolver.cs`
- Modify: `engine/helengine.editor/EditorSession.cs` (`HandlePlayRequested` passes `playPlatform.VideoModeCapability`)
- Modify (call sites gain the new argument): `engine/helengine.baseplatform.tests/Player/PlayerPlaySettingsFileTests.cs`, `C:\dev\helworks\helengine-ps2\builder.tests\Ps2PlayerAdapterTests.cs`, `engine/helengine.player.tests/PlayerFrameLimiterFactoryTests.cs` (pass `null`); `engine/helengine.editor.tests/managers/play/*Tests.cs` and `TestPlaySettings.cs`, `engine/helengine.player.tests/ContractPlaySettings.cs` (snapshot and capture take `null`)
- Test: `engine/helengine.editor.tests/managers/play/EditorPlayLaunchSettingsResolverTests.cs`, `engine/helengine.baseplatform.tests/Player/PlayerPlaySettingsFileTests.cs`

**Interfaces:**
- Consumes: `PlatformVideoModeCapability.Resolve` (B2), `PlatformDefinition.VideoModeCapability` (B3)
- Produces:
  - `PlayerPlaySettings(..., IReadOnlyDictionary<string,string> platformValues, PlatformVideoMode videoMode)` with `VideoMode` and `HasVideoMode`
  - `EditorPlaySettingsSnapshot(EditorPlaySettingsEvaluation evaluation, bool waitForDebuggerRequested, PlatformPlayerCapability capability, PlatformVideoModeCapability videoModeCapability, int editorProcessId)` with `VideoModeCapability` and `HasVideoModes`
  - `EditorPlaySettingsCapture.TryCapture(store, platformId, capability, videoModeCapability, waitForDebuggerRequested, editorProcessId, out snapshot, out failureMessage)`
  - `EditorPlayPlatformOption.VideoModeCapability`
  - Resolver rule: with modes, the resolved `video-mode` value selects `VideoMode`, and `frame-rate: platform` uses `VideoMode.PlayerFrameRate`.

- [ ] **Step 1: Write the failing tests**

Append to `EditorPlayLaunchSettingsResolverTests.cs`:

```csharp
    /// <summary>With video modes, project resolves to the project's mode and the frame rate is the mode's player rate.</summary>
    [Fact]
    public void Resolve_VideoModeFollowsProjectAndSetsFrameRate() {
        EditorPlaySettingsSnapshot snapshot = CreateVideoModeSnapshot("project");

        PlayerPlaySettings settings = EditorPlayLaunchSettingsResolver.Resolve(snapshot, CreateCookResult(true, new Dictionary<string, string> { ["video-mode"] = "pal-320x256p" }));

        Assert.Equal("pal-320x256p", settings.VideoMode.Id);
        Assert.Equal(320, settings.VideoMode.Width);
        Assert.Equal(50d, settings.TargetFrameRate);
    }

    /// <summary>An explicit Play mode overrides the project's mode.</summary>
    [Fact]
    public void Resolve_ExplicitVideoModeOverridesProject() {
        EditorPlaySettingsSnapshot snapshot = CreateVideoModeSnapshot("ntsc-256x224p");

        PlayerPlaySettings settings = EditorPlayLaunchSettingsResolver.Resolve(snapshot, CreateCookResult(true, new Dictionary<string, string> { ["video-mode"] = "pal-640x512i" }));

        Assert.Equal("ntsc-256x224p", settings.VideoMode.Id);
        Assert.Equal(60d, settings.TargetFrameRate);
    }

    /// <summary>
    /// An old project file without <c>video-mode</c> (it still holds default-width/default-height) fails Play naming the
    /// field and the platform settings file instead of silently running at 640x448.
    /// </summary>
    [Fact]
    public void Resolve_WhenProjectHasOldSizeFieldsOnly_FailsNamingFieldAndFile() {
        EditorPlaySettingsSnapshot snapshot = CreateVideoModeSnapshot("project");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => EditorPlayLaunchSettingsResolver.Resolve(
            snapshot,
            CreateCookResult(true, new Dictionary<string, string> { ["default-width"] = "640", ["default-height"] = "448" })));

        Assert.Contains("video-mode", exception.Message);
        Assert.Contains("settings/platform.ps2.json", exception.Message);
    }

    /// <summary>Creates a PS2-like snapshot whose Play video mode is the given value.</summary>
    static EditorPlaySettingsSnapshot CreateVideoModeSnapshot(string playValue) {
        PlatformVideoModeCapability modes = new PlatformVideoModeCapability("video-mode", [
            new PlatformVideoMode("ntsc-256x224p", 256, 224, true, 59.94d, 60d),
            new PlatformVideoMode("pal-320x256p", 320, 256, true, 50d, 50d),
            new PlatformVideoMode("pal-640x512i", 640, 512, false, 50d, 50d)
        ]);
        PlatformPlayerCapability capability = new PlatformPlayerCapability(true, "a.B", "ps2-staging", 0d, PlayerInputDevices.Gamepad, "ps2", "1.0.0",
            [new PlatformSettingDefinition("video-mode", "Video Mode", PlatformSettingKind.Choice, "project", true, ["project", "ntsc-256x224p", "pal-320x256p", "pal-640x512i"])]);
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.PlatformValues["ps2"] = new Dictionary<string, string> { ["video-mode"] = playValue };
        EditorPlaySettingsEvaluation evaluation = EditorPlaySettingsEvaluator.Evaluate(document, @"C:\p\user_settings\play.json", "ps2", capability);
        return new EditorPlaySettingsSnapshot(evaluation, false, capability, modes, 55);
    }
```

Change the existing `CreateSnapshot` helper in that file to pass `null` for `videoModeCapability`.

Append to `PlayerPlaySettingsFileTests.cs`:

```csharp
        /// <summary>A resolved video mode round-trips in full.</summary>
        [Fact]
        public void WriteThenRead_RoundTripsVideoMode() {
            string path = Path.Combine(Root, "mode.json");
            PlatformVideoMode mode = new PlatformVideoMode("pal-320x256p", 320, 256, true, 50d, 50d);
            PlayerPlaySettingsFile.Write(path, new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, false, 50d, new Dictionary<string, string>(), mode));

            PlayerPlaySettings read = PlayerPlaySettingsFile.Read(path);

            Assert.True(read.HasVideoMode);
            Assert.Equal("pal-320x256p", read.VideoMode.Id);
            Assert.Equal(320, read.VideoMode.Width);
            Assert.Equal(256, read.VideoMode.Height);
            Assert.True(read.VideoMode.IsProgressive);
            Assert.Equal(50d, read.VideoMode.RefreshRateHz);
            Assert.Equal(50d, read.VideoMode.PlayerFrameRate);
        }
```

(Add `using helengine.baseplatform.Definitions;`, and pass `null` as the last argument of every other `new PlayerPlaySettings(` in the file.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlayLaunchSettingsResolverTests"`
Expected: build FAIL (no 5-argument snapshot constructor, no `VideoMode`).

- [ ] **Step 3: Extend `PlayerPlaySettings` and the file**

Add the constructor parameter `PlatformVideoMode videoMode` (doc: "Resolved console video mode, or null when the platform declares no video modes.") and, when it is not null, check `frameRateUnlimited || targetFrameRate == videoMode.PlayerFrameRate`, else throw `ArgumentException` naming both. Add `public PlatformVideoMode VideoMode { get; }` and `public bool HasVideoMode => VideoMode != null;` (with XML docs). Add `using helengine.baseplatform.Definitions;`.

In `PlayerPlaySettingsFile`, add `"videoMode"` to `PropertyNames`. In `Write`, after `platform`:

```csharp
            if (settings.HasVideoMode) {
                writer.WriteStartObject("videoMode");
                writer.WriteString("id", settings.VideoMode.Id);
                writer.WriteNumber("width", settings.VideoMode.Width);
                writer.WriteNumber("height", settings.VideoMode.Height);
                writer.WriteBoolean("progressive", settings.VideoMode.IsProgressive);
                writer.WriteNumber("refreshRateHz", settings.VideoMode.RefreshRateHz);
                writer.WriteNumber("playerFrameRate", settings.VideoMode.PlayerFrameRate);
                writer.WriteEndObject();
            } else {
                writer.WriteNull("videoMode");
            }
```

In `Read`, before constructing the settings:

```csharp
            JsonElement videoModeElement;
            if (!root.TryGetProperty("videoMode", out videoModeElement)) {
                throw new InvalidOperationException($"Play settings file '{path}' is missing property 'videoMode'.");
            }

            PlatformVideoMode videoMode = null;
            if (videoModeElement.ValueKind == JsonValueKind.Object) {
                videoMode = new PlatformVideoMode(
                    RequireProperty(videoModeElement, "id", JsonValueKind.String, path).GetString(),
                    RequireProperty(videoModeElement, "width", JsonValueKind.Number, path).GetInt32(),
                    RequireProperty(videoModeElement, "height", JsonValueKind.Number, path).GetInt32(),
                    RequireBoolean(videoModeElement, "progressive", path),
                    RequireProperty(videoModeElement, "refreshRateHz", JsonValueKind.Number, path).GetDouble(),
                    RequireProperty(videoModeElement, "playerFrameRate", JsonValueKind.Number, path).GetDouble());
            } else if (videoModeElement.ValueKind != JsonValueKind.Null) {
                throw new InvalidOperationException($"Play settings file '{path}' property 'videoMode' must be an object or null.");
            }
```

Then pass `videoMode` as the last constructor argument.

- [ ] **Step 4: Extend the snapshot, capture, option and resolver**

`EditorPlaySettingsSnapshot`: add the parameter `PlatformVideoModeCapability videoModeCapability` after `capability` (doc: "Video modes of the platform, or null when it declares none."), store it, and add `VideoModeCapability` / `HasVideoModes => VideoModeCapability != null` with XML docs.

`EditorPlaySettingsCapture.TryCapture`: add `PlatformVideoModeCapability videoModeCapability` after `capability` and pass it on.

`EditorPlayPlatformOption`: add the following and assign `VideoModeCapability = loadedBuilder.Definition.VideoModeCapability;`:

```csharp
        /// <summary>
        /// Gets the video modes the platform declares, or null when it declares none.
        /// </summary>
        public PlatformVideoModeCapability VideoModeCapability { get; }
```

`EditorSession.HandlePlayRequested`: pass `playPlatform.VideoModeCapability` to `TryCapture`.

`EditorPlayLaunchSettingsResolver.Resolve`: after the platform-value loop and before `return`:

```csharp
            PlatformVideoMode videoMode = null;
            if (snapshot.HasVideoModes) {
                videoMode = snapshot.VideoModeCapability.Resolve(platformValues[snapshot.VideoModeCapability.SettingId], snapshot.PlatformId);
            }

            double targetFrameRate = unlimited ? 0d : (videoMode != null ? videoMode.PlayerFrameRate : snapshot.Capability.TargetFrameRate);
```

Use `targetFrameRate` in the constructor and pass `videoMode` last. (Moving the `unlimited` computation above this block is fine.)

Update the remaining call sites listed under **Files** by appending `null`. Snapshot and capture call sites take `null` for `videoModeCapability` in the new position.

- [ ] **Step 5: Run tests to verify they pass**

Run: `.\dotnetw.cmd test engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj`, `.\dotnetw.cmd test engine\helengine.player.tests\helengine.player.tests.csproj`, `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~managers.play"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.baseplatform/Player/PlayerPlaySettings.cs engine/helengine.baseplatform/Player/PlayerPlaySettingsFile.cs engine/helengine.baseplatform.tests/Player/PlayerPlaySettingsFileTests.cs engine/helengine.editor/managers/play/EditorPlaySettingsSnapshot.cs engine/helengine.editor/managers/play/EditorPlaySettingsCapture.cs engine/helengine.editor/managers/play/EditorPlayPlatformOption.cs engine/helengine.editor/managers/play/EditorPlayLaunchSettingsResolver.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/managers/play engine/helengine.player.tests/ContractPlaySettings.cs engine/helengine.player.tests/PlayerFrameLimiterFactoryTests.cs
git diff --cached --numstat
git commit -m "feat(play): resolve the console video mode for Play

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

(`git add engine/helengine.editor.tests/managers/play` stages only files this task changed. Check `git diff --cached --stat` first and unstage anything that is not yours.)

---

### Task B6: The PS2 player renders at the selected mode and the title names it

**Files:**
- Modify: `C:\dev\helworks\helengine-ps2\builder\player\Ps2PlayerAdapter.cs` (`ApplyPlaySettings`; delete `FramebufferWidth/Height`)
- Modify: `engine/helengine.player/PlayerWindowTitle.cs` (new `FormatWithVideoMode`)
- Modify: `engine/helengine.player/PlayerWindow.cs` (constructor takes the title)
- Modify: `engine/helengine.player/PlayerHost.cs` (choose the title)
- Test: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2PlayerAdapterTests.cs`, `engine/helengine.player.tests/PlayerWindowTitleTests.cs`

**Interfaces:**
- Consumes: `PlayerPlaySettings.VideoMode` (B5)
- Produces:
  - `PlayerWindowTitle.FormatWithVideoMode(string platformName, PlatformVideoMode videoMode, string sceneId): string` (`helengine player [PlayStation 2 · 320×224p] — scene`)
  - `PlayerWindow(string title, int clientWidth, int clientHeight)`

- [ ] **Step 1: Write the failing tests**

Replace `ApplyPlaySettings_DeclaresThePs2FramebufferSize` in `Ps2PlayerAdapterTests.cs` with:

```csharp
    /// <summary>The PS2 adapter renders at the selected video mode's framebuffer size.</summary>
    [Theory]
    [InlineData("ntsc-640x448i", 640, 448)]
    [InlineData("ntsc-320x224p", 320, 224)]
    [InlineData("pal-512x256p", 512, 256)]
    public void ApplyPlaySettings_RendersAtTheVideoModeSize(string modeId, int width, int height) {
        PlatformVideoMode mode = Ps2VideoModes.CreateCapability().Resolve(modeId, "ps2");
        PlayerPlaySettings settings = new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, false, mode.PlayerFrameRate, new Dictionary<string, string> { ["video-mode"] = modeId }, mode);

        PlayerRenderSize size = new Ps2PlayerAdapter().ApplyPlaySettings(settings);

        Assert.Equal(width, size.Width);
        Assert.Equal(height, size.Height);
    }

    /// <summary>Without a resolved video mode the PS2 adapter refuses to guess a size.</summary>
    [Fact]
    public void ApplyPlaySettings_WithoutVideoMode_Throws() {
        PlayerPlaySettings settings = new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, false, 60d, new Dictionary<string, string>(), null);

        Assert.Throws<InvalidOperationException>(() => new Ps2PlayerAdapter().ApplyPlaySettings(settings));
    }
```

Append to `PlayerWindowTitleTests.cs`:

```csharp
        /// <summary>The title names the platform and the video mode in the brackets.</summary>
        [Fact]
        public void FormatWithVideoMode_NamesPlatformAndMode() {
            PlatformVideoMode mode = new PlatformVideoMode("ntsc-320x224p", 320, 224, true, 59.94d, 60d);

            Assert.Equal("helengine player [PlayStation 2 · 320×224p] — DemoDiscMainMenu", PlayerWindowTitle.FormatWithVideoMode("PlayStation 2", mode, "DemoDiscMainMenu"));
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `.\dotnetw.cmd test engine\helengine.player.tests\helengine.player.tests.csproj --filter "FullyQualifiedName~PlayerWindowTitleTests"`
Expected: build FAIL (`FormatWithVideoMode` not found).

- [ ] **Step 3: Implement**

`Ps2PlayerAdapter.ApplyPlaySettings`:

```csharp
    /// <summary>
    /// Renders Play at the selected PS2 video mode's framebuffer size, as the GS does before the PCRTC magnifies it.
    /// </summary>
    /// <param name="playSettings">Resolved Play settings; must carry the PS2 video mode.</param>
    /// <returns>The mode's framebuffer size.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no video mode was resolved.</exception>
    public PlayerRenderSize ApplyPlaySettings(PlayerPlaySettings playSettings) {
        if (playSettings == null) {
            throw new ArgumentNullException(nameof(playSettings));
        } else if (!playSettings.HasVideoMode) {
            throw new InvalidOperationException("PS2 Play needs the resolved PS2 video mode; the editor sent none.");
        }

        return new PlayerRenderSize(playSettings.VideoMode.Width, playSettings.VideoMode.Height);
    }
```

`PlayerWindowTitle.FormatWithVideoMode`:

```csharp
        /// <summary>
        /// Formats the title for a platform with video modes, for example
        /// <c>helengine player [PlayStation 2 · 320×224p] — DemoDiscMainMenu</c>.
        /// </summary>
        /// <param name="platformName">Display name of the platform being played.</param>
        /// <param name="videoMode">Resolved video mode.</param>
        /// <param name="sceneId">Stable id of the scene being played.</param>
        /// <returns>The window title text.</returns>
        /// <exception cref="ArgumentException">The platform name or scene id is blank.</exception>
        /// <exception cref="ArgumentNullException">The video mode is null.</exception>
        public static string FormatWithVideoMode(string platformName, PlatformVideoMode videoMode, string sceneId) {
            if (string.IsNullOrWhiteSpace(platformName)) {
                throw new ArgumentException("Platform name must be provided.", nameof(platformName));
            } else if (videoMode == null) {
                throw new ArgumentNullException(nameof(videoMode));
            } else if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            return "helengine player [" + platformName + " · " + videoMode.Label + "] — " + sceneId;
        }
```

`PlayerWindow`: the constructor becomes `PlayerWindow(string title, int clientWidth, int clientHeight)`. It throws `ArgumentException` for a blank title and sets `Text = title`; update the XML docs. In `PlayerHost.Run`:

```csharp
                string title = playSettings.HasVideoMode
                    ? PlayerWindowTitle.FormatWithVideoMode(options.PlatformName, playSettings.VideoMode, options.SceneId)
                    : PlayerWindowTitle.Format(options.PlatformName, options.SceneId);
                window = new PlayerWindow(title, renderSize.Width, renderSize.Height);
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `.\dotnetw.cmd test engine\helengine.player.tests\helengine.player.tests.csproj` and `C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Ps2PlayerAdapterTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
cd /c/dev/helworks/helengine
git add engine/helengine.player/PlayerWindowTitle.cs engine/helengine.player/PlayerWindow.cs engine/helengine.player/PlayerHost.cs engine/helengine.player.tests/PlayerWindowTitleTests.cs
git commit -m "feat(player): name the console video mode in the Play window title

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
cd /c/dev/helworks/helengine-ps2
git add builder/player/Ps2PlayerAdapter.cs builder.tests/Ps2PlayerAdapterTests.cs
git commit -m "feat(ps2): render Play at the selected PS2 video mode

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task B7: The PS2 builder writes the video-mode boot record

**Files:**
- Create: `C:\dev\helworks\helengine-ps2\builder\Ps2VideoModeBootRecord.cs`, `builder\Ps2VideoModeBootRecordContents.cs`, `builder\Ps2VideoModeBootRecordWriter.cs`
- Modify: `C:\dev\helworks\helengine-ps2\builder\Ps2PlatformAssetBuilder.cs:105-123` (write the record after staging, before the assets-only return)
- Create (test helper): `C:\dev\helworks\helengine-ps2\builder.tests\Ps2TestGraphicsSelection.cs`
- Modify (fixtures that reach a successful `BuildAsync`): `builder.tests\Ps2AssetsOnlyBuildTests.cs:351-355`, `builder.tests\Ps2PlatformAssetBuilderTests.cs` (the ten `selectedGraphicsOptionValues: new Dictionary<string, string>()` sites at 969, 1112, 1302, 1482, 1635, 1761, 1888, 2014, 2131, 2254, plus the request built at 2968), `builder.tests\Ps2NativeTextureWorkItemTests.cs:130` if that request reaches `BuildAsync`
- Test: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2VideoModeBootRecordTests.cs` (new)

**Interfaces:**
- Consumes: `Ps2VideoModes` (B4), `Ps2DiscPathResolver.ResolveRuntimePath`
- Produces:
  - `Ps2VideoModeBootRecord.LogicalPath = "vidmode.bin"`, `RuntimePath = "cdrom0:\\VIDMODE.BIN;1"`, `Size = 16`, `CurrentVersion = 1`
  - `Ps2VideoModeBootRecord.Encode(PlatformVideoMode mode): byte[]`
  - `Ps2VideoModeBootRecord.Decode(byte[] bytes): Ps2VideoModeBootRecordContents`
  - `Ps2VideoModeBootRecordContents(bool isPal, bool isProgressive, int width, int height)`
  - `Ps2VideoModeBootRecordWriter.Write(string stagingRootPath, PlatformDefinition definition, string selectedGraphicsProfileId, IReadOnlyDictionary<string,string> selectedGraphicsOptionValues): PlatformVideoMode`
  - Builder diagnostic `PS2BUILD010` on any writer failure
- Record layout (little-endian, 16 bytes):

| Offset | Size | Field |
|---|---|---|
| 0 | 4 | magic `H` `V` `M` `R` |
| 4 | 2 | version `1` |
| 6 | 1 | region: 0 = NTSC, 1 = PAL |
| 7 | 1 | scan: 0 = interlaced, 1 = progressive |
| 8 | 2 | width |
| 10 | 2 | height |
| 12 | 4 | zero |

- [ ] **Step 1: Write the failing tests**

```csharp
using helengine.baseplatform.Definitions;
using helengine.ps2.builder;
using Xunit;

namespace helengine.ps2.builder.tests;

/// <summary>
/// Verifies the PS2 video-mode boot record: exact round trip for every mode, rejection of corrupt records, and the
/// writer's refusal of old or unknown project graphics values.
/// </summary>
public sealed class Ps2VideoModeBootRecordTests : IDisposable {
    /// <summary>Workspace-owned staging root.</summary>
    readonly string StagingRoot = Path.Combine(@"C:\dev\helworks\builds\helengine-ps2\video-mode-tests", Guid.NewGuid().ToString("N"));

    /// <summary>Creates the staging root.</summary>
    public Ps2VideoModeBootRecordTests() {
        Directory.CreateDirectory(StagingRoot);
    }

    /// <summary>Deletes the staging root.</summary>
    public void Dispose() {
        Directory.Delete(StagingRoot, true);
    }

    /// <summary>Every PS2 mode encodes to 16 bytes and decodes back to its region, scan and size.</summary>
    [Fact]
    public void EncodeThenDecode_RoundTripsEveryMode() {
        foreach (PlatformVideoMode mode in Ps2VideoModes.CreateCapability().Modes) {
            byte[] bytes = Ps2VideoModeBootRecord.Encode(mode);
            Ps2VideoModeBootRecordContents contents = Ps2VideoModeBootRecord.Decode(bytes);

            Assert.Equal(Ps2VideoModeBootRecord.Size, bytes.Length);
            Assert.Equal(Ps2VideoModes.IsPal(mode), contents.IsPal);
            Assert.Equal(mode.IsProgressive, contents.IsProgressive);
            Assert.Equal(mode.Width, contents.Width);
            Assert.Equal(mode.Height, contents.Height);
        }
    }

    /// <summary>A corrupt record is rejected: wrong length, magic, version, flag or a size no mode has.</summary>
    [Theory]
    [InlineData(0, (byte)'X')]
    [InlineData(4, (byte)2)]
    [InlineData(6, (byte)2)]
    [InlineData(7, (byte)2)]
    [InlineData(8, (byte)0x81)]
    [InlineData(12, (byte)1)]
    public void Decode_RejectsCorruptRecord(int offset, byte value) {
        byte[] bytes = Ps2VideoModeBootRecord.Encode(Ps2VideoModes.CreateCapability().Resolve("ntsc-640x448i", "ps2"));
        bytes[offset] = value;

        Assert.Throws<InvalidDataException>(() => Ps2VideoModeBootRecord.Decode(bytes));
        Assert.Throws<InvalidDataException>(() => Ps2VideoModeBootRecord.Decode(new byte[15]));
    }

    /// <summary>The record's staging path is the runtime path the native boot host opens.</summary>
    [Fact]
    public void LogicalPath_MapsToTheNativeRuntimePath() {
        Assert.Equal(Ps2VideoModeBootRecord.RuntimePath, Ps2DiscPathResolver.ResolveRuntimePath(Ps2VideoModeBootRecord.LogicalPath));
    }

    /// <summary>The writer stages the record for the selected mode.</summary>
    [Fact]
    public void Write_StagesTheSelectedMode() {
        PlatformVideoMode mode = Ps2VideoModeBootRecordWriter.Write(StagingRoot, Ps2PlatformDefinitionFactory.Create(), Ps2TestGraphicsSelection.ProfileId, Ps2TestGraphicsSelection.CreateOptionValues("ntsc-320x224p"));

        Assert.Equal("ntsc-320x224p", mode.Id);
        Ps2VideoModeBootRecordContents contents = Ps2VideoModeBootRecord.Decode(File.ReadAllBytes(Path.Combine(StagingRoot, Ps2VideoModeBootRecord.LogicalPath)));
        Assert.Equal(320, contents.Width);
        Assert.True(contents.IsProgressive);
    }

    /// <summary>An old project still holding default-width/default-height fails naming the field and the settings file.</summary>
    [Fact]
    public void Write_WhenOldSizeFieldsPresent_FailsNamingFieldAndFile() {
        Dictionary<string, string> values = Ps2TestGraphicsSelection.CreateOptionValues("ntsc-640x448i");
        values["default-width"] = "640";

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Ps2VideoModeBootRecordWriter.Write(StagingRoot, Ps2PlatformDefinitionFactory.Create(), Ps2TestGraphicsSelection.ProfileId, values));

        Assert.Contains("default-width", exception.Message);
        Assert.Contains("settings/platform.ps2.json", exception.Message);
    }

    /// <summary>A missing mode fails naming the field; an unknown one fails naming the value.</summary>
    [Fact]
    public void Write_WhenModeMissingOrUnknown_Fails() {
        Dictionary<string, string> missing = Ps2TestGraphicsSelection.CreateOptionValues("ntsc-640x448i");
        missing.Remove("video-mode");
        Dictionary<string, string> unknown = Ps2TestGraphicsSelection.CreateOptionValues("ntsc-640x480p");

        Assert.Contains("video-mode", Assert.Throws<InvalidOperationException>(() => Ps2VideoModeBootRecordWriter.Write(StagingRoot, Ps2PlatformDefinitionFactory.Create(), Ps2TestGraphicsSelection.ProfileId, missing)).Message);
        Assert.Contains("ntsc-640x480p", Assert.Throws<InvalidOperationException>(() => Ps2VideoModeBootRecordWriter.Write(StagingRoot, Ps2PlatformDefinitionFactory.Create(), Ps2TestGraphicsSelection.ProfileId, unknown)).Message);
    }
}
```

`builder.tests\Ps2TestGraphicsSelection.cs`:

```csharp
namespace helengine.ps2.builder.tests;

/// <summary>
/// The PS2 graphics selection test builds pass: the standard forward profile with every declared option set.
/// </summary>
public static class Ps2TestGraphicsSelection {
    /// <summary>Graphics profile id used by test builds.</summary>
    public const string ProfileId = "ps2-standard-forward";

    /// <summary>
    /// Creates a complete set of graphics option values for the standard forward profile.
    /// </summary>
    /// <param name="videoModeId">Value of the video-mode option.</param>
    /// <returns>New option values.</returns>
    public static Dictionary<string, string> CreateOptionValues(string videoModeId) {
        return new Dictionary<string, string> {
            ["video-mode"] = videoModeId,
            ["vsync-enabled"] = "true",
            ["fullscreen-enabled"] = "false",
            ["depth-handler-mode"] = "hardware"
        };
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Ps2VideoModeBootRecordTests"`
Expected: build FAIL (`Ps2VideoModeBootRecord` not found).

- [ ] **Step 3: Implement the record**

`builder\Ps2VideoModeBootRecordContents.cs`:

```csharp
namespace helengine.ps2.builder;

/// <summary>
/// The decoded fields of one PS2 video-mode boot record.
/// </summary>
public sealed class Ps2VideoModeBootRecordContents {
    /// <summary>
    /// Initializes the decoded fields.
    /// </summary>
    /// <param name="isPal">True for PAL output, false for NTSC.</param>
    /// <param name="isProgressive">True for progressive scan, false for interlaced.</param>
    /// <param name="width">Framebuffer width.</param>
    /// <param name="height">Framebuffer height.</param>
    public Ps2VideoModeBootRecordContents(bool isPal, bool isProgressive, int width, int height) {
        IsPal = isPal;
        IsProgressive = isProgressive;
        Width = width;
        Height = height;
    }

    /// <summary>Gets whether the output is PAL.</summary>
    public bool IsPal { get; }

    /// <summary>Gets whether the scan is progressive.</summary>
    public bool IsProgressive { get; }

    /// <summary>Gets the framebuffer width.</summary>
    public int Width { get; }

    /// <summary>Gets the framebuffer height.</summary>
    public int Height { get; }
}
```

`builder\Ps2VideoModeBootRecord.cs`:

```csharp
using System.Buffers.Binary;
using helengine.baseplatform.Definitions;

namespace helengine.ps2.builder;

/// <summary>
/// The small versioned file the PS2 builder stages at the disc root so the native boot host initialises gsKit with the
/// project's video mode before the first frame. Layout (little-endian, 16 bytes): magic <c>HVMR</c>, u16 version 1,
/// u8 region (0 NTSC, 1 PAL), u8 scan (0 interlaced, 1 progressive), u16 width, u16 height, 4 zero bytes. The native
/// reader is <c>src/platform/ps2/Ps2VideoModeBootRecordReader.cpp</c>; both sides accept only the 12 PS2 modes.
/// </summary>
public static class Ps2VideoModeBootRecord {
    /// <summary>Staging-root-relative logical path of the record.</summary>
    public const string LogicalPath = "vidmode.bin";

    /// <summary>Path the native boot host opens (must equal the disc path the resolver gives <see cref="LogicalPath"/>).</summary>
    public const string RuntimePath = "cdrom0:\\VIDMODE.BIN;1";

    /// <summary>Record size in bytes.</summary>
    public const int Size = 16;

    /// <summary>The only record version this builder writes and the runtime reads.</summary>
    public const ushort CurrentVersion = 1;

    /// <summary>Magic bytes at offset 0.</summary>
    static readonly byte[] Magic = [(byte)'H', (byte)'V', (byte)'M', (byte)'R'];

    /// <summary>
    /// Encodes one PS2 mode.
    /// </summary>
    /// <param name="mode">PS2 video mode.</param>
    /// <returns>The 16-byte record.</returns>
    public static byte[] Encode(PlatformVideoMode mode) {
        if (mode == null) {
            throw new ArgumentNullException(nameof(mode));
        }

        byte[] bytes = new byte[Size];
        Magic.CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), CurrentVersion);
        bytes[6] = Ps2VideoModes.IsPal(mode) ? (byte)1 : (byte)0;
        bytes[7] = mode.IsProgressive ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), checked((ushort)mode.Width));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), checked((ushort)mode.Height));
        return bytes;
    }

    /// <summary>
    /// Decodes and validates one record.
    /// </summary>
    /// <param name="bytes">Record bytes.</param>
    /// <returns>The decoded fields.</returns>
    /// <exception cref="InvalidDataException">Thrown for a wrong length, magic, version, flag, padding or a size no PS2 mode has.</exception>
    public static Ps2VideoModeBootRecordContents Decode(byte[] bytes) {
        if (bytes == null || bytes.Length != Size) {
            throw new InvalidDataException($"A PS2 video-mode boot record is exactly {Size} bytes.");
        } else if (!bytes.AsSpan(0, 4).SequenceEqual(Magic)) {
            throw new InvalidDataException("The PS2 video-mode boot record has the wrong magic.");
        } else if (BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4)) != CurrentVersion) {
            throw new InvalidDataException($"The PS2 video-mode boot record is not version {CurrentVersion}.");
        } else if (bytes[6] > 1 || bytes[7] > 1) {
            throw new InvalidDataException("The PS2 video-mode boot record has an invalid region or scan flag.");
        } else if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12)) != 0u) {
            throw new InvalidDataException("The PS2 video-mode boot record's reserved bytes are not zero.");
        }

        Ps2VideoModeBootRecordContents contents = new Ps2VideoModeBootRecordContents(
            bytes[6] == 1,
            bytes[7] == 1,
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8)),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(10)));
        foreach (PlatformVideoMode mode in Ps2VideoModes.CreateCapability().Modes) {
            if (Ps2VideoModes.IsPal(mode) == contents.IsPal && mode.IsProgressive == contents.IsProgressive && mode.Width == contents.Width && mode.Height == contents.Height) {
                return contents;
            }
        }

        throw new InvalidDataException($"The PS2 video-mode boot record names {contents.Width}x{contents.Height}, which no PS2 mode produces.");
    }
}
```

`builder\Ps2VideoModeBootRecordWriter.cs`:

```csharp
using helengine.baseplatform.Definitions;

namespace helengine.ps2.builder;

/// <summary>
/// Resolves the project's PS2 video mode from the build's graphics selection and stages the boot record. Graphics option
/// ids the selected profile does not declare (such as the removed default-width/default-height) are refused, so an old
/// project fails loudly instead of booting at 640x448.
/// </summary>
public static class Ps2VideoModeBootRecordWriter {
    /// <summary>Project file the graphics options come from, named in failures.</summary>
    const string SettingsFile = "settings/platform.ps2.json";

    /// <summary>
    /// Validates the graphics selection, resolves the mode and writes the record at the staging root.
    /// </summary>
    /// <param name="stagingRootPath">PS2 staging root.</param>
    /// <param name="definition">PS2 platform definition.</param>
    /// <param name="selectedGraphicsProfileId">Selected graphics profile.</param>
    /// <param name="selectedGraphicsOptionValues">Selected graphics option values.</param>
    /// <returns>The resolved mode.</returns>
    /// <exception cref="InvalidOperationException">Thrown for an unknown profile, an undeclared option, or a missing or unknown mode.</exception>
    public static PlatformVideoMode Write(string stagingRootPath, PlatformDefinition definition, string selectedGraphicsProfileId, IReadOnlyDictionary<string, string> selectedGraphicsOptionValues) {
        if (string.IsNullOrWhiteSpace(stagingRootPath)) {
            throw new ArgumentException("The PS2 staging root is required.", nameof(stagingRootPath));
        } else if (definition == null) {
            throw new ArgumentNullException(nameof(definition));
        } else if (selectedGraphicsOptionValues == null) {
            throw new InvalidOperationException($"The PS2 build received no graphics options; select a graphics profile and video mode in {SettingsFile}.");
        }

        PlatformGraphicsProfileDefinition profile = Array.Find(definition.GraphicsProfiles, candidate => string.Equals(candidate.ProfileId, selectedGraphicsProfileId, StringComparison.Ordinal));
        if (profile == null) {
            throw new InvalidOperationException($"PS2 graphics profile '{selectedGraphicsProfileId}' does not exist; fix graphics.selectedGraphicsProfileId in {SettingsFile}.");
        }

        foreach (string optionId in selectedGraphicsOptionValues.Keys) {
            if (!Array.Exists(profile.Settings, setting => string.Equals(setting.SettingId, optionId, StringComparison.Ordinal))) {
                throw new InvalidOperationException($"Graphics option '{optionId}' is not declared by PS2 graphics profile '{profile.ProfileId}'; remove it from graphics.selectedOptionValues in {SettingsFile}.");
            }
        }

        if (!selectedGraphicsOptionValues.TryGetValue(Ps2VideoModes.SettingId, out string modeId) || string.IsNullOrWhiteSpace(modeId)) {
            throw new InvalidOperationException($"No '{Ps2VideoModes.SettingId}' is selected in graphics.selectedOptionValues of {SettingsFile}.");
        }

        PlatformVideoMode mode = definition.VideoModeCapability.Resolve(modeId, definition.PlatformId);
        Directory.CreateDirectory(stagingRootPath);
        File.WriteAllBytes(Path.Combine(stagingRootPath, Ps2VideoModeBootRecord.LogicalPath), Ps2VideoModeBootRecord.Encode(mode));
        return mode;
    }
}
```

- [ ] **Step 4: Write the record from the builder**

In `Ps2PlatformAssetBuilder.BuildAsync`, insert this between the staging `try/catch` and `if (request.ExecutionScope == PlatformBuildExecutionScope.AssetsOnly)`:

```csharp
        if (diagnostics.Count == 0) {
            try {
                PlatformVideoMode videoMode = Ps2VideoModeBootRecordWriter.Write(
                    Path.Combine(request.WorkingRoot, "ps2-staging"),
                    Definition,
                    request.SelectedGraphicsProfileId,
                    request.SelectedGraphicsOptionValues);
                WritePhaseMarker(phaseMarkerPath, "video mode boot record written: " + videoMode.Id);
            } catch (InvalidOperationException exception) {
                AddDiagnostic(diagnostics, diagnosticReporter, PlatformBuildDiagnosticSeverity.Error, "PS2BUILD010",
                    exception.Message, string.Empty, string.Empty, request.WorkingRoot);
            }
        }
```

(The catch turns a refused project value into the build's error diagnostic, as the existing `PS2BUILD003` path does. The failure is still reported, not masked.)

- [ ] **Step 5: Update the build-request fixtures**

In each fixture listed under **Files**, set `selectedGraphicsProfileId: Ps2TestGraphicsSelection.ProfileId` and `selectedGraphicsOptionValues: Ps2TestGraphicsSelection.CreateOptionValues("ntsc-640x448i")`. A fixture that uses the 5-argument `PlatformBuildRequest` constructor and reaches `BuildAsync` must switch to the named-argument constructor used in `Ps2AssetsOnlyBuildTests.cs:337-360`.

- [ ] **Step 6: Run tests to verify they pass**

Run: `C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj`
Expected: PASS, apart from failures that were already red before this task. Record those failure names before Step 1, compare by name, and fix any new one.

If `LogicalPath_MapsToTheNativeRuntimePath` fails because the resolver renames `vidmode.bin`, pick an 8.3 lowercase logical name that the resolver maps to itself. Use it for both `LogicalPath` and `RuntimePath` here and in Task B8's `RuntimePath`.

- [ ] **Step 7: Commit**

```bash
cd /c/dev/helworks/helengine-ps2
git add builder/Ps2VideoModeBootRecord.cs builder/Ps2VideoModeBootRecordContents.cs builder/Ps2VideoModeBootRecordWriter.cs builder/Ps2PlatformAssetBuilder.cs builder.tests/Ps2VideoModeBootRecordTests.cs builder.tests/Ps2TestGraphicsSelection.cs builder.tests/Ps2AssetsOnlyBuildTests.cs builder.tests/Ps2PlatformAssetBuilderTests.cs builder.tests/Ps2NativeTextureWorkItemTests.cs
git diff --cached --numstat
git commit -m "feat(ps2): stage a video-mode boot record from the project's graphics settings

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task B8: Native boot-record reader and gsKit mapping

**Files:**
- Create: `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2VideoModeBootRecordReader.hpp`, `src\platform\ps2\Ps2VideoModeBootRecordReader.cpp`
- Create: `C:\dev\helworks\helengine-ps2\tests\native\Ps2VideoModeBootRecordTests.cpp`
- Modify: `C:\dev\helworks\helengine-ps2\Makefile`. Add the reader to `PS2_SOURCES` (lines 33-61). Add the `ps2-video-mode-tests` target next to `ps2-rendering-tests` (lines 127-133 and 200-205). Exempt the new target in the guard at line 17 and in `.PHONY` at line 184.

**Interfaces:**
- Consumes: the record layout from Task B7
- Produces:
  - `helengine::ps2::Ps2VideoModeBootRecord { bool IsPal; bool IsProgressive; std::uint16_t Width; std::uint16_t Height; }`
  - `helengine::ps2::Ps2GsVideoSettings { bool IsPal; bool IsInterlaced; bool IsFrameMode; int Width; int Height; }`
  - `Ps2VideoModeBootRecordReader::TryParse(const std::uint8_t* bytes, std::size_t length, Ps2VideoModeBootRecord& record, const char*& error): bool`
  - `Ps2VideoModeBootRecordReader::ResolveGsSettings(const Ps2VideoModeBootRecord& record): Ps2GsVideoSettings`
  - `Ps2VideoModeBootRecordReader::RuntimePath = "cdrom0:\\VIDMODE.BIN;1"`, `RecordSize = 16`

- [ ] **Step 1: Confirm gsKit can produce each mode**

Read gsKit's screen setup in the `helengine-ps2` image. It is usually `gsKit/ee/gs/src/gsInit.c`, `gsKit_init_screen` and its `gsKit_set_display_offset` / MAGH logic: `docker run --rm helengine-ps2 sh -c "find / -name gsInit.c 2>/dev/null | head -1 | xargs sed -n '1,400p'" | head -c 20000`. For each mode, check two things. First, that `Width` divides the NTSC/PAL 2560-wide pixel clock into an integer MAGH (2560/640=4, 2560/512=5, 2560/320=8, 2560/256=10). Second, that `Interlace=GS_NONINTERLACED`, `Field=GS_FRAME` with Height 224/256 are standard progressive setups. If gsKit cannot produce a mode with its standard init, remove that mode in every place: the `Ps2VideoModes.CreateCapability` list, the B4 tests, the B7 round-trip test, the native table below and the constraint list at the top of this plan. Tell Helena which mode was removed. Do not patch gsKit.

- [ ] **Step 2: Write the failing native test**

`tests/native/Ps2VideoModeBootRecordTests.cpp`:

```cpp
#include "platform/ps2/Ps2VideoModeBootRecordReader.hpp"

#include <array>
#include <cstdint>
#include <cstring>
#include <iostream>

namespace {
    /// Counts failed checks so main can return non-zero.
    int FailureCount = 0;

    /// Records one failed check with a readable message.
    void Check(bool condition, const char* message) {
        if (!condition) {
            ++FailureCount;
            std::cerr << "FAIL: " << message << "\n";
        }
    }

    /// Builds a record exactly as the C# Ps2VideoModeBootRecord.Encode does.
    std::array<std::uint8_t, 16> MakeRecord(bool isPal, bool isProgressive, std::uint16_t width, std::uint16_t height) {
        std::array<std::uint8_t, 16> bytes{};
        bytes[0] = 'H'; bytes[1] = 'V'; bytes[2] = 'M'; bytes[3] = 'R';
        bytes[4] = 1; bytes[5] = 0;
        bytes[6] = isPal ? 1 : 0;
        bytes[7] = isProgressive ? 1 : 0;
        bytes[8] = static_cast<std::uint8_t>(width & 0xFF); bytes[9] = static_cast<std::uint8_t>(width >> 8);
        bytes[10] = static_cast<std::uint8_t>(height & 0xFF); bytes[11] = static_cast<std::uint8_t>(height >> 8);
        return bytes;
    }

    /// One PS2 mode and the gsKit fields it must produce.
    struct ExpectedMode {
        bool IsPal;
        bool IsProgressive;
        std::uint16_t Width;
        std::uint16_t Height;
    };
}

int main() {
    const ExpectedMode modes[] = {
        { false, false, 640, 448 }, { false, false, 512, 448 }, { false, true, 640, 224 },
        { false, true, 512, 224 }, { false, true, 320, 224 }, { false, true, 256, 224 },
        { true, false, 640, 512 }, { true, false, 512, 512 }, { true, true, 640, 256 },
        { true, true, 512, 256 }, { true, true, 320, 256 }, { true, true, 256, 256 }
    };

    for (const ExpectedMode& mode : modes) {
        std::array<std::uint8_t, 16> bytes = MakeRecord(mode.IsPal, mode.IsProgressive, mode.Width, mode.Height);
        helengine::ps2::Ps2VideoModeBootRecord record{};
        const char* error = nullptr;
        bool parsed = helengine::ps2::Ps2VideoModeBootRecordReader::TryParse(bytes.data(), bytes.size(), record, error);
        Check(parsed, "every PS2 mode parses");
        helengine::ps2::Ps2GsVideoSettings settings = helengine::ps2::Ps2VideoModeBootRecordReader::ResolveGsSettings(record);
        Check(settings.IsPal == mode.IsPal, "region maps to GS_MODE_PAL/NTSC");
        Check(settings.IsInterlaced == !mode.IsProgressive, "scan maps to GS_INTERLACED/NONINTERLACED");
        Check(settings.IsFrameMode == mode.IsProgressive, "progressive uses GS_FRAME, interlaced GS_FIELD");
        Check(settings.Width == mode.Width && settings.Height == mode.Height, "size maps to GsGlobal Width/Height");
    }

    helengine::ps2::Ps2VideoModeBootRecord record{};
    const char* error = nullptr;
    std::array<std::uint8_t, 16> valid = MakeRecord(false, false, 640, 448);

    Check(!helengine::ps2::Ps2VideoModeBootRecordReader::TryParse(valid.data(), 15, record, error), "short record rejected");
    Check(!helengine::ps2::Ps2VideoModeBootRecordReader::TryParse(valid.data(), 17, record, error), "long record rejected");
    std::array<std::uint8_t, 16> badMagic = valid; badMagic[0] = 'X';
    Check(!helengine::ps2::Ps2VideoModeBootRecordReader::TryParse(badMagic.data(), 16, record, error), "bad magic rejected");
    std::array<std::uint8_t, 16> badVersion = valid; badVersion[4] = 2;
    Check(!helengine::ps2::Ps2VideoModeBootRecordReader::TryParse(badVersion.data(), 16, record, error), "other version rejected");
    std::array<std::uint8_t, 16> badSize = MakeRecord(false, true, 640, 480);
    Check(!helengine::ps2::Ps2VideoModeBootRecordReader::TryParse(badSize.data(), 16, record, error), "640x480 rejected");
    std::array<std::uint8_t, 16> wrongRegion = MakeRecord(true, false, 640, 448);
    Check(!helengine::ps2::Ps2VideoModeBootRecordReader::TryParse(wrongRegion.data(), 16, record, error), "PAL 448 rejected");
    std::array<std::uint8_t, 16> badPadding = valid; badPadding[12] = 1;
    Check(!helengine::ps2::Ps2VideoModeBootRecordReader::TryParse(badPadding.data(), 16, record, error), "non-zero padding rejected");
    Check(error != nullptr && std::strlen(error) > 0, "rejection carries a diagnostic");

    if (FailureCount == 0) {
        std::cout << "ps2-video-mode-tests: all checks passed\n";
    }
    return FailureCount == 0 ? 0 : 1;
}
```

In `Makefile`, after the `PS2_RENDERING_TEST_SOURCES` block add:

```make
PS2_VIDEO_MODE_TEST_TARGET := $(BUILD_DIR)/tests/ps2-video-mode-tests
PS2_VIDEO_MODE_TEST_SOURCES := \
	$(SOURCE_DIR)/platform/ps2/Ps2VideoModeBootRecordReader.cpp \
	tests/native/Ps2VideoModeBootRecordTests.cpp
```

After the `ps2-rendering-tests` rules add (tabs, not spaces):

```make
ps2-video-mode-tests: $(PS2_VIDEO_MODE_TEST_TARGET)
	$(PS2_VIDEO_MODE_TEST_TARGET)

$(PS2_VIDEO_MODE_TEST_TARGET): $(PS2_VIDEO_MODE_TEST_SOURCES)
	@mkdir -p $(dir $@)
	$(HOST_CXX) $(HOST_CXXFLAGS) -I$(SOURCE_DIR) $^ -o $@
```

Change line 17 to `ifneq ($(strip $(filter-out clean ps2-rendering-tests ps2-video-mode-tests,$(MAKECMDGOALS))),)` and add `ps2-video-mode-tests` to the `.PHONY` list on line 184.

- [ ] **Step 3: Run the native test to verify it fails**

Run (PowerShell): `docker run --rm -v "C:\dev\helworks\helengine-ps2:/workspace:ro" -v "C:\dev\helworks\builds\helengine-ps2\native-tests\video-mode-red:/build-output" -w /workspace helengine-ps2 make ps2-video-mode-tests BUILD_DIR=/build-output`
Expected: FAIL. The reader sources do not exist yet (`No rule to make target ... Ps2VideoModeBootRecordReader.cpp`).

- [ ] **Step 4: Implement the reader**

`src/platform/ps2/Ps2VideoModeBootRecordReader.hpp`:

```cpp
#pragma once

#include <cstddef>
#include <cstdint>

namespace helengine::ps2 {
    /// <summary>
    /// The video mode the PS2 builder staged in the boot record: output region, scan and framebuffer size.
    /// </summary>
    struct Ps2VideoModeBootRecord {
        /// <summary>True for PAL output, false for NTSC.</summary>
        bool IsPal;
        /// <summary>True for progressive scan, false for interlaced.</summary>
        bool IsProgressive;
        /// <summary>Framebuffer width in pixels.</summary>
        std::uint16_t Width;
        /// <summary>Framebuffer height in pixels.</summary>
        std::uint16_t Height;
    };

    /// <summary>
    /// The gsKit screen fields for one mode, as plain values so host tests need no gsKit headers. The boot host maps them
    /// to GS_MODE_PAL/NTSC, GS_INTERLACED/NONINTERLACED and GS_FRAME/FIELD.
    /// </summary>
    struct Ps2GsVideoSettings {
        /// <summary>True selects GS_MODE_PAL, false GS_MODE_NTSC.</summary>
        bool IsPal;
        /// <summary>True selects GS_INTERLACED, false GS_NONINTERLACED.</summary>
        bool IsInterlaced;
        /// <summary>True selects GS_FRAME, false GS_FIELD.</summary>
        bool IsFrameMode;
        /// <summary>GsGlobal Width.</summary>
        int Width;
        /// <summary>GsGlobal Height.</summary>
        int Height;
    };

    /// <summary>
    /// Reads the 16-byte video-mode boot record written by the C# Ps2VideoModeBootRecord (magic HVMR, version 1) and maps
    /// it to gsKit screen fields. Only the 12 PS2 modes are accepted; anything else is rejected with a diagnostic so the
    /// boot host halts instead of falling back to 640x448.
    /// </summary>
    class Ps2VideoModeBootRecordReader {
    public:
        /// <summary>Exact record size in bytes.</summary>
        static constexpr std::size_t RecordSize = 16;

        /// <summary>The only record version this runtime reads.</summary>
        static constexpr std::uint16_t CurrentVersion = 1;

        /// <summary>Disc path of the record (matches the builder's Ps2VideoModeBootRecord.RuntimePath).</summary>
        static constexpr const char* RuntimePath = "cdrom0:\\VIDMODE.BIN;1";

        /// <summary>
        /// Parses and validates one record.
        /// </summary>
        /// <param name="bytes">Record bytes.</param>
        /// <param name="length">Number of bytes read (a longer file is rejected).</param>
        /// <param name="record">Receives the decoded mode on success.</param>
        /// <param name="error">Receives a static diagnostic on failure.</param>
        /// <returns>True when the record names one of the 12 PS2 modes.</returns>
        static bool TryParse(const std::uint8_t* bytes, std::size_t length, Ps2VideoModeBootRecord& record, const char*& error);

        /// <summary>
        /// Maps a validated record to gsKit screen fields: interlaced modes use field rendering, progressive ones frame
        /// rendering, as today's 640x448 setup does.
        /// </summary>
        /// <param name="record">Validated record.</param>
        /// <returns>gsKit screen fields.</returns>
        static Ps2GsVideoSettings ResolveGsSettings(const Ps2VideoModeBootRecord& record);
    };
}
```

`src/platform/ps2/Ps2VideoModeBootRecordReader.cpp`:

```cpp
#include "platform/ps2/Ps2VideoModeBootRecordReader.hpp"

namespace helengine::ps2 {
    namespace {
        /// <summary>One accepted PS2 mode.</summary>
        struct AcceptedMode {
            /// <summary>PAL output.</summary>
            bool IsPal;
            /// <summary>Progressive scan.</summary>
            bool IsProgressive;
            /// <summary>Framebuffer width.</summary>
            std::uint16_t Width;
            /// <summary>Framebuffer height.</summary>
            std::uint16_t Height;
        };

        /// <summary>The 12 PS2 modes, matching Ps2VideoModes.CreateCapability in the builder.</summary>
        constexpr AcceptedMode AcceptedModes[] = {
            { false, false, 640, 448 }, { false, false, 512, 448 }, { false, true, 640, 224 },
            { false, true, 512, 224 }, { false, true, 320, 224 }, { false, true, 256, 224 },
            { true, false, 640, 512 }, { true, false, 512, 512 }, { true, true, 640, 256 },
            { true, true, 512, 256 }, { true, true, 320, 256 }, { true, true, 256, 256 }
        };

        /// <summary>Reads a little-endian 16-bit value.</summary>
        std::uint16_t ReadUInt16(const std::uint8_t* bytes) {
            return static_cast<std::uint16_t>(bytes[0] | (bytes[1] << 8));
        }
    }

    bool Ps2VideoModeBootRecordReader::TryParse(const std::uint8_t* bytes, std::size_t length, Ps2VideoModeBootRecord& record, const char*& error) {
        if (bytes == nullptr || length != RecordSize) {
            error = "video mode boot record is not 16 bytes";
            return false;
        } else if (bytes[0] != 'H' || bytes[1] != 'V' || bytes[2] != 'M' || bytes[3] != 'R') {
            error = "video mode boot record has the wrong magic";
            return false;
        } else if (ReadUInt16(bytes + 4) != CurrentVersion) {
            error = "video mode boot record is not version 1";
            return false;
        } else if (bytes[6] > 1 || bytes[7] > 1) {
            error = "video mode boot record has an invalid region or scan flag";
            return false;
        } else if (bytes[12] != 0 || bytes[13] != 0 || bytes[14] != 0 || bytes[15] != 0) {
            error = "video mode boot record reserved bytes are not zero";
            return false;
        }

        Ps2VideoModeBootRecord candidate{ bytes[6] == 1, bytes[7] == 1, ReadUInt16(bytes + 8), ReadUInt16(bytes + 10) };
        for (const AcceptedMode& mode : AcceptedModes) {
            if (mode.IsPal == candidate.IsPal && mode.IsProgressive == candidate.IsProgressive
                && mode.Width == candidate.Width && mode.Height == candidate.Height) {
                record = candidate;
                error = nullptr;
                return true;
            }
        }

        error = "video mode boot record names a size no PS2 mode produces";
        return false;
    }

    Ps2GsVideoSettings Ps2VideoModeBootRecordReader::ResolveGsSettings(const Ps2VideoModeBootRecord& record) {
        return Ps2GsVideoSettings{
            record.IsPal,
            !record.IsProgressive,
            record.IsProgressive,
            static_cast<int>(record.Width),
            static_cast<int>(record.Height)
        };
    }
}
```

Add `$(SOURCE_DIR)/platform/ps2/Ps2VideoModeBootRecordReader.cpp \` to `PS2_SOURCES` after `Ps2NativeFileSupport.cpp`.

- [ ] **Step 5: Run the native test to verify it passes**

Run: `docker run --rm -v "C:\dev\helworks\helengine-ps2:/workspace:ro" -v "C:\dev\helworks\builds\helengine-ps2\native-tests\video-mode-green:/build-output" -w /workspace helengine-ps2 make ps2-video-mode-tests BUILD_DIR=/build-output`
Expected: `ps2-video-mode-tests: all checks passed`, exit 0.

- [ ] **Step 6: Commit**

```bash
cd /c/dev/helworks/helengine-ps2
git add src/platform/ps2/Ps2VideoModeBootRecordReader.hpp src/platform/ps2/Ps2VideoModeBootRecordReader.cpp tests/native/Ps2VideoModeBootRecordTests.cpp Makefile
git diff --cached --numstat
git commit -m "feat(ps2): read the video-mode boot record and map it to gsKit fields

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task B9: `Ps2BootHost` boots into the recorded mode

**Files:**
- Modify: `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2BootHost.hpp` (include, member, method)
- Modify: `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2BootHost.cpp`. Delete lines 155-156. In `Run` (3156-3166), load the record between `InitializeRuntime` and `InitializeGraphics`. In `InitializeGraphics` (3288-3294), set the gsKit fields from the record. Add `LoadVideoModeBootRecord`.
- Modify: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2NativeBuildInputsTests.cs:782-797` (delete the source-string test that pins the removed constants; see the global constraint on source-string tests)

**Interfaces:**
- Consumes: `Ps2VideoModeBootRecordReader` (B8), the B1 "replace" rows
- Produces: the boot log line `video mode: <NTSC|PAL> <W>x<H><i|p>`. When the record is missing or invalid, boot calls `FatalHalt("video mode boot record ...")`.

- [ ] **Step 1: Remove the pinned source-string test**

Delete `Boot_host_when_graphics_initialize_applies_ps2_framebuffer_defaults_before_screen_init` from `Ps2NativeBuildInputsTests.cs`. It asserts substrings of hand-written source, which the global constraints forbid, and pins exactly the lines this task replaces. Its replacement is the host-runnable native test from Task B8. Add no new source-string assertion.

- [ ] **Step 2: Add the member and loader**

In `Ps2BootHost.hpp` add `#include "platform/ps2/Ps2VideoModeBootRecordReader.hpp"`, the private method `bool LoadVideoModeBootRecord();` and the member `Ps2GsVideoSettings ActiveVideoSettings;`. In the constructor's initializer list in `Ps2BootHost.cpp`, add `ActiveVideoSettings{}` after `GsGlobal(0)`. This zero-initializes it. `InitializeGraphics` never reads the value before `LoadVideoModeBootRecord` sets it, because `Run` halts first when loading fails.

Add the method after `InitializeRuntime`:

```cpp
    bool Ps2BootHost::LoadVideoModeBootRecord() {
        const char* path = Ps2VideoModeBootRecordReader::RuntimePath;
        std::FILE* file = std::fopen(path, "rb");
        if (file == nullptr) {
            FatalHalt(std::string("video mode boot record missing: ") + path + " (rebuild the disc with the current PS2 builder)");
        }

        std::uint8_t bytes[Ps2VideoModeBootRecordReader::RecordSize + 1];
        std::size_t length = std::fread(bytes, 1, sizeof(bytes), file);
        std::fclose(file);
        Ps2VideoModeBootRecord record{};
        const char* error = nullptr;
        if (!Ps2VideoModeBootRecordReader::TryParse(bytes, length, record, error)) {
            FatalHalt(std::string("video mode boot record invalid: ") + error);
        }

        ActiveVideoSettings = Ps2VideoModeBootRecordReader::ResolveGsSettings(record);
        BootLog(std::string("video mode: ") + (ActiveVideoSettings.IsPal ? "PAL " : "NTSC ")
            + std::to_string(ActiveVideoSettings.Width) + "x" + std::to_string(ActiveVideoSettings.Height)
            + (ActiveVideoSettings.IsInterlaced ? "i" : "p"));
        return true;
    }
```

- [ ] **Step 3: Use it**

In `Run`, between the `InitializeRuntime` and `InitializeGraphics` blocks:

```cpp
            if (!LoadVideoModeBootRecord()) {
                FatalHalt("video mode boot record load returned false");
            }
```

In `InitializeGraphics`, replace the `Mode`, `Interlace`, `Field`, `Width` and `Height` assignments with the code below. Keep `Aspect = GS_ASPECT_4_3`; pixel aspect is the B1 open question. Delete `Ps2DefaultFramebufferWidth/Height` (lines 155-156).

```cpp
        GsGlobal->Mode = ActiveVideoSettings.IsPal ? GS_MODE_PAL : GS_MODE_NTSC;
        GsGlobal->Interlace = ActiveVideoSettings.IsInterlaced ? GS_INTERLACED : GS_NONINTERLACED;
        GsGlobal->Field = ActiveVideoSettings.IsFrameMode ? GS_FRAME : GS_FIELD;
        GsGlobal->Width = ActiveVideoSettings.Width;
        GsGlobal->Height = ActiveVideoSettings.Height;
```

- [ ] **Step 4: Build the PS2 runtime and the tests**

Run the native test target again (expected: pass). Run the builder tests (`C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj`); compare failure names with the list recorded in B7 and fix any new one. Then build the demodisc PS2 disc headlessly. The editor CLI build path needs Task B10's settings change, so run this step after B10 when done in order. Follow the memory notes "Editor builds use vendored codegen" and "helengine platform registry pins", writing the outputs under `C:\dev\helworks\builds\helengine-ps2\ps2\<build-id>`. Expected: build success. The phase marker file shows `video mode boot record written: ntsc-640x448i`, and the disc contains `VIDMODE.BIN`.

- [ ] **Step 5: Commit**

```bash
cd /c/dev/helworks/helengine-ps2
git add src/platform/ps2/Ps2BootHost.hpp src/platform/ps2/Ps2BootHost.cpp builder.tests/Ps2NativeBuildInputsTests.cs
git diff --cached --numstat
git commit -m "feat(ps2): boot gsKit into the project's video mode

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task B10: demodisc selects its PS2 video mode

**Files:**
- Modify: `C:\dev\helprojs\demodisc\settings\platform.ps2.json` (`graphics.selectedOptionValues`)

**Interfaces:**
- Consumes: the graphics setting `video-mode` (B4) and the builder's undeclared-option refusal (B7)

- [ ] **Step 1: Edit the project settings**

In `graphics.selectedOptionValues`, replace `"default-width": "640", "default-height": "448",` with `"video-mode": "ntsc-640x448i",`. Leave the dead top-level `graphics.defaultWidth/defaultHeight` untouched; the spec keeps them out of scope. Check `git -C C:\dev\helprojs\demodisc diff --numstat`: the change must be 2 deletions and 1 insertion, with no line-ending churn.

- [ ] **Step 2: Verify through Play (announce first)**

Ask Helena to press Play on PS2 in demodisc with the Play `video-mode` setting at `project`. Expected: the title reads `helengine player [PlayStation 2 · 640×448i] — <scene>`, the console shows `[player] render 640x448 at <n>x`, and the frame loop target is 60 FPS. Then ask her to pick `pal-320x256p` in Play Settings and press Play again. Expected: `[player] render 320x256 at <n>x`, 50 FPS, and the title says `320×256p`.

- [ ] **Step 3: Commit**

```bash
cd /c/dev/helprojs/demodisc
git add settings/platform.ps2.json
git commit -m "chore(ps2): select the PS2 video mode instead of a free framebuffer size

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task B11: Phase B verification and optional PCSX2 run

**Files:**
- None (verification only)

- [ ] **Step 1: Run every suite**

Run all four .NET suites from the Phase A exit check plus `make ps2-video-mode-tests`. Compare failures by name with the recorded baselines.

- [ ] **Step 2: Ask Helena before any emulator run**

Ask: "May I boot demodisc in PCSX2 for `ntsc-640x448i`, `ntsc-512x448i`, `ntsc-320x224p` and `pal-640x256p` to confirm from the boot log that gsKit was initialised with each mode and the menu renders?" Without a yes, stop here and report.

- [ ] **Step 3: With her yes, run the four boots**

For each mode:
1. Set `video-mode` in `platform.ps2.json`.
2. Rebuild the disc and boot it in PCSX2 (from `C:\dev\helworks\emus`), following the PS2 build memory notes.
3. Read the PCSX2 log for `video mode: NTSC 640x448i` (or the matching line), then `graphics ready` and `startup scene load succeeded`.

Take no screenshots unless Helena allows it. If a menu check is wanted, use window capture with PrintWindow only. Restore `ntsc-640x448i` afterwards, and commit nothing from these runs.

---

## Self-review notes (for the executor)

- Spec coverage: the Play-settings spec decisions map to Tasks A1-A13: storage (A5), flow (A6-A9, A13), common settings (A1, A7, A9), platform settings (A1, A2), dialog (A11-A13) and testing (every task). The video-mode spec maps to Phase B: contract (B2, B3), editor→player (B5), player (A10, B6), builder (B4, B7), runtime (B8, B9), projects (B10), error handling (B5, B7, B8, B9) and testing (B2-B8, B11).
- The video-mode spec's CLI flags (`--video-mode`, `--video-width`, ...) are intentionally replaced by the `videoMode` object in the `--play-settings` file (Play-settings spec flow step 2).
- Known open item: pixel aspect for non-640x448 modes (B1 "open" row), for both the PS2 runtime and the desktop player.
