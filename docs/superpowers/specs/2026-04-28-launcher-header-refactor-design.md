# Launcher Header Refactor Design

## Summary

This change refactors the launcher so the window shell owns a compact, editor-aligned header with right-aligned actions, while each page focuses only on its body content.

The current launcher spends too much vertical space on static branding and duplicates page-level header chrome inside individual views. The refactor moves the existing page actions into the top-right header area, tightens the layout, and aligns the launcher more closely with the editor's working-surface look without changing the overall three-page flow.

## Goals

- Move the launcher action buttons into the top-right area of the shell header.
- Keep the current page flow: `Home`, `New Project`, and `Engines`.
- Reduce wasted header space and make the content area denser.
- Keep the current launcher identity while making it feel closer to the editor UI.
- Remove duplicated page-header layout logic from the individual page views.
- Make the launcher easier to extend with future UI changes.

## Non-Goals

- No rewrite of the launcher into a different navigation model.
- No removal of the existing three-page workflow.
- No engine-management feature expansion in this phase.
- No project-opening workflow redesign in this phase.
- No broad Avalonia theme-system rewrite beyond what supports the new shell layout.

## Current Problem

`LauncherShell` currently renders a wide top bar that is mostly static branding, while page-level actions and titles are implemented separately inside `HomeView`, `NewProjectView`, and `EnginesView`.

Current issues:

- The shell header uses premium vertical space without carrying the primary actions.
- Page views duplicate header structure and button placement logic.
- The launcher feels visually disconnected from the editor because the top region reads more like a splash layout than a tool surface.
- The content region is more vertically stacked than necessary, which wastes horizontal space.
- Future changes to button placement or page framing would need to be repeated across multiple views.

## Proposed Design

### 1. Make `LauncherShell` The Owner Of Header Chrome

`LauncherShell` becomes the single owner of:

- top header layout,
- active page title and subtitle presentation,
- right-aligned action area,
- page host,
- footer status area.

This centralizes the visual frame of the launcher in one place and removes header duplication from page views.

### 2. Introduce A Lightweight Header Contract For Pages

Each page should expose enough metadata for the shell to render the shared header.

Recommended shape:

- a page title,
- an optional subtitle,
- a collection of header actions with label, visual priority, enabled state, and click callback.

This does not need a complex MVVM layer. A small model or interface owned by the launcher UI is enough.

Rules:

- the shell renders the actions,
- pages define which actions they need,
- switching pages updates the shell header state,
- action wiring remains page-specific, but action presentation becomes consistent.

This keeps the layout responsibility in the shell while preserving page-owned behavior.

### 3. Move Existing Buttons Into The Shared Header

The initial migration should move the existing button sets rather than invent new ones.

Expected header actions by page:

- `Home`: `create project`, `browse project`, `engine versions`
- `New Project`: `back`, `browse`, `create project`, `clear`
- `Engines`: `back`, `install from folder`

Layout rules:

- actions are right-aligned and vertically centered with the header,
- primary actions use one consistent emphasized style,
- secondary actions use one consistent subdued style,
- action sizing is normalized so the header feels deliberate instead of page-specific.

This keeps the current workflow intact while fixing the most obvious spatial issue.

### 4. Simplify The Page Views

After header actions move upward, the page views should become body-only surfaces.

Changes by page:

- `HomeView` removes its top action row and focuses on recent-project content.
- `NewProjectView` removes its embedded header row and keeps only the project form and inline validation/status.
- `EnginesView` removes its embedded header row and keeps only the installed-engine list plus local page status.

This gives each page one clear responsibility and reduces repeated button and title code.

### 5. Tighten The Visual Layout To Match The Editor Better

The launcher should keep its dark-lilac palette, but the framing should feel closer to the editor's utilitarian layout.

Visual adjustments:

- reduce the apparent height and visual weight of the static branding block,
- keep branding compact on the left side of the header,
- place active page title and subtitle beside the brand instead of deep inside page content,
- reduce unnecessary outer padding around the main content area,
- use clearer panel boundaries and broader content width,
- keep the footer status area compact and unobtrusive.

The goal is not to copy the editor title bar exactly. The goal is to make the launcher feel like part of the same product family and use space like a tool, not a landing page.

### 6. Keep The Refactor Focused

This refactor should stay narrow and avoid mixing in unrelated cleanup.

Recommended boundaries:

- keep service logic in the existing service classes,
- keep page behavior in the page views,
- add only the minimal launcher-specific models or helpers needed for the shared header,
- avoid bundling project-opening or engine-install behavior changes with the layout refactor.

This keeps the change understandable and lowers the risk of UI regressions.

## Implementation Notes

Recommended implementation slices:

1. Add a small header-action model and page-header contract for launcher pages.
2. Refactor `LauncherShell` to render the shared header and update it when the active page changes.
3. Remove embedded header/button rows from `HomeView`, `NewProjectView`, and `EnginesView`.
4. Tighten shell and page spacing so the content area uses horizontal space better.
5. Refine button styling and header framing in `LauncherTheme` only as needed to support the new layout.

The shell should remain the composition root. It should not absorb service logic from the pages or become a catch-all for form validation.

## Testing

Add or update focused launcher UI tests to cover:

- the shell renders the active page actions in the top-right header area,
- switching pages updates the header title and actions,
- page views no longer render duplicated internal header action rows,
- the `Home` actions still trigger the same navigation behavior,
- the `New Project` and `Engines` actions still trigger their existing behavior after moving into the shell,
- the shell status bar continues to update as before.

Manual verification should also confirm:

- the header and action row align on one visual band,
- the launcher feels visually closer to the editor,
- the content area gains usable vertical space compared to the current layout.
