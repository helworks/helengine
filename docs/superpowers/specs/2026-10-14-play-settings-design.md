# Play settings — design

Status: approved in conversation 2026-10-14 ("ss"), awaiting written-spec review.

## Goal

The editor's Play session gets its own settings — how the desktop player opens and runs for the selected platform —
separate from the platform's runtime settings (`settings/platform.<id>.json`, which describe the shipped game). Some
settings are common to every platform (window scale, debugger wait, …); each platform can declare its own (PS2 video
mode, Windows free resolution, DS/3DS screen layout, …). This is the foundation the per-platform player rollout
(`.superpowers/platform-player-brief.md`) builds on: every platform agent declares its Play settings here.

## Decisions

- Play settings are **local, per user**: stored under `user_settings/` (git-ignored), never in tracked project
  settings. They are a developer preference, like `lastPlayPlatform`.
- Edited from the Play platform selector: `[▶][PS2 ▾][■]` → the `▾` menu gains **"Play Settings…"**, which opens a
  dialog for the currently selected Play platform.
- Settings use the existing `PlatformSettingDefinition` shape (Boolean / Text / Choice, default, required,
  allowed values) so the dialog renders them with the same controls as the profiles dialog.
- Defaults come from the declarations; a platform setting may default to the project's own runtime value (see
  "Project-derived defaults").
- Current-format only: the stored document has a version; an unknown version or unknown setting id fails clearly
  naming the file and id. No legacy reader.

## Common settings (declared by the engine, every platform)

| Id | Kind | Values | Default |
|---|---|---|---|
| `window-scale` | Choice | `1x`, `2x`, `3x`, `4x`, `fit` | `fit` (largest integer scale that fits the work area) |
| `window-monitor` | Choice | `editor`, `primary` | `editor` (monitor the editor window is on) |
| `wait-for-debugger` | Boolean | | `false` (replaces today's hard-wired flag) |
| `shadows` | Boolean | | `true` (replaces today's `--shadows`) |
| `frame-rate` | Choice | `platform`, `unlimited` | `platform` (the platform's real refresh) |

## Platform settings (declared on `PlatformPlayerCapability`)

`PlatformPlayerCapability` gains `PlaySettings: PlatformSettingDefinition[]` (may be empty). Ids must not collide with
the common ids (validated). The adapter receives the resolved values and applies them. Examples the rollout will fill:

- PS2: `video-mode` (the 12 NTSC/PAL modes of the console video-mode spec, plus `project` = use the project's
  configured mode; default `project`).
- Windows / Android: `resolution` (Text `WxH`, default `project` = the project's `default-width/height`),
  Android `orientation` (`landscape` / `portrait`).
- DS / 3DS: `screen-layout` (`stacked`; extensible later), `screen-gap` (pixels between screens, default `0`).
- Fixed-resolution handhelds (PSP, Vita): nothing beyond the common settings unless the agent finds a real need.

## Storage

`user_settings/play.json`:

```json
{
  "version": 1,
  "common": { "window-scale": "fit", "window-monitor": "editor", "wait-for-debugger": false,
              "shadows": true, "frame-rate": "platform" },
  "platforms": { "ps2": { "video-mode": "project" } }
}
```

Missing file or missing keys → declared defaults (no write until the user saves the dialog). Values are validated
against the current declarations on load and before every Play; an invalid stored value fails the Play with a message
naming the file, platform and id, and the dialog shows the offending value highlighted for correction.

## Flow

1. Editor (UI thread, at Play press — captured into the existing `EditorPlayPreparationInputs` snapshot): resolve
   common + platform Play settings, resolving `project` defaults against the project's runtime settings.
2. `EditorPlaySessionService` passes them to the player as one `--play-settings <path>` file written to the Play
   workspace (`cache/`), not as many CLI flags; the existing `--shadows`, `--wait-for-debugger` and frame-rate
   arguments are folded into it.
3. `PlayerLaunchOptions` reads and validates it; `PlayerHost` applies the common ones (window scale/monitor,
   debugger wait, shadows, frame pacing); the adapter gets the platform section through a new
   `IPlatformPlayerAdapter` method and applies it (render size, screen layout, …).

## Relation to the console video-mode spec

`docs/superpowers/specs/2026-10-14-console-video-mode-design.md` defines the project's runtime `video-mode` for the
console build. Play settings only choose how Play renders: PS2's Play `video-mode` defaults to `project`, so Play
matches the console unless overridden. The video-mode spec's player part ("window opens at an integer scale of the
mode") is implemented through the common `window-scale` setting defined here.

## Testing

Host-runnable: declaration validation (id collisions, choice defaults); `play.json` round trip, defaults when absent,
rejection of unknown version / id / value; `project` default resolution; settings file round trip editor → player;
window-scale computation for several monitor sizes; the dialog's view-model (values, validation state) without a GUI;
the snapshot captures settings at press time (an edit during preparation applies to the next Play).

## Out of scope

Per-platform settings beyond the examples (each platform agent declares its own during the rollout); editing Play
settings while the player runs (applies to the next Play).
