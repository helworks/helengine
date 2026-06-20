# Remove Platform-Specific Shared Engine Paths Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove DS, 3DS, Windows-host, and PS2 identity branches from shared engine projects while preserving only generic shared behavior.

**Architecture:** Apply the cleanup in small production/test batches. Each batch first updates focused tests to encode the new generic behavior, then removes the corresponding shared-code branch, then runs a narrow validation slice before proceeding.

**Tech Stack:** C#, xUnit, shared engine/editor/platform projects, `dotnet test`

---

### Task 1: Document And Snapshot Scope

**Files:**
- Create: `docs/superpowers/specs/2026-06-20-remove-platform-specific-shared-engine-paths-design.md`
- Create: `docs/superpowers/plans/2026-06-20-remove-platform-specific-shared-engine-paths.md`

- [ ] **Step 1: Verify the shared-code targets**

Run: `rtk rg -n --hidden -S -e NintendoDs -e Nintendo3Ds -e WindowsLauncherInstallRootLocator -e helengine-windows -e PreferredEditorPreviewPlatformId -e PS2_PLATFORM -e Ps2DiscPathResolver C:\dev\helworks\helengine-gc\.worktrees\helengine\engine --glob '!**/tests/**'`
Expected: matches only in shared production projects that are in scope for this purge.

- [ ] **Step 2: Save the approved design and plan**

Write the design and plan documents listed above exactly as checked into git for this cleanup batch.

- [ ] **Step 3: Commit the documentation**

Run:

```bash
rtk git -C C:\dev\helworks\helengine-gc\.worktrees\helengine add docs/superpowers/specs/2026-06-20-remove-platform-specific-shared-engine-paths-design.md docs/superpowers/plans/2026-06-20-remove-platform-specific-shared-engine-paths.md
rtk git -C C:\dev\helworks\helengine-gc\.worktrees\helengine commit -m "Document shared engine platform-specific purge"
```

Expected: a documentation-only commit on `main`.

### Task 2: Remove DS And 3DS Runtime And Editor Branches

**Files:**
- Modify: `engine/helengine.core/components/2d/DebugComponent.cs`
- Modify: `engine/helengine.bepu/BepuRuntimeComponentRegistration.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildQueueItemDocument.cs`
- Modify: `engine/helengine.editor/managers/project/EditorGeneratedBootScenePreparationService.cs`
- Test: `engine/helengine.editor.tests/DebugComponentTests.cs`
- Test: `engine/helengine.bepu.tests/BepuRuntimeComponentRegistrationTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorBuildQueueItemDocumentTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorGeneratedBootScenePreparationServiceTests.cs`

- [ ] **Step 1: Rewrite the focused tests to encode generic behavior**

Update the tests so they assert:

- `DebugComponent` always emits the generic overlay labels.
- `BepuRuntimeComponentRegistration` always attaches the default world schedule.
- Build queue creation no longer injects DS companion scenes.
- Generated boot-scene preparation no longer writes DS/3DS remapping entries.

- [ ] **Step 2: Run the focused tests to capture expected failures**

Run:

```bash
.\dotnetw.ps1 test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~DebugComponentTests|FullyQualifiedName~EditorBuildQueueItemDocumentTests|FullyQualifiedName~EditorGeneratedBootScenePreparationServiceTests" -v minimal /p:BuildInParallel=false /p:UseSharedCompilation=false
.\dotnetw.ps1 test .\engine\helengine.bepu.tests\helengine.bepu.tests.csproj --filter "FullyQualifiedName~BepuRuntimeComponentRegistrationTests" -v minimal /p:BuildInParallel=false /p:UseSharedCompilation=false
```

Expected: failures that still reflect the removed DS and 3DS branches.

- [ ] **Step 3: Remove the production DS and 3DS branches**

Implement the minimal production changes:

- Delete `UsesNintendoDsPerformanceOverlayLabels` and the DS-only label path from `DebugComponent`.
- Delete the DS solve-schedule constants and branch from `BepuRuntimeComponentRegistration`.
- Make `ApplyPlatformSceneExpansions(...)` in `EditorBuildQueueItemDocument` a no-op.
- Make `EditorGeneratedBootScenePreparationService.BuildMappings(...)` return `null` for all platforms and remove DS/3DS helper methods/constants.

- [ ] **Step 4: Run the focused tests to verify the batch**

Run the same commands from Step 2.
Expected: PASS.

- [ ] **Step 5: Commit the DS and 3DS purge**

Run:

```bash
rtk git -C C:\dev\helworks\helengine-gc\.worktrees\helengine add engine/helengine.core/components/2d/DebugComponent.cs engine/helengine.bepu/BepuRuntimeComponentRegistration.cs engine/helengine.editor/managers/project/EditorBuildQueueItemDocument.cs engine/helengine.editor/managers/project/EditorGeneratedBootScenePreparationService.cs engine/helengine.editor.tests/DebugComponentTests.cs engine/helengine.bepu.tests/BepuRuntimeComponentRegistrationTests.cs engine/helengine.editor.tests/managers/project/EditorBuildQueueItemDocumentTests.cs engine/helengine.editor.tests/managers/project/EditorGeneratedBootScenePreparationServiceTests.cs
rtk git -C C:\dev\helworks\helengine-gc\.worktrees\helengine commit -m "Remove DS and 3DS shared engine branches"
```

Expected: a focused cleanup commit.

### Task 3: Remove Windows Preview Preference From Shared Editor Code

**Files:**
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Modify: `engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs`
- Test: `engine/helengine.editor.tests/ComponentPropertiesViewGeneratedAssetTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/EditorSceneAssetReferenceResolverTests.cs`

- [ ] **Step 1: Rewrite preview-selection tests**

Update tests so they assert:

- The active platform is used when it is valid for preview.
- The first supported platform is used as fallback when no active platform is selected.
- No behavior depends on `"windows"` being present.

- [ ] **Step 2: Run the focused tests to capture expected failures**

Run:

```bash
.\dotnetw.ps1 test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~ComponentPropertiesViewGeneratedAssetTests|FullyQualifiedName~EditorSceneAssetReferenceResolverTests" -v minimal /p:BuildInParallel=false /p:UseSharedCompilation=false
```

Expected: failures in the tests updated to reject the Windows preview preference.

- [ ] **Step 3: Remove the Windows preview preference**

Implement the minimal production changes:

- Delete `PreferredEditorPreviewPlatformId`.
- Resolve the active platform first.
- If no active platform is available, fall back to the first supported platform.

- [ ] **Step 4: Run the focused tests to verify the batch**

Run the same command from Step 2.
Expected: PASS.

- [ ] **Step 5: Commit the preview cleanup**

Run:

```bash
rtk git -C C:\dev\helworks\helengine-gc\.worktrees\helengine add engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs engine/helengine.editor.tests/ComponentPropertiesViewGeneratedAssetTests.cs engine/helengine.editor.tests/serialization/scene/EditorSceneAssetReferenceResolverTests.cs
rtk git -C C:\dev\helworks\helengine-gc\.worktrees\helengine commit -m "Remove Windows preview preference from shared editor"
```

Expected: a focused cleanup commit.

### Task 4: Remove Windows Host Discovery And Source-Build Assumptions

**Files:**
- Modify: `engine/helengine.platforms/AvailablePlatformProviderResolver.cs`
- Delete: `engine/helengine.platforms/WindowsLauncherInstallRootLocator.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/managers/project/EditorProjectBootstrapContext.cs`
- Modify: `engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs`
- Modify: `engine/helengine.editor/managers/project/EditorSourceBuildWorkspaceLocator.cs`
- Test: `engine/helengine.platforms.tests/TestLauncherInstallRootLocator.cs`
- Test: `engine/helengine.editor.tests/EditorSessionPlatformsTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionAssetImportSettingsTests.cs`

- [ ] **Step 1: Rewrite or remove tests that depend on the Windows launcher locator**

Update the tests so they build `AvailablePlatformProviderResolver` from `PlatformDiscoveryOptions` alone and stop depending on `WindowsLauncherInstallRootLocator`.

- [ ] **Step 2: Run the focused tests to capture expected failures**

Run:

```bash
.\dotnetw.ps1 test .\engine\helengine.platforms.tests\helengine.platforms.tests.csproj --filter "FullyQualifiedName~PlatformInstallationResolverTests" -v minimal /p:BuildInParallel=false /p:UseSharedCompilation=false
.\dotnetw.ps1 test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionPlatformsTests|FullyQualifiedName~EditorSessionBuildQueueTests|FullyQualifiedName~EditorSessionAssetImportSettingsTests" -v minimal /p:BuildInParallel=false /p:UseSharedCompilation=false
```

Expected: failures due to the removed Windows launcher/source-root assumptions.

- [ ] **Step 3: Remove the Windows host assumptions from production code**

Implement the minimal production changes:

- Make `AvailablePlatformProviderResolver` accept only `PlatformDiscoveryOptions` and remove launcher-installed-platform merging.
- Remove `WindowsLauncherInstallRootLocator.cs`.
- Update editor bootstrap/session/material-migration code to construct the simplified resolver.
- Remove `ResolveHelEngineWindowsRootPath()` and any `helengine-windows` assumptions from `EditorSourceBuildWorkspaceLocator`.

- [ ] **Step 4: Run the focused tests to verify the batch**

Run the same commands from Step 2.
Expected: PASS, or a narrowed set of unrelated pre-existing failures that must be called out explicitly.

- [ ] **Step 5: Commit the Windows host cleanup**

Run:

```bash
rtk git -C C:\dev\helworks\helengine-gc\.worktrees\helengine add engine/helengine.platforms/AvailablePlatformProviderResolver.cs engine/helengine.platforms/WindowsLauncherInstallRootLocator.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor/managers/project/EditorProjectBootstrapContext.cs engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs engine/helengine.editor/managers/project/EditorSourceBuildWorkspaceLocator.cs engine/helengine.editor.tests/EditorSessionPlatformsTests.cs engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs engine/helengine.editor.tests/EditorSessionAssetImportSettingsTests.cs engine/helengine.platforms.tests/TestLauncherInstallRootLocator.cs
rtk git -C C:\dev\helworks\helengine-gc\.worktrees\helengine commit -m "Remove Windows host assumptions from shared engine"
```

Expected: a focused cleanup commit.

### Task 5: Remove PS2 Identity Branches From Shared Path And Codegen Services

**Files:**
- Modify: `engine/helengine.baseplatform/Paths/PlatformPackagedAssetPathResolver.cs`
- Delete: `engine/helengine.baseplatform/Paths/Ps2DiscPathResolver.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformPreprocessorSymbolService.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`
- Test: `engine/helengine.baseplatform.tests/*` or add a focused test if needed for packaged-path policy rejection

- [ ] **Step 1: Rewrite the focused tests**

Update tests so they assert:

- Shared code emits only generic capability symbols.
- Rooted packaged-path policies fail explicitly in shared code instead of resolving through a PS2-specific helper.

- [ ] **Step 2: Run the focused tests to capture expected failures**

Run:

```bash
.\dotnetw.ps1 test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests" -v minimal /p:BuildInParallel=false /p:UseSharedCompilation=false
```

If packaged-path tests exist, run the narrow base-platform slice as well.

- [ ] **Step 3: Remove the PS2 identity branches**

Implement the minimal production changes:

- Delete `Ps2DiscPathResolver.cs`.
- Make `PlatformPackagedAssetPathResolver` throw for `RootedOrContentRelative`.
- Remove platform-identity symbol emission from `EditorPlatformPreprocessorSymbolService` and keep only capability-based symbols.

- [ ] **Step 4: Run the focused tests to verify the batch**

Run the same command from Step 2, plus any focused base-platform tests that cover packaged-path resolution.
Expected: PASS.

- [ ] **Step 5: Commit the PS2 cleanup**

Run:

```bash
rtk git -C C:\dev\helworks\helengine-gc\.worktrees\helengine add engine/helengine.baseplatform/Paths/PlatformPackagedAssetPathResolver.cs engine/helengine.baseplatform/Paths/Ps2DiscPathResolver.cs engine/helengine.editor/managers/project/EditorPlatformPreprocessorSymbolService.cs engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs
rtk git -C C:\dev\helworks\helengine-gc\.worktrees\helengine commit -m "Remove PS2 identity branches from shared engine"
```

Expected: a focused cleanup commit.

### Task 6: Verify The Shared Production Sweep

**Files:**
- Modify: any remaining focused tests or production files found by the final sweep

- [ ] **Step 1: Run the final production-code sweep**

Run:

```bash
rtk rg -n --hidden -S -e NintendoDs -e Nintendo3Ds -e WindowsLauncherInstallRootLocator -e helengine-windows -e PreferredEditorPreviewPlatformId -e PS2_PLATFORM -e GAMECUBE_PLATFORM -e Ps2DiscPathResolver C:\dev\helworks\helengine-gc\.worktrees\helengine\engine --glob '!**/tests/**'
```

Expected: no matches in shared production code.

- [ ] **Step 2: Run the focused verification commands from each batch**

Re-run the narrow test commands used in Tasks 2 through 5.
Expected: PASS for the affected slices.

- [ ] **Step 3: Commit any final touch-ups**

Run:

```bash
rtk git -C C:\dev\helworks\helengine-gc\.worktrees\helengine status --short
```

If the worktree is clean, no commit is needed. If a final test-aligned touch-up was required, commit it with a focused message.
