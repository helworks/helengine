# Scene Hierarchy Tree View Design

## Summary

This change turns the editor scene hierarchy from an always-expanded indented list into a tree view with explicit expand and collapse controls.

Parent rows gain a left-side arrow affordance. Clicking that arrow toggles only that row's expanded state. Clicking the rest of the row keeps the current selection behavior. When the hierarchy has keyboard focus, `Up` and `Down` move across visible rows, `Right` expands the focused parent row, and `Left` collapses the focused parent row.

The implementation builds on the current `SceneHierarchyPanel` row-pooling model and preserves the recently added row context menu and reparent flow.

## Goals

- Render the scene hierarchy as a tree view with explicit expand and collapse affordances.
- Make only the left arrow toggle expansion state.
- Preserve existing row-body selection behavior.
- Preserve existing row right-click context-menu behavior.
- Persist expansion state across hierarchy refreshes for entities that still exist.
- Default newly seen parent entities to expanded so new branches remain visible.
- Add keyboard navigation for focused hierarchy rows using `Up`, `Down`, `Left`, and `Right`.
- Add regression coverage for mouse and keyboard tree-view behavior.

## Non-Goals

- No rewrite of the hierarchy into a new recursive widget system.
- No change to drag-and-drop or reparenting interaction beyond preserving current behavior.
- No change to row selection semantics outside the new tree-view keyboard rules.
- No rename or redesign of the existing row context menu.
- No generalized tree-view framework for other editor panels in this phase.

## Current Problem

`SceneHierarchyPanel` currently flattens every visible entity into one always-expanded list and uses indentation alone to communicate parent-child structure.

Current gaps:

- Parent rows cannot be collapsed.
- Large scenes always show every descendant row.
- The hierarchy has no explicit tree-view affordance.
- Keyboard focus exists for rows, but there is no tree-navigation behavior for `Left` and `Right`.
- Visible-row traversal assumes every descendant is always present.

This makes the hierarchy harder to scan in large scenes and prevents common tree-view workflows users expect in an editor.

## Proposed Design

### 1. Keep `SceneHierarchyPanel` As The Tree-State Owner

`SceneHierarchyPanel` remains the owner of hierarchy presentation and input wiring.

It gains a persistent expanded-state map keyed by `Entity`. This state determines which descendants are included in the flattened visible row list during hierarchy refresh and layout.

Rules:

- Parent entities default to expanded the first time they appear in the panel.
- Leaf entities do not need expansion state.
- Refreshing the hierarchy preserves expansion state for entities that still exist.
- Removed entities are pruned from the expanded-state map during refresh.

This keeps the change local to the existing panel instead of introducing a separate tree model or recursive widget hierarchy.

### 2. Flatten Only Visible Branches

`RefreshHierarchy()` will continue to build a flat row list, but recursion will now depend on the expanded state of each parent.

Rules:

- Root entities are always considered visible.
- Child entities are appended only when their parent is currently expanded.
- Visible-row order remains the same pre-order traversal already used by the panel.
- Existing row pooling and row reuse stay intact.

This preserves the panel's current rendering and focus architecture while making visibility branch-aware.

### 3. Add A Dedicated Arrow Affordance To Rows

`SceneHierarchyRow` gains explicit tree-view presentation state and arrow hit-target support.

Each parent row will render a small left-side arrow glyph:

- collapsed parent: `>`
- expanded parent: `v`
- leaf row: no arrow glyph

Layout rules:

- The arrow occupies a fixed left-side slot before the label indentation area.
- The label indentation still reflects depth.
- Clicking inside the arrow slot toggles expansion for that row only.
- Clicking outside the arrow slot but inside the row body preserves normal row selection.

This makes expand and collapse a precise action that does not interfere with selection.

### 4. Preserve Existing Context Menu Behavior

The existing scene-hierarchy right-click context menu stays row-based and continues to open from the row body.

Rules:

- Right-clicking a row opens the existing context menu for that entity.
- Right-clicking a parent row does not toggle its expansion state.
- Right-clicking the arrow area should still behave like a row right-click, not an expansion toggle.

This keeps the new tree affordance compatible with the recently added `Reparent` workflow.

### 5. Add Keyboard Tree Navigation

When the scene hierarchy owns keyboard focus, navigation follows visible rows only.

Rules:

- `Up` moves focus to the previous visible row.
- `Down` moves focus to the next visible row.
- `Right` expands the focused row when it is a collapsed parent.
- `Left` collapses the focused row when it is an expanded parent.
- Pressing `Right` on an already expanded parent does nothing.
- Pressing `Left` on a collapsed row does nothing.
- Leaf rows ignore `Left` and `Right`.

Focus and selection rules:

- The hierarchy keeps its current row-focus model.
- Keyboard traversal uses the visible row order after expansion filtering.
- Expansion and collapse should refresh visible rows without breaking focus ownership.
- If the currently focused entity remains visible after refresh, focus should stay on that entity's row.
- If a collapse hides the previously focused descendant, focus should remain on the parent row that was collapsed.

This matches common tree-view expectations without changing the editor's broader keyboard-focus architecture.

### 6. Visual Priority

Tree-view visuals should compose with the panel's existing row-state styling.

Priority:

- pressed
- hover
- selected
- keyboard focused
- base color

The arrow glyph must remain readable in every row state, but the row background behavior should continue following the current hierarchy-row rules.

## Implementation Notes

Recommended implementation slices:

1. Extend `SceneHierarchyRow` with arrow visual state and arrow hit-region metadata.
2. Extend `SceneHierarchyPanel` refresh and layout code to maintain expanded state and build only visible branches.
3. Update row input handling so left-click distinguishes arrow toggles from row selection.
4. Extend hierarchy keyboard handling for `Left` and `Right` while preserving current `Up` and `Down` behavior across visible rows.
5. Add targeted tests for arrow toggling, preserved selection behavior, preserved context-menu behavior, and keyboard traversal.

The panel should avoid placing tree-state logic into `EditorSession`. Expansion state is presentation state and belongs to the hierarchy UI.

## Testing

Add or update tests to cover:

- parent rows render a tree affordance while leaf rows do not,
- clicking the arrow on a parent collapses its descendants,
- clicking the arrow again re-expands its descendants,
- clicking the row body still selects without toggling expansion,
- right-clicking a parent row still opens the existing context menu,
- `Right` expands the focused collapsed parent,
- `Left` collapses the focused expanded parent,
- `Up` and `Down` move across only currently visible rows,
- refresh preserves expansion state for surviving entities.

Run the most targeted editor test coverage available for `SceneHierarchyPanel` keyboard and row interaction behavior.
