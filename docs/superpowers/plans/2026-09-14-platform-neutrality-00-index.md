# Platform neutrality migration plan index

**Status:** Planned; no migration implementation performed by this documentation change.
**Checkout:** C:/dev/helworks/helengine (main at audit baseline b9307947).
**Design:** [Platform neutrality migration design](../specs/2026-09-14-platform-neutrality-design.md).

## Execution order and ownership

| Order | Plan | Primary destination | Completion gate |
|---|---|---|---|
| 1 | [01 — Content paths](2026-09-14-platform-neutrality-01-content-paths.md) | Generic core alias contract; Wii U startup owns mapping | Actual Wii U package loads the unchanged material path/alias |
| 2 | [02 — Project scene routing](2026-09-14-platform-neutrality-02-project-scene-routing.md) | .heproj configuration and generic scene resolver | DemoDisc names and DS scene inference removed from shared behavior; affected project migrated |
| 3 | [03 — Overlay settings](2026-09-14-platform-neutrality-03-overlay-settings.md) | Core settings; PS2/3DS startup supplies values | Same overlay output without platform-name branches |
| 4 | [04 — Rendering adapters](2026-09-14-platform-neutrality-04-rendering-adapters.md) | Windows graphics adapter and explicit host capabilities | Shared editor builds without concrete backend references |
| 5 | [05 — Cook policies/codecs](2026-09-14-platform-neutrality-05-cook-policies.md) | Neutral builder contracts; DS builder implementation | Production cook preserves bytes/paths and loads target codecs through builder boundary |
| 6 | [06 — Host filesystems](2026-09-14-platform-neutrality-06-host-filesystems.md) | Neutral contract plus Windows/Linux adapters | Real native contract suites pass on Windows and Linux x64 |
| 7 | [07 — Font ownership/guards](2026-09-14-platform-neutrality-07-font-and-guards.md) | DS tooling if needed; architecture tests | Unused helper removed or real caller migrated; automated boundaries enforced |

Plan 01 and plan 03 are independent of the editor renderer work. Plan 02 must finish project migration before removing legacy routing. Plan 05's manifest changes depend on plan 02; shared shader selection depends on plan 04. Plan 06 modifies host composition after plan 04. Plan 07 guards can be introduced incrementally after their corresponding migration, but final acceptance follows all six.

No parallel workflow is implied or launched by these documents. Each repository gets its own reviewed commit; do not cherry-pick shared-engine code into platform repositories.

## Preflight for implementation

- [ ] Verify cwd, git root, branch, HEAD, worktrees, AGENTS.md and working diff in every affected repository.
- [ ] Read the shared design and the individual plan.
- [ ] Preserve unrelated dirty geometry, shader serializer, boot/build code and release scripts seen during the audit. Re-read current versions; never restore baseline files over current work.
- [ ] Identify exact consumers in external game/platform repositories before modifying them.
- [ ] Record builder/engine compatibility ranges and the release order for contract changes.
- [ ] Capture deterministic before-migration fixtures; keep outputs in a visible workspace build directory.
- [ ] Add contracts, update platform implementations and real consumers, validate, then remove the shared special case.
- [ ] Run the smallest meaningful tests per task; native/toolchain validation is an additional acceptance gate where specified.
- [ ] Commit only explicit task files. Publishing, deployment and pushing require separate user direction.

## Progress and evidence

| Plan | Status | Verification |
|---|---|---|
| 01 | Planned | Not run; documentation only |
| 02 | Planned | Not run; documentation only |
| 03 | Planned | Not run; documentation only |
| 04 | Planned | Not run; documentation only |
| 05 | Planned | Not run; documentation only |
| 06 | Planned | Not run; documentation only |
| 07 | Planned | Not run; documentation only |

## Relationship to earlier editor refactors

The 14 editor-refactor commits established some interfaces/services; they did not complete platform neutrality. These plans require actual production rewiring, native/platform verification and removal of obsolete code paths. They do not require unrelated completion of the entire editor cook graph or session service-graph migration.

## Scope boundaries

Do not delete working renderer projects, erase platform metadata, change output formats, or generalize game-specific behavior by merely renaming a class. No code changes, platform build outputs or external project edits accompany these plans.
