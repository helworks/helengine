# Mesh Component Tessellation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add editor-only, per-platform MeshComponent tessellation settings that cook reusable local-space model variants sized for an entity's final static world scale.

**Architecture:** Store MeshComponent tessellation values as editor-only detached platform-override member values, rather than runtime component properties. Extend scene rewriting to resolve target-platform component metadata and static accumulated scale together; a dedicated variant service clones the already platform-processed model, performs scale-aware conforming subdivision, writes one generated cooked model per stable variant identity, and rewrites only enabled MeshComponent model references.

**Tech Stack:** C#/.NET, Helengine scene persistence and component platform overrides, `ModelTessellationProcessor`, xUnit, existing platform asset builders.

---

## Files and boundaries

- `engine/helengine.editor/managers/scene/MeshComponentTessellationSettings.cs`: typed editor-only values and validation.
- `engine/helengine.editor/managers/scene/MeshComponentTessellationSettingsService.cs`: reads/writes the detached platform override member values, applies defaults, and constructs invariant variant identities.
- `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`: adds the two MeshComponent rows to every platform section without exposing runtime properties.
- `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`: carries composed static world scale through recursive entity rewriting and passes target component settings to component transformation.
- `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`: creates/reuses generated model variants and replaces only the MeshComponent model reference in its temporary packaging representation.
- `engine/helengine.editor/managers/asset/ModelTessellationProcessor.cs`: accepts a scale vector used only for edge-distance measurement while retaining local-space output.

## Task 1: Define editor-only per-platform component settings

**Files:**
- Create: `engine/helengine.editor/managers/scene/MeshComponentTessellationSettings.cs`
- Create: `engine/helengine.editor/managers/scene/MeshComponentTessellationSettingsService.cs`
- Test: `engine/helengine.editor.tests/managers/scene/MeshComponentTessellationSettingsServiceTests.cs`

- [ ] Write failing tests for defaults, platform-specific value storage, clone/read isolation, invariant identity, and invalid values:

```csharp
[Fact]
public void GetForPlatform_WhenNoOverrideExists_ReturnsDisabledDefault() {
    EntityComponentSaveState state = new EntityComponentSaveState();
    MeshComponentTessellationSettings settings = Service.GetForPlatform(state, "ps2");

    Assert.False(settings.Tessellate);
    Assert.Equal(1.0d, settings.TessellationMaxEdgeLength);
}

[Fact]
public void SetForPlatform_WhenPs2DiffersFromWindows_PreservesBothValues() {
    Service.SetForPlatform(State, "ps2", new MeshComponentTessellationSettings(true, 0.25d));
    Service.SetForPlatform(State, "windows", new MeshComponentTessellationSettings(false, 1.0d));

    Assert.True(Service.GetForPlatform(State, "ps2").Tessellate);
    Assert.False(Service.GetForPlatform(State, "windows").Tessellate);
}
```

- [ ] Run the new test class and confirm the missing settings service produces the expected compile failure.

- [ ] Implement a one-class-per-file `MeshComponentTessellationSettings` with `Tessellate` and `TessellationMaxEdgeLength`, constructors that reject non-finite/non-positive edge lengths, and documented defaults.

- [ ] Implement `MeshComponentTessellationSettingsService` using two stable detached override member names, `MeshTessellate` and `MeshTessellationMaxEdgeLength`. Read and write `EntityComponentPlatformOverrideState.MemberValues` through `EntityComponentSaveState`; do not add fields to runtime `MeshComponent` or platform runtime schemas. Parse and serialize edge lengths with `CultureInfo.InvariantCulture` and the round-trip `R` format.

- [ ] Implement `BuildVariantIdentity(sourceModelReference, platformId, settings, worldScale)` with named newline-delimited fields and invariant `R` formatting for edge length and each scale component. Reject blank platform/source IDs and non-finite or zero scale components.

- [ ] Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~MeshComponentTessellationSettingsServiceTests"`.

## Task 2: Expose settings in every platform MeshComponent inspector

**Files:**
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Modify: `engine/helengine.editor/components/ui/ComponentPropertyRow.cs` only if a row needs a typed editor-only settings binding
- Test: `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`

- [ ] Add failing inspector tests that select a MeshComponent under PS2 and Windows platform sections, set `Tessellate` and `Tessellation Max Edge Length`, and assert their detached override state differs by platform while the runtime component has no new property.

- [ ] Add a MeshComponent-specific platform settings subsection after the normal component properties. It must be shown for every non-common platform section, use the existing boolean/scalar row widgets, and bind through `MeshComponentTessellationSettingsService` instead of reflection.

- [ ] Use these row labels and defaults exactly:

```text
Tessellate                    Boolean, default false
Tessellation Max Edge Length  Scalar double, default 1.0
```

- [ ] Hide or disable the edge-length editor while `Tessellate` is false. On edit, reject parse failure, NaN, infinity, zero, and negatives before mutating the pending override; preserve the existing inspector error behavior rather than replacing invalid values with defaults.

- [ ] Ensure the existing override chrome can clear either detached member back to its default and that undo/redo snapshots include the `EntityComponentSaveState` mutation.

- [ ] Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~PropertiesPanelMutationTests"`.

## Task 3: Add scale-aware edge measurement to the tessellator

**Files:**
- Modify: `engine/helengine.editor/managers/asset/ModelTessellationProcessor.cs`
- Test: `engine/helengine.editor.tests/ModelTessellationProcessorTests.cs`

- [ ] Write failing geometry tests for non-uniform scale. A local edge of length one with `worldScale = new float3(4f, 1f, 1f)` and maximum world edge length `1.1d` must subdivide; the same source with `float3.One` must not. Assert output positions remain local-space values.

- [ ] Add the overload below; retain the existing two-argument method as a wrapper using `float3.One` so model-import tessellation behavior remains unchanged:

```csharp
public static void Apply(ModelAsset asset, double maximumEdgeLength, float3 measurementScale) {
    ValidateMaximumEdgeLength(maximumEdgeLength);
    ValidateMeasurementScale(measurementScale);
    // Build local-space vertices and indices exactly as today.
    // Compare edge deltas after multiplying X, Y, and Z by measurementScale.
}
```

- [ ] Update only `GetDistanceSquared` and its callers to use double-precision `delta * measurementScale` values. Keep midpoint positions, normals, UVs, seams, submeshes, index-width promotion, output cap, and atomic mutation local-space and unchanged.

- [ ] Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ModelTessellationProcessorTests"`.

## Task 4: Carry final static world scale through scene rewriting

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/SceneComponentPackagingTransformServiceTests.cs`

- [ ] Add failing packager tests for an enabled MeshComponent on an entity with local scale `new float3(2f, 1f, 1f)`, and another beneath a parent with scale `new float3(3f, 1f, 1f)`. Assert the child receives `new float3(6f, 1f, 1f)` as the measurement scale after target-platform transform overrides are applied.

- [ ] Change recursive scene rewriting to receive `float3 parentWorldScale`. After `ApplyTargetPlatformTransformOverride(entityAsset)`, calculate:

```csharp
float3 worldScale = entityAsset.LocalScale * parentWorldScale;
RewriteComponentRecord(componentRecord, buildRootPath, worldScale);
RewriteEntityAsset(childEntityAsset, buildRootPath, worldScale);
```

Use `float3.One` for root entities. Reject non-finite or zero components before requesting a tessellation variant, but do not reject scale on components with tessellation disabled.

- [ ] Extend `SceneComponentPackagingTransformService.TryTransform` and its internal MeshComponent transform path to accept a context object containing the target platform ID, source component record, resolved `MeshComponentTessellationSettings`, and final world scale. Keep all non-MeshComponent transformations behaviorally identical.

- [ ] Resolve the settings from the original wrapped component record's selected platform override; consume them inside packaging and do not emit `MeshTessellate` or `MeshTessellationMaxEdgeLength` in the packaged runtime component payload.

- [ ] Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorWindowsBuildScenePackagerTests|FullyQualifiedName~SceneComponentPackagingTransformServiceTests"`.

## Task 5: Cook and deduplicate component model variants

**Files:**
- Create: `engine/helengine.editor/managers/project/MeshComponentTessellationVariantService.cs`
- Create: `engine/helengine.editor/managers/project/MeshComponentTessellationVariantRequest.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Test: `engine/helengine.editor.tests/managers/project/MeshComponentTessellationVariantServiceTests.cs`

- [ ] Write failing variant-service tests for disabled passthrough, equal-request reuse, different scale/threshold separation, and source immutability:

```csharp
[Fact]
public void Resolve_WhenTwoRequestsHaveEqualIdentity_WritesOneVariantAndReturnsOneReference() {
    SceneAssetReference first = Service.Resolve(Request(sourceModel, new float3(4f, 1f, 1f), 0.5d));
    SceneAssetReference second = Service.Resolve(Request(sourceModel, new float3(4f, 1f, 1f), 0.5d));

    Assert.Equal(first.RelativePath, second.RelativePath);
    Assert.Single(TestAssetWriter.WrittenModelAssets);
}
```

- [ ] Implement `MeshComponentTessellationVariantRequest` as an immutable request containing the resolved source model asset/reference, target platform ID, settings, world scale, build root, and source component identity for diagnostics. It must reject null model/reference and invalid scale/settings.

- [ ] Implement the variant service with a dictionary keyed by `MeshComponentTessellationSettingsService.BuildVariantIdentity`. For an enabled request: deep-clone the source `ModelAsset`, call `ModelTessellationProcessor.Apply(clone, edgeLength, worldScale)`, write it beneath `cooked/generated/models/tessellation/<sha256>.hasset`, and return a file-system packaged `SceneAssetReference` using that runtime path. For disabled requests, return the original rewritten model reference without writing an asset.

- [ ] Use the source model after `RewriteFileSystemModelReference`/`RewriteGeneratedModelReference` has resolved the platform-processed model. This makes import-level tessellation happen first and component refinement second. Register the variant output with the existing cooked-artifact and builder cook-work-item tracking so PS2/PSP consume it via their normal model builders.

- [ ] Preserve original model IDs, materials, submeshes, bounds, and source arrays; only the cloned generated asset is tessellated. Fail with a source-reference and component identifier in the exception when a requested model cannot be resolved.

- [ ] Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~MeshComponentTessellationVariantServiceTests"`.

## Task 6: End-to-end regression and platform verification

**Files:**
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- Modify: `C:/dev/helworks/helengine-ps2/builder.tests/Ps2PlatformAssetBuilderTests.cs` only for a generated-variant consumption test

- [ ] Add an end-to-end scene-packaging test with two scaled cubes sharing the same source model and settings, plus an unenabled third cube. Verify two equal components reference the same generated path, the unenabled component retains the normal imported path, and only one variant model is written.

- [ ] Add a combined-settings test: enable model-import tessellation and component tessellation for a scaled entity, then assert component cooking receives the already processed source and creates a distinct second-stage variant.

- [ ] Add failure tests for zero/non-finite world scale, invalid member text, missing source model, malformed index data, and the existing 1,000,000 triangle limit. Each test must assert the packaged scene/model output is not partially written.

- [ ] Add a PS2 builder test that consumes an in-range generated tessellation variant through `Ps2PlatformAssetBuilder` without renderer-specific code; retain the existing clear error for a variant that exceeds PS2's 16-bit index limit.

- [ ] Run editor tests: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj`.
- [ ] Run PS2 builder tests from `C:/dev/helworks/helengine-ps2`: `dotnet test builder.tests/helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Ps2PlatformAssetBuilderTests"`.
- [ ] Run `git diff --check` in both repositories. Confirm no generated source, runtime `MeshComponent` member, or PS2 renderer file changed; review every new public/member declaration for substantive XML comments.
