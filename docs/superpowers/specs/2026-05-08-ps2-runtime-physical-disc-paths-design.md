## Goal

Move PS2 packaged asset resolution to a build-owned physical-disc-path contract. The Windows/editor build pipeline should compute and emit PS2-safe physical paths for all packaged assets up front, and the PS2 runtime should consume those paths directly without reconstructing aliases at runtime.

## Problem

The current PS2 path contract is split across two places:

- the PS2 builder stages physical ISO filenames using PS2-safe naming rules
- the PS2 runtime reimplements those same naming rules in C++ and reconstructs physical paths from logical engine paths during boot and asset loads

That split is causing the exact class of failures we are seeing:

- startup scene resolution still depends on logical cooked paths being translated correctly at runtime
- fonts and secondary assets can fail independently from startup because they are resolved through the same runtime reconstruction path
- the PS2 runtime wastes scarce CPU time doing string rewriting and hash-based alias generation that should have been completed on the Windows build machine

The runtime boundary is wrong. The PS2 binary should not need to know how logical engine asset paths map to physical disc filenames.

## Desired Outcome

After this change:

- `helengine` remains the source of truth for logical cooked asset paths
- the PS2 builder computes the exact physical disc path for every packaged file
- the PS2 build emits metadata that maps packaged logical paths to packaged physical disc paths
- the PS2 runtime uses those emitted physical paths directly for all packaged file opens
- runtime path alias reconstruction is removed from the PS2 host

This applies to all packaged assets, not only the startup scene:

- scenes
- fonts
- textures
- materials
- models
- shaders
- any other cooked asset included in the PS2 build

## Non-Goals

- changing the engine-wide logical cooked path model used by other platforms
- reducing PS2 packaging to only the startup scene
- changing the set of scenes selected by the build dialog
- introducing a separate PS2-only authored scene list
- redesigning asset serialization formats

PS2 should still package all scenes selected in the build dialog and all dependent assets required by those scenes.

## Recommended Approach

Use a builder-owned mapping contract:

1. Keep logical cooked paths in the shared manifest model because the engine already uses them consistently.
2. During the PS2 packaging step, compute a PS2 physical disc path for every staged cooked file.
3. Emit a PS2 runtime asset-path manifest that maps logical cooked paths to physical disc paths.
4. Emit the startup scene physical disc path directly into the PS2 native startup manifest.
5. Remove PS2 runtime alias reconstruction and make all packaged file reads resolve through the emitted PS2 path metadata.

This keeps the engine-wide manifest contract stable while moving platform-specific filename translation entirely into the platform builder, where it belongs.

## Alternatives Considered

### Runtime reconstruction only for startup

Rejected. It leaves fonts and all later asset loads on the same broken contract and keeps the expensive path reconstruction logic alive in the runtime.

### Full PS2-only manifest fork with only physical paths

Rejected for now. It would work technically, but it creates a larger divergence from the shared engine build-manifest model than we need. A PS2-specific physical-path manifest layered on top of the shared logical manifest is enough.

### Keep the current duplicated builder/runtime aliasing logic

Rejected. It is the source of the bug class we are trying to remove. The same naming algorithm should not be maintained independently in C# and C++.

## Architecture

### Main engine producer side

The shared editor pipeline already emits explicit logical cooked paths for scenes through the cooked manifest. That remains unchanged.

The main engine side should add one PS2-oriented extension point:

- when the PS2 packaging step is active, the platform build outputs must include a generated PS2 physical-path manifest derived from the staged cooked outputs

The shared cooked manifest continues to describe:

- scene ids
- logical cooked relative paths
- artifact identities

The PS2 packaging layer adds:

- logical cooked path -> physical PS2 disc path mapping

### PS2 builder side

[`Ps2DiscLayoutWriter.cs`](/C:/dev/helworks/helengine-ps2/builder/Ps2DiscLayoutWriter.cs) becomes the authoritative source of physical disc layout. It should:

- stage all selected cooked outputs, not a filtered subset
- compute the PS2 physical relative path for every staged file exactly once
- copy the file into the disc layout using that computed path
- record the mapping from the file's logical cooked path to its physical disc path

That mapping should then be written into a generated runtime data file consumed by the native PS2 runtime.

### PS2 startup manifest

[`EditorRuntimeNativeManifestWriter.cs`](/C:/dev/helworks/helengine/engine/helengine.editor/managers/project/EditorRuntimeNativeManifestWriter.cs) should continue to embed the startup scene path for native players, but the PS2 output of that process must become the physical PS2 disc path, not the logical cooked path.

That means the PS2 runtime startup manifest should contain something like:

- `\COOKED\SCENES\DEMODISC.HAS;1`

and not:

- `cooked/scenes/DemoDiscMainMenu.hasset`

### PS2 runtime side

[`Ps2BootHost.cpp`](/C:/dev/helworks/helengine-ps2/src/platform/ps2/Ps2BootHost.cpp) should stop:

- sanitizing path tokens
- hashing long names into short aliases
- converting logical cooked paths into PS2 physical paths

Instead, it should:

1. read the startup scene physical path directly from the generated startup manifest
2. resolve all other packaged file opens through the generated PS2 asset-path manifest
3. pass physical disc paths directly into `sceCdSearchFile` / `sceCdRead`

The PS2 runtime may still prepend the `cdrom0:` device root where needed, but it should not derive filenames.

## Data Flow

### Current flow

1. Editor cooks assets into logical cooked paths
2. PS2 builder copies them to PS2-safe disc filenames
3. PS2 runtime receives logical cooked paths
4. PS2 runtime reconstructs physical disc filenames again
5. PS2 runtime attempts to open the reconstructed paths

### New flow

1. Editor cooks assets into logical cooked paths
2. PS2 builder stages all selected cooked outputs into PS2-safe physical disc paths
3. PS2 builder emits:
   - startup scene physical path
   - packaged logical path -> physical path manifest
4. PS2 runtime loads startup scene from the emitted physical path
5. PS2 runtime resolves all later packaged asset reads through the emitted mapping

## Packaging Scope

PS2 packaging should include:

- every scene selected in the build dialog
- all dependent cooked assets for those scenes
- the startup scene among that full set

This change does not reduce packaging to only the demo-disc startup scene. It only fixes how PS2 paths are represented and consumed.

## Error Handling

Build-time failures should happen when:

- two logical cooked files collide to the same PS2 physical disc path
- the startup scene logical path does not resolve to a staged physical disc path
- a staged file mapping cannot be emitted into the PS2 runtime manifest

Runtime failures should happen when:

- the startup manifest references a physical path that does not exist on disc
- a packaged asset load requests a logical cooked path that is not present in the PS2 runtime asset-path manifest

Those runtime failures should log the missing logical id and the expected physical disc path source, but the runtime should no longer log or derive aliasing rules.

## Testing Strategy

### Main repo tests

Add or update tests for:

- PS2 startup manifest generation emits a physical disc path instead of a logical cooked path
- PS2 path metadata generation maps logical cooked paths to physical disc paths
- startup scene selection still follows the configured build-order startup scene

### PS2 builder tests

Add or update tests for:

- all staged cooked files receive deterministic physical disc paths
- logical cooked paths map to the expected physical disc paths
- collisions in PS2 physical naming fail the build
- the disc layout still contains all selected scenes from the build dialog

### PS2 runtime tests

Add focused checks for:

- startup scene load consumes the physical startup path directly
- packaged asset reads use emitted mapping lookups instead of runtime alias reconstruction
- the removed alias helper functions are no longer present in the PS2 host

### End-to-end verification

Build `city` for PS2 and verify:

- the output disc layout contains all selected scenes
- the generated PS2 runtime metadata contains physical disc paths
- the startup scene entry points at the packaged demo-disc menu scene
- PCSX2 boots the ISO and loads the menu instead of falling back to the debug scene

## Migration Notes

There is already stale behavior in `helengine-ps2` that still assumes the old logical startup alias flow. This design intentionally treats that behavior as obsolete.

The implementation should remove the duplicated alias algorithm from the PS2 runtime rather than trying to keep the old and new contracts alive at the same time. A dual-path transition would preserve exactly the ambiguity that caused the current bug.
