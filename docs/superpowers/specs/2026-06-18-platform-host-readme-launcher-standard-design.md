# Platform Host README And Emulator Launcher Standard Design

## Goal

Standardize the Helengine platform host repositories so each one exposes the same minimal root documentation surface and the same canonical emulator-launch entrypoint.

This applies only to the platform host repositories:

- `helengine-wii`
- `helengine-ps2`
- `helengine-gc`
- `helengine-ds`
- `helengine-psp`
- `helengine-psvita`
- `helengine-switch`
- `helengine-wiiu`
- `helengine-windows`
- `helengine-3ds`

## Scope

This design covers:

- the required root `README.md` skeleton for each platform host repo
- the canonical emulator launcher entrypoint and parameter contract
- the file layout for moving overflow documentation out of the root README
- the rollout order for normalizing the selected repos

This design does not change:

- the underlying editor CLI build contract
- native builder internals
- emulator-specific runtime behavior beyond the launcher entrypoint name and parameter shape
- non-platform-host repositories

## Desired End State

Every platform host repository should present the same top-level workflow:

1. Read the root `README.md`
2. Build the platform output through the shared editor CLI wrapper when supported
3. Launch that built artifact through `scripts/launch_in_emulator.ps1`

Everything else belongs in repo-local docs under `docs/`.

## Root README Standard

Each platform host repository root `README.md` must be reduced to a short skeleton with only these responsibilities:

- identify the repository
- explain the preferred build command
- explain the canonical emulator launch command
- link to deeper docs

The root README should not contain:

- milestone history
- renderer bring-up notes
- low-level builder helper commands
- generated-core bring-up instructions
- verification matrices
- Docker build details
- proof-of-life notes
- duplicate launcher implementation details that belong in script code

### Required Root README Structure

Each root `README.md` should follow this section order:

1. Title
2. One short intro paragraph
3. `## Build`
4. `## Run In Emulator`
5. `## More Docs`

### Build Section Rules

When a platform supports editor-driven builds, the root README must prefer the shared wrapper command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ..\helengine\artifacts\build-platform.ps1 `
  -Project ..\helprojs\city\project.heproj `
  -Platform <platform-id> `
  -Output ..\helprojs\city\<platform-output>
```

The README may briefly explain what that wrapper does, but it should not inline lower-level Docker or native-build alternatives.

Docker and low-level build flows should move to `docs/Docker.md`.

### Run In Emulator Section Rules

The run section must use the canonical launcher entrypoint and explicit artifact path:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\launch_in_emulator.ps1 `
  -ArtifactPath <path-to-built-artifact>
```

The root README should stay focused on how to invoke the launcher, not on all emulator bootstrap internals.

### More Docs Section Rules

The final section should link to repo-local docs, especially:

- `docs/Docker.md`
- any focused platform-specific deeper notes that remain necessary

## Canonical Launcher Standard

Every platform host repository must expose the same canonical emulator launcher path:

- `scripts/launch_in_emulator.ps1`

Older platform-specific launcher names should be removed rather than preserved as compatibility wrappers.

### Parameter Contract

Every canonical launcher must accept:

- `-ArtifactPath`

`-ArtifactPath` is mandatory. The script must fail fast when it is missing.

This gives every repo the same user-facing launch contract even when the actual built artifact differs by platform.

### Artifact Validation

The launcher may remain platform-aware internally and should validate the artifact shape appropriate for that platform, such as:

- ISO-like disc images
- raw disc images
- RPX executables
- ROM files
- packaged binaries

The platform-specific validation stays inside the script implementation, but the public interface remains the same.

### Required Launcher Responsibilities

Every canonical launcher should own the emulator boot-preparation flow for its platform, including as applicable:

- verifying the artifact exists
- verifying the emulator executable exists
- force-closing an already running emulator process when that is required by the current workflow
- preparing or validating emulator user/profile directories when required
- preparing any required seed files when required
- printing at minimum:
  - artifact path
  - artifact last write time
  - emulator executable path
  - spawned process id

Additional platform-specific output is allowed if it is useful, but the script should keep fail-fast behavior and a clear user-facing contract.

## Documentation Split

Overflow details should move into repo-local `docs/`.

### Required Docs Split

- `README.md`
  - short skeleton only
- `docs/Docker.md`
  - Docker build flow and low-level native build alternatives

Additional documents under `docs/` are allowed when needed for platform-specific detail, but the root README should stay minimal.

### Content That Should Move Out Of Root README

Examples of content that belongs in `docs/` instead of the root README:

- packaged-disc implementation notes
- generated-core build notes
- builder-helper command catalogs
- extended verification expectations
- detailed emulator-profile seeding behavior
- renderer or runtime milestone notes

## Inventory And Normalization Workflow

The implementation should start with a repo inventory rather than immediate bulk rewrite.

Each target platform host repo should be classified for:

- existing root `README.md`
- existing emulator launch script
- current launcher script name
- built artifact type
- emulator name
- whether the preferred shared wrapper build already works
- whether extra docs need to be extracted from the current README

## Rollout Plan

Normalization should proceed in this order:

1. Inventory all target platform host repos
2. Write down the standard contract in one place
3. Normalize the repos that already have both a root README and a launcher first
4. Add or replace launchers in repos that are missing the canonical entrypoint
5. Move overflow documentation from root READMEs into `docs/`
6. Run a final consistency pass across every targeted repo

## Acceptance Criteria

The work is complete when every targeted platform host repo satisfies all of the following:

- root `README.md` exists
- root `README.md` contains only the agreed skeleton
- root `README.md` prefers the shared editor CLI wrapper build when supported
- Docker and low-level build notes are moved to `docs/Docker.md`
- `scripts/launch_in_emulator.ps1` exists
- `scripts/launch_in_emulator.ps1` requires `-ArtifactPath`
- the old platform-specific launcher entrypoint names are removed
- the README run example uses the canonical launcher path and parameter name

## Risks

The main risks in this normalization are:

- accidentally dropping platform-specific launch requirements while simplifying README content
- rewriting launcher names without updating all repo-local references
- inconsistently handling repos that already have richer emulator bootstrap logic

The inventory-first rollout is intended to reduce those risks by forcing per-repo contract verification before rewrite.

## Recommendation

Implementation should follow an inventory-first normalization pass anchored to this standard, then update each target repo toward the same public surface:

- minimal root README
- `docs/Docker.md` for lower-level flows
- `scripts/launch_in_emulator.ps1` with mandatory `-ArtifactPath`
