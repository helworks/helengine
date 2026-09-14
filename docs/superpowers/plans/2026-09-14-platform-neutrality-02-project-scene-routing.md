# Project-owned scene routing Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans task-by-task. Delegation requires the user's applicable opt-in; do not launch an independent review without permission. Steps use checkbox syntax.

**Goal:** Remove DemoDisc identifiers and Nintendo scene inference from shared engine behavior.

**Architecture:** Store explicit per-platform scene routing in .heproj and feed it into generic boot-scene preparation. The generated bootstrap ID remains an engine-owned identifier; game scene names remain project data.

**Tech Stack:** C#/.NET 9, xUnit, existing engine and platform builder contracts.

**Spec:** [Platform neutrality migration design](../specs/2026-09-14-platform-neutrality-design.md)

## Global constraints

Read [the shared design](../specs/2026-09-14-platform-neutrality-design.md). All its compatibility and execution constraints apply. Paths are relative to C:/dev/helworks/helengine unless an absolute sibling-repository path is given. New paths are explicitly marked Create. Revalidate external repository instructions, branch and dirty state before implementing there.

For each task: run the named characterization tests first, add the new failing regression, implement the described change, rerun the focused tests, inspect the diff, then commit only that task's explicit files. Do not treat a zero-test filter as success. Build outputs must use a visible workspace-owned directory.

## File map

- Modify: engine/helengine.projectfile/ProjectFileDocument.cs, ProjectFileJsonModel.cs, ProjectFileReader.cs, ProjectFileWriter.cs.
- Create: engine/helengine.projectfile/ProjectSceneRoutingDocument.cs and ProjectPlatformSceneRoutingDocument.cs.
- Create: engine/helengine.projectfile.tests/ProjectSceneRoutingTests.cs.
- Modify: engine/helengine.editor/managers/project/EditorGeneratedBootScenePreparationService.cs, EditorBuildQueueItemDocument.cs, EditorPlatformBuildGraphRunner.cs.
- Modify: engine/helengine.editor.tests/managers/project/EditorGeneratedBootScenePreparationServiceTests.cs and EditorBuildQueueItemDocumentTests.cs.
- Replace: engine/helengine.core/content/PlatformMenuSceneResolver.cs with Create engine/helengine.core/content/EngineSceneIdentifiers.cs.
- Migrate every GeneratedBootSceneId caller identified by repository search.
- Create: tools/project-scene-routing-migration/README.md describing the explicit project-data migration, including reviewed before/after JSON. No automatic edits to external game projects.

### Task 1: Persist routing without guessing game names

**Data contract:** ProjectFileDocument.SceneRouting is a ProjectSceneRoutingDocument with a Platforms dictionary. Each ProjectPlatformSceneRoutingDocument has BootSceneId (string) and SceneAliases (Dictionary<string,string>). Missing routing preserves ordinary selected-scene order, without DemoDisc-name or suffix inference.

- [ ] Add round-trip tests using arbitrary scene names and platform IDs. Include a v1 document without SceneRouting, malformed aliases, duplicate keys, null records and missing target scenes.
- [ ] Implement model, JSON conversion, reader and writer changes together. Reject blank mappings, cycles and references outside the selected project's scene catalog. Do not change SupportedProjectFormatVersion without reader compatibility tests.
- [ ] Use this fixture:
```json
{"SceneRouting":{"Platforms":{"handheld-test":{"BootSceneId":"SmallMenu","SceneAliases":{"MainMenu":"SmallMenu","City":"CityLow"}}}}}
```
Adapt JSON casing to the current serializer's established convention; reader/writer must agree.
- [ ] Run `dotnet test engine/helengine.projectfile.tests/helengine.projectfile.tests.csproj --no-restore --filter FullyQualifiedName~ProjectSceneRoutingTests`.
- [ ] Commit: `feat: persist explicit project scene routing`.

### Task 2: Consume project routing in all build entry points

**Interface:** Create engine/helengine.editor/managers/project/EditorProjectSceneRoutingResolver.cs with `ProjectPlatformSceneRoutingDocument Resolve(ProjectFileDocument project, string platformId, IReadOnlyList<string> selectedSceneIds)`. Missing configuration produces identity routing using the existing selected-scene boot-order semantics; it never invents game-specific mappings.

- [ ] Add tests for desktop identity routing, explicit handheld aliases, excluded alias destinations, empty selections, and platform IDs unknown to the engine.
- [ ] Update generated boot preparation and queue normalization to use the resolver. Remove the ds/3ds menu-selection and _ds suffix heuristics after the project-data migration is available.
- [ ] Thread the project document through GUI and CLI callers; ensure queue persistence and execution use identical resolved routing.
- [ ] Run `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore -m:1 --filter "FullyQualifiedName~EditorGeneratedBootScenePreparationServiceTests|FullyQualifiedName~EditorBuildQueueItemDocumentTests|FullyQualifiedName~EditorCliBuildRunner"`.
- [ ] Commit: `refactor: resolve boot scenes from project configuration`.

### Task 3: Migrate legacy game data and remove constants

- [ ] Write the exact DemoDisc compatibility mapping into the migration README as project JSON:
```json
{"BootSceneId":"DemoDiscMainMenuHandheld","SceneAliases":{"DemoDiscMainMenu":"DemoDiscMainMenuHandheld"}}
```
List existing scene pairs from the actual project before adding any _ds mappings. Provide a dry-run and backup procedure; do not infer unrelated projects from their names.
- [ ] Obtain the external project's actual path at execution time and its owner authorization before modifying it. Keep the engine removal task incomplete until the affected project configuration and build selections have been migrated.
- [ ] Move only GeneratedBootSceneId = "GeneratedBootScene" into EngineSceneIdentifiers and update its callers. Remove PlatformMenuSceneResolver once no compiled caller remains.
- [ ] Add a source boundary test rejecting DemoDisc identifiers in shared production code; allow them only in migration documents and compatibility fixtures.
- [ ] Compare old/new boot scene routing and selected scene manifests on desktop and DS/3DS builds. Re-run projectfile and boot/queue tests; commit engine and project data separately.

## Completion

An arbitrary project can choose arbitrary scene aliases without engine changes. Existing DemoDisc builds retain equivalent routing through explicit project data. Dirty boot/build files must be reconciled with their current owner before execution.
