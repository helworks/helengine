# Platform override tree

**Status:** model/format/build plan implemented 2026-09-19; editor UI plan pending
**Scope:** helengine editor, core scene asset format, and the platform scene packager. Project-side generator changes follow in a separate step once this lands.

## Problem

Entity and component overrides are authored per platform. An entity carries one existence override per platform id, one transform override per platform id, and so on, with an optional environment nested under each platform. There is no way to say "this exists on handhelds" once; it has to be said once per handheld.

The demodisc scene generator papers over that with `PlatformSceneAuthoringHelperService.RestrictEntitySubtreeToPlatforms`, which reads the project's supported platform list and writes one `Exists = false` override per platform outside the requested set. That snapshot is taken at generation time. On 2026-09-18 the PS1 build of `cube_test` received both Nintendo DS screen rigs, each with its own sun and cube, because PS1 was not a supported platform when the scene was generated and so has no override at all. Fourteen platforms carried an explicit exclusion; the fifteenth defaulted to "exists". Any platform added after a scene is generated silently receives everything.

The fix is not more per-platform entries. It is letting an override target a group, storing the group rather than its expansion, and resolving group membership when the scene is built.

## Decisions

1. **Overrides are authored on a tree, not a list.** Common is the root. Beneath it are typed levels: **Group**, **Platform**, **Build Config**. Each kind appears at most once on a path, in an order the user chooses per entity. Groups nest, so the Group level is a chain of nodes rather than one.

2. **Resolution walks the path.** For a concrete target, one platform in one build config, each level contributes exactly one node: the group chain that contains the platform, the platform itself, the config. Walk from Common downward and the deepest node carrying an override wins. The path is unique for a target, so two overrides can never conflict and there is no priority setting. The level order the user chose is the only tiebreak between kinds, and it is deliberate.

3. **An override is keyed by its path.** `ps1` beneath `debug` and `ps1` at the first level are different scopes. Today's `EditorOverrideScope` is a fixed two-step path, platform then environment; this generalises it to an ordered list of typed steps.

4. **Build Config is the project's existing environments.** Debug and release as they are today. No new concept.

5. **Group definitions are project settings.** They live beside `settings/platforms.json` and are edited in a tree-editor modal. A group id may not equal a platform id; the modal refuses it, so one id namespace is safe.

6. **Level order is per entity, with a project default.** The order applies to the entity and its components. New entities take the project default, which ships as Common → Platform → Build Config so the common case costs nothing and matches today. The order may also be empty: an entity with just Common is authored once and has no per-platform, group or config variation at all. Its tab strip shows only Common, and resolution returns the Common value for every target.

7. **Reordering an entity with authored overrides relocates and drops explicitly.** Every path that has an equivalent under the new order is relocated. Every path that does not is listed, and the user confirms the drop before it happens. Silent remapping is not allowed.

8. **Existing scenes are regenerated, not converted.** The repository's `CurrentFormatOnlySourceContractTests` forbids persisted-data compatibility paths in production source: no old-layout readers, no version ranges, no conversion helpers. The format version moves and the reader rejects the previous one with the existing "regenerate" message. The demodisc scenes are generator output, so regeneration is a build step, not a data loss. Under the default order every regenerated override lands on the same path it had before, so authored intent is unchanged.

## Model

```
Common
├── Group chain        zero or more nested group nodes containing the platform
├── Platform           exactly one
└── Build Config       exactly one environment
```

Any prefix of the entity's chosen order is a valid place to author. Example orders and what an override at each level means:

| Order | Meaning of an override at each level |
|---|---|
| Common → Platform → Build Config | today's behaviour; config beats platform |
| Common → Group → Platform → Build Config | groups first; a family-wide rule, refined per platform, refined per config |
| Common → Build Config → Platform | config split first; platform beats config |
| Common → Group | form factor only; never per platform |

The generator's "exists only on handhelds" becomes two overrides: `Common: Exists = false`, `Handheld: Exists = true`. Adding PSP to Handheld later applies to every scene that says this, without regenerating any of them.

## Resolution

Inputs: an entity, its level order, the project's group tree, a target platform id, a target environment id.

1. Build the target path by taking, for each level in the entity's order, the node that matches the target. For Group that is the chain from the outermost group containing the platform to the innermost. For Platform it is the platform. For Build Config it is the environment.
2. Find every authored override whose path is a prefix of the target path.
3. The longest such prefix wins. Common is the empty prefix and always applies as the fallback.

Each level yields exactly one node, so step 2 never yields two overrides of equal length that differ. If the group tree is inconsistent, a platform in two groups or a group id that collides with a platform, the group settings are invalid and the build fails naming the offending ids. That is a settings error, not a scene error.

This runs in two places and must agree: `EditorPlatformBuildScenePackager` when writing a platform's scene, and the editor's existence, transform and component editing services when previewing. Both use one shared resolver.

## Storage and format

**Group settings.** `settings/platform-groups.json`, loaded by an `EditorProjectPlatformGroupsService` shaped like `EditorProjectPlatformsService`: `Load` normalises and seeds, `Save` writes. A group is `{ id, displayName, platformIds, children }`. A platform may appear in exactly one group across the whole tree, or in none, in which case its group chain is empty.

**Override path.** `EditorOverrideScope` becomes an ordered list of steps, each `{ kind, id }` with kind in Group, Platform, BuildConfig. The step kind and step record live in `helengine.core` so the runtime reader and the editor share them. `SceneEntityPlatformExistenceOverrideAsset`, `SceneEntityPlatformTransformOverrideAsset` and `SceneEntityPlatformComponentOverrideAsset` each carry the path as a step array in place of the current `PlatformId` and `EnvironmentId` pair. Common is the empty path; an existence override may sit on it, which is how "exists nowhere except where a deeper node says otherwise" is expressed.

**Level order.** Stored on the entity's `EntitySaveComponent` and serialised with the entity as a presence flag plus a kind array. Absent means the project default, which is recorded in `settings/platform-groups.json` and ships as Platform → Build Config.

**Versioning.** `PackagedAssetBinarySerializer.CurrentVersion` and `EditorAssetBinarySerializer.CurrentVersion` are 24 and `SceneEntityPayloadVersion` is 8. All move by one. The component override wrapped-payload version moves by one as well. Each reader keeps rejecting every other version with its existing message; there is no reader for the previous layout.

## Editor

**Modal.** A "Platform Groups" dialog on `EditorDialogBase`, opened from the same menu as Build Settings. It shows the group tree: add a group at the root or under a group, rename, delete, and move platforms between groups. Platforms not in any group are listed under an "Ungrouped" heading. Saves on every change. Refuses a group id that equals a platform id or another group id.

**Properties panel.** The override tab strip already shows one row of tabs and drives existence, transform and component editing. It becomes one row per level of the selected entity's order. The first row shows Common and the children of Common for the first level: groups if the first level is Group, platforms if it is Platform, configs if it is Build Config. Selecting a tab reveals the next row with that node's children. The existence row, transform fields and component fields edit the currently selected path.

**Level order control.** A small control beside the tab strip shows the entity's order and lets the user add a level, choosing from the kinds not yet used, or remove one. While the entity has nothing authored below Common the order changes freely. Otherwise the control opens a confirmation listing what migrates and what drops, and applies only on confirm.

## Reordering

Changing an entity's level order maps each authored path step by step: a step whose kind exists in the new order keeps its id at the new depth; a step whose kind was removed has no home. A path can be relocated when every step has a home and the resulting path is a valid prefix of the new order.

Paths that cannot be relocated are dropped only after the user confirms a list that names each one by its meaning, "Exists = false on ps1 under debug", not by an internal id. The words "migrate" and "upgrade" are reserved by the source contract test for persisted-data conversion and must not appear in this feature's production source; the operation is called relocation.

## Delivery

Two implementation plans. The first delivers the model, the format, the resolver, the editing services and the packager, so group-scoped overrides can be authored through `PlatformSceneAuthoringHelperService` and resolved at build time while the existing two-row properties strip keeps working under the default order. The second delivers the editor UI: the Platform Groups dialog, the multi-level tab strip and the level-order control.

## Build

The packager resolves each entity against the target platform and selected environment using the shared resolver and the project's group settings. Because the settings live under `settings/`, they are already staged into every platform build alongside `platforms.json`; no builder changes are needed. Existence overrides are still stripped from the packaged scene after resolution, as today.

## Generator follow-up

Once this lands, `RestrictEntitySubtreeToPlatforms` and `ExcludeEntitySubtreeFromPlatforms` stop expanding to per-platform entries and instead write the two-entry form above against a named group. The demodisc generator then authors handheld rigs and UI exclusions against groups. That is a project-side change and is not part of this spec.

## Out of scope

- Multiple independent trees per project. One tree; the freedom is in the per-entity level order.
- Overlapping group membership or tag-style grouping. Ruled out deliberately: it reintroduces conflicts.
- Runtime resolution. The runtime never sees groups; packaging resolves them.
- Changes to any platform builder repository.

## Verification

- Resolver tests: unique path for every target; deepest prefix wins; Common fallback; every order permutation; nested group chains; inconsistent group settings rejected with both ids named.
- Serialization tests: round trip of the new path including the empty Common path and a nested group chain; the previous payload version is rejected with the existing message shape.
- Reordering tests: a path that relocates, a path that drops, and the order change refused without confirmation when drops exist.
- Editor tests for the existing existence, transform and component services against the new scope, so the properties panel and the packager cannot disagree.
- An end-to-end check on the demodisc `cube_test` scene: after the generator follow-up, a PS1 build receives one camera rig and one sun.
