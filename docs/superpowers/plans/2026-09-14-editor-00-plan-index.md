# Current Editor Refactor Implementation Plans

Planning baseline: `C:\dev\helworks\helengine`, main at `118a0057` plus the working changes present on September 14, 2026. These documents are plans only; no implementation, merge, package publication or deployment was performed.

## Plans

1. [Shared backend boundaries](2026-09-14-editor-01-backend-boundaries.md)
2. [Session lifetimes](2026-09-14-editor-02-session-lifetimes.md)
3. [Asset import services](2026-09-14-editor-03-asset-import-services.md)
4. [Inspector editing](2026-09-14-editor-04-inspector-editing.md)
5. [Unified cook graph](2026-09-14-editor-05-cook-graph.md)

## Execution order and gates

The numbering matches the requested five refactors, not an unconditional execution order.

1. Establish the current source-root/physics/material/tessellation changes as the tested baseline. Preserve all pre-existing changes and portable-release tooling.
2. Implement plan 01's backend boundary. Independently, characterize audio and cooking behavior.
3. Complete plan 05 before the broad modularization migrations: the August design explicitly requires unified cooking, canonical authoring, current-format persistence and stable local publication first.
4. Implement plan 03 using the stable authoring/cook boundary.
5. Implement plan 02 using the resulting project services and injected backend capabilities.
6. Implement plan 04 against the established scene/history owner.

Plans 02 and 03 both change session composition; execute their migration tasks serially. Plans 03 and 05 must not establish separate asset identity or cook-cache policies. Plan 01 preserves current DirectX behavior and does not promise Vulkan GPU picking.

## Relationship to existing designs

The August 26 editor modularization and unified asset cook graph designs remain authoritative for behavior and data contracts. These plans provide current-file migration slices; do not execute the old and new task lists twice. The backend capability contract is an explicit refinement of the accepted September scope.

These plans do not certify that all four August modularization prerequisites have landed. Verify their behavioral completion before executing dependent tasks. The original model/worker constraints are retained as execution requirements, and no agents or independent review sessions were launched to write these documents.

## Validation and delivery

Each task includes owned files, API/lifetime contracts, concrete regression cases, a test filter, migration actions and a commit boundary. Characterization tests pass before moving code; new contract tests fail before implementation. GPU/SDK integration checks are distinct from fake-backend unit tests.

No generated source rewrites, format migrations, compatibility facades or blanket removal of projects. The Nintendo DS debug-font helper is a separately identified cleanup candidate, not a reason to remove platform functionality.
