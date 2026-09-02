# Editor Current-Format Fixture Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task.

**Goal:** Repair editor tests that still manufacture pre-identity or otherwise obsolete native asset fixtures, while preserving strict current-format loading and canonical-path validation.

**Architecture:** Native editor fixtures must use the same current binary envelope and embedded identity contract as authored assets. Test writers will assign deterministic lowercase 32-character identities before serialization. Assertions that depended on retired payload wording or a single-component entity will be updated to the current typed/runtime-ID-aware contract. Blueprint loading will use the editor-native deserializer, never the packaged runtime serializer.

**Tech Stack:** C#/.NET 9, xUnit, HelEngine editor binary serialization

---

### Task 1: Modernize platform-packager native fixtures

**Files:**
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerAudioTests.cs`

- [ ] **Step 1: Preserve representative RED evidence**

Run one scene-packager test and the audio packager class. Confirm they fail because `.helen` fixtures are missing embedded identity metadata.

- [ ] **Step 2: Add deterministic embedded identities to native fixture writers**

Follow the deterministic SHA-256-derived identity pattern already used in `EditorPlatformAssetCookServiceTests`. Add a class-level test helper where useful; do not add production utilities or sidecars. Ensure every serialized native scene, animation clip, texture, and shader fixture in the scoped test classes has a non-empty lowercase 32-character `AuthoringAssetId`. Route direct scene serialization through the current writer or set the identity explicitly. Preserve any identity deliberately supplied by a test.

- [ ] **Step 3: Remove the remaining project-specific City packaging test**

Delete `Package_WhenWindowsBuilderCompatibilityMetadataAndScriptResolverAreSupplied_PackagesCityStyleScriptComponents` and any helper types used exclusively by it. This is project behavior and does not belong in the editor suite. Do not replace it with a lookup into DemoDisc or another sibling project.

- [ ] **Step 4: Verify the two packager classes**

Run both complete test classes. Expected: identity failures are gone, the City-specific test no longer exists, and production identity validation remains unchanged.

- [ ] **Step 5: Commit only Task 1 files**

Commit with message `Update packager tests to current asset identity`.

### Task 2: Modernize runtime scene-load and scene-file fixtures

**Files:**
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`

- [ ] **Step 1: Add embedded identity to source-scene fixture writes**

Ensure direct `.helen` scene writes use deterministic embedded identities. Do not allow identity-less native files through production code.

- [ ] **Step 2: Preserve canonical packaged paths**

Change the mixed-case animation fixture ID, path, reference, and expected value consistently to `animations/runtime-scene-load.hanim`. Keep `CanonicalPackagedAssetPath.ValidateCanonical` strict.

- [ ] **Step 3: Update automatic payload-version assertions**

Make the two obsolete version-error assertions verify the current diagnostic: received version, current version, and regenerate/rebuild guidance. Do not reintroduce compatibility wording or fallback behavior.

- [ ] **Step 4: Make component assertions runtime-ID-aware**

For the sprite and rounded-rectangle cases, select the expected typed component rather than asserting that the entity has exactly one component. Preserve the automatically attached `SceneEntityRuntimeIdComponent`.

- [ ] **Step 5: Exercise malformed files at the intended layer**

In `Load_WhenSceneFileIsInvalid_ThrowsInvalidOperationException`, create the load service and its identity index before publishing `Broken.helen`, then invoke the load. This keeps identity-index initialization from preempting the `SceneFileLoadService` wrapping behavior under test.

- [ ] **Step 6: Verify both complete test classes and commit**

Run `RuntimeSceneLoadServiceTests` and `SceneFileLoadServiceTests`. Commit only the two listed files with message `Update scene loading tests to current format`.

### Task 3: Modernize asset-browser fixtures and fix Blueprint editor deserialization

**Files:**
- Modify: `engine/helengine.editor.tests/managers/asset/SceneAssetBrowserIntegrationTests.cs`
- Modify: `engine/helengine.editor.tests/managers/asset/BlueprintAssetBrowserIntegrationTests.cs`
- Modify: `engine/helengine.editor/content/EditorContentManagerConfiguration.cs`

- [ ] **Step 1: Add embedded identities to browser fixtures**

Assign valid current embedded identities to the serialized Scene and Blueprint assets. Do not rely on the asset manager swallowing invalid fixture exceptions.

- [ ] **Step 2: Add a focused editor Blueprint processor assertion if existing coverage is insufficient**

The integration test must prove that the shared editor content manager loads a current native Blueprint file as `BlueprintAsset`.

- [ ] **Step 3: Register Blueprint with the editor-native deserializer**

Replace only the Blueprint registration's core `AssetContentProcessor<BlueprintAsset>` with a `BinaryContentProcessor<BlueprintAsset>` that deserializes through `global::helengine.files.AssetSerializer` and validates/casts the result to `BlueprintAsset`. Do not add Blueprint handling to `PackagedAssetBinarySerializer` and do not alter runtime packaged asset semantics.

- [ ] **Step 4: Verify both browser integration classes and commit**

Run both complete classes. Commit only the three listed files with message `Load Blueprint assets with editor serialization`.

### Task 4: Verify the repaired current-format cluster

**Files:**
- Modify: none

- [ ] **Step 1: Run the combined focused filter**

Run all six repaired test classes in one test command. Expected: every selected test passes with no code generator UI and no testhost abort.

- [ ] **Step 2: Confirm strict production contracts remain intact**

Run `AssetIdentityMetadataServiceTests`, `CurrentFormatOnlySourceContractTests`, and the binary serialization tests that cover embedded asset identity. Record any independent failures for their own repair; do not weaken the loaders to make this cluster green.
