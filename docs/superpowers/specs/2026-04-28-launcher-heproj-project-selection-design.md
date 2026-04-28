# Launcher `.heproj` Project Selection Design

## Summary

This change makes the launcher treat the `.heproj` file as the canonical project identity instead of treating the containing folder as the project.

Today the launcher still browses for project folders, stores recent projects by directory path, and validates recent entries with `Directory.Exists(...)`. That does not match the actual product model, where a project is the `*.heproj` file. The refactor should align browse, recents, dedupe, and display behavior around the project file path.

## Goals

- Make the `.heproj` file the canonical project identity in the launcher.
- Change project browsing to select `*.heproj` files instead of folders.
- Store recent projects by full `.heproj` file path.
- Deduplicate recent projects by full `.heproj` file path.
- Show the full `.heproj` file path in recent project cards.
- Keep metadata loading behavior intact where possible while removing folder-based identity assumptions.

## Non-Goals

- No broad redesign of the launcher page flow.
- No change to engine install picking, which should remain folder-based.
- No project-opening runtime integration in this phase.
- No backward-compatibility layer for keeping folder-based recent-project entries alive unless later requested.

## Current Problem

The launcher currently mixes folder semantics into project selection:

- `Browse project` opens a folder picker.
- `BuildProjectFromFolderAsync(...)` assumes `project.heproj` lives under the selected directory.
- `RecentProject.Path` is currently treated like a folder path.
- `RecentProjectsService.LoadAsync()` validates entries with `Directory.Exists(...)`.
- Recent-project cards display the folder path instead of the actual project file.

This creates a mismatch between the launcher UX and the real object users open and reason about.

## Proposed Design

### 1. Make `.heproj` The Only Project Identity

The launcher should treat the full `.heproj` file path as the project path everywhere that refers to a project entry.

Rules:

- `RecentProject.Path` stores the full path to the selected `.heproj` file.
- Recent-project dedupe uses the full `.heproj` file path with case-insensitive comparison.
- Recent-project validity is based on `File.Exists(...)`.
- Any new recent-project entry created by the launcher should point at `.../project.heproj`, not at the containing directory.

This keeps the data model aligned with what the user actually selects.

### 2. Change `Browse Project` To A File Picker

`Browse project` should use a file picker instead of a folder picker.

Expected behavior:

- The picker title should describe project-file selection, not folder selection.
- The picker should filter to `*.heproj`.
- Cancel behavior remains unchanged.
- If the selected file is missing or invalid, the launcher should show a status message like `Selected file is not a helengine project.`

This removes the current extra inference step of selecting a folder and then hoping it contains a project file.

### 3. Replace Folder-Based Project Loading With File-Based Loading

The current `BuildProjectFromFolderAsync(...)` flow should become file-based.

Expected behavior:

- Validate that the chosen path points to an existing `.heproj` file.
- Build the default project display name from metadata first, then fall back to the file stem or parent folder name when metadata is missing.
- Set `RecentProject.Path` to the selected file path.
- Set `Created`, `LastOpened`, `Description`, and `Version` from project metadata when available.

Metadata lookup order:

1. Read the selected `.heproj` file first.
2. If additional metadata is still needed, optionally read sibling settings files such as `settings/project.json`.
3. Do not derive canonical identity from the folder even when sibling settings are consulted.

This preserves useful metadata without keeping the old folder-based assumptions.

### 4. Store Newly Created Projects By Their `.heproj` File

Projects created through the launcher should immediately enter recents using the generated project file path, not the parent directory.

That means:

- the post-create recent-project entry points at `Path.Combine(projectDirectory, "project.heproj")`,
- the display path shown in the UI matches the file users would reopen later,
- future dedupe behavior stays consistent between created and browsed projects.

This avoids having two different path conventions depending on how the project entered recents.

### 5. Show The Full `.heproj` Path In The Home View

The recent-project cards should keep the secondary path line, but it should show the full `.heproj` file path.

This is intentional, not incidental:

- users need to know which concrete project file the launcher is tracking,
- it distinguishes projects more reliably than a directory-only display,
- it matches the new canonical project identity.

No extra path shortening is required in this phase.

### 6. Let Old Folder-Based Recents Fall Out Naturally

Existing saved recent-project entries that point at directories should drop out once validation switches from `Directory.Exists(...)` to `File.Exists(...)`.

This is the cleanest default behavior because:

- the launcher data model becomes internally consistent immediately,
- no migration heuristics are needed,
- no ambiguous folder-to-file inference is preserved in the new model.

If later needed, a separate migration feature can be designed explicitly.

## Error Handling

- Invalid or non-existent `.heproj` selections should not create recent entries.
- The launcher should show a clear status message when a selected file is not a valid project.
- Metadata parsing failures should keep the launcher resilient by falling back to inferred display values, but should not change the canonical file-based identity.
- File-picker unavailability should continue to surface a platform capability message, but the text should refer to file picking instead of folder picking when appropriate.

## Testing

Add or update focused launcher tests to cover:

- `Browse project` uses a `.heproj` file-oriented path and no longer depends on folder identity.
- newly created projects are added to recents using the full `project.heproj` path.
- `RecentProjectsService` loads only entries whose `.heproj` file still exists.
- recent-project dedupe occurs by full file path.
- the home view renders the full `.heproj` path on the project card.
- invalid project-file selections show the expected error status.

Manual verification should confirm:

- selecting an existing `.heproj` file adds the correct recent-project entry,
- the recent-project card shows the full project file path,
- stale folder-based recents disappear after reload,
- create and browse both produce the same path format in recents.
