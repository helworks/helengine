# Editor DPI Scaling Design

## Goal

Add editor UI DPI scaling that works on high-DPI monitors and supports one user-controlled global override.

The editor must support two scale modes:
- `Auto`: use the current monitor DPI as the effective editor UI scale
- `Override`: use one explicit user-selected scale value as the full effective editor UI scale

The override must replace monitor DPI scaling for editor chrome and layout. It must not multiply the monitor DPI scale.

This design applies to editor UI chrome and layout metrics. It does not change scene render resolution or runtime backbuffer sizing.

## Scope

This slice adds the editor-side infrastructure needed to persist, resolve, and apply UI scale preferences at runtime.

The first implementation includes:
- one global editor preferences file
- one focused `Preferences...` entry in the File menu
- one DPI scale preference with `Auto` and explicit override modes
- one central runtime UI scale/metrics layer
- live application of scale changes inside the current editor window without restarting the process

This slice does not attempt a full editor-wide pixel-constant rewrite in one pass. The first implementation should move the most visible editor chrome and shared layout metrics onto the new scale system so the feature is usable without claiming every control has been fully normalized.

## Preference Ownership

UI scale is a global editor preference, not a per-project setting.

Project-local settings already live under `user_settings` inside each project and are used for build configuration, platform selection, and other project-scoped editor behavior. DPI scaling is different:
- it reflects user workstation preference
- it should follow the user across projects
- it belongs to the editor host, not to build output or project content

The preference should be stored in a user-profile path such as:

`%AppData%\helengine\editor\preferences.json`

This location keeps the setting user-specific, portable across projects, and extendable for future editor-global preferences.

## Data Model

Add a small editor preferences document with room for future settings.

Recommended types:
- `EditorPreferencesDocument`
- `EditorPreferencesService`
- `EditorUiScaleMode`
- `EditorUiScaleSettings`
- `EditorUiMetrics`

The initial document only needs the UI scale setting.

Recommended shape:
- `UiScaleMode`
- `UiScalePercent`

Where:
- `UiScaleMode` is `Auto` or `Override`
- `UiScalePercent` is required when the mode is `Override`

The persisted value should be constrained to a known set of supported percentages in the first slice:
- `75`
- `100`
- `125`
- `150`
- `175`
- `200`

The service should validate these values strictly instead of accepting arbitrary percentages in the first version.

## Runtime Scale Resolution

The editor host should resolve one effective scale for the current window:

- when mode is `Auto`, effective scale = current monitor DPI scale
- when mode is `Override`, effective scale = explicit override percent converted to a scale factor

Examples:
- `Auto` on a 150% monitor resolves to `1.5`
- `Override` set to `125%` resolves to `1.25`, even on a 150% monitor

The host must never combine both values. Override semantics are explicit and total.

`MainForm` should own monitor DPI detection because it already sits at the WinForms boundary and already handles resize and window lifecycle events. It is the correct place to observe monitor-DPI changes, reload effective scale, and trigger editor UI refresh behavior.

## Central UI Metrics Layer

The editor needs one shared runtime source of scaled UI values instead of scattering ad-hoc multiplication across controls.

`EditorUiMetrics` should expose scaled values derived from the current effective scale. It should be the single place that answers questions such as:
- what is the current title bar height
- what is the dock title bar height
- what font pixel size should the base UI use
- what font pixel size should the snap modifier overlay use
- what are the current shared paddings, row heights, and dialog chrome sizes

This keeps scale math centralized and makes it possible to migrate more editor UI constants over time without changing the persistence model again.

The first pass should route the following through metrics:
- base UI font size
- snap modifier font size
- title bar height
- title bar icon size and padding
- dock title bar height
- dock tab strip height
- common dialog chrome sizes
- key paddings and row heights used by shared editor panels

## Preferences UI

Add a `Preferences...` command to the editor File menu.

The first version should be a focused modal, not a full multi-category preferences shell. The modal only needs the controls required for UI scaling:
- one mode selector with `Auto` and `Override`
- one override percentage selector
- standard apply and close/cancel actions

Behavior:
- when `Auto` is selected, the override value control is disabled
- when `Override` is selected, the selected percentage becomes the entire effective UI scale
- pressing Apply persists the new value and updates the current editor window immediately

The fixed percentage list should match the validated persisted values so the UI cannot author unsupported settings.

## Live Update Strategy

The editor should apply preference changes live when possible and without destabilizing the current session.

The preferred update pipeline is:
1. persist the validated preference
2. resolve the new effective scale
3. rebuild scale-sensitive resources such as fonts and shared metrics
4. rerun layout for the current editor presentation

The design should not rely on patching individual controls opportunistically. That approach would leave the editor in mixed-scale state and create hard-to-debug layout drift.

The safer approach is one controlled presentation refresh path that updates the scale-dependent editor UI resources in one place. If the current architecture makes true in-place mutation too coupled, the fallback should be one bounded rebuild of the editor presentation layer while preserving editor session state as much as possible.

This fallback is still acceptable as long as:
- the process does not restart
- the current project remains open
- the current editor session state is preserved or deterministically restored

## Monitor DPI Changes

When the editor is in `Auto`, moving the window between monitors or receiving a DPI change notification should trigger the same scale refresh pipeline used by a preferences change.

When the editor is in `Override`, monitor DPI changes should not alter the effective editor UI scale. The host may still receive WinForms DPI events, but the editor metrics layer should continue using the override value.

This keeps the model predictable:
- `Auto` tracks the monitor
- `Override` tracks the saved preference

## Failure Behavior

Preferences persistence should be strict about valid values and tolerant of bad files.

Expected behavior:
- missing preferences file: create defaults using `Auto`
- malformed preferences file: fall back to defaults and rewrite a valid document
- unsupported scale mode or percentage: fall back to defaults and rewrite a valid document

The editor should fail fast on internal contract violations such as:
- a missing scale value when mode is `Override`
- a metrics request made before scale initialization
- a host path attempting to apply a non-positive effective scale

The editor should not silently combine monitor DPI and override scale, and it should not invent arbitrary percentages for invalid files.

## Testing

The implementation should add coverage for:
- creating default global preferences when no file exists
- saving and reloading `Auto` mode
- saving and reloading `Override` mode
- rejecting malformed or unsupported persisted values and restoring defaults
- resolving effective scale from monitor DPI in `Auto`
- resolving effective scale from explicit override in `Override`
- ignoring monitor DPI changes while in `Override`
- responding to monitor DPI changes while in `Auto`
- updating title bar and shared metrics when the effective scale changes
- opening `Preferences...` from the File menu
- applying a preference change without losing the active editor session

At least one integration-style test should prove that scale changes trigger layout refresh behavior in a live editor session instead of only updating stored data.

## Out Of Scope

This slice does not include:
- arbitrary free-form scale percentages
- per-project UI scale settings
- scene or game render-resolution scaling
- runtime/player DPI scaling changes
- a full editor-wide audit of every hard-coded pixel value
- a generalized multi-page preferences system

Those can be added later on top of the global preferences and metrics foundation introduced here.
