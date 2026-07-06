# Generic Codegen Feature Overrides

## Summary

HelEngine should stop expressing platform runtime feature policy through platform-named codegen presets such as `ds-lite`, `ps2-lite`, and `native-core-boot`. The generic seam already exists at the platform-definition and editor-generated-core layers; the missing piece is a generic way for a platform codegen profile to declare feature overrides that codegen consumes directly.

This design adds a generic codegen setting for forced-disabled runtime features, routes that setting through the editor and CLI option builder, and makes named presets compatibility aliases that resolve into the same generic mechanism instead of carrying platform-specific policy branches inside codegen.

## Problem

Current behavior mixes two concerns:

1. Codegen owns named presets with platform-specific policy such as `ps2-lite`, `n64-minimal`, and `native-core-boot`.
2. Platform builders select those named presets to get feature pruning behavior.

That shape is wrong for HelEngine:

- Codegen should not need to know which platforms want `debug_overlay` disabled.
- Platform builders should provide generic policy values and let codegen apply them.
- Existing direct CLI preset users still need a compatible migration path.

The recent DS size work exposed the problem directly:

- a DS-named preset was added just to force-disable `debug_overlay`
- preset application overwrote caller-provided preprocessor symbols
- that symbol loss broke the cooked-material seam in generated `RenderManager3D`

The symbol merge bug is already fixed generically, but the policy path is still too platform-aware.

## Goals

- Make runtime feature pruning policy generic and platform-supplied.
- Keep codegen ignorant of DS, PS2, Wii, GameCube, and similar platform policy decisions.
- Preserve existing CLI preset identifiers as compatibility aliases for now.
- Ensure preset aliases and platform profile settings both flow through one canonical feature-override builder.
- Keep current generated output behavior for existing presets after migration.

## Non-Goals

- Remove all preset identifiers immediately.
- Introduce generic force-enable support in the same pass.
- Redesign the external feature catalog format.
- Change runtime feature ownership rules beyond what is needed for this migration.

## Chosen Design

### Canonical Policy Input

Add one generic codegen option:

- setting id: `codegen-forced-disabled-features`
- serialized form: delimiter-separated feature ids, using the same accepted delimiters as existing string list options (`;`, `,`, or whitespace after normalization)

This option is the canonical source of feature-pruning policy for this pass.

Examples:

- `debug_overlay`
- `debug_overlay;shaders`
- `shaders;text2d;render2d`

### Why Only Forced-Disabled Features

All current preset policies in scope are disable-only:

- `windows-no-shaders`
- `ps2-lite`
- `native-core-boot`
- `n64-minimal`
- DS `debug_overlay` disable

Adding a symmetric force-enable setting now would be speculative. If HelEngine later needs that capability, it can be added as a separate generic option without changing the disabled-feature path.

## Architecture

### 1. HelEngine Base Platform Metadata

Add a stable codegen setting id in `PlatformCodegenSettingIds`:

- `ForcedDisabledFeatures = "codegen-forced-disabled-features"`

Platform codegen profiles use this setting just like any other generic codegen option. HelEngine platform definitions become the owners of feature-pruning policy.

Examples after migration:

- DS default profile sets `codegen-forced-disabled-features=debug_overlay`
- PS2 preset-facing definitions set `codegen-forced-disabled-features=shaders;debug_overlay`
- N64 minimal policy sets `codegen-forced-disabled-features=shaders;debug_overlay;render2d;text2d`

### 2. Editor Generated-Core Regeneration

`EditorGeneratedCoreRegenerationService.BuildArguments` already forwards generic codegen settings through `--set key=value`.

No platform-specific branch should be added here.

Required behavior:

- when a platform profile exposes `codegen-forced-disabled-features`, the editor forwards it unchanged
- when a preset alias is selected, the resolved generic disabled-feature option is forwarded the same way

### 3. Codegen CLI Option Builder

`CodegenCliOptionsBuilder` becomes responsible for building `CPPBuildFeatureProfile` from generic selected options.

New logic:

1. Start from `CPPBuildFeatureProfile.CreateDefault()`
2. Parse `codegen-forced-disabled-features`
3. Apply `WithMode(featureId, CPPFeatureMode.Disabled)` for each parsed feature id
4. Preserve any existing preset-driven disabled features by having preset resolution feed the same generic option rather than mutating `BuildFeatureProfile` directly

This makes generic option parsing the single canonical feature-policy builder.

### 4. Codegen Preset Catalog

Named presets remain as compatibility aliases, but they stop being the canonical policy implementation.

Instead of directly constructing feature-disabled profiles inside `CPPConversionPresetCatalog`, each preset resolves to generic option values and common profile defaults.

Canonical rule:

- preset aliases may still choose compiler/platform/runtime/restriction defaults
- feature pruning must be expressed through the same generic disabled-feature option consumed by `CodegenCliOptionsBuilder`

This keeps direct CLI users working:

- `--preset ps2-lite`
- `--preset native-core-boot`
- `--preset windows-no-shaders`

But the preset implementation becomes generic-policy-backed rather than platform-policy-backed.

### 5. Additional Preprocessor Symbols

The recent symbol-merge fix stays.

Invariant:

- preset application must never erase caller-provided `AdditionalPreprocessorSymbols`

That behavior is required independently of feature override migration because platform-owned seams such as cooked material resolution rely on those symbols.

## Data Flow

### Platform-Selected Generic Policy

1. Platform definition exposes `codegen-forced-disabled-features`
2. Editor resolves effective codegen option values
3. Editor forwards `--set codegen-forced-disabled-features=...`
4. CLI option builder parses the setting
5. CLI option builder builds `CPPBuildFeatureProfile` from parsed feature ids
6. Converter writes feature defines and prunes generated output accordingly

### Preset Compatibility Path

1. Caller passes `--preset native-core-boot`
2. Preset catalog resolves compiler/platform/runtime/restriction defaults
3. Preset catalog also contributes generic option defaults including `codegen-forced-disabled-features`
4. CLI option builder parses those generic option values the same way it parses platform-supplied ones
5. Generated feature policy matches current behavior

## Migration Plan

### Phase 1

Introduce the generic disabled-feature setting and parser without removing presets.

### Phase 2

Convert preset implementations to express feature policy through the generic disabled-feature option instead of direct `BuildFeatureProfile` mutation.

### Phase 3

Convert platform definitions that currently rely on named presets for feature policy to use the generic option directly where appropriate.

For this pass:

- DS default codegen profile stops using `ds-lite`
- DS default codegen profile sets `codegen-forced-disabled-features=debug_overlay`
- existing preset aliases remain for compatibility

### Phase 4

Update tests to assert canonical generic behavior first and preset alias behavior second.

## Implementation Notes

### Parsing

Feature-id parsing should be strict enough to reject blank entries but not so strict that it invents platform branches.

Rules:

- ignore empty tokens after split/trim
- preserve first occurrence order
- de-duplicate repeated feature ids

### Error Handling

Unknown feature ids should fail fast during codegen option processing or feature-profile application. Silent acceptance would hide platform-configuration mistakes.

### Restrictions

Restriction profiles stay separate from feature overrides.

Reason:

- feature overrides control emitted code and feature defines
- restriction profiles validate disallowed systems and fail unsupported builds early

The migration should not collapse those two responsibilities together.

## Testing

### HelEngine Tests

- `PlatformCodegenSettingIds` exposes the new generic setting id
- DS platform definition uses the generic disabled-feature setting instead of `codegen-preset-id`
- editor build-argument tests confirm the generic setting is forwarded through `--set`

### Codegen Tests

- CLI options builder parses `codegen-forced-disabled-features` into `CPPBuildFeatureProfile`
- preset alias tests confirm `windows-no-shaders`, `ps2-lite`, `native-core-boot`, and `n64-minimal` still produce the same disabled feature modes
- feature-pruning end-to-end tests confirm generic disabled-feature settings remove the same generated files as before
- preset application tests confirm caller-provided `AdditionalPreprocessorSymbols` remain intact

### Integration Tests

- DS generated-core regeneration reports `debug_overlay` as `ForcedDisabled` without selecting a DS-specific preset
- DS full build still succeeds
- PS2 and native-core-boot targeted tests still observe the same feature disable outcomes

## Risks

### Preset Compatibility Drift

If preset aliases stop matching prior outputs, direct CLI users could get subtle behavior changes. Tests must compare feature outcomes, not only preset ids.

### Generic Option / Restriction Mismatch

If a preset alias contributes restriction defaults but the generic disabled-feature option is wrong, builds may pass validation while emitting the wrong surface. End-to-end output tests are required.

### Over-Migration

Removing presets entirely in this pass would create unnecessary churn. Keeping compatibility aliases while changing the canonical policy seam is the lower-risk migration.

## Decision

Adopt a generic `codegen-forced-disabled-features` setting as the canonical feature-pruning seam, preserve preset ids as compatibility aliases backed by that same generic mechanism, and migrate DS plus existing preset-owned pruning behavior onto the generic path in this pass.
