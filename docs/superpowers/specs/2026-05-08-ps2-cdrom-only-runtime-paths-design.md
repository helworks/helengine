# PS2 CDROM-Only Runtime Paths Design

## Goal

Make the PS2 runtime consume final packaged `cdrom0:` paths directly, instead of reconstructing or translating logical cooked paths at runtime.

This removes the current split responsibility between:

- build-time packaging decisions
- generated native path rewriting
- runtime fallback file-open behavior

For PS2, the package should be authoritative.

## Problem

The current PS2 path flow is too indirect:

- shared build systems still produce logical cooked paths
- PS2 packaging rewrites many of those references to physical disc paths
- generated native file helpers still try to bridge logical paths, physical paths, and possible future devices
- runtime failures still surface as generic `Failed to open file: cdrom0:\...` errors after slow fallback behavior

That is the wrong boundary for PS2. The runtime should not spend CPU time reconstructing package layout decisions that were already known when the disc was built.

## Scope

This design is PS2-only.

Other platforms keep their current logical cooked-path flow.

This change covers:

- startup scene resolution for PS2 native builds
- packaged scene asset references inside PS2 cooked scene payloads
- packaged asset reads performed through generated PS2 `File` / `FileStream`

This change does not redesign content loading for Windows or other platforms.

## Desired Contract

For PS2 builds:

- all packaged runtime asset references must already be final `cdrom0:` paths
- the PS2 startup manifest must store the final physical startup scene path
- the generated PS2 runtime file layer must read those `cdrom0:` paths directly
- PS2 runtime code must not consult logical cooked-path manifests for packaged asset reads

Example final paths:

- `cdrom0:\COOKED\SCENES\DEA3B742.HAS;1`
- `cdrom0:\COOKED\FONTS\DE01835E.HEF;1`

## Approach Options

### Option 1: Package-Authoritative PS2 Paths

The builder rewrites every packaged PS2 asset reference to the final physical disc path, and the PS2 runtime reads those paths directly through `sceCdSearchFile` / `sceCdRead`.

Pros:

- matches the actual platform
- removes runtime ambiguity
- avoids expensive fallback probing
- gives one source of truth

Cons:

- PS2 packaging becomes more opinionated
- shared generated file helpers need a PS2-specific direct-read path

### Option 2: Keep Logical Paths, Translate at Runtime

The package keeps logical cooked paths, and runtime helpers continue translating them into physical disc paths.

Pros:

- preserves more shared behavior across platforms

Cons:

- this is the current failure-prone model
- wastes CPU on PS2
- makes debugging much harder

### Recommendation

Use Option 1.

PS2 should treat packaged paths as final disc paths, not as abstract content identifiers.

## Architecture

### 1. Builder-Owned Physical Paths

The PS2 builder remains responsible for:

- exporting final physical disc filenames
- rewriting cooked scene/material/font references to those physical `cdrom0:` paths
- emitting the final bootable ISO layout

There should be no second path-rewrite step after packaging.

### 2. PS2 Startup Manifest Stores Physical Path

The native startup manifest for PS2 must embed the final physical startup scene path, not a cooked-relative logical path.

Example:

- old: `cooked/scenes/DemoDiscMainMenu.hasset`
- new: `cdrom0:\COOKED\SCENES\DEA3B742.HAS;1`

This change is PS2-only. Other platforms may continue storing their existing startup scene path format.

### 3. Generated PS2 File Layer Reads `cdrom0:` Directly

Generated PS2 `File` / `FileStream` behavior should be simplified:

- if the requested path is a `cdrom0:` path or an already-physical disc path, treat it as a direct disc read
- open through PS2-native disc APIs
- do not consult the PS2 asset-path manifest
- do not fall back to `fopen(cdrom0:...)`

This means the runtime path bridge is removed for packaged reads.

### 4. Shared `ContentManager` Stays Generic

`ContentManager` should continue calling `File.OpenRead(...)`.

The platform-specific behavior belongs in the generated PS2 native file layer, not in shared managed content-loading code.

## Data Flow

### Build Time

1. The editor cooks all scenes selected in the build dialog.
2. The PS2 builder maps each staged file to its final physical disc path.
3. The PS2 builder rewrites all packaged asset references in cooked payloads to those final `cdrom0:` paths.
4. The PS2 builder writes the PS2 startup manifest with the final physical startup scene path.
5. The PS2 disc layout and ISO are produced from that already-finalized package.

### Runtime

1. PS2 boot reads the startup scene path from the native startup manifest.
2. The path is already a final `cdrom0:` path.
3. The generated PS2 file layer opens it directly through PS2 disc APIs.
4. Nested asset loads such as fonts also use already-final `cdrom0:` paths and follow the same direct-read path.

## Error Handling

Build-time failures should happen when:

- a packaged asset reference cannot be rewritten to a physical disc path
- two staged files collide to the same physical disc path
- the selected startup scene cannot be mapped to a final physical path

Runtime failures should happen only when:

- the referenced disc file is actually missing from the ISO
- disc search/read APIs fail
- the asset content is corrupt

Runtime failure messages should report the final `cdrom0:` path being opened.

## Performance Expectations

This should reduce the current long delay before failure because PS2 will stop:

- probing multiple fallback path shapes
- consulting intermediate logical-path translation helpers for packaged reads
- attempting generic file-open behavior on disc paths

The runtime should either:

- find the physical file quickly
- or fail quickly with a direct disc-read error

## Testing

### Automated

`helengine` tests should verify:

- PS2 startup manifest generation emits a physical `cdrom0:` startup path
- generated PS2 file/file-stream code does direct `cdrom0:` reads without logical-path manifest lookup

`helengine-ps2` tests should verify:

- packaged scene/font/material references are rewritten to final `cdrom0:` paths
- selected scenes from the build dialog are still all exported
- physical disc path collisions fail the build

### End-to-End

1. Build `city` for PS2.
2. Inspect the exported startup manifest and packaged scene payloads.
3. Confirm they contain final `cdrom0:` asset paths.
4. Boot the ISO in PCSX2.
5. Confirm the demo menu loads without the current font-open exception.

## Non-Goals

- no host filesystem support
- no HDD device support
- no attempt to unify PS2 runtime path behavior with Windows
- no broader content system redesign outside the PS2 path contract

## Success Criteria

This work is complete when:

- PS2 packaged asset references are final `cdrom0:` paths
- PS2 startup scene path is a final `cdrom0:` path
- PS2 runtime packaged reads no longer depend on logical-path reconstruction
- the current `Failed to open file: cdrom0:\COOKED\FONTS\DE01835E.HEF;1` startup failure is gone
- the demo-disc menu boots from the ISO using the packaged assets directly from disc
