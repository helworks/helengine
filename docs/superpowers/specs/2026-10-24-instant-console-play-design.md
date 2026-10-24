# Instant console Play: native builds into an emulator from the editor Play button

Date: 2026-10-24. Status: design, awaiting Helena's approval.

## Intent

Helena changes a `+` to a `-` in game C#, presses Play in the editor with a console selected, and sees the change
running as native console code in that console's emulator, as close to immediately as the stack allows. The build
is deterministic: the same sources produce the same bytes, so input tapes replayed before and after a change are
comparable. Later the same stack optimises the code it produces (profile-guided layout for the instruction cache),
measured, never by default.

Why: today one change costs about 3 to 6 minutes before it runs on a console target, and the editor's Play button
never runs native code (see Current state). The PSoXide video (reference/ps1/PSoXide-video/transcript.md) showed the
loop we want: agents and people build, run and debug in an emulator with exact, repeatable replay.

## Current state (measured or read on 2026-10-24)

- Editor Play runs a desktop C# player that imitates the platform (`PlatformPlayerCapability`,
  `engine/helengine.editor/managers/play/`). Only PS2 declares it (`Ps2PlatformDefinitionFactory.cs:351`). No Play
  path builds native code or starts an emulator.
- Native builds go through the editor build graph (`EditorPlatformBuildGraphRunner`): codegen of the whole Core,
  asset cook, the platform's `builder/` executor, docker, make, packaging.
- Codegen (`codegen.exe --cpp`, `EditorGeneratedCoreRegenerationService.cs:105-197`) deletes and regenerates all
  ~1300 Core files every build: 93-333 s (Xbox), 210 s (GameCube). Engine Core regeneration does not depend on project
  scripts, which compile separately (`CodeCookService.CompileModules`).
- `EditorNativeBuildCache.SyncGeneratedCore` (`EditorPlatformBuildGraphRunner.cs:1559`) already copies the fresh
  generated Core into a stable per-profile tree, only files whose bytes changed, so unchanged files keep timestamps.
- N64, DC, PS2, Xbox and GC compile the generated Core as one unity TU (`helengine_core_unity.cpp`): 62 s GC, 77 s PS2.
  N64 and Xbox wipe all objects whenever the generated-Core hash changes; DC keys the whole cache dir on it.
- PS1 defaults to scene-code modules (`PlayStation1PlatformDefinitionFactory.cs:87`): resident Core files compile one
  per object (`PS1_RESIDENT_CORE_SRCS`) plus fixed-VMA overlay modules, after an uncached docker run that compiles
  every generated unit to extract symbols (`compile-scene-module-candidates.py`) and partitions them into resident and
  overlays. Its object cache is fixed and wiped only on toolchain/flag fingerprint changes (`build.ps1:398-430`), but
  in scene-module mode the fingerprint includes the generated `helcpp_config.hpp` and `runtime/feature_config.hpp`.
  Helena builds PS1 discs with the editor's Build button today. The code reading suggested the executor needs
  `HELENGINE_PS1_BUILD_RUN_ROOT` in scene-module mode (and `HELENGINE_BUILD_CACHE_ROOT`, read by
  `EditorBuildIsolationPathResolver`, for external modules), which no editor code sets; neither is set at user or
  machine scope. Phase 1 starts by confirming an editor Build of the PS1 monolith end to end and records how the
  run root is resolved; if it cannot build unattended, fixing that is phase 1's first task.
- Overhead: PS1 copies the generated Core and content into its fixed run root and hashes it plus the whole PS1 source
  tree (~24 s in one measured run); the editor makes a fresh GUID execution root per full Build
  (`EditorPlatformBuildGraphWorkspaceFactory.cs:89-91`); docker starts at least twice per build; PS2 and GC run make
  without `-j`.
- Determinism: PS1, DC and Xbox pin their toolchain images and SDK commits (DC verified identical ELF hashes). PS2
  (`FROM ubuntu:24.04` + `ps2dev latest`) and GC (`devkitppc:latest` + `pacman -Syu`) are unpinned.
- Hot-swap: nothing exists. PS1 has fixed-VMA overlays ("scene modules", up to 8 slots, bound to the resident by
  `resident_build_id`), which is the closest building block. libdragon (N64) ships DSO support, unused.
- Emulator control: emucap fork (helworks/emucap) with the PSoXide PS1 backend (helworks/PSoXide-emulator,
  `psx_backend=psoxide`): frame/instruction/poll stepping, screenshots, RAM read/write, save states,
  `verify_determinism`; PSoXide has `.pxtape` input tapes that stop at the Nth controller poll. Our PS1 disc boots to
  the demodisc menu in PSoXide as of 77b8893. emucap also drives PPSSPP, PCSX2, Dolphin, xemu, Flycast and
  Mupen64Plus (untested by us except PPSSPP).
- Layout optimisation prior art: PSoXide SDK `tools/psoxide-pgo` (tape-driven AutoFDO and I-cache function order).
  Measured gains: PGO up to +9% FPS on one game and a loss on another; function ordering 0.4-1.2%. Build path alone
  moved FPS 0.4%.

Detailed per-console findings are in the recon reports summarised above; the builder should re-read each console's
`builder/` executor and `scripts/build.ps1` before changing it.

## Decided with Helena

- Trigger: the editor's Play button.
- Both reload styles are wanted: (a) restart and replay an input tape back to where she was, and (b) swap the
  changed code into the running game keeping live state. Start with (a), and build (a) so that (b) can be added
  without redoing it.
- All old consoles, starting with PS1, then N64, Dreamcast, PS2, Xbox, GameCube. Other platforms (PSP, DS, 3DS,
  Wii, Wii U, PS3, Vita, Xbox 360, SNES) come after, same design.
- Code-layout optimisation for the instruction cache is wanted, for all platforms, after the loop works.
- Phase 1 has no time target: it proves the loop works end to end. Speed is the next phase.
- The edits to optimise for are in the project's game scripts; the engine Core usually stays unchanged between
  Plays. Game scripts are converted per code module (`EditorPlatformCodeCookService.CompileModules` runs
  `codegen --cpp` per module project into `code/<ModuleId>`), separately from Core regeneration. Phase 1's first
  win is therefore: when the engine inputs, codegen and options are unchanged, Play reuses the generated Core and its
  native objects as they are, and reconverts and recompiles only the game script modules that changed. Engine-side
  incrementality (codegen that regenerates only changed Core classes, splitting the Core unity TU) comes after.
- PS1 Play in phase 1 builds a monolithic EXE (scene-code modules off), with fewer moving parts to prove the loop.
  The build mode is a pipeline option from the start; phase 2 moves PS1 Play to the shipped scene-code-module disc,
  together with live hot-patching, which builds on its overlays.
  Fit, from the 2026-10-19 module disc (`builds\ps1\package\helengine_ps1.json`, `scene-modules-receipt.json`): resident
  `BOOT.EXE` 1,069,056 B, nine overlays 237,312 B in total, three slots 157,696 B, heap `0x80158000`-`0x801F0000`
  (~608 KB). A monolith carries ~78 KB more code, so the heap drops to ~530 KB. Code fits; whether the game's peak heap
  fits is unproven, so phase 1 starts by building the monolith and running every demodisc scene in PSoXide.

## Goals

1. Phase 1 (PS1 vertical slice): Play with PS1 selected and the "emulator" Play target builds the project natively,
   incrementally, and runs it in PSoXide through emucap in a window Helena can see and play. A second Play after a
   one-character C# change rebuilds only what that change requires and returns to the same point of play by replaying
   the input recorded during the previous session.
2. Every stage reports its duration in the Play log, so phase 2 starts from numbers.
3. The PS1 native build is deterministic: two builds of the same sources from a clean cache and from a warm cache
   produce byte-identical EXE and disc images. A test proves it.
4. The build keeps what phase-2 hot-patching needs: the unstripped ELF, the link map and a build id per Play build.

## Non-goals

- Replacing the desktop C# Play player. It stays as it is and remains the default Play target.
- A time target in phase 1.
- Live hot-patching in phase 1 (only its prerequisites).
- Real-hardware runs.
- Switch and the desktop/mobile platforms (Windows, Linux, macOS, Android, iOS).

## Anti-goals (failure even if it "works")

- A fast path that can silently run stale code. If the incremental path cannot prove the image matches the sources,
  it must rebuild or fail loudly, never run the old image.
- Non-determinism traded for speed.
- Breaking or slowing the existing full build/package path the editor's Build uses.
- Shared-infrastructure changes (csharpcodegen, editor core, shared fonts) made without proposing them to Helena
  first.
- A history of builds: Play builds overwrite one location per platform.

## Design

### 1. Play target "emulator"

Add a Play target choice next to the Play platform: `desktop player` (today's behaviour, default) or `emulator`.
A platform offers the emulator target when its definition declares an emulator capability (new, alongside
`PlatformPlayerCapability`): the emucap system id, the emucap backend (PS1: `psx`, `psx_backend=psoxide`), which
build artifact to launch (disc cue or EXE), and whether it supports input tapes. Platform definitions stay
engine-only (no game script types).

Pressing Play with the emulator target runs the native Play pipeline below instead of the desktop player.

### 2. Native Play pipeline

Stages, each timed and logged:

1. **Skip unchanged engine work.** Key the generated Core and its native objects on the engine inputs, codegen
   build and options; when the key matches, Core regeneration and Core compilation are skipped entirely.
2. **Reconvert only changed game modules.** Convert a game script module only when its sources or dependencies
   changed, and write generated files only when their bytes change (unchanged files keep their timestamps), so make
   recompiles only real changes. Codegen output must be deterministic (same input, same bytes); add a test.
   Later (engine edits): codegen regenerates only changed Core classes, and the Core unity TU is split into a fixed
   number of shards with a stable file-to-shard assignment (unaffected by adding unrelated files).
3. **Incremental native build.** Per-object make dependencies decide what recompiles. Remove the fingerprints that
   wipe the whole object cache when the generated Core changes (they stay for toolchain/flag changes). Copy and hash
   only what make needs, not the whole input tree into a new timestamped folder.
4. **Persistent build container.** One long-lived container per platform with fixed mount points, reused via
   `docker exec` across Play presses, so docker start-up leaves the loop and paths stay identical (determinism).
5. **Package.** Rebuild the disc image only when its inputs changed; disc timestamps stay fixed (PS1 already uses a
   fixed volume date).
6. **Launch.** Start or restart the emulator session through emucap with the built image.
7. **Return to the same point.** If the previous Play session recorded an input tape, replay it to its last poll
   (or a poll Helena chose), then hand control to her. Every Play session records its input to a tape.

The pipeline lives where the editor's build graph lives; the PS1-specific parts live in helengine-ps1's builder.

### 3. Emulator window and control

Helena must see and play the game in a normal emulator window. The same session must stay reachable through emucap
so agents can read memory, step and screenshot it. PSoXide's GUI frontend already has an MCP server and the emucap
host is headless; the builder decides how to give one session both (for example the GUI frontend loading and
replaying a tape, with emucap attached, or the emucap host gaining a window).

### 4. Determinism

- Pinned toolchain image and SDK (PS1 already is). Fixed container paths, `-ffile-prefix-map`, no `__DATE__`/
  `__TIME__`, no timestamps or GUIDs in the EXE or disc.
- A deterministic-build test: build twice (clean and warm cache), compare EXE and disc hashes.
- The emulator side is already deterministic for PSoXide (`verify_determinism` passes on our disc).

### 5. Prerequisites for live hot-patching (phase 2)

Each Play build keeps the unstripped ELF, the link map and a build id (hash of the image) in the platform's Play
output folder, overwritten each time. Phase 2 will compile a changed function, link it against the running build's
symbol map, write it into a reserved patch area through emucap `write_memory`, and redirect the old function's entry
to it; changes to data layout (struct sizes, vtables, globals) fall back to the replay path. PS1's scene-module
overlays and `resident_build_id` are the starting point to evaluate.

### 6. Code layout (later phase)

Port the psoxide-pgo approach per platform: profile from tape replays in the emulator, function ordering by the
target's I-cache geometry, a gate that measures each variant on training and unseen tapes, and "off" always a
candidate. Requires function sections and a linker ordering hook per platform.

## Phases

1. PS1 vertical slice (goals 1-4).
2. Speed: a resident codegen server that reparses only changed C# files; measure and remove the biggest remaining
   stage; live hot-patching on PS1.
3. Roll out to N64, Dreamcast, PS2, Xbox, GameCube: pin PS2 and GC toolchains first; per-platform emulator capability
   through emucap (Mupen64Plus, Flycast, PCSX2, xemu, Dolphin backends), tapes where the emulator supports them.
4. Code-layout optimisation, platform by platform, measured.

## Constraints

- Work in the real checkouts on main; never worktrees or archive exports. Check each repo's branch and uncommitted
  changes before editing; other sessions' uncommitted work stays untouched.
- One build output location per platform, overwritten.
- Temporary files under `C:\temp`, cleaned up.
- Ask Helena before launching emulators or capturing the screen during development; capture windows only with
  PrintWindow.
- Shared infra changes (csharpcodegen, editor build graph, Play settings) are proposed to Helena before they are made.
- The Play settings + PS2 video mode plan (paused 2026-10-15, `docs/superpowers/plans/2026-10-15-play-settings-
  handoff.md`) touches the same Play code; read it first and do not undo its work.

## Calls made by Claude (Helena to check)

- The emucap and PSoXide-emulator forks (helworks/emucap, helworks/PSoXide-emulator, clones under `reference/`) are
  ours to change and commit, in small commits; they are not helengine shared infra. The visible-window session and
  automatic tape recording are new work there (today the emucap PSoXide backend and `emucap-host` are headless, and
  recording is a manual F8 in the GUI frontend that also writes timestamped copies, which must become one
  overwritten tape per project and platform).
- Play builds use the same configuration as the shipped build (Release), so Play behaves like the disc; debug info
  is kept beside it in the ELF without changing the code bytes.
- Tape divergence: a tape records a state hash (PSoXide display/VRAM hash) at its last poll. After a code change the
  replay compares it, and the Play log says whether play returned to the same state or diverged; control is handed
  over either way. Phase 1 replays to the last recorded poll; choosing another poll is later work.
- Codegen changes go into `C:\dev\helworks\csharpcodegen` and reach editor builds through the vendored submodule
  `engine/vendor/csharpcodegen` (editor builds use the vendored copy).
- Files with other sessions' uncommitted changes are not edited; when the plan needs one, the builder stops and
  asks Helena.

## Left to the builder

- Shard count and assignment rule, as long as it is stable and deterministic.
- How codegen's write-if-changed is implemented.
- How the persistent container is managed (start, health, restart on toolchain change).
- How one emulator session is both a visible window and emucap-controllable.
- Tape file location and naming (per project and platform, overwritten).
- Exact Play log format for stage timings.
