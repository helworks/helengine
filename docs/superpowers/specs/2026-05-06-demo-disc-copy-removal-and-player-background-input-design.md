# Demo Disc Copy Removal And Player Background Input Design

## Summary

This change does two focused things:

1. Remove the `Helengine Demo Disc` title and the `Lilac nights, bright experiments, and a little street grit.` subtitle from the baked city demo-disc menu so those text entities no longer exist in the generated scene.
2. Keep background input policy centralized in the input layer, with shipped player hosts defaulting to background input disabled while editor hosts can still opt in explicitly.

The work stays intentionally narrow. The demo-disc menu copy removal is a hard-coded demo-menu change, not a generic menu-system redesign. The background-input work uses the existing `InputSystem` / `IInputBackend` toggle instead of inventing a second host-only switch.

## Goals

- Remove the baked demo-disc title entity and subtitle entity from the generated city main menu.
- Keep the generated city menu source aligned with the baked scene source of truth.
- Make player background input behavior explicit and default-off.
- Preserve the existing engine-level input toggle so editor or specialized hosts can still enable background input intentionally.

## Non-Goals

- No generic optional-title or optional-subtitle feature for every menu definition.
- No redesign of the general menu layout system.
- No new duplicate background-input toggle outside the input system.
- No editor UI preference or project setting for this policy in this pass.

## Current State

The demo-disc generator currently emits both title and subtitle copy from `tools/demo-disc-scene-writer/DemoDiscSceneWriter.cs`, and the baked menu scene factory always materializes those strings into dedicated text entities:

- `demo-disc-menu-title`
- `demo-disc-menu-subtitle`

Those entities are created directly in `engine/helengine.editor/managers/menu/DemoMenuSceneAssetFactory.cs`.

For input policy, the engine already has the right abstraction:

- `InputSystem.SetBackgroundInputEnabled(bool)`
- `IInputBackend.ReceiveInputInBackground`
- `InputBackendWindows.ReceiveInputInBackground`

What is still missing is an explicit player-host policy path and matching regression coverage that proves inactive player windows stay non-interactive by default.

## Design

### 1. Demo-disc menu copy removal

This stays hard-scoped to the demo-disc menu workflow.

- Update `DemoDiscSceneWriter` so the generated city-side provider no longer emits the `Helengine Demo Disc` title text or the `Lilac nights...` subtitle text.
- Update the baked demo-menu scene generation path in `DemoMenuSceneAssetFactory` so it does not create:
  - `demo-disc-menu-title`
  - `demo-disc-menu-subtitle`
- Reclaim the space by leaving the rest of the menu composition as the top-level visible content. No invisible placeholders or blank text entities should remain.

The implementation should not broaden `MenuDefinition` into a generic optional-title system for all menus. If the current title requirement means the definition still needs an internal string value, that is acceptable as long as the baked demo-disc scene no longer creates those two entities and the generated city menu source no longer presents the removed copy as authored UI text.

### 2. Background input policy

The input toggle remains owned by the input system.

- `InputSystem` remains the single public policy surface for enabling or disabling background input.
- `IInputBackend.ReceiveInputInBackground` remains the backend contract.
- `InputBackendWindows` continues enforcing foreground-only keyboard and mouse-button capture when the toggle is `false`.

Host policy:

- Player/runtime Windows hosts should explicitly stay on the default `false` path.
- Editor hosts may continue to opt in explicitly if needed.

The important part is that player behavior is no longer accidental. The runtime bootstrap path should make the intended default obvious in code and tests.

## Data Flow

### Demo-disc generation

1. `DemoDiscSceneWriter.WriteAll(...)` builds the baked menu definition and generated provider source.
2. The generated provider source no longer contains the removed title/subtitle copy.
3. `DemoMenuSceneBuildService` passes the definition to `DemoMenuSceneAssetFactory`.
4. `DemoMenuSceneAssetFactory` skips baking the title and subtitle entities into `DemoDiscMainMenu.helen`.

### Input policy

1. The runtime host creates `InputBackendWindows`.
2. The host binds it through `Core.Initialize(...)` / `InputSystem.SetBackend(...)`.
3. The player host leaves `ReceiveInputInBackground` disabled through the input-system policy surface.
4. `InputBackendWindows` suppresses keyboard and mouse-button capture while the window is not foreground active.
5. If another host explicitly enables background input through `InputSystem.SetBackgroundInputEnabled(true)`, the backend continues reporting those states.

## Testing

### Demo-disc menu

Add or update tests in `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs` to prove:

- generated provider source no longer contains `Helengine Demo Disc`
- generated provider source no longer contains `Lilac nights, bright experiments, and a little street grit.`
- baked scene root children do not include `demo-disc-menu-title`
- baked scene root children do not include `demo-disc-menu-subtitle`

If a more stable assertion surface is needed, entity-name assertions are acceptable as long as they prove the scene no longer contains those two baked text entities.

### Background input

Add or update input tests to prove:

- inactive-window mouse-button input remains suppressed by default
- inactive-window keyboard input remains suppressed by default
- explicit background-input enablement still allows inactive-window keyboard and mouse-button capture

These tests should stay focused on the input-system/backend contract rather than trying to validate full menu navigation in a packaged player binary.

## Risks

- The demo-disc menu builder currently bakes title/subtitle entities directly, so removing them changes the menu’s top spacing. The patch should keep the menu visually coherent without introducing placeholder entities.
- The input toggle already exists, so the main risk is not implementation complexity but wiring drift between host code and tests. The fix should avoid creating a second source of truth.

## Acceptance Criteria

- Regenerated city demo-disc menu source no longer includes the removed title/subtitle copy.
- Regenerated `Scenes/DemoDiscMainMenu.helen` no longer contains baked title/subtitle entities.
- Windows player hosts default to background input disabled through the existing input-system policy.
- Regression tests cover both the menu-copy removal and the explicit default-off background-input behavior.
