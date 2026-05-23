# CSharpCodegen External Feature Metadata Design

## Goal

Remove all `helengine` knowledge from `csharpcodegen` so the converter becomes a pure generic C#-to-C++ tool that consumes externally published feature metadata instead of hardcoded engine feature ids, type roots, and runtime ownership rules.

## Current Problem

`csharpcodegen` currently contains compiled-in product knowledge:

- built-in feature ids such as shaders, sprites, text, and host file access
- hardcoded root type mappings such as `helengine.*`
- hardcoded runtime requirement ownership metadata
- generated config/report behavior tied to built-in feature enums

That violates the required boundary:

- `csharpcodegen` must be generic
- `helengine` must publish its own feature meanings
- other users of `csharpcodegen` must be able to supply different feature sets or none at all

## Design

### Ownership Boundary

`csharpcodegen` will own only generic feature mechanics:

- loading external feature metadata
- validating metadata
- scanning Roslyn symbols against external root rules
- resolving enabled and disabled features from external definitions
- pruning generic runtime requirements from external ownership metadata
- emitting generic feature reports and config defines from external ids

`csharpcodegen` must not own:

- built-in feature ids
- built-in feature enums that imply one product vocabulary
- hardcoded `helengine.*` root types
- hardcoded runtime requirement ownership for one engine

`helengine` will own:

- its checked-in feature metadata files
- its feature ids
- its root-type detection rules
- its runtime requirement ownership metadata
- its default feature modes and conflict policy

### Manifest Model

The external contract will use free-form string ids.

The metadata needs to describe:

- feature definitions
- type-root detection rules
- runtime requirement ownership
- optional default feature mode
- optional conflict policy

The ids remain strings at the manifest boundary, for example:

- `shaders`
- `render2d`
- `debug_overlay`
- `host_file_system`

Inside `csharpcodegen`, those become ordinary loaded objects, not enums compiled into the tool.

### Data Flow

The feature flow becomes:

1. `helengine` stores checked-in feature metadata files.
2. `helengine.editor` passes those metadata files into `csharpcodegen` during generated-core/codegen execution.
3. `csharpcodegen` loads the external metadata and scans symbols against the external root rules.
4. `csharpcodegen` resolves feature decisions from external definitions and emits:
   - `cpp-conversion-report.json`
   - generated config defines such as `HE_CPP_FEATURE_*`
   - runtime-pruning results
5. `helengine` consumes those outputs exactly as caller-owned metadata results, not as meanings compiled into the converter.

### Config Define Generation

Generated C++ config defines must be derived generically from free-form string ids.

For example:

- feature id `debug_overlay`
- generated define `HE_CPP_FEATURE_DEBUG_OVERLAY`

This must be a pure string sanitization rule in `csharpcodegen`, not a lookup through a built-in enum.

### Runtime Requirement Ownership

Runtime support pruning must become metadata-driven too.

That means requirement ownership such as regex, file system helpers, text readers, or shader-only helpers must no longer be compiled into `csharpcodegen`. Instead, external metadata will declare which requirement ids belong to which feature ids.

If a caller publishes no feature metadata, `csharpcodegen` should still work in a featureless mode, but it must not silently fall back to built-in product assumptions.

## Approach Options

### Option 1: External Metadata With Temporary Compatibility Path

Add the external metadata path first, switch `helengine` to use it, then delete the old built-in feature path after the new path is verified.

Pros:

- safer migration
- lets Windows and generated-core builds stay working while the contract flips
- enables focused verification before final deletion

Cons:

- temporary duplication during migration

This is the recommended approach.

### Option 2: One-Step Hard Cut

Delete all built-in feature logic from `csharpcodegen` immediately and move `helengine` to external metadata in the same pass.

Pros:

- shortest total lifetime for the bad boundary

Cons:

- harder to debug if anything regresses
- higher chance of stranding builds mid-migration

This is not recommended.

### Option 3: Remove Feature Detection Entirely

Make callers pass all enabled features explicitly and remove auto-detection from `csharpcodegen`.

Pros:

- very clean converter boundary

Cons:

- loses useful automatic pruning and reporting
- pushes more work into every caller

This is not recommended.

## Error Handling

`csharpcodegen` must fail hard on invalid external metadata.

Validation must catch:

- duplicate feature ids
- empty or invalid ids
- unknown feature references from root rules
- unknown feature references from runtime requirement ownership
- invalid mode or conflict-policy values
- malformed manifest files

Rules:

- if metadata paths are explicitly provided and invalid, fail
- if no feature metadata is provided, run with no external features declared
- never fall back to built-in product assumptions

## Testing Strategy

### CSharpCodegen Tests

Generic converter tests should cover:

- manifest parsing and validation
- generic root-type detection from fixture manifests
- generic report generation from string ids
- generic config define generation from string ids
- generic runtime-requirement pruning from fixture manifests

Those tests must stop referencing `helengine` types as built-in truths and instead supply fixture metadata explicitly.

### Helengine Tests

`helengine` should cover:

- integrity of checked-in feature metadata
- generated-core/codegen invocation passes metadata paths correctly
- Windows generated-core output still detects shader/runtime features from external metadata

## Risks

### Compatibility Surface Is Larger Than One Type Map

The current built-in feature system is used by:

- feature scanning
- feature profiles
- conversion reports
- generated config headers
- runtime requirement pruning

The migration must update all of them consistently or the new external path will partially work and partially drift.

### Existing Tests Encode Engine Vocabulary

Many `csharpcodegen` tests currently assume feature enums and `helengine` roots. Those tests must be rewritten around fixture metadata instead of renamed wrappers around the old coupling.

### Temporary Migration Layer Can Linger

If the compatibility path is introduced but never removed, the boundary stays polluted. The plan must explicitly delete the built-in feature path after `helengine` is proven on the external metadata contract.

## Success Criteria

This change is complete when:

- `csharpcodegen` contains no hardcoded `helengine` feature ids
- `csharpcodegen` contains no hardcoded `helengine.*` root-type mappings
- `csharpcodegen` contains no built-in feature enum that implies one product vocabulary
- feature scanning, config generation, conversion reports, and runtime pruning all work from external metadata
- `helengine` publishes its own feature metadata and passes it into codegen
- Windows generated-core output still detects shader features correctly through the external metadata path
