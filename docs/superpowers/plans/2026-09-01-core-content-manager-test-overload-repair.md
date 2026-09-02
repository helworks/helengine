# Core Content Manager Test Overload Repair Plan

**Goal:** Restore the reusable content-manager registration test without weakening the production renderer guard.

**Root cause:** `ConfigureProjectContentManager_WhenCalledTwice_RemainsReusable` constructs an uninitialized `Core` and calls the render-dependent configuration overload with `Core.Instance.RenderManager2D`, which is correctly null until core initialization.

### Task 1: Use the render-independent registration seam

**Files:**
- Modify: `engine/helengine.editor.tests/CoreContentManagerTests.cs`

- [ ] Change only the failing test call to `EditorContentManagerConfiguration.ConfigureSharedAssetContentManager(contentManager)`.
- [ ] Keep the production render-dependent overload and its null guard unchanged.
- [ ] Run `CoreContentManagerTests`; expect 5/5 passing.
- [ ] Run `git diff --check` and commit only the test with message `Use render-independent content manager test seam`.
