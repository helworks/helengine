# Build Platform Completion Hardening Design

## Purpose

Close the four remaining review findings in the direct-source build-platform rework and restore practical Windows native-build path headroom. This amendment preserves deterministic cache reuse and direct source builds without reintroducing repository copies or invocation-scoped cache directories.

## Input Safety

The wrapper canonicalizes the project, output, cache, and editor project paths before creating directories, writing state, acquiring locks, publishing the editor, or launching it.

`-AdditionalArgs` must reject case-insensitive exact and inline-value forms of every wrapper-owned editor switch: `--project`, `--build`, `--build-profile`, and `--output`. This keeps locking, cache selection, state metadata, and the editor's effective arguments tied to the same canonical invocation.

The final output and the selected project's cache tree must be disjoint. Equality and either ancestor relationship are rejected with a configuration error using canonical, case-insensitive, directory-boundary-aware comparisons. This prevents normal editor workspace cleanup from deleting output and prevents output publication from modifying cache internals.

## Compact Cache Identity

The cache layout remains deterministic and versioned, uses full 128-bit hexadecimal path hashes, and shortens internal path segments enough to preserve CMake/MSVC object-path headroom. The layout retains separate project, editor, platform, configuration, and profile identities.

Editor artifacts and publish output include a hash of the canonical editor `.csproj` path. Two engine worktrees building the same authored project therefore cannot reuse one another's timestamp-based editor outputs, while repeated builds from one engine checkout still reuse them.

Changing the layout version intentionally prevents old and new layouts from being mixed. Existing cache maintenance remains responsible for deleting obsolete entries; the wrapper does not perform an implicit broad migration or deletion.

## Serialization and State Identity

After validation, the wrapper acquires locks in this fixed order under one shared timeout budget:

1. canonical-project global mutex;
2. canonical-output global mutex;
3. cache-local project file lock.

The output mutex uses a distinct versioned name and a full canonical-path hash. Locks release in reverse order on every exit. Same-project builds remain serialized across cache roots, while different projects overlap only when their final outputs differ.

`build-waiter` generates a unique invocation ID before launching its child and supplies it through a dedicated environment variable. The wrapper validates and uses that value as the state document's `buildId`; direct wrapper invocations without the variable generate their own ID. The waiter requires an exact ID match in addition to a zero child exit, current successful state, and fresh required artifacts. A lock timeout or another invocation's state can therefore never satisfy the waiter.

## Failure Behavior

Invalid additional arguments or path overlap fail before filesystem mutation. Lock acquisition failures preserve existing output and state. Once running state has been written, terminal wrapper failures continue to record failed state without replacing the native process exit code. Environment and locks are restored through the existing `finally` path.

## Testing

Each behavior is implemented test-first and observed failing for the intended reason:

- output/cache equality and both ancestor directions;
- reserved additional switches in separated and inline forms;
- different projects targeting one output serialize across processes;
- waiter rejects a successful but foreign invocation state;
- different editor checkout paths resolve to different editor caches while repeat calls remain stable;
- compact layout containment and segment validation;
- a real CMake/MSVC build through the stable wrapper cache, run as an explicit Windows native smoke test.

After targeted tests pass, verification reruns the workspace, profile, profile-behavior, streaming, locking, and maintenance PowerShell suites; real editor smoke; build-waiter tests; focused editor tests; scope scans; `git diff --check`; and an independent whole-branch review. The three existing point-shadow native tests may retain their documented 254-character temporary-path environment failure, but the new stable-cache native smoke must pass.

## Non-goals

- Reintroducing repository cloning or per-invocation cache trees.
- Atomic staging and promotion of complete platform outputs.
- Automatic deletion or migration of earlier cache-layout versions.
- Broad changes to editor native test temporary-directory policy.
