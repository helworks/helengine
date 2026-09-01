# Remove City Project Tests From Editor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove DemoDisc project tests, still named `City`, from the HelEngine editor test assembly without weakening generic editor coverage.

**Architecture:** Delete the top-level `City*.cs` project test files, the fixture helper used only by two of those files, and the six already-renamed `Demodisc*.cs` files that still read the external project directly. Keep engine-owned tests whose local sample names contain `CityStyle`, because those tests are hermetic and validate general editor behavior rather than the external DemoDisc project.

**Tech Stack:** C#/.NET 9, xUnit, SDK-style implicit compile items, PowerShell, ripgrep

---

### Task 1: Remove the misplaced DemoDisc project tests

**Files:**
- Delete: `engine/helengine.editor.tests/CityCubeTestSceneSourceTests.cs`
- Delete: `engine/helengine.editor.tests/CityDemoDiscLightIndicatorSourceTests.cs`
- Delete: `engine/helengine.editor.tests/CityDemoDiscLogoAnimationSourceTests.cs`
- Delete: `engine/helengine.editor.tests/CityDsBuildConfigSourceTests.cs`
- Delete: `engine/helengine.editor.tests/CityGameCubeImportedTextureResolutionTests.cs`
- Delete: `engine/helengine.editor.tests/CityGameSceneSourceTests.cs`
- Delete: `engine/helengine.editor.tests/CityGroundCubeProbeSceneSourceTests.cs`
- Delete: `engine/helengine.editor.tests/CityHandheldMainMenuRuntimeResolutionSourceTests.cs`
- Delete: `engine/helengine.editor.tests/CityHandheldMainMenuSceneAssetPresenceTests.cs`
- Delete: `engine/helengine.editor.tests/CityHandheldMainMenuSourceTests.cs`
- Delete: `engine/helengine.editor.tests/CityMenuBuildConfigSourceTests.cs`
- Delete: `engine/helengine.editor.tests/CityMenuSourceTests.cs`
- Delete: `engine/helengine.editor.tests/CityNintendo3DsCubeTestPackagedSceneRuntimeTests.cs`
- Delete: `engine/helengine.editor.tests/CityNintendoDsBuildConfigSourceTests.cs`
- Delete: `engine/helengine.editor.tests/CityNintendoDsBuildQueueItemTests.cs`
- Delete: `engine/helengine.editor.tests/CityPhysicsSceneSourceTests.cs`
- Delete: `engine/helengine.editor.tests/CityPspBuildConfigSourceTests.cs`
- Delete: `engine/helengine.editor.tests/CityPsVitaBuildConfigSourceTests.cs`
- Delete: `engine/helengine.editor.tests/CityRenderingRotationSourceTests.cs`
- Delete: `engine/helengine.editor.tests/CityRenderOnlySlopePackagedSceneRuntimeTests.cs`
- Delete: `engine/helengine.editor.tests/CityRenderOnlySlopeScenePackagingTests.cs`
- Delete: `engine/helengine.editor.tests/CityShowcaseFontReferenceSourceTests.cs`
- Delete: `engine/helengine.editor.tests/CityStaticMeshShowcasePackagedSceneTests.cs`
- Delete: `engine/helengine.editor.tests/CityStaticMeshShowcaseSceneSourceTests.cs`
- Delete: `engine/helengine.editor.tests/CityTiltTrialBallDriveSourceTests.cs`
- Delete: `engine/helengine.editor.tests/CityTiltTrialHandheldMenuTopScreenSourceTests.cs`
- Delete: `engine/helengine.editor.tests/CityTiltTrialMarbleMaterialTests.cs`
- Delete: `engine/helengine.editor.tests/CityTiltTrialPackagedSceneRuntimeTests.cs`
- Delete: `engine/helengine.editor.tests/CityTiltTrialRuntimeEntityLookupSourceTests.cs`
- Delete: `engine/helengine.editor.tests/CityTiltTrialWalnutMaterialTests.cs`
- Delete: `engine/helengine.editor.tests/CityWiiUBuildConfigSourceTests.cs`
- Delete: `engine/helengine.editor.tests/CityWindowsBuildConfigSourceTests.cs`
- Delete: `engine/helengine.editor.tests/testing/CityFixtureRepository.cs`
- Delete: `engine/helengine.editor.tests/DemodiscNintendoDsBottomScreenFontSourceTests.cs`
- Delete: `engine/helengine.editor.tests/DemodiscNintendoDsScaffoldSourceTests.cs`
- Delete: `engine/helengine.editor.tests/DemodiscPhysicsNintendoDsBottomScreenTests.cs`
- Delete: `engine/helengine.editor.tests/DemodiscTiltTrialEditorSessionCloseTests.cs`
- Delete: `engine/helengine.editor.tests/DemodiscTiltTrialImportedTextureResolutionTests.cs`
- Delete: `engine/helengine.editor.tests/DemodiscTiltTrialSceneLoadTests.cs`

- [ ] **Step 1: Preserve RED evidence for the misplaced boundary**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~CityTiltTrialBallDriveSourceTests" -v:minimal
```

Expected: FAIL because the tests read obsolete external paths under `C:\dev\helprojs\city`; this demonstrates that the engine test assembly currently depends on project-owned source files.

- [ ] **Step 2: Delete only the project-owned test files and their private fixture**

Delete every file listed under **Files**. The first implementation pass removed the 33 City-named files; the required ownership scan then exposed six already-renamed DemoDisc project tests, which this plan now includes explicitly. Do not edit the test project file because the SDK-style project discovers `*.cs` files implicitly. Do not rename these tests and do not copy them into another engine test directory.

- [ ] **Step 3: Verify the ownership boundary by source scan**

Run:

```powershell
$cityFiles = Get-ChildItem -LiteralPath engine\helengine.editor.tests -File -Filter 'City*.cs'
$demodiscFiles = Get-ChildItem -LiteralPath engine\helengine.editor.tests -File | Where-Object Name -CMatch '^Demodisc.*\.cs$'
$externalReferences = rtk rg -n --glob '*.cs' 'C:\\dev\\helprojs\\(?:city|demodisc)' engine\helengine.editor.tests
if ($cityFiles.Count -ne 0) { $cityFiles | ForEach-Object FullName; throw 'City project tests remain in the editor test root.' }
if ($demodiscFiles.Count -ne 0) { $demodiscFiles | ForEach-Object FullName; throw 'DemoDisc project tests remain in the editor test root.' }
if ($LASTEXITCODE -eq 0) { $externalReferences; throw 'External City or DemoDisc project references remain in editor tests.' }
if ($LASTEXITCODE -ne 1) { throw "ripgrep failed with exit code $LASTEXITCODE" }
```

Expected: PASS with no top-level `City*.cs` or `Demodisc*.cs` files and no hard-coded `C:\dev\helprojs\city` or `C:\dev\helprojs\demodisc` references anywhere in the editor test project.

- [ ] **Step 4: Verify no orphaned City fixture symbols remain**

Run:

```powershell
rtk rg -n --glob '*.cs' 'CityFixtureRepository|CityFixtureBuildProject|CityTextureFixtureProject' engine\helengine.editor.tests
```

Expected: exit code 1 and no output.

- [ ] **Step 5: Build the editor test assembly**

Run:

```powershell
rtk dotnet build engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore -v:minimal
```

Expected: exit code 0. Any unrelated pre-existing test failures are outside this deletion task, but compilation must remain clean.

- [ ] **Step 6: Inspect the exact deletion scope**

Run:

```powershell
rtk git diff --name-status -- engine/helengine.editor.tests
rtk git diff --check
```

Expected across the two implementation commits: exactly the 39 listed files appear with status `D`; `git diff --check` exits 0. The unrelated modified `engine/helengine.core/scene/runtime/AutomaticScriptComponentRuntimeDeserializer.cs` must not be staged or changed.

- [ ] **Step 7: Commit the removal**

```powershell
rtk git add -- engine/helengine.editor.tests/City*.cs engine/helengine.editor.tests/Demodisc*.cs engine/helengine.editor.tests/testing/CityFixtureRepository.cs
rtk git diff --cached --name-status
rtk git commit -m "Remove DemoDisc tests from editor suite"
```

Expected: the staged diff contains only listed deletions not already committed by the first implementation pass, and the commit succeeds.

### Task 2: Re-run the editor gate and report the remaining baseline

**Files:**
- Modify: none
- Test: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Run the full editor test suite**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore -v:minimal
```

Expected: all City/DemoDisc project test failures are absent. Record the exact pass, fail, and skip counts; do not attribute any remaining failures to this deletion without inspecting them.

- [ ] **Step 2: Confirm the branch remains whitespace-clean**

Run:

```powershell
rtk git diff --check main..HEAD
```

Expected: exit code 0 and no output.

- [ ] **Step 3: Report rather than broaden scope**

If the full editor suite still fails, list the remaining failing test groups and stop. Do not repair unrelated engine failures under this plan.
