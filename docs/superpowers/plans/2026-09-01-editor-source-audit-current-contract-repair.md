# Editor Source Audit Current Contract Repair Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task.

**Goal:** Align editor-owned source audits with current engine ownership and current-format contracts, while removing the last DemoDisc-specific menu audits from the editor suite.

**Architecture:** Preserve production lifetime behavior and the current-format scanner. Source audits will assert current owner-bound services, centralized native ownership helpers, reference-counted scene audio, and shader-owned material types at their present locations. Newline-sensitive assertions will normalize checked source text instead of depending on checkout line endings.

**Tech Stack:** C#/.NET 9, xUnit, source-contract tests

---

### Task 1: Repair current-format and newline-sensitive assertions

**Files:**
- Modify: `engine/helengine.core/scene/PhysicsValidationSceneFactory.cs`
- Modify: `engine/helengine.editor.tests/CurrentFormatOnlySourceContractTests.cs`
- Modify: `engine/helengine.editor.tests/managers/asset/EditorAuthoringMutationJournalTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`

- [ ] Replace the forbidden historical-format word in the physics validation comment with current terminology; do not weaken the scanner.
- [ ] Normalize source line endings in mutation-journal and generated-core assertions before checking multiline snippets.

### Task 2: Update current ownership and feature-catalog audits

**Files:**
- Modify: `engine/helengine.editor.tests/serialization/FontAssetSourceAuditTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/HelengineFeatureCatalogIntegrityTests.cs`
- Modify: `engine/helengine.editor.tests/SceneManagerSourceTests.cs`
- Modify: `engine/helengine.editor.tests/ViewportWorkspacePanelControllerSourceTests.cs`
- Modify: `engine/helengine.editor.tests/RuntimeSceneAssetReferenceResolverSourceTests.cs`

- [ ] Assert `FontAsset` delegates source-texture lifetime through `DisposeAndDelete` and `TextureAsset` owns pixel-array release.
- [ ] Expect the current host-filesystem source feature-catalog entry rather than removed `ContentManager` ownership.
- [ ] Assert SceneManager's reference-counted transient audio release path.
- [ ] Assert viewport interaction services are derived from the current viewport instance.
- [ ] Update runtime asset resolver assertions for owner-bound renderer/content sources, direct generated-asset tracking, and copied ownership lists passed to `CreateOwned`.

### Task 3: Update engine runtime ownership audits and remove DemoDisc coupling

**Files:**
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeOwnershipSourceAuditTests.cs`

- [ ] Update entity, camera, drawable, viewport-snapshot, and FPS assertions to current centralized/native owner-bound forms.
- [ ] Relocate material-runtime assertions to `helengine.shader` source paths for `ShaderRuntimeMaterial`, `MaterialLayout`, and `MaterialPropertyBlock`.
- [ ] Delete the two obsolete menu teardown/transition test methods; do not add replacement editor audits for DemoDisc runtime code.

### Task 4: Verify the focused source contract

**Files:**
- Modify: none

- [ ] Run the nine source-audit classes together. Expected: 160 passed, 3 skipped, 0 failed after deleting two obsolete tests.
- [ ] Run `git diff --check` and inspect all production/test changes.
- [ ] Commit only the planned files with message `Update editor source audits to current ownership`.
