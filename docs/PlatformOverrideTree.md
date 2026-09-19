# Platform override tree

## Scope paths

An override scope is an ordered path of typed steps beneath a root called Common. Common is the empty path: an
entity or component with no authored path applies everywhere. In core, a path is represented as a
`SceneOverrideScopeStepAsset[]` (each step has a `Kind` and an `Id`); `SceneOverrideScopePath` builds, normalizes,
compares and formats these arrays (`Common`, `Platform`, `Group`, `PlatformBuildConfig`, `Normalize`, `Format`,
`AreEqual`). In the editor, the same path is a typed value, `EditorOverrideScope`, with `EditorOverrideScope.Common`
as the empty path and helpers to walk it (`FromSteps`/`ToSteps`, `Append`, `Parent`, `IsPrefixOf`, `TryGetStepId`,
`IsCommon`, `Depth`).

## Level kinds and level order

A step's kind is `SceneOverrideScopeStepKind`: `Group` (a platform group from `settings/platform-groups.json`),
`Platform` (a project platform id), or `BuildConfig` (a project environment id such as debug or release). Each kind
appears at most once on a path; the Group level may hold a chain of nested group steps, every other level holds
exactly one step. The order in which a path may visit these kinds is chosen per entity and applies to the entity and
its components (`SceneEntityAsset.HasOverrideLevelOrder`/`OverrideLevelOrder`); when an entity has not authored one,
`EditorOverrideLevelOrder.Default` applies: Platform then Build Config, matching today's behaviour. The order may
also be empty, meaning the entity is authored once against Common with no per-platform, group or config variation.
`EditorOverrideLevelOrder.Validate` rejects an order that lists a kind twice, and `IsValidPath` checks that a given
scope is a valid prefix walk of an order.

## Platform groups settings

Group definitions live in `settings/platform-groups.json`, managed by `EditorProjectPlatformGroupsService`. `Load`
seeds a default document (empty group tree, default level order) and writes it back when seeding was needed;
`Read` performs the same seeding in memory but never writes, so read-only callers such as the packager and the
viewport never race to create the file. Mutating a loaded document goes through `AddGroup`, `RenameGroup`,
`DeleteGroup`, `AssignPlatform` and `UnassignPlatform`; `Validate` rejects blank or duplicate group ids, a group id
that collides with a platform id, and a platform assigned to two groups; `FindGroupChain` returns the group ids from
the root down to the group holding a platform. Adding `consoles` at the root, `handheld` beneath it, and assigning
`ps1` to `consoles` and `ds`/`psp` to `handheld` serializes as:

```json
{
  "groups": [
    {
      "id": "consoles",
      "displayName": "consoles",
      "platformIds": [
        "ps1"
      ],
      "children": [
        {
          "id": "handheld",
          "displayName": "handheld",
          "platformIds": [
            "ds",
            "psp"
          ],
          "children": []
        }
      ]
    }
  ],
  "defaultLevelOrder": [
    "Platform",
    "BuildConfig"
  ]
}
```

Properties are camelCase and enum values are written as their names, so `defaultLevelOrder` reads as
`["Platform", "BuildConfig"]` rather than as numbers.

## Resolution

Resolving a target (one platform, one build config) against an entity's authored overrides walks the entity's level
order from Common downward: for each level, the target contributes exactly one node (the group chain containing the
platform for Group, the platform itself for Platform, the environment for Build Config), and the deepest authored
prefix of that walk wins. Because the path for a target is unique, two overrides can never conflict; there is no
priority setting, only the level order itself. For example, an entity authored with `Common: Exists = false` and
`Handheld: Exists = true` exists on every platform in the Handheld group and nowhere else; adding PSP to Handheld
later makes the entity exist on PSP without touching the scene, and a PS1 build (PS1 not in Handheld) still resolves
to the Common value.

## Shared resolver

`EditorOverrideScopeResolver` turns a concrete build target into its override scope path and selects the deepest
authored prefix. `EditorOverrideScopeResolver.Load(projectRoot)` reads `settings/platform-groups.json` and
`settings/platforms.json` without writing either, `ResolveLevelOrder` falls back from the entity's order to the
project default to `EditorOverrideLevelOrder.Default`, `BuildTargetPath` builds the path for a platform/environment
pair, and `TrySelectDeepest` picks the longest matching prefix from a list of overrides. `EditorPlatformBuildScenePackager`
and `EditorPlatformExistenceViewportSyncService` both build their resolver through `EditorOverrideScopeResolver.Load`
and both select overrides through `TrySelectDeepest`, so the packaged build and the editor viewport preview always
agree on which override applies.

## Format versions

This work moves four binary format versions forward together: the scene entity payload is version 9
(`SceneEntityPayloadFormat.SceneEntityPayloadVersion`), the editor asset container is version 25
(`EditorAssetBinarySerializer.CurrentVersion`), the packaged asset container is version 25
(`PackagedAssetBinarySerializer.CurrentVersion`), and the wrapped component override payload is version 5
(`ComponentPlatformOverridePayloadService`'s `WrappedPayloadVersion`). A scene authored against an older version is
rejected outright with the existing "unsupported version, regenerate" message; there is no reader that accepts an
older layout and no conversion path, because the repository's `CurrentFormatOnlySourceContractTests` forbids
persisted-data compatibility code in production source. Recovering an older scene means regenerating it with the
project's own scene generator, not migrating the file in place.

## Generator API

`PlatformSceneAuthoringHelperService` gives the project's scene generator three entry points to author against the
tree instead of one entry per platform: `RestrictEntitySubtreeToScope(rootEntity, scope)` sets `Common: Exists =
false` and `scope: Exists = true` across a subtree, so the subtree exists only beneath that scope and picks up new
platforms added to it later; `ExcludeEntitySubtreeFromScope(rootEntity, scope)` sets `Exists = false` at one scope
while leaving every other authored path untouched; and `SetEntitySubtreeLevelOrder(rootEntity, order)` records the
level order a subtree's overrides are authored against. Together these let a generator say "this exists only on
Handheld" once, against a group, instead of writing one exclusion per platform outside the set.

## Editor UI

The Platform Groups dialog, the multi-level tab strip in the properties panel, and the level-order control are not
part of this work. They are the second implementation plan referenced by the spec's Delivery section; this plan
delivers the model, the format, the resolver, the editing services and the packager, and the existing two-row
properties strip keeps working under the default level order until that second plan lands.
