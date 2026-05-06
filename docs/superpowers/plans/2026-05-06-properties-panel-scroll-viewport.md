# Properties Panel Scroll Viewport Implementation Plan

Use the `superpowers:test-driven-development` skill while executing this plan. Use the `superpowers:verification-before-completion` skill before claiming the work is finished.

## Goal

Add a real scroll viewport to `PropertiesPanel` so the panel body clips its child content to the visible area, supports scrolling through long property forms, and prevents offscreen controls from drawing or receiving input outside the panel bounds.

## Architecture

Follow the existing clipped panel-content pattern already used elsewhere in the editor UI:

- Keep the panel shell and title bar on the shared editor UI camera.
- Move the scrollable properties body into a dedicated content root rendered by a panel-content camera and layer.
- Drive vertical scrolling through `ScrollComponent`.
- Keep panel-owned modal overlays on `ModalHost` so they remain above the clipped properties body.

The implementation should preserve the current stacked document layout of property rows and editors. This is a clipping and scrolling change, not a rewrite of the property rendering model.

## Tech Stack

- C#
- HelEngine editor UI entity/component system
- `ScrollComponent`
- Editor UI camera draw-order tiers
- xUnit editor test suites

## Tasks

### Task 1: Add failing regression coverage for the properties viewport

Update `engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs` to cover the new viewport behavior before implementation.

Add tests that verify:

1. A tall property layout creates a positive scroll range instead of expanding unbounded.
2. The panel body owns a clipped content camera/layer path rather than placing property controls directly under the shell root.
3. Controls that are outside the visible body are not rendered or interactable until scrolled into view.

Implementation notes:

- Reuse the existing panel test harness that opens a `PropertiesPanel` through `EditorSession`.
- Prefer assertions against concrete panel state such as scroll offset range, content root parenting, camera/layer assignment, and hit testing behavior.
- Keep the modal host out of scope for clipping assertions so panel-owned dialogs can continue to render above the viewport.

Validation:

- Run the focused `PropertiesPanelComponentShellTests` filter and confirm the new assertions fail for the current implementation.

### Task 2: Add clipped scroll viewport infrastructure to `PropertiesPanel`

Update the runtime panel implementation to create a dedicated viewport for the properties body.

Files:

- `engine/helengine.editor/EditorLayerMasks.cs`
- `engine/helengine.editor/components/ui/PropertiesPanel.cs`

Changes:

1. Add a dedicated editor layer for properties panel content.
2. In `PropertiesPanel`, create:
   - a content camera entity/component for the clipped body
   - a scroll viewport root under the panel shell
   - a scroll content root under that viewport
   - a `ScrollComponent` configured for the body bounds
3. Move property body children to the scroll content root and assign them to the new properties content layer.
4. Leave panel-owned modal content attached to `ModalHost`.

Implementation constraints:

- Keep XML comments substantive on new members.
- Preserve the current ownership model for title bar and shell chrome.
- Do not introduce fallback or best-effort behavior; if required panel state is missing, fail explicitly.

### Task 3: Wire layout, clipping, and scrolling behavior

Finish the layout integration so the viewport tracks the panel body size and scroll position.

Changes:

1. Compute the visible viewport rectangle from the panel bounds below the title bar.
2. Compute total document height from the existing stacked layout pass.
3. Configure `ScrollComponent` using pixel-based vertical units:
   - `ItemCount` = total document height in pixels
   - `VisibleItemCount` = viewport height in pixels
4. Apply the scroll offset by repositioning the scroll content root on the Y axis.
5. Update the content camera viewport and bounds whenever the panel layout changes.
6. Ensure wheel scrolling applies only when the pointer is inside the properties body.

Implementation notes:

- Preserve existing row/layout methods where possible; adapt them to report total content height after arranging controls.
- Keep draw order aligned with existing shared tiers so panel content renders below modal UI.
- Ensure hidden/offscreen controls do not leak input beyond the clipped viewport.

### Task 4: Verify regressions and adjacent panel behavior

Run focused and adjacent verification after implementation.

Minimum verification:

1. `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~PropertiesPanelComponentShellTests"`
2. `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~PropertiesPanelMutationTests|FullyQualifiedName~EditorSessionPreferencesTests"`

If an adjacent suite fails, determine whether it is caused by this change before making further edits.

## Risks

- `ScrollComponent` is item-oriented, so the plan relies on pixel-based units to achieve document-style scrolling. The implementation must keep that mapping consistent.
- `PropertiesPanel` has several existing layout paths for transforms, component editors, and action buttons. The final document-height calculation must include all of them.
- Modal overlays launched from the properties panel must remain on the modal layer path and not be clipped by the panel viewport.

## Definition of Done

- The Properties panel body clips child rendering to its viewport.
- Long property content scrolls inside the panel instead of drawing outside panel bounds.
- Offscreen controls are not interactable until scrolled into view.
- Focused properties-panel tests pass, along with the adjacent regression filters above.
