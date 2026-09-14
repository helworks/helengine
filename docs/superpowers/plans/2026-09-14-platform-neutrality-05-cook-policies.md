# Platform-owned cook policies and codecs Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans task-by-task. Delegation requires the user's applicable opt-in; do not launch an independent review without permission. Steps use checkbox syntax.

**Goal:** Remove DS-specific path, audio validation and encoder registration from shared orchestration.

**Architecture:** Extend existing asset cook capabilities for neutral naming and audio limits. Move the payload encoder contract to baseplatform and discover console encoders through the existing builder loader, never by referencing editor from a builder.

**Tech Stack:** C#/.NET 9, xUnit, existing platform builder contracts and native host APIs.

**Spec:** [Platform neutrality migration design](../specs/2026-09-14-platform-neutrality-design.md)

## Global constraints

Read [the shared design](../specs/2026-09-14-platform-neutrality-design.md). All its compatibility and execution constraints apply. Paths are relative to C:/dev/helworks/helengine unless an absolute sibling-repository path is given. New paths are explicitly marked Create. Revalidate external repository instructions, branch and dirty state before implementing there.

For each task: run the named characterization tests first, add the new failing regression, implement the described change, rerun the focused tests, inspect the diff, then commit only that task's explicit files. Do not treat a zero-test filter as success. Build outputs must use a visible workspace-owned directory.

## File map

- Modify: engine/helengine.baseplatform/Definitions/PlatformAssetCookCapabilityDefinition.cs; reuse its existing OutputFileExtension property.
- Create: engine/helengine.baseplatform/Definitions/PlatformAssetNamingPolicy.cs, PlatformAudioLimits.cs.
- Move: engine/helengine.editor/managers/asset/processing/IEditorAudioPayloadEncoder.cs to engine/helengine.baseplatform/Builders/IPlatformAudioPayloadEncoder.cs; rename the interface.
- Create: engine/helengine.baseplatform/Builders/IPlatformAudioPayloadEncoderProvider.cs.
- Modify: engine/helengine.editor/managers/project/EditorPlatformAssetBuilderLoader.cs, ImportedTextureRuntimePathResolver.cs, SceneComponentPackagingTransformService.cs, EditorRuntimeNativeManifestWriter.cs.
- Modify: engine/helengine.editor/managers/asset/processing/EditorAudioSampleProcessor.cs, Pcm16AudioPayloadEncoder.cs, managers/asset/AssetImportManager.cs and production registration callers.
- Move: NintendoDsImaAdpcmAudioPayloadEncoder.cs to C:/dev/helworks/helengine-ds/builder/audio/; update namespace and implement the baseplatform interface.
- Modify externally: C:/dev/helworks/helengine-ds/builder/NintendoDsPlatformDefinitionFactory.cs and its concrete IPlatformAssetBuilder implementation.
- Create: engine/helengine.editor.tests/managers/project/PlatformCookPolicyTests.cs.
- Modify existing audio encoder/processor, packaging audio and imported-texture tests; move DS-specific payload fixtures to the DS builder tests.

### Task 1: Model target output policy as capability data

**Contracts:** PlatformAssetNamingPolicy enum values PreserveAssetId and RuntimeAssetIdHex16. PlatformAudioLimits(int maximumSampleRate, int maximumChannels) rejects non-positive values. Extend PlatformAssetCookCapabilityDefinition with naming policy and optional audio limits using a backward-compatible constructor overload; default naming remains PreserveAssetId. Use the existing OutputFileExtension. Missing audio limits means no additional platform limit, not missing asset validation.

- [ ] Add tests with a fictional platform: identical policy yields identical naming and limits regardless of platform ID. Include extension normalization and runtime-ID stability.
- [ ] Refactor ImportedTextureRuntimePathResolver to accept PlatformAssetCookCapabilityDefinition instead of branching on targetPlatformId. Preserve the exact RuntimeAssetIdGenerator.Generate algorithm and lowercase 16-digit hex formatting.
- [ ] DS declares RuntimeAssetIdHex16 plus .hetex and limits 22050 Hz/1 channel. Existing targets retain existing naming rules.
- [ ] Replace ValidateAudioSettingsForTarget's ds branch with explicit capability limit checks; preserve descriptive errors, rejecting 22051 Hz or 2 channels under the DS policy.
- [ ] Add regression:
```csharp
var limits = new PlatformAudioLimits(22050, 1);
Assert.Equal(22050, limits.MaximumSampleRate);
Assert.Equal(1, limits.MaximumChannels);
```
Add behavioral tests invoking actual packaging, not only these value assertions.
- [ ] Run `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore -m:1 --filter "FullyQualifiedName~PlatformCookPolicyTests|FullyQualifiedName~EditorWindowsBuildScenePackagerAudioTests"`; run affected DS definition tests.
- [ ] Commit contract, editor integration and DS declaration changes separately.

### Task 2: Register the DS codec through the builder boundary

**Interfaces:** IPlatformAudioPayloadEncoder keeps `string EncodingFamilyId { get; }` and `byte[] Encode(short[] samples)`. IPlatformAudioPayloadEncoderProvider exposes `IReadOnlyList<IPlatformAudioPayloadEncoder> GetAudioPayloadEncoders()`. Both live in baseplatform; the DS builder implements the optional provider.

- [ ] Preserve golden payload fixtures for silence, signed extremes, odd sample counts, block headers and nibble order before moving code.
- [ ] Move the DS implementation without changing math or payload layout. Keep PCM processing in shared code and register PCM explicitly.
- [ ] Builder loader queries the optional provider after version compatibility checks and supplies its codecs to the authoring/cook scope before the registry freezes. Importer registration and codec registration remain distinct.
- [ ] Remove the default NintendoDsImaAdpcmAudioPayloadEncoder construction from EditorAudioSampleProcessor. Validate duplicate encoding-family registration and missing requested family as explicit errors.
- [ ] Ensure a plugin that requests adpcm-buffered without providing its codec fails with the encoding family and builder identity in the diagnostic. Do not fall back to PCM.
- [ ] Add tests for two builder scopes with independent codec registries, registry lifetime during unload, duplicate IDs, unknown codecs, and byte-identical DS output.
- [ ] Run editor audio tests and `dotnet test C:/dev/helworks/helengine-ds/builder.tests/helengine.ds.builder.tests.csproj --no-restore --filter FullyQualifiedName~Audio`. Verify the filter selects real tests.
- [ ] Commit `refactor: load target audio encoders from builders` with coordinated platform version requirements.

### Task 3: Finish manifest and real cook integration

- [ ] Replace NintendoDsGeneratedBootSceneRelativePath handling in EditorRuntimeNativeManifestWriter with the resolved scene artifact path from the build manifest. Preserve the currently emitted DS value.
- [ ] Audit SceneComponentPackagingTransformService, EditorWindowsBuildScenePackager and AssetImportManager for remaining target-ID dispatch. Move target-specific shader/material selection to existing builder/cook capability contracts; keep preview shader target supplied by plan 04.
- [ ] Test the existing EditorPlatformAssetCookService and platform build entry points. Do not limit validation to the new cook-graph classes, which are not yet the full production path.
- [ ] Compare DS texture filenames, audio bytes, manifest references and boot paths before/after using a deterministic fixture. Verify desktop packaging stays unchanged.
- [ ] Run focused PlatformCookPolicyTests, EditorPlatformAssetCookServiceTests, EditorPlatformBuildGraphRunnerTests and DS packaging tests.
- [ ] Remove obsolete shared DS codec tests once equivalent tests live with the implementation; retain neutral registration tests in shared editor. Commit `refactor: finish platform-owned cook policy routing`.

## Completion

No ds branch remains in shared import/package behavior for these findings. A new platform can reuse the same neutral policy values without adding shared-engine code. Existing builder compatibility and actual package contents are verified before rollout.
