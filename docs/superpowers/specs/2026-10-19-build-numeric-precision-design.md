# Build numeric precision

## Status and intent

Proposed implementation contract for Helena's approved direction: numeric precision belongs to a build configuration, independently of its platform. The same project must support Windows Float and Windows Double builds and an experimental PS1 16-bit build. Storage and arithmetic precision are independently selected. This document is for architectural review before implementation.

The immediate motivation is the measured PS1 menu AnimationPlayerComponent cost of approximately 1.152 ms per update. Source inspection found explicit double intermediates in float3.Lerp, float4.Lerp and animation time wrapping. That observation identifies candidate costs; it does not measure the contribution of each operation or establish an FP16 speed improvement.

## Existing integration points

- EditorBuildProfileSettingsDocument persists selected build profile and option values.
- PlatformBuildProfileDefinition binds builder profiles to graphics/codegen profiles and option defaults.
- PlatformBuildRequest carries resolved build/codegen options, target variants, cook profiles and cache roots.
- EngineBinaryReader/Writer currently expose fixed-width single/double operations; ReadFloat2/3/4 and their writers currently use 32-bit components.
- Native backends, generated Core, scene modules and shader/material layouts share contracts that cannot change independently.

Extend these existing build/profile paths. Do not introduce a parallel platform-only precision setting or a separate build launcher.

## Build policy

The proposed normalized policy contains StorageBits and ArithmeticBits. Both accept 16, 32 or 64. StorageBits controls the engine-configurable numeric values retained in runtime objects and precision-aware cooked data. ArithmeticBits controls evaluation of their mathematical operations and intermediates. A 16/32 build stores configurable values in binary16 and evaluates them in binary32; it is not advertised as 16-bit arithmetic.

Every resolved build has an explicit policy and policy-version identity. Builder capability declarations determine which combinations are supported. The selected build value takes precedence over a platform-provided default. The proposed default is 32/32 for a build without an explicit selection; the resolved manifest always records the actual values. Migrating the current mixed float/double implementation to configurable 32-bit arithmetic changes its intermediate rounding and must invalidate old artifacts and pass the numerical validation below. Loading a project does not rewrite its authored assets.

Windows Float and Windows Double are independent selections for the same target, using 32/32 and 64/64 respectively. Neither selection changes a global engine installation or another running build's policy. The current fixed output per platform is overwritten when switching configurations; no output history or worktrees are created.

## Configurable and fixed numeric contracts

Introduce one explicitly identifiable engine scalar, Real, with engine math operations that honor the arithmetic policy. Use it in engine-configurable vectors, matrices, quaternions, transforms and animation values. Preserve the current float2/3/4/4x4 API names during migration; document that their engine component precision follows the build policy. Their names do not authorize rewriting unrelated System.Single values.

Real must remain semantically identifiable to the managed build, authoring/cook pipeline and converter. Native lowering belongs in the converter's type/math emission, not generated-file patches, textual substitutions, global float/double macros or blanket remapping of all C# primitives. The managed implementation and emitted native representation must implement the same selected numeric semantics.

Storage and evaluation are distinct representations: operands expand to ArithmeticBits, compound-expression intermediates retain that precision, and values round to StorageBits when committed to configurable stored destinations. A wrapper that narrows to StorageBits after every operator does not implement the mixed 16/32 contract. Apply the same boundary rules in managed execution and native lowering.

Fixed IEEE single/double primitives remain available for explicitly fixed contracts. Examples include external SDK calls, specified GPU vertex/uniform formats and intentionally wide clocks. These are converted at named boundaries and are not silently widened or narrowed by the build setting. Audit and document exceptions in the migrated paths.

For configurable math, interpolation, literals, modulo, square root, trigonometry and normalization must use the configured evaluation path; intermediate promotion to double must not bypass ArithmeticBits. Define binary16/32/64 semantics explicitly, including round-to-nearest ties-to-even at storage conversion. A 16-bit arithmetic policy rounds observable primitive operation results to binary16; an implementation that evaluates in wider precision without the specified rounding is not equivalent. Do not enable fast-math as an implicit part of this feature.

Authoring values already stored at 32-bit precision do not gain lost information when a 64-bit build reads them. New precision-aware authoring must preserve the intended source precision and must not rewrite existing assets merely because a developer selected another build.

## Cook, serialization and native ABI

Keep fixed-width ReadSingle/ReadDouble and corresponding writers truthful. Add explicit precision-aware scalar/vector encoding for configurable values, with policy/version metadata in their containing cooked payload. Preserve endianness handling. Do not reinterpret old 32-bit bytes as half or double.

Cook evaluates layout and transforms using the selected arithmetic contract and encodes their configurable fields using StorageBits. GPU/material/texture fields whose formats are separately specified retain those formats and use explicit conversion boundaries.

The resolved policy and representation version participate in generated-Core identity, cooked asset keys, graph dependencies, native object keys, module ABI/build IDs and package metadata. Core, scene modules and runtime content must reject incompatible identities before consuming precision-dependent structures. Match structure size/alignment in managed/native bridge tests. Cached artifacts from a different policy must not be reused.

## Platform capabilities and PS1 experiments

The installed PS1 MIPS compiler uses soft-float, reports 32-bit float and 64-bit double, and does not advertise an FP16 type in its target macros. Do not assume a native half arithmetic path exists or that reduced storage accelerates execution.

Implement and measure the supported policies in explicit stages:

1. Windows 32/32 and 64/64 through the complete build path, plus a PS1 32/32 baseline that eliminates unintended double intermediates in configurable math.
2. PS1 16/32 storage conversion, recording runtime RAM, disc size, CPU cost and visible/numeric error.
3. An explicit portable or supported native 16-bit arithmetic implementation before enabling PS1 16/16. Unsupported selections fail with a precise diagnostic; they never fall back silently.

A fixed-point math backend is a separate architectural proposal, not an alternative encoding silently substituted for binary16. This design does not approve its implementation.

## Validation and acceptance

- Resolve and persist two precision selections for Windows without changing global platform defaults.
- Verify type layouts and conversions for signed zero, representative normal/subnormal values, overflow, infinity and NaN classification; test exact rounding boundaries and endianness.
- Confirm configurable math intermediates match the policy in generated C++, including interpolation, modulo and normalization. Test explicit fixed-format interop separately.
- Build Windows 32/32 and 64/64 from the real main checkouts; run the same menu and rendering scenes, reporting crashes and numeric/visual deviations.
- Switch policies and prove cooked content, generated Core, modules and native objects invalidate correctly. Deliberately mismatched content/modules must fail before use.
- Repeat the PS1 menu CPU capture with the same scene/content and comparable 600-frame steady windows. Report component and native renderer costs separately, measurement quantum, warmup resets and instrumentation overhead limits.
- For the 16-bit experiments, include precision-loss cases such as adjacent integer coordinates above 2048 and long-running time accumulation. Performance and RAM improvements require measured evidence.
- Keep all scratch scripts, captures, logs and compiler intermediates in C:\temp. Final platform outputs retain their existing fixed locations.

## Repository instruction correction

C:\dev\helworks\helengine\AGENTS.md currently says: "Avoid Mathf; use double math and convert back to float where needed (modern processors handle double faster in this context)." Replace that unconditional instruction as part of implementation with a requirement to honor the selected build arithmetic policy, preserve explicitly fixed numeric contracts and substantiate performance claims with target measurements.

## Review boundary

This document proposes the Real type, migration boundary, build-policy propagation and serialization/ABI rules for review. No numeric runtime behavior or public type layout has been changed. After architectural approval, create a dependency-ordered implementation plan before production edits.