# Tilt Trial platform selector layout

## Goal

Use the two-stage Tilt Trial level selector only on Nintendo DS and Nintendo 3DS. All other platforms should show the level list, selected-level information, actions, and a static preview placeholder together in one selector view.

## Design

`TiltTrialLevelSelectComponent` will expose an explicit serialized `UseDetailsStage` setting. The setting is enabled on the DS/3DS selector root and disabled on the standard and PS2 selector roots.

When `UseDetailsStage` is enabled, the existing list-to-details navigation remains unchanged. When it is disabled, both panels remain enabled after binding, selecting a level only updates the selected row and details, and Accept/Enter plays the selected level directly. Escape/Back continues to return to the main menu.

The standard selector will gain a static preview placeholder inside its details panel. The PS2 selector will receive the equivalent placeholder sized for its 640x448 canvas. The placeholder is intentionally not bound to `PreviewTexturePath`; actual preview assets are out of scope.

## Data flow and platform behavior

The scene factory will serialize the mode on each generated selector root. The runtime controller will read that mode after scene deserialization and choose either the existing two-stage state machine or the combined-view state machine. DS and 3DS continue to use their dedicated handheld selector scene; all other platform builds use the standard selector, except PS2 which uses its existing compact selector layout.

## Error handling

Existing required-entity and required-component validation remains unchanged. The new preview placeholder is static and requires no additional runtime binding, so missing preview data cannot prevent selector startup.

## Verification

Add source-level tests covering:

1. DS/3DS selector generation enables `UseDetailsStage`.
2. Standard and PS2 selector generation disable the staged flow.
3. Standard and PS2 layouts contain preview placeholder panels.
4. The runtime controller has separate combined-view and staged-view navigation branches.

Run the focused DemoDisc source tests, regenerate the scenes, build Windows, and launch it to verify the combined selector stays on one screen. The existing handheld scene paths remain available for later DS/3DS validation.

## Scope limits

This change does not add real preview textures, alter gameplay-level loading, change the DS/3DS visual layout, or redesign the existing selector colors and typography beyond fitting the static placeholder into the current panels.
