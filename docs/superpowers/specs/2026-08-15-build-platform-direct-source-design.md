# Direct-Source Platform Build Design

## Purpose

Rework `scripts/build-platform.ps1` so canonical platform builds run against the authored project instead of copying the project into a new invocation directory. The build must minimize redundant SSD writes, retain useful incremental intermediates, serialize source-mutating builds for the same project, and write platform results directly to the requested output directory.

## Current Problem

The current wrapper creates a GUID-scoped build root for every invocation, copies nearly the entire project into that root with `robocopy /E`, publishes a complete editor host into the same invocation tree, and directs editor-generated and native build state beneath additional invocation-specific directories. None of these invocation trees are removed by the wrapper.

This behavior has three harmful consequences:

- every build rewrites a project-sized source tree;
- editor, generated-code, and native intermediates cannot be reused reliably across invocations;
- abandoned invocation roots accumulate without a bounded lifecycle.

The project copy was intended to isolate prebuild commands that mutate authored project files. That isolation is no longer desired. The accepted contract is that canonical command-line builds may update generated or authored project assets directly.

## Goals

- Build from the exact project path supplied through `-Project`.
- Remove full-project copying, mirrors, hardlinks, and per-invocation workspace roots.
- Permit prebuild commands to mutate the authored project.
- Allow only one active canonical build per authored project.
- Allow builds for different projects to run concurrently.
- Reuse stable editor, generated-code, and native intermediates.
- Write final platform artifacts directly into `-Output`.
- Preserve intermediates after failure for incremental retry and diagnosis.
- Make cache cleanup explicit, narrow, and path-safe.
- Distinguish current successful output from stale artifacts left by a previous build.

## Non-Goals

- Making same-project builds concurrent.
- Rolling back source changes made by prebuild commands.
- Providing transactional or atomic final-output publication.
- Preserving a source snapshot for reproduction.
- Automatically deleting caches after an ordinary build.
- Redesigning every platform-specific compiler or packager beyond the path contract required by this wrapper.
- Removing legitimate toolchain intermediates needed for incremental compilation.

## Selected Approach

The wrapper will become a direct-source orchestrator with stable caches.

It will:

1. resolve and validate the authored project;
2. acquire an operating-system-backed project lock;
3. resolve deterministic cache locations;
4. restore and publish the editor into the stable editor cache;
5. invoke the editor with the original project path;
6. direct generated and native intermediates into stable platform/configuration/profile caches;
7. write platform artifacts directly into the requested output root;
8. record terminal build state; and
9. release the project lock in all terminal paths.

The wrapper will not create an isolated project directory.

## Command Contract

The existing required arguments remain:

- `-Project`
- `-Platform`
- `-Output`

The existing configuration and profile arguments remain:

- `-Configuration`
- `-BuildProfile`
- `-EditorProject`
- `-AdditionalArgs`

The workspace argument changes meaning:

- Add `-CacheRoot` as the preferred explicit cache-root argument.
- Keep `-WorkspaceRoot` as a deprecated alias for `-CacheRoot` for one compatibility cycle.
- Reject an invocation that supplies both names with different values.

Add maintenance and locking arguments:

- `-LockTimeout` accepts a `TimeSpan` and defaults to two hours.
- `-Clean` removes the selected build's stable caches before continuing with the build.
- `-PruneCacheOlderThanDays` removes project-cache entries older than the supplied positive age before continuing with the build.

An ordinary invocation performs no broad cache deletion.

## Cache Root and Layout

The default cache root is:

```text
C:\dev\helworks\builds\helengine\cache
```

The wrapper derives a stable project hash from the canonical authored project root. The hash identifies paths and locks without embedding a potentially long project path.

```text
<CacheRoot>/
  locks/
    <project-hash>.lock
  projects/
    <project-hash>/
      cache-metadata.json
      editor/
        <configuration>/
          artifacts/
          publish/
      platforms/
        <platform>/
          <configuration>/
            <build-profile>/
              generated-dotnet/
              generated-core/
              native/
              build-graph/
```

The layout has no build GUID directory. Repeating the same project, platform, profile, and configuration resolves the same cache paths.

The wrapper updates `cache-metadata.json` with the canonical project root and the cache's last-used UTC timestamp. This small metadata write supports safe inspection and age-based pruning without scanning authored project contents.

## Direct-Source Contract

The wrapper passes the canonical authored `project.heproj` path to the editor. It must not call `Copy-ProjectIntoIsolatedWorkspace`, `robocopy`, or any equivalent copy mechanism.

Prebuild commands execute against the authored project and may update generated scenes, manifests, bindings, or other project-owned files. These mutations are intentional and are not reverted when the build completes or fails.

Source control remains the mechanism for reviewing or reverting authored-project changes.

## Stable Editor and Build Intermediates

Editor restore and publish paths are stable per project and configuration. A second invocation reuses the existing NuGet/MSBuild artifacts and published editor output when the underlying build inputs permit incremental reuse.

Headless editor builds need a stable-cache mode distinct from the interactive editor's invocation-isolation mode:

- introduce `HELENGINE_BUILD_CACHE_ROOT` for the headless wrapper;
- stop setting `HELENGINE_BUILD_WORKSPACE_ROOT` from `build-platform.ps1`;
- treat `HELENGINE_BUILD_WORKSPACE_ROOT` as deprecated compatibility behavior outside the canonical wrapper;
- when `HELENGINE_BUILD_CACHE_ROOT` is set, resolve generated project scripts and build-graph workspaces from deterministic project/platform/configuration/profile paths without execution GUIDs;
- retain unique execution roots for editor workflows that do not opt into stable-cache mode.

The CLI build route must use stable keys for its project-script compilation and selected queue item. The existing persisted queue-item id may separate build-graph caches within the platform/configuration/profile cache when needed, but a new random execution id must not become a directory segment in stable-cache mode.

## Project Locking

Canonical builds lock by canonical authored project root, not by platform. This is required because platform-specific prebuild commands can mutate shared project assets.

The lock file lives under `<CacheRoot>/locks/<project-hash>.lock`. The owner holds a writable handle that denies other writers but permits metadata readers for the complete restore, publish, prebuild, cook, and package sequence. The file contains metadata describing the owner process, project, platform, profile, output path, and start time.

When another build targets the same project:

- it waits rather than failing immediately;
- it periodically reports the active build and elapsed wait time;
- it exits with a clear non-zero result if `-LockTimeout` elapses;
- it acquires the lock automatically when the owning process exits or releases its handle.

A leftover metadata file is not itself a lock. Operating-system handle ownership determines whether the project is busy, so a crashed build cannot leave a permanent stale lock.

Builds whose canonical project roots differ use different lock identities and may execute concurrently.

## Execution Flow

The wrapper performs the following sequence:

1. Validate command arguments.
2. Canonicalize the editor project, authored project, output, and cache paths.
3. Derive the project hash and selected stable cache paths.
4. Acquire the project lock, waiting up to `-LockTimeout`.
5. Perform explicitly requested cache cleaning or pruning.
6. Mark the output state as `running`.
7. Restore the editor into the stable editor artifacts path.
8. Publish the editor into the stable editor publish path.
9. Set the direct-source and stable-cache environment for the child editor process.
10. Invoke the editor with the original `project.heproj`, selected platform/profile, and exact output path.
11. Mark output state as `succeeded` when the editor exits successfully.
12. Mark output state as `failed` when any build stage fails.
13. Restore inherited environment values and release the lock from the outermost `finally` block.

The lock must be acquired before any source-mutating prebuild command and held until the final build state is recorded.

## Direct Output and Build State

The wrapper passes `-Output` through unchanged. It does not create a staging output, copy a completed output tree into place, or restore a previous output after failure.

The wrapper maintains this small state file:

```text
<Output>/.helengine-build-state.json
```

The state records:

- a build id used only for diagnostics, not for directory allocation;
- canonical project path;
- platform;
- build profile;
- configuration;
- start and completion UTC timestamps;
- terminal status: `running`, `failed`, or `succeeded`;
- process exit code when available.

An interrupted write or process termination may leave the state missing, invalid, or `running`; none of those states count as success.

The existing build waiter continues checking required artifact freshness and non-zero length. It additionally requires a valid `succeeded` state whose start timestamp belongs to the current waiter invocation. Old ISO, executable, or package files cannot make a failed current build appear successful.

## Failure and Cancellation Behavior

Failure is intentionally non-transactional:

- authored project mutations remain;
- partially written output remains;
- stable intermediates remain;
- no source or output rollback runs;
- the state file records failure when the wrapper can do so;
- exact project, cache, and output paths are printed;
- inherited environment variables are restored;
- the project lock is released.

This behavior minimizes redundant writes and makes the next invocation an incremental retry.

## Cache Maintenance and Safety

`-Clean` is scoped to the selected project, editor configuration, platform, and build profile. It may remove:

- the selected editor configuration cache;
- the selected platform/configuration/profile generated and native caches.

It must not remove:

- the authored project;
- the final output root;
- another project hash;
- another platform/configuration/profile cache unless explicitly selected;
- the cache root itself.

`-PruneCacheOlderThanDays` considers only directories beneath `<CacheRoot>/projects`. It uses cache metadata rather than source timestamps and skips any project whose lock is currently held.

Before either operation deletes data, every target is canonicalized and verified to remain beneath the intended cache subtree. Reparse points are rejected as cleanup targets. A missing or malformed cache identity fails closed instead of broadening the deletion scope.

## Diagnostics

Each invocation prints a concise path summary before launching child processes:

- authored project path;
- project lock identity;
- editor cache path;
- platform/configuration/profile cache path;
- output path.

Waiting builds print bounded periodic lock status. Successful and failed builds print the terminal state-file path and retain existing live stdout/stderr streaming.

## Migration

The rework does not automatically delete legacy `C:\dev\helworks\b` contents. Removing previously accumulated data is a separate, explicitly authorized cleanup operation.

Migration steps are:

1. introduce direct-source behavior and stable cache arguments;
2. add stable-cache support to the headless editor path;
3. update build-waiter state validation;
4. replace token-based wrapper tests with behavior tests;
5. update canonical build documentation and build skills to use `-CacheRoot` terminology;
6. retain the `-WorkspaceRoot` alias for one compatibility cycle; and
7. remove the alias only after callers have migrated.

## Testing

### Wrapper behavior

Behavior tests use a disposable authored project and fake `dotnet` executable. They verify:

- the original `project.heproj` path reaches the editor invocation;
- no project copy or mirror is created;
- `robocopy` is not invoked;
- two equivalent builds resolve identical cache paths;
- cache paths contain no invocation GUID segment;
- `-Output` is forwarded unchanged;
- environment values are restored after success and failure;
- terminal build state is recorded correctly.

### Locking

Cross-process tests verify:

- a second build for the same project waits;
- a build for a different project proceeds independently;
- the waiter acquires the lock after the owner exits;
- timeout returns a clear non-zero result;
- process termination does not create a permanent stale lock.

### Cache maintenance

Tests verify:

- `-Clean` removes only the selected cache slices;
- pruning honors last-used metadata and age;
- locked project caches are skipped;
- path traversal, malformed hashes, and reparse points fail closed;
- source and output sentinels survive every maintenance operation.

### Editor stable-cache mode

Tests verify:

- headless builds use deterministic generated-code and build-graph roots;
- repeated CLI builds reuse those roots;
- interactive editor paths retain their existing isolation when stable-cache mode is absent;
- stable-cache mode does not append random execution ids.

### Build waiter

Tests verify:

- a current successful state plus fresh required artifacts succeeds;
- missing, stale, invalid, `running`, or `failed` state does not succeed;
- old artifacts cannot satisfy a failed current build.

### Integration smoke test

One focused integration test restores and publishes the real editor through `build-platform.ps1` with a lightweight fake platform build. It confirms direct-source invocation and stable editor-cache reuse without packaging a full platform artifact.

## Success Criteria

The design is complete when:

- canonical builds never copy the authored project;
- repeated equivalent builds reuse stable intermediates;
- same-project builds serialize and different-project builds remain concurrent;
- no ordinary build creates a GUID-scoped root beneath `C:\dev\helworks\b` or the new cache;
- platform output is written directly to the caller's output root;
- current build success cannot be confused with stale artifacts;
- failed builds retain source mutations, partial output, and intermediates as specified;
- cleanup operations are explicit and cannot escape the cache tree; and
- legacy invocation directories stop accumulating.
