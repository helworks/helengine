# Build Dialog Logs and Progress Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the Build modal with a new bottom log section and progress bar without moving any existing controls.

**Architecture:** Keep the current build-planning layout pinned to the original 560px content region, then add a separate bottom section inside the enlarged dialog panel. Reuse the existing build queue status data to populate a multiline log text block and compute progress, so the new section stays aligned with persisted queue state and does not require a new build pipeline.

**Tech Stack:** C#, engine UI components (`EditorEntity`, `RoundedRectComponent`, `SpriteComponent`, `TextComponent`), xUnit.

---

### Task 1: Add the bottom log section to `BuildDialog`

**Files:**
- Modify: `engine/helengine.editor/components/ui/BuildDialog.cs`

- [ ] **Step 1: Write the failing test**

Add a layout assertion in `engine/helengine.editor.tests/BuildDialogTests.cs` that checks the new log section exists, sits below the current footer controls, and leaves the existing footer Y positions unchanged.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~BuildDialogTests --no-restore`
Expected: the new build-log assertions fail because the log section is not yet present.

- [ ] **Step 3: Write minimal implementation**

Add `BuildLogsRoot`, a bordered log panel background, a progress bar background/fill, and a multiline read-only log text block. Keep existing control layout calculations on the original 560px content height so the lower-left controls and queue button stay put.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~BuildDialogTests --no-restore`
Expected: the new layout test passes and the existing build dialog tests remain green.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/ui/BuildDialog.cs engine/helengine.editor.tests/BuildDialogTests.cs docs/superpowers/plans/2026-05-01-build-dialog-logs-and-progress.md
git commit -m "Add build dialog log section"
```
