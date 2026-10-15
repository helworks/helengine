# Play settings + PS2 video mode — handoff (paused 2026-10-15)

Plan: `docs/superpowers/plans/2026-10-14-play-settings-and-ps2-video-mode.md`.
SDD ledger, briefs, reports and reviews (git-ignored, shared checkout):
`C:\dev\helworks\helengine\.superpowers\sdd\2026-10-14-play-settings-and-ps2-video-mode\progress.md` — read it first.

## Where the code lives

- **helengine**: worktree `C:\dev\helworks\helengine\.worktrees\play-settings`, branch `main` (HEAD `e73e849b` + this note).
  The shared checkout `C:\dev\helworks\helengine` is on another session's `feat/webgpu-shared-shaders`; do not commit there.
- **helengine-ps2**: `main` (HEAD `12cb9e6`). Builds need `-p:HelengineRoot=<worktree>` until the shared checkout has these commits.
- **demodisc**: `main` has `b544c99` (`video-mode` in `settings/platform.ps2.json`). The demodisc checkout itself is currently on `feat/webgpu-demodisc`.
- Nothing is pushed. Helena approved pushing (including the non-plan commits ahead of origin) **after B16 passes**, in order:
  helengine `main` → helengine-ps2 `main` → demodisc `main`.

## Done

- Phase A (Play settings: store, dialog, `--play-settings` file, window scale/monitor/pacing, fixed render size) — reviewed.
- Phase B B1–B15 (pixel aspect for 3D + UI, video-mode contract, 12 PS2 modes with gsKit-derived aspect 4:3/3:2/22:15,
  boot record `VIDMODE.BIN`, `VMODE` in SYSTEM.CNF, native reader + `VerifyGsInit`, PS2 projection) — reviewed.
- Extra fixes during execution: sharp-bilinear Play magnify, async-Play hardening, script-build pipe hang
  (`3776b518`, `10b7e187`), CPPOWN007 (`5367fb2`), native core empty settings catalog (`e73e849b`),
  Codex's `File::` support (`82f0318`) and cached-DMA texture upload fix (`abe8fa4`).
- Final whole-branch review: no code blocker; push blocked only on B16.

## Open — resume here

1. **PS2 2D sprite rotation (blocker for B16 visual check).** In PCSX2 the main-menu background moves but does not rotate.
   Root cause found when pausing: the PS2 rotation geometry `src/platform/ps2/rendering/Ps2TexturedQuadGeometry.hpp`
   (and `Ps2TexturedQuadMath.hpp`, tests) exist only as the **Codex PS1 session's untracked files**; committed PS2 code
   draws textured quads without rotation. WIP `12cb9e6` (`Ps2TexturedQuadPrimitive.hpp` + test, not wired, not in the
   Makefile) depends on that untracked header. Next: decide with Helena whether Codex commits its geometry files or we do,
   then wire the primitive into the PS2 2D textured-quad draw, add native tests, rebuild, verify in PCSX2.
2. **B16** (PCSX2, Helena approved runs + PrintWindow on the PCSX2 window only; HelenUI OCR for its dialogs):
   ntsc-640x448i already gets through the video-mode boot (`video mode`/`vram` lines, no halt). Re-run after (1):
   ntsc-640x448i, ntsc-512x448i, ntsc-320x224p (3:2), pal-640x512i. Tooling: `C:\dev\helworks\builds\helengine-ps2\ps2\`
   (`b16-run-build.cmd`, `b16-pcsx2-run.ps1`, `b16fix` toolchain, private portable PCSX2). Report: `task-B16-report.md`.
3. Push (see above), then point Helena's editor install/registry at the new builds.
4. Manual checks only Helena can do: Play Settings dialog, Shift/setting debugger wait, unlimited frame rate,
   window scale/monitor, PS2 Play window (4:3, sharp-bilinear, ClearType fringes, title). A worktree editor with a private
   registry is launchable via `C:\dev\helworks\builds\helengine\manual-test\launch-editor.cmd`.

## Decisions recorded (Helena)

UI fits in display space; PS2 aspect follows gsKit magnification; sharp-bilinear magnify; glyph width on non-square
modes accepted as is; Shift OR setting forces debugger wait; Play shadows can only turn off; core fix for empty settings.

## Parked / not blocking

Retired common Play setting blocks `play.json` load (A11 m3); editor `EnsureSettingDefaults` fills a missing
`video-mode` (B11 F4); Windows pointer mapping window→render pixels before Windows/Android Play rollout (A10 I2);
`[NativeMigrationRequired]` note for fixed-size present (A10 M4); DS `H = 1.5W` viewport heuristic; codegen should fail
fast on unresolved names (CPPOWN007 F5); console video-mode spec still mentions the old `--video-*` flags (M7).
Platform player rollout (all platforms except Switch/SNES) waits for this plan; brief at `.superpowers/platform-player-brief.md`.
