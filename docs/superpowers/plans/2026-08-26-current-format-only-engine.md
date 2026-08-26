# Current-Format-Only Engine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Every implementation worker must be `gpt-5.6-luna` with reasoning effort `xhigh`. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete all production backward-compatibility behavior so every engine-owned persisted format and API accepts only its current contract.

**Architecture:** Tighten one persistence boundary at a time, regenerate its current fixtures in the same task, and delete the successful historical-load tests. Finish with a source guard that rejects new compatibility symbols and version-range readers.

**Tech Stack:** C#/.NET 9, xUnit, HELE binary serializers, PowerShell source-contract tests, generated demodisc fixtures.

**Spec:** `docs/superpowers/specs/2026-08-26-current-format-only-engine-design.md`

## Global Constraints

- GPT-5.6 Sol may coordinate and review but must not edit implementation files.
- Every code or fixture change must be made by a spawned GPT-5.6 Luna worker at `xhigh`; stop if that worker cannot be spawned.
- Readers accept exactly `CurrentVersion`; older and newer versions fail explicitly.
- No migration, conversion, alias, fallback, deprecated forwarding overload, or compatibility cycle remains.
- Missing disposable caches may be rebuilt; source-controlled authored files are never silently replaced.
- Native authored assets without current embedded identity fail and are regenerated.
- Update all available engine/platform callers in the same task that removes an API.
- Follow repository `AGENTS.md` and read the TDD skill plus `writing-good-tests.md` before changing tests.
- Preserve unrelated working-tree changes.

---

### Task 1: Current-Only Authored Asset Serializer

**Files:**
- Modify: `engine/helengine.files/assets/EditorAssetBinarySerializer.cs`
- Modify: `engine/helengine.editor.tests/BinarySerializationTests.cs`
- Modify: current serializer fixture helpers referenced by `BinarySerializationTests`

**Interfaces:**
- Consumes: `EditorAssetBinarySerializer.CurrentVersion` and current HELE header validation.
- Produces: exact-version authored asset reader with no historical layout branches.

- [ ] **Step 1: Replace successful historical-load assertions with exact rejection tests**

Add a helper and representative tests:

```csharp
static void AssertUnsupportedEditorAssetVersion(byte version) {
    using MemoryStream stream = new MemoryStream();
    EngineBinaryHeaderSerializer.Write(stream, new EngineBinaryHeader(
        EditorAssetBinarySerializer.FormatId,
        version,
        (ushort)EditorBinaryRecordKind.Asset,
        (ushort)EditorAssetBinaryValueKind.SceneAsset));
    stream.Position = 0;

    InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
        () => EditorAssetBinarySerializer.Deserialize(stream));
    Assert.Contains(version.ToString(), exception.Message, StringComparison.Ordinal);
    Assert.Contains(EditorAssetBinarySerializer.CurrentVersion.ToString(), exception.Message, StringComparison.Ordinal);
    Assert.Contains("Regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
}
```

Call it for `CurrentVersion - 1` and `CurrentVersion + 1`. Keep current model, material, animation, scene, blueprint, texture, font, and audio round trips.

- [ ] **Step 2: Run the authored serializer tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~BinarySerializationTests" -v:minimal
```

Expected: at least the previous-version assertion fails because old versions are still accepted.

- [ ] **Step 3: Enforce exact version and delete old readers**

Use one guard at every authored asset entry point:

```csharp
if (header.Version != CurrentVersion) {
    throw new InvalidOperationException(
        $"Editor asset version '{header.Version}' is unsupported; version '{CurrentVersion}' is required. Regenerate the authored asset.");
}
```

Delete `LegacyVersion`, version threshold constants used only for reading, `ReadLegacy*` methods, discard-old-tail helpers, and all `version >=`/`version <=` layout branches. Current readers read every current field unconditionally.

- [ ] **Step 4: Run authored serializer tests and verify GREEN**

Run the Step 2 command. Expected: PASS.

- [ ] **Step 5: Commit Task 1**

```powershell
rtk git add -- engine/helengine.files/assets/EditorAssetBinarySerializer.cs engine/helengine.editor.tests/BinarySerializationTests.cs
rtk git commit -m "Require current authored asset format"
```

### Task 2: Current-Only Packaged Runtime Serializer

**Files:**
- Modify: `engine/helengine.core/assets/PackagedAssetBinarySerializer.cs`
- Modify: `engine/helengine.core/assets/font/FontAssetBinarySerializer.cs`
- Modify: `engine/helengine.shader/content/ShaderMaterialAssetBinarySerializer.cs`
- Modify: matching tests under `engine/helengine.core.tests` and `engine/helengine.editor.tests`

**Interfaces:**
- Consumes: current packaged asset headers emitted by platform cooking.
- Produces: runtime readers that reject every non-current package version.

- [ ] **Step 1: Add current-minus-one/current-plus-one rejection tests**

For each serializer, write only a valid header with the wrong version and assert the received/current versions plus regeneration guidance. Delete tests that successfully deserialize old scene entities, old material constants, old texture metadata, or signed runtime IDs.

- [ ] **Step 2: Run the focused runtime serializer tests and verify RED**

```powershell
rtk dotnet test engine\helengine.core.tests\helengine.core.tests.csproj --no-restore --filter "FullyQualifiedName~PackagedAssetBinarySerializer|FullyQualifiedName~FontAssetBinarySerializer" -v:minimal
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~BinarySerializationTests" -v:minimal
```

Expected: old-version rejection tests fail.

- [ ] **Step 3: Delete packaged compatibility layouts**

Replace range guards with exact guards and delete methods such as `ReadLegacySceneEntityAsset`, `ReadLegacyMaterialConstantBufferAsset`, `ReadAndDiscardLegacyPackedMeshTail`, and `ReadLegacyRuntimeAssetId`. Read all current fields unconditionally.

- [ ] **Step 4: Run runtime and packaging tests**

```powershell
rtk dotnet test engine\helengine.core.tests\helengine.core.tests.csproj --no-restore -v:minimal
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~RuntimeScene|FullyQualifiedName~Packaging" -v:minimal
```

Expected: PASS using current generated packages only.

- [ ] **Step 5: Commit Task 2**

```powershell
rtk git add -- engine/helengine.core/assets engine/helengine.shader/content engine/helengine.core.tests engine/helengine.editor.tests
rtk git commit -m "Require current runtime package formats"
```

### Task 3: Current-Only Import and Material Settings

**Files:**
- Modify: `engine/helengine.editor/serialization/AssetImportSettingsBinarySerializer.cs`
- Modify: `engine/helengine.editor/serialization/TextureAssetImportSettingsBinarySerializer.cs`
- Modify: `engine/helengine.editor/serialization/ModelAssetImportSettingsBinarySerializer.cs`
- Modify: `engine/helengine.editor/serialization/MaterialAssetImportSettingsBinarySerializer.cs`
- Modify: `engine/helengine.editor/serialization/MaterialAssetCommonSettingsDocumentBinarySerializer.cs`
- Modify: `engine/helengine.editor/serialization/MaterialAssetPlatformOverrideDocumentBinarySerializer.cs`
- Modify: `engine/helengine.editor/managers/asset/AssetImportManager.cs`
- Delete: `tools/material-settings-migration`
- Modify: `engine/helengine.editor.tests/BinarySerializationTests.cs`
- Modify: `engine/helengine.editor.tests/managers/asset/AssetImportManagerTests.cs`

**Interfaces:**
- Consumes: current typed texture/model/material settings.
- Produces: typed exact-version settings flow with no generalized conversion.

- [ ] **Step 1: Write failing tests for stale typed and generalized settings**

Add cases asserting that every typed serializer rejects its previous version and that `AssetImportManager` reports an obsolete generalized settings file rather than converting it. Preserve tests for current non-default texture, model, and material settings.

- [ ] **Step 2: Run focused settings tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~AssetImportSettingsBinarySerializerTests|FullyQualifiedName~BinarySerializationTests|FullyQualifiedName~AssetImportManagerTests" -v:minimal
```

- [ ] **Step 3: Remove version ranges and conversion paths**

Each serializer uses:

```csharp
if (header.Version != CurrentVersion) {
    throw new InvalidOperationException(
        $"{settingsKind} settings version '{header.Version}' is unsupported; version '{CurrentVersion}' is required. Regenerate the settings file.");
}
```

Delete `TryLoadImportSettings` fallback use, `ConvertLegacyTextureImportSettings`, preservation flags, and rewrite-on-load branches. Delete the generalized serializer if no current writer consumes it; otherwise keep only the current type that has a current caller and rename it away from compatibility terminology. Delete `tools/material-settings-migration` and its project entry because regeneration now goes exclusively through current public editor commands.

- [ ] **Step 4: Run settings and importer tests**

Run the Step 2 command, then:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~AssetImportSettingsView|FullyQualifiedName~MaterialAssetSettingsService|FullyQualifiedName~EditorSessionAssetImportSettings" -v:minimal
```

Expected: PASS.

- [ ] **Step 5: Commit Task 3**

```powershell
rtk git add -- engine/helengine.editor/serialization engine/helengine.editor/managers/asset/AssetImportManager.cs engine/helengine.editor.tests tools/material-settings-migration
rtk git commit -m "Remove obsolete import settings formats"
```

### Task 4: Current-Only Component, Scene, and Physics Payloads

**Files:**
- Modify: `engine/helengine.editor/serialization/scene/AutomaticScriptComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
- Modify: `engine/helengine.core/scene/runtime/PersistedComponentTypeResolver.cs`
- Modify: `engine/helengine.physics3d/runtime/PhysicsSceneFeatureAnalyzer3D.cs`
- Modify: `engine/helengine.physics3d/Physics3DRuntimeComponentRegistration.cs`
- Modify: `engine/helengine.bepu` registration callers
- Modify: matching tests under `engine/helengine.editor.tests`, `engine/helengine.physics3d.tests`, and `engine/helengine.bepu.tests`

**Interfaces:**
- Consumes: current tagged field names, component IDs, and physics payload versions.
- Produces: exact current component resolution and analysis.

- [ ] **Step 1: Add rejection/source-absence tests before deletion**

Add tests that old tagged field aliases, assembly-qualified old engine IDs, and old collider payload versions fail. Add source-contract assertions that `NormalizeLegacyEngineComponentTypeId`, `ResolveLegacyTaggedFieldName`, and `LegacyBoxColliderPayloadVersion` are absent.

- [ ] **Step 2: Run the focused tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~AutomaticScriptComponentPersistenceDescriptor|FullyQualifiedName~RuntimeComponentRegistry" -v:minimal
rtk dotnet test engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj --no-restore --filter "FullyQualifiedName~PhysicsSceneFeatureAnalyzer3D|FullyQualifiedName~PhysicsWorld3DSceneLoad" -v:minimal
```

- [ ] **Step 3: Delete aliases and old payload readers**

Resolve component type IDs by the current exact ID only. Match tagged fields by current stable field name only. Require the current physics component payload version and member count; delete old six-member defaults and registration forwarding entry points. Update current BEPU registration callers directly.

- [ ] **Step 4: Run editor, physics, and BEPU suites**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~AutomaticScriptComponentPersistence|FullyQualifiedName~RuntimeComponent" -v:minimal
rtk dotnet test engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj --no-restore -v:minimal
rtk dotnet test engine\helengine.bepu.tests\helengine.bepu.tests.csproj --no-restore -v:minimal
```

- [ ] **Step 5: Commit Task 4**

```powershell
rtk git add -- engine/helengine.editor/serialization/scene engine/helengine.core/scene/runtime engine/helengine.physics3d engine/helengine.bepu engine/helengine.editor.tests engine/helengine.physics3d.tests engine/helengine.bepu.tests
rtk git commit -m "Require current component payloads"
```

### Task 5: Current-Only Build, Platform, and Public APIs

**Files:**
- Modify: `engine/helengine.baseplatform/Manifest/PlatformBuildManifest.cs`
- Modify: `engine/helengine.baseplatform/Requests/PlatformShaderArtifactCookRequest.cs`
- Modify: `engine/helengine.baseplatform/Results/PlatformMaterialCookResult.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeSceneAssetReferenceResolver.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildConfigService.cs`
- Delete: `engine/helengine.editor/managers/project/EditorLegacyBuildProfileIdNormalizer.cs` if present
- Modify: `engine/helengine.editor/managers/scene/MeshComponentModifierStackService.cs`
- Modify: current callers and tests across engine/platform projects

**Interfaces:**
- Consumes: current build manifests, shader dependency requests, build profiles, modifier stacks, and runtime resolver constructors.
- Produces: one current constructor/API per concept.

- [ ] **Step 1: Add source-contract tests for obsolete public surfaces**

Assert that current constructors require explicit platform name/version and typed shader dependencies; old resolver arguments, build-profile normalizers, and modifier-stack fallback methods are absent.

- [ ] **Step 2: Run affected build/editor tests and verify RED**

```powershell
rtk dotnet test engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj --no-restore -v:minimal
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorBuildConfigService|FullyQualifiedName~MeshComponentModifierStack|FullyQualifiedName~RuntimeSceneAssetReferenceResolver" -v:minimal
```

- [ ] **Step 3: Remove obsolete overloads and normalizers**

Delete placeholder defaults such as `unspecified-platform`, constructors that synthesize missing current fields, cook requests that translate old shader-ID arrays, ignored resolver arguments, build-profile rewrites, and modifier-stack reads/writes of superseded per-platform fields. Update every current caller to construct the complete current type.

- [ ] **Step 4: Compile all engine projects and fix current callers only**

```powershell
rtk dotnet build helengine.ui\helengine.editor.app\helengine.editor.app.csproj --no-restore -v:minimal
```

Expected: PASS. Do not restore removed overloads to fix a caller.

- [ ] **Step 5: Run build/platform tests and commit**

```powershell
rtk dotnet test engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj --no-restore -v:minimal
rtk dotnet test engine\helengine.platforms.tests\helengine.platforms.tests.csproj --no-restore -v:minimal
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorBuildConfigService|FullyQualifiedName~MeshComponentModifierStack|FullyQualifiedName~RuntimeSceneAssetReferenceResolver" -v:minimal
rtk git add -- engine helengine.ui
rtk git commit -m "Remove obsolete engine API contracts"
```

### Task 6: Regenerate Current Fixtures and Demodisc

**Files:**
- Modify: current binary fixtures under engine test projects
- Modify: current native files and settings in `C:\dev\helprojs\demodisc`
- Modify: generation commands only when they still call removed APIs

**Interfaces:**
- Consumes: current writers from Tasks 1–5.
- Produces: current-only source-controlled fixtures with no converter requirement.

- [ ] **Step 1: Inventory files failing solely because they are stale**

Run focused tests and the demodisc generation commands. Record each unsupported-version path. Do not bulk-regenerate files that already load.

- [ ] **Step 2: Regenerate through current public commands**

Run the current project-authored commands through the public editor CLI:

```powershell
rtk dotnet run --project helengine.ui\helengine.editor.app\helengine.editor.app.csproj -- --project C:\dev\helprojs\demodisc\project.heproj --editor-command menu.generate-rendering-scenes
rtk dotnet run --project helengine.ui\helengine.editor.app\helengine.editor.app.csproj -- --project C:\dev\helprojs\demodisc\project.heproj --editor-command menu.generate-physics-scenes
rtk dotnet run --project helengine.ui\helengine.editor.app\helengine.editor.app.csproj -- --project C:\dev\helprojs\demodisc\project.heproj --editor-command menu.generate-game-scenes
rtk dotnet run --project helengine.ui\helengine.editor.app\helengine.editor.app.csproj -- --project C:\dev\helprojs\demodisc\project.heproj --editor-command menu.regenerate-demo-disc-main-menu
```

Never patch binary versions or invoke serializer internals from a one-off migration tool.

- [ ] **Step 3: Verify current files and no compatibility calls**

```powershell
rg -n "Legacy|Migrate|Upgrade|ConvertLegacy|NormalizeLegacy" C:\dev\helprojs\demodisc\assets\codebase -g '*.cs'
rtk dotnet test C:\dev\helprojs\demodisc\city.sln --no-restore -v:minimal
```

Expected: no obsolete calls; tests PASS.

- [ ] **Step 4: Commit engine fixture changes and demodisc changes separately**

```powershell
rtk git add -- engine
rtk git commit -m "Regenerate current engine fixtures"
rtk git -C C:\dev\helprojs\demodisc add -- assets settings project.heproj
rtk git -C C:\dev\helprojs\demodisc commit -m "Regenerate current project formats"
```

### Task 7: Production Compatibility Guard and Final Verification

**Files:**
- Create: `engine/helengine.editor.tests/CurrentFormatOnlySourceContractTests.cs`
- Modify: project files only if the new test file is not globbed automatically

**Interfaces:**
- Consumes: repository production source tree.
- Produces: enforcement against reintroducing compatibility code.

- [ ] **Step 1: Write the failing repository guard**

Implement a test that scans production `.cs` files outside vendor and tests and reports forbidden matches with path and line. Patterns include symbol words `Legacy`, `Migrate`, `Upgrade`, `ConvertLegacy`, `NormalizeLegacy`, `backward compatibility`, and persistence guards accepting version ranges. Explicitly exclude `helengine.nativeownership/NativeMigrationRequiredAttribute.cs` because it describes managed-to-native implementation ownership, not persisted-data compatibility.

- [ ] **Step 2: Run the guard and remove every production match**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~CurrentFormatOnlySourceContractTests" -v:minimal
```

Expected first run: FAIL with remaining production matches. Remove the behavior and update current callers; do not broaden the allowlist for convenience.

- [ ] **Step 3: Run full verification**

```powershell
rtk dotnet build helengine.ui\helengine.editor.app\helengine.editor.app.csproj --no-restore -v:minimal
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore -v:minimal
rtk dotnet test engine\helengine.core.tests\helengine.core.tests.csproj --no-restore -v:minimal
rtk dotnet test engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj --no-restore -v:minimal
rtk dotnet test engine\helengine.platforms.tests\helengine.platforms.tests.csproj --no-restore -v:minimal
rtk git diff --check
```

Expected: PASS and clean whitespace check.

- [ ] **Step 4: Commit Task 7**

```powershell
rtk git add -- engine/helengine.editor.tests/CurrentFormatOnlySourceContractTests.cs engine helengine.ui
rtk git commit -m "Enforce current-only engine contracts"
```
