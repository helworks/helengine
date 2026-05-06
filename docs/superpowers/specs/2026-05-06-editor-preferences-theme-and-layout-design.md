# Editor Preferences Theme And Layout Design

## Summary

Expand the editor-global Preferences dialog so it is sized for future growth, and add a new editor-global `Theme` preference that is applied only when the user clicks `Apply`.

The implementation should avoid hardcoded theme options inside the dialog. Theme selection must come from a reusable catalog with stable ids, display names, and palette factories so new themes can be added later by registering one additional catalog entry.

## Goals

- Make the Preferences dialog substantially larger so it has comfortable headroom for additional editor-global settings later.
- Add a `Theme` option to Preferences.
- Keep theme selection editor-global, not project-scoped.
- Persist the selected theme across editor restarts.
- Apply both theme and UI scale only on `Apply`.
- Make future theme additions low-friction and localized.

## Non-Goals

- Do not introduce project-level theme settings.
- Do not add live preview while the Preferences dialog is open.
- Do not build a full generic settings framework in this change.
- Do not redesign the editor theme system beyond what is needed for a stable theme catalog and persistence.

## Current Context

Today `EditorPreferencesDialog` only edits UI scale and is sized tightly around two selectors. `EditorPreferencesService` persists only `UiScaleMode` and `UiScalePercent`. `ThemeManager` already exposes multiple built-in palettes:

- `CreateNeon90s()`
- `CreateDarkTheme()`
- `CreateLightTheme()`

Those palettes exist at runtime, but there is no stable persisted `ThemeId`, no theme catalog abstraction, and no theme selector in Preferences.

## Architecture

### Editor Theme Catalog

Introduce one editor theme catalog responsible for defining available themes in one place. Each entry should contain:

- `Id`
- `DisplayName`
- a palette factory or palette resolver

The catalog should be the single source of truth for:

- the themes shown by `EditorPreferencesDialog`
- validation of persisted `ThemeId`
- mapping persisted ids back to runtime palettes
- future theme additions

Adding a new theme later should require adding one new catalog entry rather than modifying dialog string arrays, persistence validation logic, and session apply code separately.

### Editor Preferences Value Object

Move the preferences flow from a scale-only payload toward one editor-global preferences value object that contains:

- UI scale settings
- `ThemeId`

This combined preferences object should be used by:

- `EditorPreferencesDialog.Show(...)`
- `EditorPreferencesDialog.ConfirmRequested`
- `EditorPreferencesService.Load()`
- `EditorPreferencesService.Save(...)`
- the `EditorSession` apply path

Existing scale-specific editor-session behavior can remain internally, but the dialog and persistence boundary should stop pretending Preferences are scale-only.

## Data Model

### Persisted Preferences Document

Extend `EditorPreferencesDocument` with a persisted `ThemeId` field.

The document should contain:

- `UiScaleMode`
- `UiScalePercent`
- `ThemeId`

Default values:

- `UiScaleMode = Auto`
- `UiScalePercent = 100`
- `ThemeId =` the catalog id for the current default theme (`Neon 90s`)

### Validation Rules

`EditorPreferencesService.Load()` should validate both preference dimensions:

- UI scale must still match the currently supported scale model.
- `ThemeId` must resolve to a known catalog entry.

If any persisted value is invalid or missing:

- use a valid default
- rewrite the document to normalized valid data

This should match the service’s existing recovery behavior for malformed or unsupported UI scale data.

## Dialog Layout And UX

### Dialog Size

Increase the Preferences dialog from the current compact shell to a much larger future-facing shell.

Recommended new default and minimum size:

- `PanelWidth = 560`
- `PanelHeight = 420`

This is intentionally larger than the current content requires. The goal is to leave real unused content space for more editor-global settings later, without immediately revisiting shell dimensions.

### Form Structure

Keep the dialog as a simple vertical form. Do not introduce grouped panels yet.

The visible rows should be:

1. `Theme`
2. `Scale Mode`
3. `Scale Override`

Behavior:

- `Theme` is always enabled.
- `Scale Mode` keeps current behavior.
- `Scale Override` remains enabled only when `Scale Mode = Override`.
- `Apply` commits all pending edits together.
- `Cancel` and title-bar close discard all pending edits.

The dialog should initialize from current editor-global preferences every time it opens, so reopening after cancel shows the last applied values.

### Theme Options

The theme combo box should use the catalog’s display names, not a hardcoded local array.

Initial built-in options should be:

- `Neon 90s`
- `Dark`
- `Light`

The persisted value should be the stable catalog id, not the display name.

## Session Apply Flow

`EditorSession` should treat Preferences as editor-global runtime state.

When Preferences opens:

- load current editor-global preferences state
- show the current `ThemeId`
- show the current UI scale settings

When the user clicks `Apply`:

- persist the combined editor preferences object
- resolve and apply the selected theme through the theme catalog and `ThemeManager`
- apply UI scale through the existing scale path

When the user changes the theme selection inside the open dialog:

- do not apply anything yet

The theme should not change until the confirmed `Apply` action.

## Theme Application Rules

Theme application should continue to flow through `ThemeManager` as the runtime source of truth.

The new theme catalog should resolve a selected `ThemeId` to the palette that `ThemeManager.SetTheme(...)` receives.

Theme changes must update the existing editor surfaces that already depend on `ThemeManager.Current`, including the scene viewport background path in `EditorSession`.

This change should not invent a second runtime theme state.

## Testing

### Dialog Tests

Extend `EditorPreferencesDialogTests` to cover:

- the larger dialog minimum size
- show-time layout for the added `Theme` label and combo box
- theme selector loads the currently selected theme
- `Apply` returns combined editor preferences with both theme and scale values
- `Theme` selector remains enabled while `Scale Override` still toggles with scale mode

### Preferences Service Tests

Extend `EditorPreferencesServiceTests` to cover:

- default document now includes the default `ThemeId`
- persisted `ThemeId` round-trips correctly
- invalid persisted `ThemeId` falls back to default and rewrites the file
- malformed documents still recover to a valid normalized preferences document

### Session Tests

Extend `EditorSessionPreferencesTests` to cover:

- confirmed Preferences changes apply the selected theme only on `Apply`
- canceling or closing Preferences does not apply pending theme edits
- existing UI scale apply behavior still works alongside the new theme preference

## Implementation Constraints

- Keep this feature editor-global only.
- Do not store theme selection in project files, scene files, or build config.
- Avoid hardcoded duplicated theme lists across dialog, session, and persistence code.
- Prefer a registry/catalog shape that can scale to more themes later without refactoring the dialog contract again.

## Rollout Notes

This is intentionally a targeted preferences expansion, not a general settings framework. The dialog shell, preference payload, and theme catalog should be designed so more editor-global options can be added later with incremental rows and tests, instead of another full modal redesign.
