# Remove Platform-Specific Shared Engine Paths Design

## Goal

Remove platform-specific behavior and naming from shared `engine/helengine*` projects while keeping platform-owned leaf assemblies intact. Shared runtime, editor, and platform libraries should stop hard-coding Nintendo DS, Nintendo 3DS, Windows, and PS2 behavior.

## Scope

In scope:

- Shared runtime behavior in `helengine.core` and `helengine.bepu`
- Shared editor behavior in `helengine.editor`
- Shared platform abstractions in `helengine.platforms`
- Shared packaged-path helpers in `helengine.baseplatform`
- Tests that currently lock in removed platform-specific behavior

Out of scope:

- Deleting platform-owned leaf assemblies such as `helengine.editor.windows`, `helengine.core.windows`, `helengine.directx11`, or `helengine.vulkan`
- Reworking platform definitions, builder contracts, or asset formats beyond what is required to remove the hard-coded shared-engine branches

## Current Problems

The shared engine layer still contains explicit platform branches:

- `DebugComponent` formats different overlay labels for DS only.
- `BepuRuntimeComponentRegistration` changes physics solve scheduling for DS only.
- `EditorBuildQueueItemDocument` and `EditorGeneratedBootScenePreparationService` contain DS and 3DS companion-scene routing rules.
- `ComponentPropertiesView` and `EditorSceneAssetReferenceResolver` prefer the `"windows"` platform when selecting a preview material path.
- `AvailablePlatformProviderResolver`, `EditorSession`, `EditorProjectBootstrapContext`, and legacy material migration directly depend on `WindowsLauncherInstallRootLocator`.
- `EditorSourceBuildWorkspaceLocator` assumes a sibling `helengine-windows` repository.
- `PlatformPackagedAssetPathResolver` special-cases PS2 rooted paths via `Ps2DiscPathResolver`.
- `EditorPlatformPreprocessorSymbolService` emits explicit `PS2_PLATFORM` and `GAMECUBE_PLATFORM` symbols from shared code.

These branches make the shared engine layer reflection-hostile, harder to reason about, and dependent on platform-specific historical contracts that should live in platform-owned code.

## Design

### 1. Remove DS and 3DS shared runtime/editor behavior

- `DebugComponent` will always use the generic overlay labels.
- `BepuRuntimeComponentRegistration` will always create the default BEPU world.
- `EditorBuildQueueItemDocument` will stop injecting DS companion scenes into selected scene lists.
- `EditorGeneratedBootScenePreparationService` will stop deriving DS and 3DS boot-scene mapping tables and will only write the generated boot scene when the generic build path requests it.

This removes DS and 3DS logic from shared code instead of renaming or abstracting the old contract.

### 2. Remove Windows preview preference from shared editor code

- Shared material preview selection will stop preferring `"windows"`.
- Preview resolution will use the active platform when present, otherwise the first supported platform.

This keeps preview selection deterministic without encoding a preferred platform in shared editor code.

### 3. Remove Windows launcher/source-build assumptions from shared code

- `AvailablePlatformProviderResolver` will stop depending on launcher-managed install roots.
- `WindowsLauncherInstallRootLocator` will be removed from shared engine projects.
- Editor bootstrap and scene migration code will stop constructing the Windows locator.
- `EditorSourceBuildWorkspaceLocator` will stop resolving a sibling `helengine-windows` repository from shared code.

The shared engine will rely on development overrides, explicit platform catalogs, and existing platform-definition inputs instead of probing Windows-specific host state.

### 4. Remove PS2-specific rooted-path and gameplay-symbol branches from shared code

- `PlatformPackagedAssetPathResolver` will reject rooted packaged-path policies in shared code instead of dispatching to a PS2-specific resolver.
- `Ps2DiscPathResolver` will be removed from shared base-platform code.
- `EditorPlatformPreprocessorSymbolService` will stop emitting explicit platform identity symbols and will keep only generic capability symbols.

This keeps shared code focused on generic capabilities rather than platform identity.

## Error Handling

- Removed shared functionality should fail explicitly when a caller still depends on the old contract.
- Where shared code can no longer satisfy a platform-specific request, it should throw a clear `InvalidOperationException` rather than silently synthesizing behavior.

## Testing

Focused regression coverage will be updated to assert the new generic behavior:

- Debug overlay tests should validate the generic label output regardless of platform metadata.
- BEPU registration tests should validate that all platforms use the default solve schedule.
- Build-queue and generated-boot-scene tests should stop expecting DS/3DS scene expansion or mapping.
- Material preview tests should validate active-platform or first-supported-platform fallback instead of Windows preference.
- Platform discovery/bootstrap tests should validate development override and built-in fallback behavior without launcher-root injection.
- Packaged-path and generated-core symbol tests should validate the generic failure/capability path after PS2-specific branches are removed.

## Success Criteria

- Shared production code under `engine/helengine.core`, `engine/helengine.bepu`, `engine/helengine.editor`, `engine/helengine.platforms`, and `engine/helengine.baseplatform` no longer contains the targeted DS, 3DS, Windows launcher/source-build, or PS2-specific branches.
- Shared production code no longer references `WindowsLauncherInstallRootLocator`, `helengine-windows`, `Ps2DiscPathResolver`, `PS2_PLATFORM`, `GAMECUBE_PLATFORM`, `NintendoDs`, or `Nintendo3Ds` for the removed contracts.
- Focused regression tests pass for each cleanup batch.
