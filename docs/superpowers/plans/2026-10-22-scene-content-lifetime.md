# Scene Content Lifetime Implementation Plan

**Goal:** Release PS1 serialized content as soon as scene preparation and its loaded callbacks finish, instead of retaining those bytes until scene unload.

**Approved direction:** Helena requires consumed serialized data to be deleted immediately because PS1 RAM cannot retain it. Runtime entities, decoded assets and uploaded textures keep their existing ownership. Existing streams independently pin their source until disposal. Later raw reads require a fresh asynchronous preload scope; no synchronous CD wait is introduced.

**Architecture:** Keep the existing provider and operation interfaces. SceneManager ends the content preparation lease on successful synchronous/asynchronous publication and on failed loaded callbacks. Code module leases remain scene-owned through unload. The PS1 source already frees bytes when its last scene preparation and stream references end.

**Baseline:** Exact disc SHA256 e6288a0b75137b7df0af1866181c80ae695b24c74188be3017b808c80b99287f; main menu live heap 391048 bytes, including 156256 bytes of allocations holding ten serialized assets. Evidence is C:\temp\helengine\ps1\ram-summary.json and /tmp/helengine/ps1/heap-analysis.json on the Mac.

## Tasks

- [x] Add regression coverage for synchronous/asynchronous success, reads inside loaded callbacks, callback failure, repeated loading and code lifetime through unload. RED: five expected failures before the production fix.
- [x] End content leases at the preparation boundary while preserving code module leases and cancellation cleanup. Update interface documentation and the lifecycle fixture.
- [x] Run the focused lifecycle/ownership/loading tests and verify GREEN: 45 passed, zero failed.
- [x] Build from the actual main checkouts, overwrite the fixed PS1 output and run the resulting CPU-profiled 32/32 disc on the Mac. Verified disc SHA256: e9f38fa46ebf2b13bc4c6f12d15e10fae1f4fddb02d4949f10f65b74f6c1761d.
- [x] Read the guest allocator again; all 121 content-cache records have zero source buffers/references/owners. The 149 entities and component counts are unchanged, and complete 600-frame profiler windows have no dropped samples or clock faults.

Work on main without worktrees or source exports. Preserve the unrelated PS1 edits in scripts/build.ps1 and scripts/native-object-cache.ps1. All temporary outputs use the fixed platform directory. The previously observed PS1_RENDER_POSE_REJECTED remains a separate recorded issue.

## Verification and current build blocker

- Lifecycle correction is published as `879be493`; 45 focused lifecycle/ownership/loading tests pass.
- The real Linux cook exposed a source-path casing error. `db2ae76a` preserves authored texture casing; its regression was observed failing before the fix, then all 46 focused packaging/factory tests passed.
- The PS1 cooker used Windows-only System.Drawing. PS1 commit `56c9b63` reuses the existing portable Magick importer. The PNG regression failed on Linux before the fix; three focused PNG/TIM tests pass afterward, and all 27 cook work items complete.
- The actual MIPS SDK compiles the regenerated SceneManager.cpp, including ReleaseSceneContentPreparation. Evidence: /tmp/helengine/ps1/scene-manager-native-check.json on Linux.
- Converter commits 00a126f and 5ba47bb resolve the Guid dependency using managed string storage and the existing process-local token sequence, preserving hosted atomics and using a plain counter for freestanding compilation. Three execution modes and actual MIPS cross-compilation pass; all 599 generated candidates compile. Gizmos and model IDs were preserved; no generated code was patched or excluded.
- PS1 commit b3e2cfc makes native Docker outputs use the Unix host UID/GID. Existing root-owned intermediates were repaired once. Only this ownership hunk was committed; unrelated script changes remain preserved.
- The completed native CPU-profiled disc passed the scene-module/retail-memory receipt and runs in the Mac DuckStation. The initial unprofiled native build was intentionally stopped after successful cook, generation and partitioning to use those same inputs for the profiler build through the official runner.

## Measured result

- Loaded-menu heap including allocator headers: 391048 -> 239304 bytes, reduction 151744 bytes (148.2 KiB, 38.8%).
- Consumed serialized asset allocations: 156256 -> 0 bytes. Only the 2420-byte cache index payload remains; it contains no retained file buffers.
- Free heap: 440432 -> 583984 bytes, net gain 143552 bytes (140.2 KiB). The complete newer engine build uses 8192 more static resident bytes than the older baseline; the net gain includes this difference.
- The remaining heap difference includes other current-main/cooker changes; this is a comparison of complete builds, not an isolated single-change benchmark. Raw source retention was independently verified as zero.
- Evidence on the Mac: /tmp/helengine/ps1/ram-comparison.json, /tmp/helengine/ps1/cache-leases-after.json and /tmp/helengine/ps1/profiler-after.txt. Local review copies: C:\temp\helengine\ps1\ram-comparison.json, C:\temp\helengine\ps1\cache-leases-after.json and C:\temp\helengine\ps1\profiler-after.txt.
- The emulator remains running the new disc at /tmp/helengine/ps1/package/helengine_ps1.cue. Peak live loading allocation and peak stack were not measured; allocator high-water address span is not peak allocation.
