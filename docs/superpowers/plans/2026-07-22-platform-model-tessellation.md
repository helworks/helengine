# Platform Model Tessellation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add opt-in, per-platform model-import tessellation that produces crack-free smaller triangles at cook/import time, while leaving MeshComponent and all runtime renderers unchanged.

**Architecture:** `ModelAssetProcessorSettings` owns the two platform-override values. The editor persists, clones, compares, displays, and hashes those values. During model import, `ModelAssetProcessor` invokes a dedicated topology-aware `ModelTessellationProcessor` before its existing winding operation; the cooked `ModelAsset` is then consumed by the existing platform builders exactly as any other imported model.

**Tech Stack:** C#/.NET, Helengine editor asset-import pipeline, existing `ModelAsset` raw mesh schema, xUnit tests, PS2 asset-builder tests.

---

## Guardrails and completion criteria

- Work directly in the existing main checkouts, as requested; do not create a worktree and do not disturb unrelated dirty files.
- Do not alter `MeshComponent`, scene serialization, or platform runtime renderers. This is an editor/import-time transformation only.
- New production types and all new/changed members must have substantive XML documentation, one class per file, PascalCase fields, no tuples, no local functions, and double-precision geometric calculations.
- Defaults are `Tessellate = false` and `TessellationMaxEdgeLength = 1.0d`. A supplied threshold must be finite and greater than zero; malformed models and an output exceeding 1,000,000 triangles must fail with a clear exception rather than silently skipping work.
- Tessellation must be conforming: a split geometric edge is represented consistently in every adjacent triangle, including UV/normal seams, with no T-junctions. Preserve material slot grouping and submesh index ranges.
- The cache identity includes the enabled state and threshold formatted with `CultureInfo.InvariantCulture`; changing either reimports the active platform’s model.

## Task 1: Add settings defaults and typed model-settings persistence tests

**Files:**
- Modify: `engine/helengine.editor/managers/asset/ModelAssetProcessorSettings.cs`
- Modify: `engine/helengine.editor/serialization/ModelAssetImportSettingsBinarySerializer.cs`
- Modify: `engine/helengine.editor.tests/BinarySerializationTests.cs`

- [ ] First add red tests beside `ModelAssetImportSettingsBinarySerializer_RoundTripsPlatformSettings` proving a Windows override can round-trip `FlipWinding`, `Tessellate`, and a non-default `TessellationMaxEdgeLength`; add a handcrafted version-1 payload test proving it deserializes as `Tessellate == false` and `TessellationMaxEdgeLength == 1.0d`.
- [ ] Extend `ModelAssetProcessorSettings` with documented `bool Tessellate { get; set; }` and `double TessellationMaxEdgeLength { get; set; } = 1.0d`; preserve the implicit `false` default for the toggle.
- [ ] Bump `ModelAssetImportSettingsBinarySerializer.CurrentVersion` from 1 to 2. Version 2 writes, per platform, winding, tessellation enabled state, then the double edge length. Its reader accepts versions 1 and 2; version 1 reads only winding and explicitly assigns the two defaults, while version 2 reads and validates the new threshold.
- [ ] Centralize threshold validation on the settings/processor boundary so persisted NaN, infinity, zero, and negative values produce an `InvalidOperationException` that identifies the invalid tessellation edge length.
- [ ] Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BinarySerializationTests"`.

## Task 2: Make generic platform section persistence version-aware

**Files:**
- Modify: `engine/helengine.editor/managers/asset/IAssetPlatformSettingsSectionDefinition.cs`
- Modify: `engine/helengine.editor/managers/asset/AssetPlatformSettingsSectionRegistry.cs`
- Modify: `engine/helengine.editor/serialization/AssetImportSettingsBinarySerializer.cs`
- Modify: `engine/helengine.editor/managers/asset/ModelAssetPlatformSettingsSectionDefinition.cs`
- Modify: every other implementation of `IAssetPlatformSettingsSectionDefinition` under `engine/helengine.editor/managers/asset/`
- Modify: `engine/helengine.editor.tests/serialization/AssetImportSettingsBinarySerializerTests.cs`

- [ ] Add red tests for the generic `AssetImportSettingsBinarySerializer`: version-10 round-trip retains both new model values, and a version-9 model-section fixture remains readable with the two defaults.
- [ ] Change the section-definition and registry deserialization contract to receive the enclosing asset-import-settings format version. Existing non-model definitions ignore it, preserving their wire format.
- [ ] Bump `AssetImportSettingsBinarySerializer.CurrentVersion` from 9 to 10; accept both versions 9 and 10 in its header validation and pass the parsed version to `DeserializeSection`.
- [ ] In `ModelAssetPlatformSettingsSectionDefinition`, clone and compare all three fields. Serialize the v10 payload as winding, tessellate flag, and edge length; when the supplied format version is 9, deserialize only winding and set the declared defaults. Do not infer old/new payload length from stream state.
- [ ] Update all affected interface implementations, XML docs, and test doubles to compile with the version-aware method signature. Keep section ordering and non-model payload bytes unchanged.
- [ ] Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AssetImportSettingsBinarySerializerTests"`.

## Task 3: Build a tested, topology-aware tessellation service

**Files:**
- Add: `engine/helengine.editor/managers/asset/ModelTessellationProcessor.cs`
- Add: supporting single-class files under `engine/helengine.editor/managers/asset/` only as needed for edge keys, triangle work records, and generated-vertex bookkeeping
- Add: `engine/helengine.editor.tests/ModelTessellationProcessorTests.cs`
- Modify: `engine/helengine.editor/managers/asset/ModelAssetProcessor.cs`

- [ ] Begin with red unit tests for a one-triangle model: every final edge is at or below the requested maximum, interpolation produces the expected position/UV/normal midpoint, and `Tessellate == false` leaves the source geometry byte-for-byte unchanged.
- [ ] Add red topology tests for two triangles sharing a geometric edge, including duplicated source vertices representing a UV seam: both incident triangles must use compatible split geometry, the seam keeps distinct interpolated UV vertices, and no final edge exceeds the limit. Add a multi-material/submesh test asserting material slot names, ordering, and rebuilt `IndexStart`/`IndexCount` remain correct.
- [ ] Implement `ModelTessellationProcessor.Apply(ModelAsset asset, double maxEdgeLength)`. Validate required streams, indexed triangles, index bounds, index divisibility, and each input submesh range before mutation. Use `double` squared-distance comparisons and reject non-finite positions/thresholds.
- [ ] Construct adjacency by geometric position edge rather than raw vertex index so attribute seams share the same split decision. Represent each triangle as mutable corner indices, keep one midpoint result per source attribute edge, and track geometric-edge split requirements separately from attribute vertices.
- [ ] Repeatedly select triangles with an over-limit edge, split their longest edge at a midpoint, and propagate that edge split through every incident triangle until the mesh is conforming. Interpolate position and texture coordinates linearly; interpolate normals then normalize them. Reuse original vertices and generated vertices whenever their attribute edge is identical.
- [ ] Rebuild position, normal, UV, and index arrays only after all splits complete. Preserve 16-bit indices when the final vertex count permits; otherwise emit `Indices32`. Preserve every submesh’s material slot and triangle order within its original submesh. Cap the final output at 1,000,000 triangles and throw before producing a partial asset.
- [ ] Call this service at the start of `ModelAssetProcessor.Apply` only when `settings.Tessellate` is true, then run the existing winding flip. Keep the no-tessellation code path behavior unchanged.
- [ ] Add tests for winding order after tessellation, 16-to-32-bit promotion, malformed input rejection, non-finite/invalid thresholds, and the triangle cap. Test source mutation is atomic on failure by asserting the original arrays and submeshes remain unchanged.
- [ ] Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ModelTessellationProcessorTests|FullyQualifiedName~AssetImportManagerModelTests"`.

## Task 4: Wire settings through import state, cloning, and cache identity

**Files:**
- Modify: `engine/helengine.editor/managers/asset/AssetImportManager.cs`
- Modify: `engine/helengine.editor/managers/project/EditorSession.cs`
- Modify: `engine/helengine.editor.tests/AssetImportManagerModelTests.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionAssetImportSettingsTests.cs`

- [ ] Add red import-manager tests modeled on `TryLoadModelAsset_WhenWindowsFlipWindingChanges_ReimportsModel`: changing only `Tessellate`, and separately only `TessellationMaxEdgeLength`, must change the Windows cache identity/reimport result; another platform override must remain independent.
- [ ] Extend every model-settings cloning path, particularly `EditorSession.CloneModelProcessorSettings` and the import manager’s model settings copy paths, to carry both fields. Add session tests that switch platform tabs and confirm each pending override stays isolated.
- [ ] Extend both model cache-identity constructors in `AssetImportManager` to include named tessellation values. Format `TessellationMaxEdgeLength` with `CultureInfo.InvariantCulture` and a round-trip-safe format; do not let the current UI culture affect cooked asset identity.
- [ ] Ensure `GetCurrentPlatformModelProcessorSettings` supplies the selected platform’s settings directly to the existing processor invocation, so import runs tessellation before writing the cached generic `ModelAsset`.
- [ ] Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AssetImportManagerModelTests|FullyQualifiedName~EditorSessionAssetImportSettingsTests"`.

## Task 5: Expose the per-platform controls in the asset-import editor

**Files:**
- Modify: `engine/helengine.editor/components/ui/AssetImportSettingsView.cs`
- Modify: `engine/helengine.editor.tests/AssetImportSettingsViewTests.cs`

- [ ] Add failing UI tests proving model import settings display a `Tessellate` checkbox for the active platform, show the `Tessellation Max Edge Length` editor only while it is enabled, bind its value per platform, and preserve the default 1.0 value on an older settings asset.
- [ ] Add documented label, host, text, and textbox members alongside the existing model winding controls. Lay them out under `Flip Winding` using the same label/control sizing system used by the texture resolution field.
- [ ] Bind the toggle to the selected platform’s `ModelAssetProcessorSettings.Tessellate`. Bind the numeric field using invariant-culture parsing; reject invalid, non-finite, or non-positive edits with a status/error message and do not update pending settings until the text is valid.
- [ ] Update all control-state, visibility, pending-settings cloning, equality, reset, and apply paths so the threshold control is disabled/hidden when not applicable and never leaks a selected platform’s values into another platform.
- [ ] Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AssetImportSettingsViewTests"`.

## Task 6: Verify downstream platform consumption without runtime changes

**Files:**
- Modify or add test coverage in: `C:/dev/helworks/helengine-ps2/builder.tests/Ps2PlatformAssetBuilderTests.cs`
- Do not modify: `C:/dev/helworks/helengine-ps2/src/platform/ps2/**`

- [ ] Add a builder-level regression test that supplies a valid imported/tessellated `ModelAsset` with preserved submeshes to `Ps2PlatformAssetBuilder` and verifies it cooks successfully through the normal packed-mesh path. The test should prove that tessellated editor output requires no PS2 renderer-specific branch.
- [ ] Keep PS2’s existing 16-bit index limit behavior explicit: a final 32-bit generic model that cannot be represented by the PS2 builder must retain its existing clear failure, rather than truncating indices. Test the successful within-limit case.
- [ ] Run: `dotnet test builder.tests/helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Ps2PlatformAssetBuilderTests"` from `C:/dev/helworks/helengine-ps2`.

## Task 7: Full regression and implementation review

**Files:**
- Review all files changed by Tasks 1–6; no generated files.

- [ ] Run the focused editor suite: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj` from `C:/dev/helworks/helengine`.
- [ ] Run the focused PS2 builder suite: `dotnet test builder.tests/helengine.ps2.builder.tests.csproj` from `C:/dev/helworks/helengine-ps2`.
- [ ] Inspect `git diff --check` in both repositories and review the changed file lists. Confirm no worktree was created, no generated code was edited, no `MeshComponent` or runtime renderer changed, and all added/changed C# declarations meet the repository XML-documentation and one-class-per-file rules.
- [ ] Manually validate in the editor: configure a smaller PS2 edge length with Windows tessellation off, apply/reimport, confirm the platform tabs retain distinct values, and verify the resulting PS2 cooked asset is produced by the normal build pipeline.
