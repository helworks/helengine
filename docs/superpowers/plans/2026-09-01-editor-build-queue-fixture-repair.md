# Editor Build Queue Fixture Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task.

**Goal:** Restore the four build-queue tests that currently fail while scanning raw `{}` files pretending to be native scenes.

**Architecture:** The build-queue fixture will create minimal current-format `SceneAsset` files with embedded identities. The fixture's generic first scene will be renamed from the obsolete City example to Main; production binary-header and identity validation remain strict.

**Tech Stack:** C#/.NET 9, xUnit, HelEngine editor asset serialization

---

### Task 1: Replace raw build-queue scene placeholders

**Files:**
- Modify: `engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs`

- [ ] **Step 1: Preserve the RED class result**

Run `EditorSessionBuildQueueTests`. Confirm four tests fail with `The binary payload does not start with the HELE header.`

- [ ] **Step 2: Rename the generic first fixture scene**

Change `CurrentSceneId` and its path from `Scenes/City.helen` to `Scenes/Main.helen`. Update fixture selections and the expected visible label from `City` to `Main`. Do not introduce any DemoDisc/City project dependency.

- [ ] **Step 3: Write current-format native scenes**

Replace both constructor `File.WriteAllText(..., "{}")` calls with one class-level fixture writer that serializes a minimal `SceneAsset` through the editor `AssetSerializer`. Each scene must include its stable ID, a valid lowercase 32-character embedded `AuthoringAssetId`, and an empty root-entity array. Add substantive XML documentation to the helper.

- [ ] **Step 4: Verify the complete build-queue class**

Run `EditorSessionBuildQueueTests`. Expected: every test passes and neither binary-header nor embedded-identity validation is weakened.

- [ ] **Step 5: Commit only the fixture repair**

Commit `EditorSessionBuildQueueTests.cs` with message `Update build queue scene fixtures`.
