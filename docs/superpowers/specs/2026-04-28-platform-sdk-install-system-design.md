# Platform SDK Install System Design

## Summary

This change designs a launcher-managed platform install system that can prepare engine versions for multiple target platforms without reinstalling the same SDKs or platform support packages repeatedly.

Today the launcher only tracks locally discovered engine builds. It has no model for platform SDKs, platform builders, shared platform support files, or the dependency relationship between an engine version and the platform toolchains it needs. That is not enough for a future installer that must understand platform requirements before downloading an engine version, compare them against local installs, and reuse shared dependencies across engine versions.

The new design introduces a central platform catalog, a local artifact registry, and an install planner. Engine binaries, platform SDKs, platform builders, and shared platform files become separate managed artifact types. The launcher can then show users which platform support is already available, which pieces can be reused, and which downloads are still required.

## Goals

- Let the launcher understand platform requirements before downloading an engine version.
- Support installing multiple platforms for a single engine version.
- Let users choose which platforms to install for an engine version.
- Reuse SDKs across engine versions when the required SDK identity matches exactly.
- Reuse platform builders across engine versions when the required builder identity matches exactly.
- Reuse shared platform files across engine versions when the required platform-files identity matches exactly.
- Keep engine installs separate from shared platform toolchains.
- Persist only the minimal launcher portability data in the Windows registry.
- Keep the authoritative install manifests on disk under the managed install roots.
- Allow the shared toolchain root to be user-configurable.
- Keep the catalog integration abstract so a mock source can be used before Sweet Square network support exists.

## Non-Goals

- No real network download implementation in this change.
- No Sweet Square-specific protocol decisions in this design.
- No attempt to make SDK, builder, or platform-file versions fuzzy or range-based.
- No automatic deletion of shared artifacts during uninstall without asking the user first.
- No registry-heavy design that stores full install manifests outside the managed folders.

## Current Problem

The current launcher model is engine-centric and shallow:

- engine installs are tracked locally,
- platform support requirements are not modeled,
- SDKs are not first-class installable artifacts,
- platform builders are not first-class installable artifacts,
- there is no install planner,
- the launcher cannot compare local toolchains against an engine version before download.

That creates several practical problems:

- the launcher cannot tell users what platform support an engine install needs,
- repeated installs risk duplicating large SDK payloads,
- uninstall logic has no way to reason about shared dependencies,
- a portable launcher has no minimal locator contract for rediscovering existing installs.

## Proposed Design

### 1. Separate Engine Installs From Shared Toolchains

The launcher should treat engine binaries and platform toolchains as different managed domains.

Engine installs remain engine-version-specific.

Shared toolchains live under one configurable root with managed subfolders:

- `sdks/`
- `platform-builders/`
- `platform-files/`

This gives the launcher one place to reuse shared platform dependencies without tying them to a single engine install.

### 2. Introduce A Central Platform Catalog

The launcher should learn platform requirements from a central catalog before downloading an engine version.

That catalog is the source of truth for:

- available engine versions,
- supported target platforms for each engine version,
- the exact SDK version required by each platform,
- the exact platform-builder version required by each platform,
- the exact shared platform-files version required by each platform.

The initial implementation can use a mocked local catalog source. The launcher should depend on an interface such as `IEnginePlatformCatalog` so the future Sweet Square integration can replace the mock without changing the planning layer.

### 3. Model Shared Platform Dependencies As Independent Artifacts

Every reusable dependency should have a stable identity:

- `ArtifactKind`
- `ArtifactId`
- `Version`

There are three shared artifact kinds in this design:

- `Sdk`
- `PlatformBuilder`
- `PlatformFiles`

Reuse rules are strict:

- same kind + same id + same version means reuse,
- different version means a different install,
- the launcher should not try to treat near matches as compatible.

This gives the engine installer deterministic behavior and avoids hidden compatibility guesses.

### 4. Store Only Locator Paths In The Windows Registry

The launcher will be portable, so it should not rely on its own install directory for persistence.

The Windows registry should store only the minimum locator data needed for a fresh launcher copy to rediscover managed content:

- `engine install root`
- `shared toolchain root`

The actual install manifests should not live in the registry. They should live on disk under those roots, next to the managed content they describe.

That keeps the registry small and stable while ensuring the real install state travels with the managed folders.

### 5. Keep Authoritative Install Manifests On Disk

The launcher should persist its detailed install state as files under the managed roots.

That state should include:

- installed engine versions,
- installed shared artifacts,
- engine-to-platform bindings,
- install timestamps and paths.

This allows a new launcher binary to rediscover the complete environment once it knows the root folders from the registry.

### 6. Add An Install Planner

Before any install begins, the launcher should compute a plan from three inputs:

- the selected engine version,
- the user-selected platforms,
- the current local install registry.

The planner should produce clear categories:

- engine files that must be installed,
- shared artifacts already installed and reusable,
- shared artifacts missing and required,
- invalid or conflicting local state that blocks installation.

This planner is the main system boundary that keeps install logic predictable and testable.

### 7. Let Users Choose Platforms Per Engine Install

Installing an engine version should not imply installing every possible platform automatically.

The launcher flow should be:

1. Choose engine version.
2. Choose one or more target platforms.
3. Review the computed plan.
4. Confirm installation.

This keeps downloads intentional and avoids filling the machine with unused toolchains.

### 8. Ask Before Removing Unused Shared Artifacts

When a user uninstalls an engine version, the launcher should remove the engine files and delete that engine's platform bindings.

After that, it should check whether any shared artifacts became unreferenced.

If shared artifacts are no longer referenced, the launcher should ask the user whether to remove them. It should not silently delete them and it should not silently keep them without telling the user.

This matches the fact that shared SDKs, builders, and platform files may still be valuable for future installs even when they are not currently referenced.

## Data Model

### Catalog Model

The central catalog should describe engine versions and per-platform requirements.

Suggested shape:

- `EngineCatalogEntry`
  - `EngineVersion`
  - `Platforms`
- `PlatformRequirement`
  - `PlatformId`
  - `SdkArtifact`
  - `PlatformBuilderArtifact`
  - `PlatformFilesArtifact`

Each artifact reference should carry:

- `ArtifactKind`
- `ArtifactId`
- `Version`

### Local Registry Model

The local registry should describe what is installed and how installed engines reference shared artifacts.

Suggested records:

- `InstalledArtifact`
  - `Kind`
  - `Id`
  - `Version`
  - `InstallPath`
  - `InstalledAt`
- `InstalledEngine`
  - `EngineVersion`
  - `InstallPath`
  - `InstalledAt`
- `InstalledEnginePlatformBinding`
  - `EngineVersion`
  - `PlatformId`
  - `SdkArtifactKey`
  - `PlatformBuilderArtifactKey`
  - `PlatformFilesArtifactKey`

This gives uninstall and reuse logic exact reference information instead of making it infer relationships from folder names.

## Workflow

### Install Flow

1. The launcher loads engine/version platform requirements from the central catalog.
2. The user chooses an engine version.
3. The user chooses which platforms to install.
4. The launcher computes the install plan.
5. The launcher shows which artifacts are already installed, which will be reused, and which must be downloaded.
6. The user confirms.
7. The launcher installs missing engine and shared artifacts into their managed roots.
8. The launcher writes the updated on-disk manifests and bindings.

### Uninstall Flow

1. The user chooses an installed engine version to remove.
2. The launcher removes the engine files and that engine's bindings.
3. The launcher computes which shared artifacts are no longer referenced.
4. The launcher asks whether the user wants to remove those now-unused shared artifacts.
5. The launcher updates the on-disk manifests accordingly.

### Reuse Behavior

If another engine version requires the same SDK, builder, or platform-files identity, the launcher reuses the existing install instead of downloading it again.

This applies independently to each shared artifact type.

## Error Handling

The launcher should fail clearly rather than trying to paper over invalid install state.

Expected failure cases include:

- malformed or incomplete catalog entries,
- missing artifact identity fields,
- local manifests that reference missing install paths,
- registry locator paths that point to invalid roots,
- conflicting local state where on-disk content and manifests disagree.

Behavior rules:

- catalog validation failures block planning before download begins,
- invalid local registry state should be surfaced as a repairable launcher error,
- missing registry locators should fall back to defaults only when the user has never chosen custom roots,
- shared artifact reuse should require exact identity matches.

## UI Direction

The launcher `Engines` experience should become a small install manager rather than a flat list of local builds.

It should be able to show:

- available engine versions from the catalog,
- platform availability per engine version,
- whether each platform is already satisfied locally,
- whether installing a platform will reuse existing artifacts or download new ones.

The install surface should present:

- engine selection,
- platform selection,
- plan review,
- install confirmation.

The uninstall surface should present:

- the engine being removed,
- any newly unused shared artifacts,
- explicit keep/remove choices.

## Testing

Add focused coverage for:

- catalog parsing and validation,
- install-plan computation for selected platforms,
- reuse of identical SDK, builder, and platform-files artifacts,
- separation of engine installs from shared toolchain installs,
- registry locator persistence and rediscovery behavior,
- uninstall prompts for newly unreferenced shared artifacts,
- launcher UI behavior for platform selection and plan rendering.

## Implementation Notes

- Keep the catalog access behind an interface so the mocked source can later be replaced by Sweet Square.
- Keep registry interaction behind a small service instead of scattering registry calls through launcher views.
- Keep planning logic outside UI classes so the launcher can test install decisions without rendering controls.
- Prefer explicit artifact keys over path-based inference when connecting engines to shared platform dependencies.
