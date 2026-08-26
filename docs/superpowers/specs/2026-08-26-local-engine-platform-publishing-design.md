# Local Engine and Platform Publishing Design

## Summary

Helengine source development will provide one atomic command that publishes the current engine revision and registers every selected local platform against that exact revision. The command may also update one project's `requiredEngineVersion` after the installation has been validated.

Release projects continue to require exact engine versions. This design does not weaken matching rules or introduce a floating development alias. It makes exact local revisions easy to produce and consume correctly.

## Goals

- Derive one exact engine version from the current source revision.
- Publish the editor/runtime payload required to launch and build that revision.
- build or locate selected platform builder assemblies and source payloads;
- write matching installation entries atomically;
- validate that every registered path and builder can load before publication succeeds;
- optionally update a project's exact engine pin only after successful validation;
- make repeated publication of an unchanged revision idempotent;
- preserve other installed revisions and platforms; and
- provide a machine-readable result for scripts and tests.

## Non-Goals

- Allowing a project pinned to one revision to use a different revision.
- Treating semantic-version compatibility ranges as installation identity.
- Publishing remote release artifacts.
- Installing third-party SDKs or accepting licenses.
- Building a project as part of publication.
- Deleting older local installations automatically.

## Problem

Projects carry an exact `requiredEngineVersion`, and platform discovery filters entries by exact string equality. During source development, a new engine commit can therefore launch but expose no buildable platforms until matching platform entries are created manually. This happened when demodisc was pinned to `1.0.0+fb94b93...` while the local platform manifest contained only an older engine revision.

The exactness is correct. The missing piece is a first-class publication workflow that updates the engine payload, platform payloads, manifest, and optional project pin as one validated operation.

## Command Contract

Add the canonical PowerShell entry point:

```powershell
scripts/publish-local-engine.ps1 \
  -EngineRoot C:\dev\helworks\helengine \
  -Configuration Debug \
  -Platforms windows,ps2 \
  -Project C:\dev\helprojs\demodisc\project.heproj
```

Parameters:

- `-EngineRoot`: optional; defaults to the script's repository root.
- `-Configuration`: `Debug` or `Release`; defaults to `Debug`.
- `-Platforms`: optional platform ID list; defaults to platform plugin manifests discoverable from the source checkout.
- `-UserSettingsRoot`: optional; defaults to the engine source checkout's `user_settings` root for local development.
- `-PublishRoot`: optional; defaults to a stable revision/configuration root beneath `builds/helengine/local-publish`.
- `-Project`: optional project file whose exact pin is updated after publication succeeds.
- `-NoBuild`: validates and registers already-published payloads without invoking builds.
- `-Force`: rebuilds payloads even when the validated publication receipt matches the requested inputs.

No parameter permits publishing dirty or ambiguous version identity silently.

## Exact Version Identity

Add a root `engine-version.json` document containing the source checkout's base product version:

```json
{
  "version": "1.0.0"
}
```

One shared `EngineSourceVersionResolver` determines the exact version by combining:

- the `version` value from root `engine-version.json`; and
- the full lowercase Git commit ID.

Clean example:

```text
1.0.0+fb94b93fbfd8c1e895c910a57903970c0e303900
```

Dirty source publication is rejected because the same commit ID would not uniquely identify its bytes. Supporting dirty publication is outside this design.

The resolver is shared by the script, engine assembly metadata generation, launcher installation detection, and tests. Version construction is not duplicated in PowerShell and C#.

## Publication Layout

Stable output is keyed by exact version and configuration:

```text
<PublishRoot>/
  <engine-version>/
    <configuration>/
      engine/
      platforms/
        <platform-id>/
      publication.json
```

`engine` contains the published editor/runtime host. Each platform directory contains or points to its builder assembly, player source root, generated-core roots, codegen tool, and plugin manifest according to the existing platform contract.

`publication.json` records:

- schema version;
- exact engine version;
- source root and commit ID;
- configuration;
- selected platform IDs;
- resolved payload paths;
- content hashes of builder assemblies and plugin manifests; and
- completion timestamp and terminal status.

The timestamp is diagnostic and is not part of identity or up-to-date comparison.

## Atomic Publication Flow

The command:

1. canonicalizes and validates all roots;
2. acquires one publication lock keyed by source root, version, and configuration;
3. resolves the exact source version;
4. checks an existing successful receipt and returns unchanged when every recorded hash and path still validates;
5. builds into a sibling staging directory;
6. loads each platform plugin and builder contract from staging;
7. validates every declared payload path;
8. writes a staged installation-manifest update;
9. atomically publishes the staged revision directory;
10. atomically replaces `user_settings/platforms.json` with entries for the new exact revision while retaining unrelated entries;
11. reloads the new entries through `AvailablePlatformProviderResolver` and confirms all selected platforms are installed;
12. updates the optional project pin through `ProjectFileWriter`; and
13. writes and prints a structured success result.

If any stage before manifest replacement fails, the existing installation remains unchanged. If project-pin update fails, the published installation remains valid and the command reports that publication succeeded but project update failed; it does not corrupt the project file.

## Manifest Update Rules

The installation store gains an atomic writer. Entries are keyed by exact engine version plus platform ID.

- Re-publishing the same key replaces that key after validation.
- Other platform IDs for the same revision remain.
- Other engine revisions remain.
- Entries are written in ordinal engine-version then platform-ID order.
- Duplicate keys are invalid in the persisted document.
- All stored paths are absolute or explicitly relative to the manifest root under the existing resolution contract.

The resolver continues exact string matching. There is no fallback to the nearest commit or semantic version.

## Project Pin Update

When `-Project` is supplied, the command reads the project with `ProjectFileReader`, changes only `RequiredEngineVersion`, and writes it atomically with `ProjectFileWriter`. The update occurs only after the selected platform set has been resolved successfully for the exact version.

The command prints the previous and current pin. It does not regenerate project assets, change supported platforms, or launch a build.

## Diagnostics and Result

Human output includes:

- exact engine version;
- clean or dirty source status;
- publication root;
- each selected platform and validated builder path;
- installation manifest path;
- optional project pin change; and
- whether the run built, republished, or reused an existing publication.

The final stdout line is compact JSON with `status`, `engineVersion`, `publishPath`, `manifestPath`, `platforms`, `projectPath`, and `projectUpdated`. Scripts consume this line rather than parsing prose.

## Testing Strategy

### Version tests

- clean revision produces the base version plus full commit ID;
- different commits produce different versions;
- dirty source is rejected;
- a missing, malformed, or non-semantic `engine-version.json` value is rejected; and
- PowerShell and C# callers use the same resolver result.

### Manifest tests

- atomic writer inserts one exact version/platform key;
- replacement preserves unrelated keys;
- output ordering is deterministic;
- duplicate keys and invalid payloads fail; and
- resolver loads the newly written exact entries.

### Publication tests

- successful staging publishes engine and selected platforms;
- builder load failure changes neither publication nor manifest;
- identical second run is unchanged;
- `-Force` republishes and remains valid;
- concurrent publication serializes through the lock; and
- an interrupted staging directory is ignored and safely prunable.

### Project tests

- a valid publication updates only `requiredEngineVersion`;
- failed publication leaves the project untouched;
- malformed projects are reported without damaging the published installation; and
- the updated project resolves every selected platform through normal editor bootstrap.

### End-to-end test

Publish the current engine and Windows platform, update a disposable demodisc project copy, then invoke the canonical build command without a temporary version rewrite. The build must discover the exact platform entry and reach the platform builder.

## Success Criteria

- One command publishes the current engine and matching selected platforms.
- Exact project/platform version matching remains strict.
- A project pin is never updated to an unvalidated installation.
- Repeating an unchanged publication performs no rebuild or manifest churn.
- Concurrent or failed publications cannot leave a partially registered revision.
- Demodisc cooks from its committed exact pin without a temporary project file.
