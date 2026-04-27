# Open Map Selection Highlight Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep the clicked map row highlighted in the Open Map modal until the user selects a different map or closes the modal.

**Architecture:** Persist file-row selection inside `AssetBrowserView` using each entry's stable `RelativePath`, then clear that state from `OpenFileDialog` when the modal resets. Cover the behavior with a focused `OpenFileDialogTests` regression that activates a scene row and inspects the row background color.

**Tech Stack:** C#, xUnit, Helengine editor UI components, modal asset-browser view

---

### Task 1: Add a failing Open Map selection test

**Files:**
- Modify: `engine/helengine.editor.tests/OpenFileDialogTests.cs`

- [ ] Write a regression test that shows the dialog, activates one `.helen` row through `AssetBrowserView`, and asserts that the matching row background remains `ThemeManager.Colors.AccentSecondary`.
- [ ] Run the focused `OpenFileDialogTests` selection test and confirm it fails before implementation.

### Task 2: Persist browser row selection

**Files:**
- Modify: `engine/helengine.editor/components/ui/asset/AssetBrowserView.cs`
- Modify: `engine/helengine.editor/components/ui/asset/AssetBrowserRow.cs`
- Modify: `engine/helengine.editor/components/ui/asset/OpenFileDialog.cs`

- [ ] Add explicit selected-row state to `AssetBrowserRow` and persistent selected-entry tracking to `AssetBrowserView`.
- [ ] Update row background precedence so selected rows keep the accent highlight after pointer release while preserving pressed, hover, and keyboard-focus feedback.
- [ ] Clear browser selection when the open dialog shows, hides, or the list background clears selection.

### Task 3: Verify the modal behavior

**Files:**
- Verify: `engine/helengine.editor.tests/OpenFileDialogTests.cs`

- [ ] Re-run the focused Open Map selection test and confirm it passes.
- [ ] Run adjacent open-dialog coverage to ensure the change does not regress scene-open behavior or modal hit testing.
