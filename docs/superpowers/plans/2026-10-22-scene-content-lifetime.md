# Scene Content Lifetime Implementation Plan

**Goal:** Release PS1 serialized content as soon as scene preparation and its loaded callbacks finish, instead of retaining those bytes until scene unload.

**Approved direction:** Helena requires consumed serialized data to be deleted immediately because PS1 RAM cannot retain it. Runtime entities, decoded assets and uploaded textures keep their existing ownership. Existing streams independently pin their source until disposal. Later raw reads require a fresh asynchronous preload scope; no synchronous CD wait is introduced.

**Architecture:** Keep the existing provider and operation interfaces. SceneManager ends the content preparation lease on successful synchronous/asynchronous publication and on failed loaded callbacks. Code module leases remain scene-owned through unload. The PS1 source already frees bytes when its last scene preparation and stream references end.

**Baseline:** Exact disc SHA256 e6288a0b75137b7df0af1866181c80ae695b24c74188be3017b808c80b99287f; main menu live heap 391048 bytes, including 156256 bytes of allocations holding ten serialized assets. Evidence is C:\temp\helengine\ps1\ram-summary.json and /tmp/helengine/ps1/heap-analysis.json on the Mac.

## Tasks

- [x] Add regression coverage for synchronous/asynchronous success, reads inside loaded callbacks, callback failure, repeated loading and code lifetime through unload. RED: five expected failures before the production fix.
- [x] End content leases at the preparation boundary while preserving code module leases and cancellation cleanup. Update interface documentation and the lifecycle fixture.
- [x] Run the focused lifecycle/ownership/loading tests and verify GREEN: 45 passed, zero failed.
- [ ] Build from the actual main checkouts, overwrite the fixed PS1 output and run the resulting disc on the Mac.
- [ ] Read the guest allocator again; verify serialized bytes released, menu entities/assets still valid and profiler progressing. Record actual savings and any limits.

Work on main without worktrees or source exports. Preserve the unrelated PS1 edits in scripts/build.ps1 and scripts/native-object-cache.ps1. All temporary outputs use the fixed platform directory. The previously observed PS1_RENDER_POSE_REJECTED remains a separate recorded issue.

## Verification and current build blocker

- Lifecycle correction is published as `879be493`; 45 focused lifecycle/ownership/loading tests pass.
- The real Linux cook exposed a source-path casing error. `db2ae76a` preserves authored texture casing; its regression was observed failing before the fix, then all 46 focused packaging/factory tests passed.
- The PS1 cooker used Windows-only System.Drawing. PS1 commit `56c9b63` reuses the existing portable Magick importer. The PNG regression failed on Linux before the fix; three focused PNG/TIM tests pass afterward, and all 27 cook work items complete.
- The actual MIPS SDK compiles the regenerated SceneManager.cpp, including ReleaseSceneContentPreparation. Evidence: /tmp/helengine/ps1/scene-manager-native-check.json on Linux.
- Complete candidate compilation is blocked by RotationGizmoGeometry.cpp: its existing Guid.NewGuid dependency requires hosted services/std::string in the converter runtime. Evidence: /tmp/helengine/ps1/scene-module-candidates/ebb5a00d59672ee94791a824.log on Linux. No generated code was patched or excluded.
- Making Guid portable is a separate runtime architectural extension; feedback was requested under Helena's architectural decision rule. Do not replace model IDs with constants or remove gizmos to bypass this failure.
- The Mac emulator still runs the exact baseline disc. No after-build RAM saving has been claimed or measured yet. Resume the complete build, CPU profiler and guest allocator snapshot after the Guid decision.
