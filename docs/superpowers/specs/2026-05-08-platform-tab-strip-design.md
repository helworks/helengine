# Platform Tab Strip Design

## Summary

Standardize platform tabs behind one reusable editor-owned pattern, starting with the asset processor surface. The shared pattern must preserve the current tab look, support horizontal overflow for larger platform counts, expose left and right overflow affordances, and automatically reveal the selected tab during mouse and keyboard navigation.

## Goals

- Replace ad hoc platform-tab row logic in `AssetImportSettingsView` with a shared platform tab strip.
- Keep individual tabs based on the existing `TabComponent`.
- Support horizontal overflow for future larger platform counts, including cases like `15` platforms.
- Provide left and right arrow affordances when the row overflows.
- Auto-reveal the selected tab when selection changes or keyboard focus moves to a clipped tab.
- Keep the design reusable so `BuildDialog` can adopt it later without another redesign.

## Non-Goals

- Do not migrate `BuildDialog` in this pass.
- Do not redesign the visual style of `TabComponent` again in this pass.
- Do not add drag scrolling.
- Do not add multi-row wrapping.
- Do not push platform-tab overflow behavior into `helengine.core` yet.

## Current Context

There are currently two local platform-tab implementations in the editor:

- `engine/helengine.editor/components/ui/AssetImportSettingsView.cs`
- `engine/helengine.editor/components/ui/BuildDialog.cs`

Both surfaces:

- build `TabComponent` instances directly
- maintain their own tab-host entity lists
- position tabs manually
- track selected platform state locally

Only `AssetImportSettingsView` is in scope for integration in this pass, but the extracted API must fit `BuildDialog` next.

## Proposed Architecture

Add a reusable editor-owned tab-strip view in `helengine.editor`, tentatively named `PlatformTabStripView`.

Responsibilities:

- create and dispose tab host entities
- create and dispose left/right arrow button entities
- own the horizontal scroll offset state
- own overflow visibility and enablement logic
- own selected-platform state display
- own keyboard navigation and selected-tab reveal behavior
- lay out one horizontal strip of `TabComponent` instances within a clipped viewport

Responsibilities that stay outside the strip:

- platform-specific business state
- loading and saving settings
- reacting to platform selection by updating the rest of the screen
- higher-level host layout decisions

`TabComponent` remains the reusable individual tab button. The new strip composes it rather than replacing it.

## Overflow Model

The strip uses a single horizontal row only.

Behavior:

- If all tabs fit inside the available width, the row shows no active overflow affordance.
- If tabs overflow, left and right arrow buttons appear at the edges of the strip.
- Clicking an arrow scrolls horizontally by a fixed amount, approximately one tab width plus spacing.
- Selecting a clipped tab automatically scrolls just enough to fully reveal it.
- Keyboard navigation also auto-reveals the newly selected tab.
- Standard focus traversal must continue to work, with the row scrolling to keep the focused tab visible.

The strip should prefer deterministic fixed-step movement over inertial scrolling.

## Keyboard Behavior

Keyboard support is a first-class requirement.

Required behavior:

- moving through tabs with keyboard input must select the next or previous platform
- when the newly selected tab is clipped, the strip scrolls to fully reveal it
- keyboard-driven selection must behave the same way as mouse-driven tab selection in terms of reveal logic

The exact key plumbing should follow existing editor input patterns already used by button-like controls, but the strip owns the reveal behavior once a tab becomes selected.

## Asset Processor Integration

Integrate the shared strip into `AssetImportSettingsView`.

That means:

- remove the local platform tab host/button bookkeeping from `AssetImportSettingsView`
- replace local tab rebuilding and local tab layout with the shared strip
- keep the existing processor settings editing flow intact
- keep the existing selected-platform state in the host view unless the strip API makes a narrower state handoff cleaner

The asset processor remains the feature owner for:

- supported platform ids
- active platform id
- handling selection changes
- showing platform-specific processor controls

The strip only owns the row UI and interaction mechanics.

## Reuse Expectations

The extracted strip must already be shaped for later reuse in `BuildDialog`.

Minimum expected API shape:

- `SetPlatforms(...)`
- `SetSelectedPlatform(...)`
- selection changed callback or event
- layout update entry point with width and top-left positioning inputs
- disposal / clear path for rebuilding host-owned entities safely

The exact names can change during implementation, but the responsibilities should stay stable.

## Visual Behavior

The strip should preserve the current shared tab visual language:

- active tab darker
- inactive tabs lighter
- the row remains tab-like rather than a segmented button bar

Overflow arrows should read as part of the same editor surface, not as floating special-case buttons.

The shared strip should not introduce a new visual style that conflicts with the build dialog tab seam work already in progress.

## Error Handling

The shared strip should fail loudly on invalid state.

Examples:

- no callback when selection is required
- selecting a platform id that is not in the current tab list
- invalid layout widths

Do not silently fabricate fallback tab data.

## Testing Strategy

Add focused editor tests around the new strip and the asset processor integration.

Coverage should include:

- one tab created per supplied platform
- selected tab visual state updates correctly
- overflow arrows appear only when needed
- arrow interaction scrolls the visible window
- selecting an offscreen tab auto-reveals it
- rebuilding the strip disposes previous tab hosts correctly
- `AssetImportSettingsView` still reflects selected-platform changes correctly after adopting the strip

This pass does not need `BuildDialog` migration tests yet, but the strip tests should be reusable when that migration happens.

## Implementation Notes

- Keep the new strip in `helengine.editor`, not `helengine.core`.
- Reuse `TabComponent` for tabs rather than creating another button type.
- Keep files focused: one class per file.
- Follow existing editor entity/component patterns for UI ownership and disposal.

## Rollout

1. Add the shared editor-owned platform tab strip.
2. Integrate it into `AssetImportSettingsView`.
3. Verify the asset processor uses the standardized overflow-aware tab row.
4. Leave `BuildDialog` on its current local implementation for now, but align future migration to the same strip.
