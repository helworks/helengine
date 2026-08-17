# Build Platform Safety Hardening Design

## Context

The direct-source build wrapper currently serializes through a lock stored beneath the selected cache root. Two invocations of the same authored project can therefore bypass serialization by selecting different cache roots. Maintenance also validates that deletion targets remain beneath cache-owned roots, but it does not reject a cache target that contains the current authored project or requested output.

This amendment closes both gaps without restoring project copies, GUID-scoped workspaces, or redundant repository writes.

## Project-wide serialization

Each wrapper invocation acquires two locks in this order:

1. A cache-independent named operating-system mutex keyed only by the hash of the canonical authored-project root.
2. The existing cache-local file lock keyed by that same project hash.

The named mutex serializes source mutations even when callers select different cache roots. The file lock remains necessary because prune must detect and exclude an active cache entry, and it retains the existing human-readable owner metadata.

All builds acquire locks in the same global-then-local order and release them in reverse order from nested `finally` blocks. A timeout while waiting for either lock preserves the wrapper's existing failure behavior and releases any lock already acquired. An abandoned named mutex is treated as successfully acquired because the previous owner no longer exists.

The named mutex is `Global\helengine.build-platform.project.v1.<project-hash>`. It contains no source path text and creates no additional disk data. One shared timeout budget covers both acquisitions: time spent waiting for the mutex is subtracted before the cache-local lock wait begins, so `-LockTimeout` remains the maximum total wait.

## Protected maintenance paths

The wrapper passes two canonical protected roots into selected clean and age-based prune:

- the authored-project root;
- the requested output root.

Before every recursive deletion, the shared guarded-delete path validates that the target is neither equal to nor an ancestor of either protected root. Comparisons use canonical full paths, Windows case-insensitive comparison, and directory-boundary-aware prefixes so sibling names cannot collide.

The protected-path check runs during initial all-target validation and again during the immediate pre-delete revalidation. Prune performs the final check while holding the candidate cache lock. An overlap fails closed: clean reports an error, while prune skips the unsafe candidate with its existing warning behavior. No protected path is deleted, whether or not it exists yet.

## Testing

Cross-process locking coverage will start two builds of the same authored project with different cache roots and prove the second cannot enter source-mutating work until the first exits. A different-project control will continue to prove overlap is allowed.

Maintenance coverage will construct disposable layouts where:

- requested output is nested beneath a selected clean target;
- the current authored source or requested output is nested beneath an expired prune candidate;
- similarly named sibling paths remain deletable;
- all protected sentinels survive and locks are released.

The locking, maintenance, workspace, profile, profile-behavior, streaming, waiter, locator, and real-editor smoke regressions remain required. The three known committed-point-shadow native integrations retain their documented Windows path-length verification caveat.

## Scope

The implementation is limited to the wrapper, cache/lock modules, and their PowerShell contract tests. It does not change interactive editor isolation, waiter semantics, shared engine settings, the legacy `C:\dev\helworks\b` path, or the user's main-worktree files.
