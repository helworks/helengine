## Goal

Change the PS2 build output from a loose ELF plus cooked files into a bootable PS2 disc package produced entirely inside Docker. The builder should export both a final ISO and the staged disc contents so emulator booting and packaging diagnostics use the same artifact layout.

## Problem

The current PS2 pipeline emits:

- `helengine_ps2.elf`
- `cooked/...`

That is sufficient for native compile verification, but it is not a bootable disc image. PCSX2 returns to the BIOS browser when this output is treated like a disc because there is no `SYSTEM.CNF`, no disc-root boot filename, and no ISO. Running the ELF directly is also not the right long-term contract because the runtime reports `HostFileSystem` disabled and resolves packaged assets from the local application directory. The packaging format and the runtime storage model are currently misaligned.

## Desired Output Contract

PS2 builds should write both of these to the requested output root:

- `game.iso`
- `disc/`

The `disc` directory should contain:

- `SYSTEM.CNF`
- `HELENGINE.ELF`
- `cooked/...`

The boot contract should become:

- `BOOT2 = cdrom0:\HELENGINE.ELF;1`

The existing loose-root `helengine_ps2.elf` export should no longer be the primary deliverable. The ISO and staged disc root become the official PS2 build outputs.

## Non-Goals

- Adding PS2 host filesystem support for emulator-side loose file loading.
- Changing the runtime startup manifest format.
- Introducing Windows-side ISO tooling.
- Packaging memory card assets, save data, or extra emulator metadata.

## Recommended Approach

Use Docker for the entire PS2 packaging boundary:

1. The editor build graph still regenerates generated core and cooks runtime assets.
2. The PS2 builder still compiles the native ELF inside Docker.
3. After the ELF is built, the PS2 builder stages a disc root in Docker.
4. Docker generates a bootable ISO from that disc root.
5. The builder copies both the staged `disc` folder and the final `game.iso` to the requested output path.

This keeps the platform contract self-contained, avoids Windows dependency drift, and matches the existing `disc-layout` storage profile instead of depending on emulator-specific loose-file behavior.

## Alternatives Considered

### Add host filesystem support first

Rejected. It would improve direct ELF boot in some emulator setups, but it would still leave the platform without a correct distributable format. It also couples the runtime to emulator-specific behavior instead of converging on the actual deployment model.

### Build the ELF in Docker, author the ISO on Windows

Rejected. This splits one platform packaging contract across two toolchains and introduces another local dependency surface that will drift across machines.

### Export only the ISO

Rejected for now. Keeping the staged `disc` directory next to the ISO makes packaging failures and boot issues much easier to inspect during the current bring-up phase.

## Architecture

### Builder orchestration

[`Ps2PlatformAssetBuilder.cs`](/C:/dev/helworks/helengine-ps2/builder/Ps2PlatformAssetBuilder.cs) remains the orchestration entrypoint, but its export responsibility changes:

- stage cooked artifacts into the output `disc/cooked/...` layout instead of the output root
- invoke native ELF build
- place the boot ELF into `disc/HELENGINE.ELF`
- create `disc/SYSTEM.CNF`
- invoke Docker ISO authoring
- verify `game.iso` exists before returning success

The builder should keep output creation linear and fail fast. A PS2 build is successful only if all three artifacts exist:

- `disc/SYSTEM.CNF`
- `disc/HELENGINE.ELF`
- `game.iso`

### Native build and ISO packaging boundary

[`Ps2NativeBuildExecutor.cs`](/C:/dev/helworks/helengine-ps2/builder/Ps2NativeBuildExecutor.cs) should own the Docker command boundary. It already owns native compilation and is the right place to extend platform-side packaging because:

- it already knows the repository root and generated-core mount layout
- it is the component that converts a prepared workspace into a PS2-native artifact
- it can surface Docker stderr/stdout for both compile and ISO authoring failures

Two acceptable internal structures:

1. Extend `Ps2NativeBuildExecutor` with one packaging phase after `make`
2. Introduce a small sibling service for Docker ISO authoring and invoke it from the builder after native compile

The first option is preferred unless the class becomes difficult to read.

### Disc root contents

The staged disc root should be deterministic:

- `SYSTEM.CNF`
- `HELENGINE.ELF`
- `cooked/...`

`SYSTEM.CNF` should be generated from code, not copied from a template file, because the contents are tiny and stable. The writer should emit the standard PS2 boot line with the exact uppercase filename selected for the disc image.

### ISO tool choice

The Docker image should gain one ISO authoring tool, preferably `xorriso` or `genisoimage`. The exact command should be wrapped behind the PS2 builder so the rest of the editor does not know or care which Docker-side tool is used.

Selection criteria:

- available in the Docker base image or easy to install reliably
- able to produce a PS2-readable ISO from a prepared disc root
- predictable command-line contract for automated builds

`xorriso` is the preferred choice if available cleanly in the image because it is actively maintained and works well for deterministic ISO generation.

## Data Flow

### Current flow

1. Editor cooks scenes and assets
2. PS2 builder copies `cooked/...` to the output root
3. PS2 builder builds `helengine_ps2.elf`
4. PS2 builder copies `helengine_ps2.elf` to the output root

### New flow

1. Editor cooks scenes and assets
2. PS2 builder prepares output directories:
   - `output/disc/`
   - `output/disc/cooked/...`
3. PS2 builder invokes Docker native compile
4. PS2 builder copies the produced native ELF to:
   - `output/disc/HELENGINE.ELF`
5. PS2 builder writes:
   - `output/disc/SYSTEM.CNF`
6. PS2 builder invokes Docker ISO packaging against `output/disc`
7. PS2 builder writes:
   - `output/game.iso`
8. Builder verifies final outputs and returns success

## Error Handling

The build should fail with explicit packaging diagnostics for these cases:

- native ELF missing after Docker compile
- `SYSTEM.CNF` generation failed
- staged `disc` root missing required files
- Docker ISO generation failed
- `game.iso` missing after ISO generation

These should not be collapsed into a generic Docker failure when the actual fault is packaging. The diagnostic path should preserve the failing phase:

- `native-build`
- `disc-stage`
- `iso-package`

## Testing Strategy

### Unit tests

Add focused tests for:

- `SYSTEM.CNF` contents use `BOOT2 = cdrom0:\HELENGINE.ELF;1`
- staged output path layout uses `disc/HELENGINE.ELF`
- ISO output path resolves to `game.iso`
- builder success requires both staged disc files and final ISO

### Integration verification

Build `city` for PS2 and verify:

- `C:\dev\helprojs\output\ps2\disc\SYSTEM.CNF` exists
- `C:\dev\helprojs\output\ps2\disc\HELENGINE.ELF` exists
- `C:\dev\helprojs\output\ps2\disc\cooked\...` exists
- `C:\dev\helprojs\output\ps2\game.iso` exists

### Emulator verification

Boot `game.iso` in PCSX2. Success means:

- the emulator recognizes the image as bootable
- control passes into the PS2 runtime instead of returning to the BIOS browser
- the runtime at least reaches its current boot path

This design does not assume scene rendering is fully correct yet. The packaging verification target is successful disc boot, not full gameplay validation.

## Scope Boundaries

This slice includes:

- PS2 builder output contract changes
- Docker-side ISO authoring
- `SYSTEM.CNF` generation
- boot filename normalization to `HELENGINE.ELF`

This slice does not include:

- adding direct hostfs support to the PS2 runtime
- changing the authored project build UI
- adding multiple ISO variants or debug/release media flavors
- introducing compression, multi-disc support, or installer metadata

## Migration Notes

The previous loose-root PS2 output was useful during early native bring-up but should now be treated as an implementation detail. The public contract for PS2 output becomes:

- inspectable staged disc layout
- bootable ISO

Any tests or docs that currently describe the output as `helengine_ps2.elf` plus `cooked/...` should be updated to reflect the new packaging contract.
