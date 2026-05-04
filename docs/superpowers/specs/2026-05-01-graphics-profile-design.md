# Graphics Profile Design

## Goal

Graphics profiles are platform-scoped runtime defaults that the editor persists and the player consumes at startup.

They control:
- default window width and height
- default fullscreen state
- default vsync state
- the concrete shader/backend target used by the platform

The graphics profile must be saved per platform, just like the build profile, so one project can keep different presentation defaults for Windows, PS2, and future platforms.

## Scope

This slice does not redesign the existing runtime renderer. It only standardizes how the editor stores graphics defaults and how the build pipeline stages them into a player-readable form.

The first consumer is the Windows player build path:
- the build queue snapshots the active platform graphics profile
- the executor writes a runtime graphics manifest into the build output
- the native player reads that manifest before creating the window and entering the render loop

## Data Model

The existing `EditorGraphicsProfileSettingsDocument` remains the persisted source of truth in `user_settings/profile_config.json`.

Per platform, the document already carries:
- `DefaultWidth`
- `DefaultHeight`
- `VSyncEnabled`
- `FullscreenEnabled`

The profile also needs a concrete runtime shader/backend target. The design treats that as part of the graphics profile rather than the platform descriptor, because the user needs to be able to choose it in the editor per platform.

The queue item will snapshot the active platform graphics profile so builds remain deterministic even if the editor profile is changed later.

## Build-Time Flow

When the user queues a Windows build:
- the editor loads the active platform graphics profile
- the queue item stores the graphics snapshot
- the Windows build executor stages a small runtime graphics manifest next to the packaged output
- the staged manifest contains width, height, vsync, fullscreen, and backend target

The Windows build output should remain target-specific, so DirectX and Vulkan outputs do not collide. The backend target must be part of the staged build identity, not just a transient editor preference.

## Runtime Flow

At startup, the Windows player should:
- load the staged graphics manifest from the build output
- configure the native window size and fullscreen mode from that manifest
- configure the presenter for vsync or uncapped presentation from that manifest
- initialize the core/runtime target using the staged backend selection

The runtime should not depend on editor-local settings files. The build output must contain everything the player needs.

## Error Handling

The build pipeline should fail clearly if:
- the active platform profile cannot be resolved
- the graphics manifest cannot be written
- the staged backend target is unsupported by the player host
- the runtime manifest is missing or malformed

The player should not silently invent defaults for missing manifest data. Missing runtime graphics data is a build or packaging error, not a recoverable runtime condition.

## Validation

The implementation should add tests for:
- persisting and reloading the per-platform graphics profile
- snapshotting graphics profile values into queued builds
- writing the runtime graphics manifest during Windows packaging
- loading the staged manifest in the player startup path

The Windows build should continue to support separate build outputs for different graphics targets.

## Renderer Settings Follow-Up

The initial graphics profile only standardized width, height, fullscreen, vsync, and backend target.

The Windows forward renderer expands that role. Platform graphics profiles also carry renderer-default settings such as depth prepass mode, HDR default, shadow quality tier, and post-processing tier. These values are staged into runtime-facing build data and consumed by the player startup and renderer planning path.
