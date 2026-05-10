# Asset Browser Right-Click Selection Design

**Goal:** Right-clicking an asset row in the asset browser should select that row first, then open the existing context menu.

**Architecture:** Keep the change local to the asset browser panel and view. The browser view should expose a selection helper that can select a row without reusing activation behavior, and the panel should use that helper when the right-click lands on a different row. The existing context menu and left-click activation paths stay unchanged.

**Tech Stack:** C#, existing editor UI components, existing asset browser view and panel classes.

---

### Behavior

- Right-clicking a different asset row updates the browser selection immediately.
- After the selection update, the asset browser panel opens the existing context menu at the clicked position.
- Right-clicking the already selected row opens the context menu without re-emitting selection changes.
- Right-clicking empty space keeps the current behavior unchanged.

### Files

- Modify `engine/helengine.editor/components/ui/asset/AssetBrowserView.cs`
- Modify `engine/helengine.editor/components/ui/asset/AssetBrowserPanel.cs`
- Add or update tests in `engine/helengine.editor.tests/components/ui/asset/`

### Testing

- Add a regression that right-clicking a different row makes that row the selected row before the context menu opens.
- Add a regression that right-clicking the same row does not reselect or duplicate the selection event.
- Keep the existing left-click activation path unchanged.
