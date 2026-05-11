# Remove All Legacy Design

## Summary

`helengine` currently carries multiple classes of legacy behavior: old asset and scene payload readers, editor migration and packaging fallbacks, repo tooling that rewrites older data forward, and tests and fixtures that preserve those paths. The new policy is strict: the current format is the only supported format. There will be no migration tool, no fallback loader, no legacy packaging branch, and no compatibility-only tests.

This design removes every legacy implementation path repo-wide and allows old payloads, old local settings files, and old editor-authored scene records to fail immediately.

## Goals

- Remove runtime support for legacy asset, scene, and component payload formats.
- Remove editor support for legacy persistence, packaging, migration, and rewrite paths.
- Remove tooling that exists only to normalize or rewrite older authored data.
- Remove tests, fixtures, and docs that preserve or describe legacy compatibility behavior.
- Keep only the current format as the single authoritative format across runtime, editor, and build systems.

## Non-Goals

- Keep backward readability for any old payload version.
- Add a one-time normalization tool.
- Preserve old project-local settings files, old scene payload layouts, or old asset payload layouts.
- Preserve legacy behavior behind feature flags or opt-in switches.
- Clean up every historical mention of the word "legacy" when it does not represent executable compatibility logic.

## Current Problem

The repository currently mixes current-format code with compatibility code for older layouts. That creates four persistent problems:

- core runtime code accepts multiple payload versions instead of one authoritative format
- editor packaging and persistence code must carry tolerant readers and compatibility mirrors
- tools and project services keep migration behavior alive instead of forcing the repository onto one format
- tests and fixtures actively lock those old paths in place

This complexity is not paying for itself. The current format already exists and is already the format the active codebase authors. Keeping the old paths only expands maintenance cost and obscures the real contract.

## Chosen Approach

Use a staged repo-wide deletion strategy in a single worktree and branch.

Each stage will remove one subsystem's compatibility code completely, then recompile and rerun current-format verification before moving to the next stage. This is intentionally not a big-bang delete-everything-first pass, because the compatibility code spans serialization, editor persistence, packaging, tools, and tests. The staged approach gives a narrower failure surface while still honoring the "no migration, no fallback" policy.

## Rejected Approaches

### Big-Bang Repo-Wide Removal

Deleting every legacy path in one shot is possible, but it creates a very poor debugging surface. The likely outcome is a large number of unrelated compile and behavior breaks with no clear subsystem boundary.

### Normalize Then Delete

This is explicitly forbidden by policy. It still preserves the old formats long enough to add more code around them, which is the opposite of the desired direction.

### Keep Readers But Delete Writers

This still leaves compatibility branches in the codebase and continues to define old formats as supported. That violates the policy that the current format is the only format.

## Design

### 1. Core Asset And Scene Serialization

Core serialization and runtime scene loading will only recognize the current payload versions.

That means:

- remove old-version branches from `EditorAssetBinarySerializer`
- remove legacy scene entity readers
- remove version-aware fallback branches from mesh and light payload serializers when those branches exist only for older layouts
- make runtime component deserializers reject non-current payload versions immediately

After this stage, any old serialized payload becomes invalid input.

### 2. Editor Persistence, Packaging, And Project Services

Editor code will stop compensating for older authored data.

That means:

- remove legacy binary scene payload readers from packaging transforms
- remove legacy single-material and old light payload fallback handling from editor persistence
- remove migration of old project-local settings or old scene identifiers
- remove compatibility mirroring paths that only exist to seed or preserve old raw material fields

After this stage, editor save, load, and package flows operate only on the current authoring contract.

### 3. Tooling And Generated Rewrite Utilities

Standalone tools and repo utilities that only exist to convert older data will be deleted.

That includes:

- scene migrators
- tooling that rewrites old scene payload layouts
- cleanup helpers whose only purpose is to delete or replace older authored artifacts during regeneration

After this stage, repository tooling only emits the current format and never tries to recognize or transform earlier ones.

### 4. Tests, Fixtures, And Documentation

Tests and fixtures that protect old behavior will be removed, not rewritten to preserve old semantics.

That means:

- delete legacy compatibility tests
- delete helpers that write old payload versions as test input
- remove or replace fixtures that exist only to validate migration or fallback behavior
- update docs and specs so they no longer claim backward compatibility where the code no longer provides it

The remaining test suite should validate only the current format and current editor/runtime paths.

### 5. Naming Cleanup

Once executable compatibility code is gone, perform a final naming sweep on touched files.

This sweep should:

- rename fields, constants, and comments whose only purpose was to describe removed compatibility behavior
- leave unrelated uses of words like "compatibility" or "fallback" alone when they describe real current-platform behavior rather than old-format support

This avoids turning the project into a cosmetic rename exercise instead of a format-contract cleanup.

## Deletion Policy

When a legacy branch is removed, replacement behavior should be one of two things only:

- use the current format path
- reject the input as unsupported

There should be no tolerant middle ground such as silent defaulting, field synthesis, compatibility mirrors, or opportunistic migration.

## Verification Strategy

Verification will be current-format only.

The branch should continue to:

- build the runtime, editor, and tools that remain in use
- pass current-format tests
- fail compilation if any deleted compatibility path is still referenced

Legacy-specific tests are expected to disappear as part of the work. They should not be replaced with new compatibility assertions.

## Risks

### Old Local Data Stops Loading

This is intentional. Old `.helen`, asset payloads, local settings files, or older generated artifacts may stop loading immediately after the relevant stage lands.

### Current-Format Code May Still Accidentally Depend On Compatibility Helpers

Some current paths may still call shared utilities that were introduced for old-format tolerance. The staged approach is designed to expose and remove those dependencies one subsystem at a time.

### Tooling And Tests May Reference Removed Formats Indirectly

A significant part of the work is deleting test helpers, fixtures, and tooling assumptions that no longer match the new contract. This is expected churn, not a sign that the policy is wrong.

## Expected Outcome

At the end of this work, `helengine` will have one format contract everywhere:

- one current asset format
- one current scene format
- one current editor persistence path
- one current packaging path
- zero migration code
- zero legacy compatibility tests

Any older data is unsupported by design.
