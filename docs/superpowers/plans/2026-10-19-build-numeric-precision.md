# Build Numeric Precision Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans for inline implementation or superpowers:subagent-driven-development if Helena explicitly selects delegation. Track the checkboxes below. Always use the real checkouts; never create worktrees, branches or source exports.

**Goal:** Build the same project with independently selected 16/32/64-bit storage and arithmetic, beginning with working Windows 32/32 and 64/64 and a measured PS1 32/32 baseline, followed by explicit PS1 half-precision experiments.

**Architecture:** Resolve an immutable numeric policy for every build and carry its versioned identity through cook, native conversion, package contracts and caches. Introduce identifiable stored `Real` and transient arithmetic values so mixed storage/arithmetic does not narrow after each operator. Preserve explicit SDK/GPU/time contracts and use the existing build/profile/queue flow.

**Tech Stack:** C#/.NET 9, Roslyn converter, generated C++20, Windows/MSVC, MIPS GCC 16.2.0 and the existing PS1 Docker toolchain, xUnit and native compile/run regression tests.

**Spec:** C:\dev\helworks\helengine\docs\superpowers\specs\2026-10-19-build-numeric-precision-design.md (approved by Helena).

## Global Constraints

- StorageBits and ArithmeticBits accept exactly 16, 32 or 64; resolved default is 32/32 and representation version starts at 1.
- Build selection overrides platform defaults; two Windows selections must coexist in the queue without global precision changes.
- Core, cooked payloads and scene modules reject incompatible policy identities before consuming dependent structures.
- Fixed-width single/double IO and external formats retain their stated widths. No blanket primitive remapping, generated-source patches, fast-math or silent precision fallback.
- Compound arithmetic retains ArithmeticBits; narrowing occurs at stored destinations. ArithmeticBits=16 has binary16 operation rounding, including on software implementations.
- Real source changes and tests run in the actual checkouts. All scratch/generated sources, test outputs and logs go under C:\temp\numeric-precision, with fixed windows and ps1 subdirectories. Final packages overwrite C:\temp\numeric-precision\windows\package and C:\temp\numeric-precision\ps1\package respectively (the original builds output was rejected by automatic approval review; the fixed C:\temp alternative is authorized).
- Preserve unrelated local edits. Do not create any worktree. Check status before editing each repository; consult Helena for another session's changes on target paths or an unapproved branch.
- Helena explicitly approved using the existing clean master in C:\dev\helworks\csharpcodegen. Keep that branch; do not create or switch branches. The engine, Windows and PS1 repositories remain on main.
- Helena authorized integrating the necessary existing viewport/backend/project changes while preserving them. Existing PS1 profiler-forwarding edits belong to the preceding profiling task and must remain intact.

Before invoking any test/build command, create its fixed scratch directory, set TEMP and TMP to C:\temp\numeric-precision\windows (or ps1 for that target), and select the listed artifacts/results roots. Do not use the default repository bin/obj or AppData temporary paths for agent-owned outputs.

## Review Focus

1. Queue Windows 32/32 and 64/64 together: immutable snapshots must prevent the second selection changing the first (Task 1).
2. Mixed 16/32 expressions must retain values that would disappear if an operator narrowed early (Tasks 2 and 7).
3. Numeric policy mismatch must be rejected before allocation/deserialization of dependent records, not after native corruption (Tasks 4 and 5).
4. GPU/material ABI and fixed clocks must retain their explicit types in a 64-bit engine build (Tasks 3 and 6).
5. Old authoring assets and non-finite/rounding boundary values must survive intentional conversions without silent format reinterpretation or project rewrites (Tasks 2, 4 and 7).

## Task 1: Resolve and snapshot build numeric policy

**Files:**
- Create C:\dev\helworks\helengine\engine\helengine.core\numeric\NumericPrecisionPolicy.cs and NumericPrecisionBits.cs (one type per named file in that directory).
- Create C:\dev\helworks\helengine\engine\helengine.baseplatform\Definitions\PlatformNumericCapabilities.cs.
- Modify C:\dev\helworks\helengine\engine\helengine.baseplatform\Definitions\PlatformCodegenSettingIds.cs, PlatformCodegenProfileDefinition.cs and PlatformBuildProfileDefinition.cs.
- Modify C:\dev\helworks\helengine\engine\helengine.baseplatform\Requests\PlatformBuildRequest.cs.
- Modify C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorBuildProfileSettingsDocument.cs, EditorBuildQueueItemDocument.cs, EditorBuildConfigService.cs and EditorPlatformBuildGraphRunner.cs.
- Create C:\dev\helworks\helengine\engine\helengine.baseplatform.tests\Definitions\PlatformNumericPolicyTests.cs and C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorBuildNumericPolicyTests.cs.

**Interfaces:** `NumericPrecisionPolicy(int storageBits, int arithmeticBits)` exposes immutable `StorageBits`, `ArithmeticBits`, `RepresentationVersion=1` and canonical `Identity` (`real-v1-s32-a32`, for example). `PlatformNumericCapabilities.RequireSupported(NumericPrecisionPolicy policy)` rejects unavailable combinations. Add `NumericPolicy` to the resolved request and queue snapshot; setting IDs are `real-storage-bits` and `real-arithmetic-bits`.

- [x] Write tests named DefaultUses32By32, ExplicitWindowsPoliciesRemainIndependent, RejectsUnsupportedBitWidth, QueuedBuildRetainsOriginalPolicy and IdentityIsDeterministic. Assert default 32/32, explicit 64/64, rejection of 8/24/128, immutable queue snapshots, canonical identity and unsupported platform combination errors. At this stage capabilities advertise only implemented combinations.
- [x] Run `dotnet test C:\dev\helworks\helengine\engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj --filter FullyQualifiedName~PlatformNumericPolicyTests --artifacts-path C:\temp\numeric-precision\windows\dotnet --results-directory C:\temp\numeric-precision\windows\test-results`; expect failure for missing contract.
- [x] Implement the immutable contract and existing profile-option/queue resolution. Reject conflicting or malformed values before cook. Expose the two settings through existing builder setting definitions, not a new settings subsystem.
- [x] Re-run that filter and `dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorBuildNumericPolicyTests --artifacts-path C:\temp\numeric-precision\windows\dotnet --results-directory C:\temp\numeric-precision\windows\test-results`; expect both to pass.
- [x] Commit only Task 1 paths in the engine checkout: `feat: resolve numeric precision per build`.

## Task 2: Managed stored scalar and evaluation semantics for 32/64

**Files:**
- Create C:\dev\helworks\helengine\engine\helengine.core\numeric\Real.cs, RealEvaluation.cs, RealMath.cs and NumericArithmetic.cs.
- Create C:\dev\helworks\helengine\build\NumericPrecision.targets; import it in C:\dev\helworks\helengine\engine\helengine.core\helengine.core.csproj.
- Create C:\dev\helworks\helengine\engine\helengine.core.tests\numeric\RealPrecisionTests.cs and RealArithmeticTests.cs.
- Modify C:\dev\helworks\helengine\AGENTS.md to replace unconditional double math with the approved build-policy instruction.

**Interfaces:** Public stored `readonly partial struct Real`; transient `readonly partial struct RealEvaluation`; `Real.ToDouble()`, explicit `Real.ToSingle()`, explicit `Real.FromDouble(double)`, and explicit fixed-width conversions. Numeric operators on `Real` produce `RealEvaluation`; conversion to a `Real` stored destination rounds once. `RealMath.Lerp`, `Sqrt`, `Sin`, `Cos`, `Atan2` and `Remainder` consume/produce evaluation values. MSBuild properties `HelengineRealStorageBits` and `HelengineRealArithmeticBits` generate only scalar representation members under the configured intermediate directory, without conditionals scattered through components.

**Host/build integration:** Propagate HelengineRealStorageBits and HelengineRealArithmeticBits consistently through all managed project references; do not reference two incompatible helengine.core representations inside one process. Configure C:\dev\helworks\helengine\engine\helengine.editor\helengine.editor.csproj and its in-process managed dependencies as one authoring 64/64 graph. Keep build policy snapshots separate from that host representation. Generated native policy comes from the request, never from the editor assembly layout. Tests of managed runtime policies compile/run sequentially in the same fixed scratch location.

**Cook boundary:** The editor's authoring instance uses the 64/64 managed representation to preserve new authoring values. `NumericArithmetic(NumericPrecisionPolicy policy)` is immutable and exposes `Add/Subtract/Multiply/Divide/Remainder/Sqrt/Sin/Cos/Atan2` plus `Store`, with canonical double transport values and explicit per-operation precision semantics. Cook kernels receive this instance explicitly; no process-global or ambient mutable precision setting. Runtime managed builds compile the selected representation, while native lowering in Task 3 uses the same contract.

- [x] Write failing tests StoredLayoutMatchesPolicy, ConversionRoundsToNearestEven, SpecialValuesRetainClassification and ExplicitStoreIsTheNarrowingBoundary for 32/32 and 64/64: exact sizeof expectations for stored/evaluation values; round-to-nearest ties-to-even, signed zero, overflow, subnormals, infinity and NaN classification. Pin 16777217 to preserved value in 64-bit and rounded 16777216 in 32-bit storage.
- [x] Run `dotnet test C:\dev\helworks\helengine\engine\helengine.core.tests\helengine.core.tests.csproj --filter FullyQualifiedName~RealPrecisionTests --artifacts-path C:\temp\numeric-precision\windows\dotnet --results-directory C:\temp\numeric-precision\windows\test-results`; expect missing-type failures.
- [x] Implement Real/RealEvaluation, representation generation and explicit cook arithmetic. Test that expanding a stored value does not itself reduce precision again; keep fixed primitive conversions visible. No FP16 capability is advertised yet.
- [x] Re-run the numeric filters sequentially with `-p:HelengineRealStorageBits=32 -p:HelengineRealArithmeticBits=32` and then 64/64, overwriting the fixed scratch output; compare `NumericArithmetic` results to the corresponding compiled managed backend. Expect all assertions to pass.
- [x] Commit only Task 2 paths: `feat: add stored and evaluation numeric representations`.

## Task 3: Lower configurable numeric types in the converter

**Files:**
- Modify C:\dev\helworks\csharpcodegen\cs2.cpp\model\CPPConversionOptions.cs, C:\dev\helworks\csharpcodegen\codegen\CodegenCliOptionsBuilder.cs and C:\dev\helworks\csharpcodegen\cs2.cpp\CPPGeneratedConfigWriter.cs.
- Create C:\dev\helworks\csharpcodegen\cs2.cpp\CPPNumericPolicyResolver.cs and CPPRealIntrinsicEmitter.cs.
- Integrate their dispatch in C:\dev\helworks\csharpcodegen\cs2.cpp\CPPConversiorProcessor.cs; register emitted numeric runtime support through the existing runtime registrar.
- Create C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPRealNumericPolicyTests.cs and CPPRealArithmeticCompilationTests.cs.
- Modify C:\dev\helworks\csharpcodegen\AGENTS.md to remove the same unconditional-double instruction.

**Interfaces:** Conversion options expose normalized storage/arithmetic bits and version. CLI accepts existing `--set real-storage-bits=32 --set real-arithmetic-bits=32`. `CPPNumericPolicyResolver.Resolve(CPPConversionOptions options)` validates supported representations. Emit `he_real_storage`, `he_real_eval`, `he_real_load` and `he_real_store` and policy identity. Match only the fully qualified engine Real/RealEvaluation symbols; ordinary System.Single and System.Double remain their original types.

- [x] Write tests RealSymbolsLowerToSelectedWidth, FixedPrimitivesAreUnchanged, NumericHelpersHonorArithmeticPolicy and ConfigIdentityIncludesBothWidths for symbol mapping, selected literals/math functions, untouched primitive interop and generated config identity. Compile/run the same managed numeric fixtures as C++; assert equal results for 32/32 and 64/64, rather than only asserting emitted text.
- [x] Run `dotnet test C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj --filter FullyQualifiedName~CPPReal --artifacts-path C:\temp\numeric-precision\windows\dotnet --results-directory C:\temp\numeric-precision\windows\test-results`; expect missing support failures.
- [x] Implement native lowering and helper emission at the actual conversion sites. Explicitly lower evaluation operations at ArithmeticBits and stores at StorageBits. Preserve the source's named fixed-precision calls; do not globally substitute sqrt/sin/modulo or float/double.
- [x] Re-run the new tests and relevant existing numeric compilation filters; require actual MSVC native execution and passing native/managed comparison. Update the engine's real vendor-submodule reference only after the converter commit is integrated into that actual checkout; never build an exported copy.
- [x] Commit converter changes as `feat: lower build-configurable real arithmetic`; record the exact converter revision used by the engine.

## Task 4: Precision-aware authored/cooked serialization and math migration

Progress: strong Real vectors/transforms/animation and explicit GPU boundaries migrated; authored tagged-v2 and asset-v26 declare numeric policies, legacy authoring widths are explicit, runtime rejects mismatches before payload. Queue policy reaches dependent cook producers and scene-package identity. Shared layout kernels specialize live RealEvaluation and per-request cook arithmetic; actual native kernels execute under 32/32, 64/64 and 32/64. Final regression, complete source-hash/cook validation and ABI identity work remain. Existing viewport edits are integrated with Helena's explicit authorization.

**Files:**
- Modify C:\dev\helworks\helengine\engine\helengine.core\model\float2.cs, float3.cs, float4.cs and float4x4.cs; C:\dev\helworks\helengine\engine\helengine.core\Entity.cs; C:\dev\helworks\helengine\engine\helengine.core\components\AnimationPlayerComponent.cs; C:\dev\helworks\helengine\engine\helengine.core\animation\AnimationClipEvaluator.cs and stored numeric fields in C:\dev\helworks\helengine\engine\helengine.core\assets\raw\animation\PositionKeyframeAsset.cs, C:\dev\helworks\helengine\engine\helengine.core\assets\raw\animation\RotationKeyframeAsset.cs and C:\dev\helworks\helengine\engine\helengine.core\assets\raw\animation\AnimationClipAsset.cs.
- Create C:\dev\helworks\helengine\engine\helengine.core\numeric\RealBinaryCodec.cs; modify C:\dev\helworks\helengine\engine\helengine.core\serialization\EngineBinaryReader.cs, EngineBinaryWriter.cs and C:\dev\helworks\helengine\engine\helengine.core\assets\PackagedAssetBinarySerializer.cs.
- Modify C:\dev\helworks\helengine\engine\helengine.editor\serialization\scene\EditorTaggedSceneComponentFieldReader.cs and EditorTaggedSceneComponentFieldWriter.cs; integrate precision-aware field codecs in their existing callers.
- Modify C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorWindowsBuildScenePackager.cs and ViewportLayoutCookProcessor.cs; adapt C:\dev\helworks\helengine\engine\helengine.core\components\layout\ViewportLayoutMath.cs, C:\dev\helworks\helengine\engine\helengine.core\components\layout\ViewportLayoutMetrics.cs and C:\dev\helworks\helengine\engine\helengine.core\components\layout\ViewportLayoutApplier.cs only after local-edit ownership is confirmed.
- Create C:\dev\helworks\helengine\engine\helengine.core.tests\numeric\RealBinaryCodecTests.cs and C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\NumericPrecisionCookTests.cs.

Fixed contracts retained during this migration: Core.FrameDeltaSeconds, TotalElapsedSeconds and physics scheduler clocks stay IEEE64; explicit ReadSingle/ReadDouble and font/shader/material encodings retain their specified wire widths; GpuFloat2/3/4/4x4 retain R32 layouts and use named upload conversions; BEPU/System.Numerics and HelPhysics.PhysicsScalar remain their fixed physics domains. ViewportCookedLayout's existing double metadata is explicit presentation transport; evaluated factors and participants use selected Real storage. Workspace JSON uses fixed double authoring transport. The generic vector names remain float2/3/4/4x4 for API compatibility, but their fields follow StorageBits.

Validation boundary: full Physics3D suite currently has seven dynamics/stack failures. The rotation integrator's component-wise quaternion product is present in baseline8c59c2f0 and selected failing torque/tilt cases reproduce at32/32. A proposed quaternion-composition correction was tested then removed because solver behavior needs separate validation; this precision change does not retune or modify that integration algorithm. Current physics serializers/analyzer/scene-loading tests pass. Full native Windows/PS1 builds and their advertised support remain Tasks6-8; native fixture success does not claim backend support.

**Interfaces:** `RealBinaryCodec.Write(EngineBinaryWriter writer, double value, NumericPrecisionPolicy policy)` and `Read(EngineBinaryReader reader, NumericPrecisionPolicy policy)` encode/decode the selected IEEE storage width with the stream's existing endianness. Add `ReadReal/WriteReal` and precision-aware vector counterparts separately from fixed ReadSingle/ReadDouble. Precision-aware cooked headers carry version/StorageBits/ArithmeticBits and are validated before their data is interpreted.

- [x] Write tests RealVectorsRoundTripBothEndiannesses, RejectsPolicyBeforePayloadRead, OldAuthoringWidthIsReadExplicitly and CookDoesNotRewriteSourceAssets for both endiannesses, vector/matrix/component/animation payloads, old authoring fixture reads and unsupported/mismatched headers. Hash authored input before/after cook and assert it remains identical. Check explicit material/uniform formats retain fixed widths.
- [x] Run the RealBinaryCodecTests and NumericPrecisionCookTests filters using the fixed artifacts/results roots from Task 1; expect failures on absent precision-aware encoding.
- [x] Migrate stored configurable fields and math to Real/evaluation operations; eliminate explicit double promotions only in those migrated paths. Existing fixed primitives and explicit clocks stay fixed. Add policy-aware overloads of ViewportLayoutMath.Resolve and ViewportLayoutApplier.ResolvePosition that take an explicit NumericArithmetic, keeping the existing runtime entry points bound to compiled RealMath. Use the same formula implementation for cook and runtime, not independently maintained formulas. Version new authored numeric field encodings and cooked payloads; read old authoring data via its original width, never guess. Before choosing the new packaged version, re-read the current version (25 during planning) and increment the current value, not a stale constant.
- [x] Evaluate cook layout through the explicit NumericArithmetic policy, sharing formula/rounding tests with runtime math; do not merely quantize the final result after doing an entire 64-bit cook. Run the new filters plus existing animation, viewport and packaged-asset tests. Require numeric agreement between cooked and runtime layout for each supported pair.
- [x] Commit the coherent type/math/serialization migration as `feat: cook and load precision-aware engine numeric data`; include only reconciled owned paths. Do not stage unrelated existing viewport work.

## Task 5: Propagate identity through generation, caches and modules

**Files:**
- Modify C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorPlatformBuildGraphRunner.cs, EditorPlatformCodeCookService.cs, EditorScenePackageKey.cs and EditorPlatformBuildGraphWorkspaceFactory.cs.
- Modify C:\dev\helworks\helengine\engine\helengine.editor\managers\project\cooking\EditorAssetCookKeyBuilder.cs and EditorAssetCookGraphRequest.cs; propagate relevant numeric settings to node keys.
- Modify C:\dev\helworks\helengine\engine\helengine.baseplatform\Manifest\PlatformBuildManifest.cs and PlatformBuildCodeModule.cs.
- Modify C:\dev\helworks\helengine-ps1\scripts\build-scene-modules.py, write-scene-module-metadata.py and C:\dev\helworks\helengine-ps1\src\platform\ps1\modules\Ps1SceneModuleMetadata.hpp and Ps1CodeModuleProvider.cpp.
- Create C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\NumericPrecisionCacheTests.cs; add actual module-header mismatch coverage to C:\dev\helworks\helengine-ps1\tests\host.

**Interfaces:** Every generated/native/cooked identity includes the same `NumericPrecisionPolicy.Identity`; generated Core and authored modules receive the identical normalized CLI values. Manifest/module numeric identity is checked alongside existing content and resident build IDs before materialization or module execution.

- [x] Write tests CookKeyChangesWithNumericPolicy, NativeKeyChangesWithNumericPolicy, EquivalentPolicyOptionsHaveSameKey and RejectsModulePolicyBeforeLoad where 32/32 and 64/64 share source hashes/profile labels but must produce different cook, generation, native and module keys. Repeat with reordered equivalent settings to prove determinism. Forge only the policy identity in real test packages and require early rejection.
- [x] Run NumericPrecisionCacheTests and the module-host fixture; expect invalidation/mismatch failures before implementation.
- [x] Propagate one resolved immutable policy through every graph phase; extend existing locks/fingerprints rather than introducing timestamp folders or deleting shared cache roots. Preserve the existing profiler-forwarding edits in the PS1 Python runner.
- [x] Re-run tests and build 32 -> 64 -> 32 using the same fixed output. Prove the final package's content/Core/modules all match its selected policy, and a concurrent queue snapshot remains unchanged.
- [x] Commit engine and PS1 changes separately with explicit matching contract revisions: `feat: bind numeric policy to build and module identities`.

## Task 6: Windows 32/64 end-to-end and PS1 32-bit baseline

**Files:**
- Modify C:\dev\helworks\helengine-windows\builder\WindowsPlatformDefinitionFactory.cs, WindowsBuildWorkspace.cs, WindowsNativeBuildExecutor.cs and WindowsRuntimeNativeManifestWriter.cs.
- Modify C:\dev\helworks\helengine-ps1\builder\PlayStation1PlatformAssetBuilder.cs, C:\dev\helworks\helengine-ps1\builder\PlayStation1SceneModuleBuildPlanWriter.cs and C:\dev\helworks\helengine-ps1\builder\PlayStation1PlatformDefinitionFactory.cs.
- Adapt Windows and PS1 native render/input/content call sites that directly consume migrated vector/scalar fields; keep SDK/GPU representations fixed through explicit load/conversion boundaries.
- Create C:\dev\helworks\helengine-windows\builder.tests\WindowsNumericPrecisionTests.cs and C:\dev\helworks\helengine-ps1\tests\helengine.ps1.platformbuilder.tests\PlayStation1NumericPrecisionTests.cs.

**Interfaces:** Windows advertises 32/32 and 64/64 only after native conversion/interop passes; PS1 initially advertises 32/32. Existing debug/profiler/release selections remain independent of numeric precision. Final reports record numeric policy and revisions; old generated Core without the matching identity is rejected.

- [x] Write builder tests WindowsPoliciesReachCoreAndModules, DoubleEnginePreservesGpuLayout and Ps1RejectsUnavailableHalfArithmetic for policy handoff, fixed DirectX buffer layout in 64-bit engine mode, option persistence and unsupported half arithmetic diagnostics. Inventory all native scalar/vector consumers; do not silently enable unported combinations on other platforms.
- [x] Run the two new builder filters in their actual project checkouts with fixed C:\temp\numeric-precision\windows or ps1 artifacts/results roots; expect failures for absent capability/handoff support.
- [x] Adapt both backends' explicit native boundaries and build receipts. Regenerate Core through the existing real converter checkout, then build Windows 32/32 and 64/64 sequentially into the same Windows output. Do not overwrite or regenerate the authored DemoDisc project from a scratch copy.
- [x] Run menu + all 14 rendering scenes on each Windows precision, report success/crash per scene and numeric/visual differences. Only capture screenshots when authorized. Build/run the PS1 32/32 menu through emulator MCP and capture comparable steady 600-frame profiler windows. Compare with C:\dev\helworks\builds\ps1\menu-profile.json before replacing that report with the new annotated results; keep no build history.
- [x] Commit backend integrations separately: `feat: build Windows and PS1 with resolved numeric policies`. Report actual tested platforms; do not claim all installed targets support the new representations.

## Task 7: PS1 16-bit storage with 32-bit arithmetic

**Files:**
- Extend C:\dev\helworks\helengine\engine\helengine.core\numeric\RealBinaryCodec.cs, NumericArithmetic.cs and C:\dev\helworks\helengine\build\NumericPrecision.targets.
- Extend C:\dev\helworks\csharpcodegen\cs2.cpp\CPPRealIntrinsicEmitter.cs and emitted runtime support registration.
- Extend C:\dev\helworks\helengine-ps1\builder\PlayStation1PlatformAssetBuilder.cs and corresponding numeric tests from Tasks 2, 3, 4 and 6.

**Interfaces:** Binary16 stored Real is exactly 2 bytes with specified alignment; evaluation remains IEEE binary32. `he_real_load/store` implement explicit software conversion without assuming MIPS `_Float16` support. Only advertise PS1 16/32 after this task's gates pass.

- [x] Add tests Binary16StorageMatchesIeeeBoundaries, MixedArithmeticDoesNotNarrowEarly and StoredIntermediateRoundsToBinary16 for signed zero, minimum subnormal, maximum finite 65504, halfway rounding, NaN/infinity classification and both endiannesses. Assert a transient expression equivalent to `(2048 + 1) - 2048` yields 1 with 16/32 arithmetic before the final store, whereas an explicit intermediate stored Real becomes 2048. Compare complete native and managed expression results, not just component sizeof.
- [x] Run the numeric, codec and converter compile/run filters; expect 16/32 rejection or absent implementation failures.
- [x] Implement the portable half storage codec/load/store and managed representation with genuine 32-bit intermediates. Test struct size/alignment and fixed GPU interfaces on the actual MIPS build, not only the host.
- [ ] Build/run PS1 menu and supported rendering scenes; measure retained runtime RAM, cooked bytes, package bytes, component/render CPU and visible/numeric errors against the saved 32/32 measurement in the current review report. Overwrite the same PS1 output. Report a slowdown honestly; reduced storage is not an acceptance claim of faster arithmetic.
- [x] Commit only verified support: `feat: add PS1 binary16 storage with float32 evaluation`.

## Task 8: Explicit binary16 arithmetic and final review

**Files:**
- Extend C:\dev\helworks\helengine\engine\helengine.core\numeric\RealMath.cs and NumericArithmetic.cs; add C:\dev\helworks\csharpcodegen\cs2.cpp\CPPBinary16ArithmeticSupportWriter.cs and its runtime registrar integration.
- Extend native/managed numeric fixtures and PS1 capabilities only after successful actual MIPS linkage and execution.
- Update C:\dev\helworks\helengine\README.md with selected-policy usage and the exact supported combinations measured by the preceding tasks.

**Interfaces:** The portable binary16 backend explicitly rounds each primitive operation result to binary16, including math-function outputs and intermediate operations; compare canonical bit patterns/classification with the managed binary16 contract. It is a separate evaluator from 16/32, not an alias or implicit wider fallback. Fixed-point substitution remains out of scope.

- [x] Write differential tests Binary16RoundsEachPrimitiveOperation, QuaternionNormalizationMatchesManaged and FixedClockDoesNotBecomeBinary16 for the mixed-expression case from Task 7 (16-bit evaluation yields 0), cancellation, subnormal transitions, rounding ties, division/square-root boundaries, quaternion normalization and long-running clock separation. Exact arithmetic must agree; transcendental functions need a documented tested error bound, never an unexamined host/native precision difference.
- [x] Run the managed and native fixtures; expect unsupported 16/16 or numeric mismatches before implementing this backend.
- [x] Implement explicit software binary16 operations/math rounding and prove semantics on MIPS. Keep unsupported storage/arithmetic combinations rejected until they have equivalent tests and backend support; accepting the two numeric enums does not advertise every Cartesian combination.
- [ ] Build/run PS1 16/16 and repeat the same CPU/RAM/scene observations. Run the targeted suites once more after the final changes. Review the full diff for primitive ABI leaks, generated-file edits, hidden double promotion, stale policy fingerprints and changes from other sessions. Use a reviewer agent only if Helena selected delegation; otherwise review inline and report that boundary.
- [ ] Commit verified changes, publish a support/measurement table and clean only task-owned disposable probes/logs. Keep the running emulator's mounted disc and shared caches intact until they are released; never recursively delete shared C:\temp\helengine state. Push only when requested.

## Execution handoff

Recommended method: inline execution in this session. These tasks are coupled by scalar semantics, serialized formats and ABI identity, so sequential ownership avoids simultaneous changes to shared contracts. Delegation remains an explicit alternative if Helena requests it; subagents must use gpt-6.1-sol with high reasoning and the same real checkouts.

Before production edits, Helena reviews this plan and selects/confirms the execution method. Converter master usage is approved. Ownership/integration authorization for existing viewport/backend/project edits has been received; preserve those changes throughout execution.

## Current validation boundary

Windows 32/32 and 64/64 passed all 15 scenes and menu Tracy captures earlier in execution. The latest final-source 64/64 rerun passed all 15 scenes again, but the subsequent 1,800-frame Tracy recapture timed out at 80 seconds. It is recorded as a failed attempt, not a replacement passing capture. Helena confirmed the Windows right alignment after reopening the menu.

PS1 16/32 runs the menu and its instrumented guest self-test passes all 65,536 binary16 encodings, 40 independent arithmetic cases and quaternion normalization. Six steady 600-frame windows show update 2.786 ms and draw 22.7845 ms, slower than the 32/32 baseline (2.2745 / 19.147 ms). Cooked bytes decrease from 471,926 to 439,560. Profiled static RAM increases from 1,370,112 to 1,421,312 bytes, and heap high-water decreases from 537,624 to 510,088 bytes. Live retained heap has not been measured. PS1 rendering-scene/visual validation remains pending.

PS1 16/16 is a local candidate, with 134 builder tests and actual MIPS compilation of its profiler/self-test passing. The complete official retail game build succeeded at 2026-10-20T13:20:24Z; it has 117 policy-checked numeric assets, nine modules, a 1,910,784-byte disc, 439,560 bytes of cooked content and 528,384 bytes of heap capacity. Its profiler build also succeeded. After explicit launch authorization, the guest numeric self-test passed, but the profiler image failed a 1,536-byte allocation while loading the menu. The retail image runs the menu without render failures and reaches 496,480 bytes of heap high-water, identical to 16/32 retail. The profiled heap capacity is only 505,856 bytes. Actual MIPS sizeof reports a 13,504-byte sink containing a 12,392-byte window with 128 component slots. Configurable capacity (default 128, 32 for this menu, saving 6,912 bytes) versus binary16 code compaction is awaiting required architectural feedback. No profiler capacity or codec change has been made. Managed numeric tests pass 52/52 in each of 16/16, 16/32, 32/32 and 64/64. Two former float-only test inputs were corrected to use the corresponding binary16 precision boundary; runtime semantics and tolerances were not changed.

Helena revoked UI automation: never send keyboard/mouse input, change focus or capture screens. No UI input or capture is automated. Helena explicitly requested emulator MCP launch for the prepared 16/16 image; the failing profiler PID33028 was closed after recording its failure, and the retail image is open as PID41188. Remaining PS1 scene/visual checks require manual navigation. The 16/16 arithmetic implementation has now executed successfully on the guest; full component profiling is blocked on the memory decision above. Candidate 16/16 capability is not committed as target-execution-verified support.

Verified PS1 16/32 support is committed as `e329190`; the exact local 16/16 selection/test delta was restored after the commit. The precision-specific vector/GPU tests are committed as `a3d4f03c`.
