# Console video mode (PS2 first) — design

Status: approved in conversation 2026-10-14, awaiting written-spec review.

## Goal

Let a project choose, per console platform, the resolution the game runs at — the console equivalent of Android's
`render-scale`, but done the way the hardware does it: the whole frame (3D, UI and text) renders into a framebuffer of
the chosen size and the video output circuit magnifies it to the screen. PS2 is the first and only implementer; the
contract is generic so other consoles can declare their own modes later. The desktop Play player honours the same
choice so what Helena sees in Play matches the console.

## Decisions (from the conversation)

- **Whole frame drops resolution, UI included.** No separate 3D render scale and no extra upscale pass. On PS2 the GS
  renders into a framebuffer of the mode's size and the PCRTC (`DISPLAY1/2` MAGH/MAGV) magnifies it on output.
- **Fixed per project configuration.** The mode is chosen in the platform's graphics preset and stays fixed from boot
  to shutdown. No runtime switching (no re-layout of VRAM while the game runs).
- **PS2 NTSC and PAL.** A single choice carries region, size and scan, so impossible combinations cannot be expressed.
- **Closed list, never free text.** Only modes the hardware really produces are offered; anything else fails clearly.

## Out of scope

- Runtime resolution changes from game code.
- Other consoles (they keep their hardcoded output until they declare a mode list).
- Exact 320×240 / 640×480 NTSC (needs a custom `DISPLAY` area; not in gsKit's standard modes).
- Android's `render-scale` (separate, already implemented by another session).

## The PS2 mode list

Setting id `video-mode`, kind `PlatformSettingKind.Choice`, required, default `ntsc-640x448i` (today's behaviour).

| Id | Framebuffer | Scan | Output |
|---|---|---|---|
| `ntsc-640x448i` | 640×448 | interlaced | NTSC 59.94 Hz |
| `ntsc-512x448i` | 512×448 | interlaced | NTSC 59.94 Hz |
| `ntsc-640x224p` | 640×224 | progressive | NTSC 59.94 Hz |
| `ntsc-512x224p` | 512×224 | progressive | NTSC 59.94 Hz |
| `ntsc-320x224p` | 320×224 | progressive | NTSC 59.94 Hz |
| `ntsc-256x224p` | 256×224 | progressive | NTSC 59.94 Hz |
| `pal-640x512i` | 640×512 | interlaced | PAL 50 Hz |
| `pal-512x512i` | 512×512 | interlaced | PAL 50 Hz |
| `pal-640x256p` | 640×256 | progressive | PAL 50 Hz |
| `pal-512x256p` | 512×256 | progressive | PAL 50 Hz |
| `pal-320x256p` | 320×256 | progressive | PAL 50 Hz |
| `pal-256x256p` | 256×256 | progressive | PAL 50 Hz |

Each mode must be validated against gsKit's mode setup (MAGH divisor of the NTSC/PAL pixel clock, MAGV, `Field`) during
implementation; a mode that gsKit cannot produce with its standard init is removed from the list rather than patched.

## Architecture

### 1. Engine contract (`helengine.baseplatform`)

- `PlatformVideoMode` (one class per file): `Id`, `Width`, `Height`, `IsProgressive`, `RefreshRateHz` (59.94 or 50),
  `PlayerFrameRate` (the desktop player's pacing target: 60 for NTSC — kept from the existing 60 FPS decision because it
  divides desktop refresh rates evenly — and 50 for PAL). The constructor validates every field and throws on invalid
  values.
- `PlatformVideoModeCapability`: the graphics-preset setting id that selects the mode and the closed array of
  `PlatformVideoMode`s, with a lookup that returns the mode for an id or fails naming the id and the platform. The
  setting's `allowedValues` must equal the capability's ids (validated when the definition is built).
- `PlatformPlayerCapability` loses its fixed frame-rate argument; the player's target frame rate now comes from the
  selected video mode. A platform with a supported player capability must declare a video-mode capability.

### 2. Editor → player

- When Play cooks (`EditorPlatformBuildGraphRunner.ExecuteAssetsOnly`), the selected graphics option values are
  resolved through the platform's video-mode capability. A missing or unknown value fails the Play with a clear message.
- `EditorPlayCookResult` carries the resolved `PlatformVideoMode`; `EditorPlaySessionService` passes it to the player as
  `--video-mode <id> --video-width W --video-height H --video-progressive true|false --player-frame-rate F`, and
  `PlayerLaunchOptions` parses and validates them (required, no defaults). The editor argument contract test keeps
  round-tripping through `PlayerLaunchOptions.Parse`.

### 3. Player

- The game renders into an offscreen render target of exactly the mode's size; the target is presented to the window
  magnified with point sampling, mirroring the PCRTC.
- The window client size is the largest integer multiple of the mode that fits the work area of the monitor the editor
  is on (minimum 1×). Interlaced modes render as full progressive frames on desktop.
- The window title shows the mode: `helengine player [PlayStation 2 · 320×224p] — <scene>`.
- Input pointer coordinates (if a platform declares a pointer) map back from window pixels to mode pixels.
- `PlayerFramePacer` uses the mode's `PlayerFrameRate`.

### 4. PS2 builder (`helengine-ps2`)

- `default-width` / `default-height` are removed from both graphics profiles and replaced by the `video-mode` choice.
- `Ps2PlatformDefinitionFactory` declares the `PlatformVideoModeCapability` with the 12 modes.
- The cook writes a small versioned boot record (magic, version, mode fields) into the cooked content; an invalid
  selection fails the build naming the value. The record's packaged path is owned by the PS2 builder.

### 5. PS2 runtime

- `Ps2BootHost` reads the boot record before `gsKit_init_screen` and sets `Mode`, `Interlace`, `Field`, `Width` and
  `Height` from it instead of the `Ps2DefaultFramebufferWidth/Height` constants. A missing or invalid record halts boot
  with a clear diagnostic; there is no silent fallback to 640×448.
- Every other place in the runtime that assumes 640×448 (viewport, 2D clip rectangle, VRAM layout, projection aspect)
  derives its value from the active mode. The implementation plan starts with an audit of these constants.

### 6. Projects

- `settings/platform.ps2.json` in demodisc (and any other project with PS2) replaces `default-width`/`default-height`
  with `video-mode`. Per the current-format-only rule there is no reader for the old fields: an old file fails naming
  the field and the file. The dead top-level `graphics.defaultWidth/defaultHeight` fields are not part of this change.

## Error handling

Every boundary fails loudly and names what was wrong: unknown mode id (editor, builder, player arguments), a definition
whose choice values disagree with its capability, a missing boot record on PS2. No defaults are synthesized for a
required value.

## Testing

Host-runnable:
- `PlatformVideoMode` / capability validation; lookup of every PS2 id; rejection of an unknown id.
- PS2 definition: the choice's allowed values equal the capability ids; default is `ntsc-640x448i`.
- Boot record round trip and rejection of a corrupt record.
- Editor → player argument round trip including the video mode.
- Player: integer window-scale computation for several monitor sizes; frame-rate selection (60 NTSC, 50 PAL); render
  target size equals the mode.
- PS2 native tests: boot record → gsKit fields for every mode.

Emulator (PCSX2), only after asking Helena: boot demodisc in `ntsc-640x448i`, `ntsc-512x448i`, `ntsc-320x224p` and one
PAL mode, confirming from the log that the GS was initialised with the requested mode and the menu renders.

## Rollout

Engine, player, PS2 builder/runtime and the demodisc settings change are pushed together: an editor with the new
contract fails on an old `platform.ps2.json`, and a PS2 build without the boot record halts at boot.
