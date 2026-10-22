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
