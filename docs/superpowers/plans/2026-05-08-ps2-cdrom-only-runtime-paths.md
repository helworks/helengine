# PS2 Cdrom-Only Runtime Paths Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make PS2 packaged runtime asset loading fully `cdrom0:`-based. The build should emit final physical PS2 paths for startup scenes and packaged asset references, and the PS2 generated runtime should read those paths directly from disc without reconstructing logical cooked paths or consulting PS2 asset-path manifests at runtime.
**Architecture:** Keep shared runtime/content flow unchanged for non-PS2 platforms. Move PS2 packaging authority to the build side: startup manifest, cooked scene references, and cooked material/font references should already contain final physical disc paths. Simplify the generated PS2 native `File` and `FileStream` support to direct `cdrom0:` reads only.
**Tech Stack:** C#, .NET 9, generated C++ core, PS2SDK disc APIs, Docker-based PS2 native build, repo-local editor tests, `helengine-ps2` builder tests.

---

## Task 1: Emit a physical PS2 startup-scene path from the engine-side native manifest writer

- [ ] Update [EditorRuntimeNativeManifestWriter.cs](C:/dev/helworks/helengine/engine/helengine.editor/managers/project/EditorRuntimeNativeManifestWriter.cs) so PS2 native startup manifest output stores the final physical `cdrom0:` startup scene path instead of a cooked-relative logical path.
- [ ] Keep existing behavior unchanged for Windows and every non-PS2 platform.
- [ ] Ensure the PS2 startup path comes from the packaged/cooked PS2 output contract, not from runtime alias reconstruction.
- [ ] If `EditorRuntimeNativeManifestWriter` does not currently have enough information to compute the physical startup path, add the minimum explicit input required rather than deriving it later in generated runtime code.
- [ ] Add or update tests near [EditorGeneratedCoreRegenerationServiceTests.cs](C:/dev/helworks/helengine/engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs) or the closest native-manifest writer test file to assert:
  - [ ] PS2 output contains a final `cdrom0:\...;1` startup scene path.
  - [ ] Windows/native desktop output still uses the current non-PS2 contract.

### Notes

- The current stale contract is `he_get_runtime_startup_scene_relative_path()`. The PS2 plan requires a physical path contract instead.
- Avoid creating a second fallback path source in runtime code. The manifest should be authoritative.

## Task 2: Rewrite packaged PS2 asset references to final `cdrom0:` paths at build time

- [ ] In `helengine-ps2`, update the PS2 packaging pipeline so all packaged asset references written into cooked outputs are final physical PS2 disc paths.
- [ ] Target the builder-side rewrite path, not runtime reconstruction. Relevant builder files are expected to include:
  - [ ] `C:\dev\helworks\helengine-ps2\builder\Ps2PlatformAssetBuilder.cs`
  - [ ] `C:\dev\helworks\helengine-ps2\builder\Ps2CookedAssetPathRewriter.cs`
  - [ ] `C:\dev\helworks\helengine-ps2\builder\Ps2DiscLayoutWriter.cs`
- [ ] Ensure scene payloads, scene asset references, cooked material texture references, font references, and any other packaged runtime file paths are rewritten to final `cdrom0:\...\;1` values before the ELF build consumes them.
- [ ] Keep exporting all scenes selected in the build dialog. Do not collapse PS2 export down to only the startup scene.
- [ ] Fail the build if a packaged runtime path cannot be converted to a physical PS2 path, instead of leaving a logical cooked path behind.
- [ ] Add or update `helengine-ps2` builder tests to cover:
  - [ ] startup scene reference rewritten to `cdrom0:`
  - [ ] font references rewritten to `cdrom0:`
  - [ ] texture/material references rewritten to `cdrom0:`
  - [ ] multiple selected scenes still staged correctly

### Notes

- The current failure `Failed to open file: cdrom0:\COOKED\FONTS\DE01835E.HEF;1` indicates the runtime is still hitting inconsistent packaged references and/or inconsistent runtime read semantics.
- The builder is the correct place to resolve physical PS2 names because that work should not happen on PS2 CPU time.

## Task 3: Remove PS2 logical-path reconstruction from generated native file support

- [ ] In [EditorGeneratedCoreRegenerationService.cs](C:/dev/helworks/helengine/engine/helengine.editor/managers/project/EditorGeneratedCoreRegenerationService.cs), simplify PS2-specific generated `File` and `FileStream` support so it reads final `cdrom0:` paths directly.
- [ ] Remove or bypass PS2 runtime helpers that normalize logical cooked keys, consult `runtime_ps2_asset_path_manifest`, or rebuild physical disc paths from logical paths.
- [ ] Keep the generated PS2 code path narrow:
  - [ ] if the path is `cdrom0:`-based, use PS2 native disc read helpers
  - [ ] if the path is not valid for PS2 packaged runtime reads, fail clearly
- [ ] Preserve non-PS2 generated native behavior.
- [ ] Update generator tests in [EditorGeneratedCoreRegenerationServiceTests.cs](C:/dev/helworks/helengine/engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs) to assert:
  - [ ] no PS2 manifest lookup helpers remain in the generated `File` / `FileStream` path
  - [ ] PS2 generated code performs direct `cdrom0:` disc reads
  - [ ] non-PS2 generated code remains unchanged

### Notes

- This task is intentionally removing complexity. Do not replace one PS2 rewrite layer with another.
- The runtime should not spend time trying multiple lookup strategies on PS2. That was part of the long startup delay.

## Task 4: Make the PS2 boot host consume the physical startup path directly

- [ ] In `helengine-ps2`, update:
  - [ ] `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2BootHost.cpp`
  - [ ] `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2BootHost.hpp`
- [ ] Remove any remaining startup-scene logical-path translation or alias reconstruction from the boot host.
- [ ] Ensure boot uses the physical startup path emitted by the manifest and disc reads that exact file.
- [ ] Keep boot error output explicit and fast:
  - [ ] missing startup file path
  - [ ] disc read failure
  - [ ] scene deserialization failure
- [ ] Do not reintroduce fallback checkerboard/test-scene rendering for startup failures.

### Notes

- The boot host should become thinner after this change, not more complex.
- Startup error messages should report the actual physical file path being opened.

## Task 5: Remove PS2 runtime dependency on logical-to-physical asset manifests

- [ ] Audit whether `runtime_ps2_asset_path_manifest` is still needed once packaged outputs and generated PS2 file support are both physical-path based.
- [ ] If it is no longer required for packaged runtime reads, remove its generation/use from the active PS2 runtime path.
- [ ] If some small subset still depends on it, document that dependency explicitly and keep it out of the hot path for normal packaged reads.
- [ ] Update `helengine-ps2` builder tests accordingly.

### Notes

- This is a cleanup task, but it matters because the current manifest-driven bridge is exactly what the new design is replacing.
- If removal is too large for one pass, at minimum the runtime must stop depending on it for startup scene, font, and general packaged asset reads.

## Task 6: Verify with focused tests before the real export

- [ ] Run focused `helengine` generator/native-manifest tests.

```powershell
dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests --no-restore
```

- [ ] Run focused `helengine-ps2` builder tests.

```powershell
dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Ps2PlatformAssetBuilderTests|FullyQualifiedName~Ps2DiscLayoutWriterTests|FullyQualifiedName~Ps2RuntimeAssetPathManifestWriterTests" --no-restore
```

- [ ] If new native-manifest writer tests live under a different class filter, run that targeted filter too and record the exact command.

## Task 7: Rebuild `city` for PS2 and inspect the packaged result

- [ ] Rebuild the editor app if required by the generator/native-manifest changes.

```powershell
dotnet build helengine.ui\helengine.editor.app\helengine.editor.app.csproj -c Debug
```

- [ ] Run a real PS2 export for `city`.

```powershell
dotnet C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.dll --project "C:\dev\helprojs\city" --build ps2 --output "C:\dev\helprojs\output\ps2"
```

- [ ] Inspect the output contract:
  - [ ] `C:\dev\helprojs\output\ps2\game.iso`
  - [ ] `C:\dev\helprojs\output\ps2\disc\SYSTEM.CNF`
  - [ ] `C:\dev\helprojs\output\ps2\disc\HELENGIN.ELF`
  - [ ] packaged cooked scene/font/material files
- [ ] Confirm the rebuilt cooked scene payloads no longer contain logical cooked file paths for runtime-loaded PS2 assets.
- [ ] Confirm the startup manifest now contains the final physical `cdrom0:` startup scene path.

## Task 8: Runtime acceptance check in PCSX2

- [ ] Boot the freshly exported [game.iso](C:/dev/helprojs/output/ps2/game.iso) in PCSX2.
- [ ] Confirm the previous failure does not recur:
  - [ ] no `Failed to open file: cdrom0:\COOKED\FONTS\DE01835E.HEF;1`
- [ ] Confirm startup no longer stalls for multiple minutes before the exception path.
- [ ] Confirm the demo disc menu scene loads instead of dropping into the fallback/test pattern path.
- [ ] If text is still missing after packaged file I/O is fixed, treat that as a separate renderer/font rendering task and do not muddy this plan’s scope.

## Task 9: Commit strategy

- [ ] Commit `helengine` changes separately with a message focused on PS2 generated/native manifest runtime path simplification.
- [ ] Commit `helengine-ps2` changes separately with a message focused on PS2 physical `cdrom0:` packaging/runtime consumption.
- [ ] Do not mix unrelated dirty worktree files into either commit.

## Implementation Checklist

- [ ] PS2 startup manifest uses final physical `cdrom0:` path
- [ ] PS2 packaged asset references are rewritten at build time
- [ ] PS2 generated `File` / `FileStream` no longer reconstruct logical cooked paths
- [ ] PS2 boot host opens the physical startup file directly
- [ ] active PS2 runtime path no longer depends on logical-to-physical manifest bridging
- [ ] focused tests pass
- [ ] real `city` PS2 export succeeds
- [ ] PCSX2 no longer throws the font-open startup exception

## Rollback Plan

- [ ] Revert the `helengine` generator/native-manifest changes if non-PS2 generated outputs regress.
- [ ] Revert the `helengine-ps2` packaging/runtime changes if the disc layout or boot contract becomes invalid.
- [ ] Keep rollback split by repo so a bad PS2 runtime change does not force reverting unrelated engine work.
