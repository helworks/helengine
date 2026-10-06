# Cross-platform incremental native builds

## Goal

An unchanged second build of the same project/platform/profile should reuse generated native sources and compiled objects. A content-only change should refresh the package without forcing a complete native compile. A changed generated source, compiler option, toolchain, or profile must invalidate the affected native work.

Windows now proves the basic shape: generate into a disposable graph workspace, mirror finalized C++ into a stable cache without touching identical files, and preserve the native build tree. Direct editor builds use `cache/build/windows/<profile>`; wrapper builds use their selected `-CacheRoot` profile slice. Extend this pattern through a platform-neutral cache contract rather than duplicating Windows path rules in every builder.

## Current platform behavior

| Platform | Native build behavior | Required work |
| --- | --- | --- |
| Windows | CMake/Ninja; stable generated-source mirror and native tree implemented | Validate two real builds and record compile counts once the smoke fixture uses the current asset format |
| PS2 | Docker `make BUILD_DIR=/build-output`; no explicit clean; build output already comes from `WorkingRoot/ps2-native` | First migration: stable generated-core mirror and native working root; preserve builder-owned manifest files |
| DS | Docker `make clean` before build; generated-core stager deletes and recopies its destination | Keep a stable native tree, replace destructive staging with content-aware sync, then remove clean while retaining a separate fresh NitroFS package tree |
| 3DS | Docker `make clean` before build | Stable generated/native roots; remove clean; keep RomFS staging independently fresh |
| GameCube | Docker `make clean all packaged-disc-assets` | Separate object output from disc assets; remove clean; rerun packaging when content changes |
| Wii | Docker `make clean && make` | Separate object output from disc assets; remove clean; rerun packaging when content changes |
| Wii U | Docker `make`, but deletes repository `build/` first | Make the native output root project/profile-scoped and persistent; stop deleting it; keep package-content staging fresh |
| Switch | Docker `make clean all` | Stable native output and generated-source roots; remove clean; keep RomFS staging fresh |
| PSP | Docker `make clean all` | Stable native output and generated-source roots; remove clean; keep package staging fresh |
| PS Vita | Builder deletes `WorkingRoot`; Docker `make clean all` | Preserve a dedicated native root while resetting only package staging; remove clean |
| PS1 | Build script creates a new named run directory | Keep immutable per-run artifacts, add a separate project/profile compile-object cache and deterministic generated-source root |
| PS3 | Build script requires a new BuildId and refuses an existing run directory | Keep that contract; add a separate shared compile-object cache and leave each BuildId output isolated |
| Dreamcast | Docker/Make native build through an editor platform builder and script | Preserve objects outside each package run; retain fresh CDI/content staging |
| N64 | Docker/Make native build through an editor platform builder and script | Cache compiler outputs in the main checkout; preserve independent ROM packaging and existing unrelated work |
| SNES | Script-driven native Make build, without an editor platform builder | Cache the script's object tree where supported while retaining fresh ROM output |
| Xbox | Script and editor builder drive a native Make build | Keep native objects stable while preserving per-run XBE/content output and unrelated rendering edits |
| Xbox 360 | Editor builder and PowerShell tools drive the native pipeline | Reuse compiler intermediates where the toolchain supports dependency tracking; keep XEX/package output isolated |

## Shared contract

1. Add an explicit `NativeObjectCacheRoot` to the platform build request/workspace. The existing `GeneratedCoreCppRootPath` points to the editor-mirrored stable generated source tree during native building. Do not reinterpret `WorkingRoot`, which builders use for disposable staging.
2. Resolve those paths from the wrapper's deterministic profile cache when `-CacheRoot` is supplied. Direct editor builds use the authored project's ignored `cache/build/<platform>/<profile>` tree. Keep output and cache roots disjoint and hold the project/profile lock across source synchronization and the native build.
3. Generate into a fresh graph directory, then mirror finalized native inputs to the stable source cache by content. Preserve timestamps for identical files and remove stale generated files. Builder-owned native manifests need an explicit ownership list or a separate overlay so the editor mirror does not replace them every run.
4. Place compiler objects and dependency files in a persistent, project/profile-scoped directory. Keep cooked content, disc trees, VPK/WUHB/ISO assembly, logs, and final output on their existing fresh or invocation-owned paths. Remove unconditional `clean` only after the builder's output paths and dependency rules are verified.
5. Record a cache fingerprint for compiler/toolchain identity, build profile, codegen options, preprocessor symbols, and generated-source layout. Invalidate native objects when the build system does not track one of these inputs itself. A failed build must not publish a valid fingerprint.
6. Measure compile commands and build duration in native two-build smoke checks where toolchains are available. Keep any future UI timing breakdown separate from this cache correctness change.

## Delivery order

1. Extract the Windows content-aware mirror into a platform-neutral editor service and request fields, keeping Windows tests green.
2. Migrate PS2 first. Its Docker build already accepts a host `BUILD_DIR` and does not force `make clean`, so the change can validate the shared contract with little native Makefile work.
3. Migrate DS, 3DS, GameCube, Wii, Wii U, Switch, PSP, and Vita individually. For each, move object output to the stable cache, remove the destructive clean, and prove package-only changes still update the final artifact.
4. Add separate compile caches to PS1 and PS3 without reusing or weakening their per-run artifact directories.
5. Migrate Dreamcast, N64, SNES, Xbox, and Xbox 360 after checking each native entry point. The N64 `helengine-n64-impl` directory is a second worktree of the same repository; change the `helengine-n64` main checkout only.
6. If a changed generated component still recompiles an oversized unity source, split generated C++ into a bounded number of stable translation units and measure the tradeoff between parallel compilation and header overhead.

## Acceptance checks for each platform

- Build the same small project twice. The second run executes no compile commands, while its output and build state are current.
- Change one generated native input. Only its dependent objects and the link/package steps rerun.
- Change only cooked content. Packaging reruns without rebuilding unchanged native objects.
- Change profile or toolchain fingerprint. The affected native cache invalidates while another profile remains usable.
- Fail a native build, then repair it. No stale success marker or partial output is accepted.
- Run two different projects concurrently. Their native caches and final outputs stay isolated.

## Verification record (2026-09-23)

The shared editor cache and platform builder changes have focused passing tests. Native two-build diagnostic smokes compiled 8 then 0 objects on Dreamcast, 6 then 0 on N64, and 4 then 0 on SNES; paired artifacts had matching hashes. An Xbox 360 editor-shaped smoke with external inputs reported native and content cache hits on its unchanged second build. A content-only mutation kept its native object cache hot and rebuilt the embedded content object.

The remaining acceptance checks need representative authored projects and toolchain runs per platform. In particular, the Windows smoke fixture uses an older asset format, the PS1 Docker smoke did not complete, and the PSP test assembly has unrelated stale API calls. Focused builder and source-level tests cover those changes, but they do not establish native compile-count savings across every target yet.
