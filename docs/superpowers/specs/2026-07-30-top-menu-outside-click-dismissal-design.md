# Top Menu Outside-Click Dismissal

## Goal

Close the editor title-bar menu stack whenever the user presses either mouse button anywhere outside its visible top-level menu or submenu.

## Behavior

- A press inside any visible title-bar context menu keeps the entire menu stack open so the receiving menu can process the interaction.
- A press on another top-level title-bar menu button retains the existing menu-switching behavior.
- A press anywhere else in the editor closes every title-bar menu and submenu before the receiving editor control continues its normal input handling.
- The behavior applies only to the title-bar menu stack; unrelated panel context menus retain their existing dismissal ownership.

## Design

`EditorTitleBar` will own the menu-stack dismissal decision because it owns the complete set of title-bar menus and submenus. A lightweight update component attached to the title-bar root will inspect pointer press state once per frame. When one menu is visible and the pointer is outside every visible title-bar menu and top-level menu button, it will call the existing `HideMenus` method.

The observer will run independently of whichever editor interactable receives the click. This avoids relying on the visible context menu update component to infer a title-bar-wide dismissal while another engine control has captured pointer input.

## Verification

Regression tests will prove that an outside press closes the File menu, a press inside an open menu preserves it until normal item activation, and switching to another top-level menu remains unchanged.
