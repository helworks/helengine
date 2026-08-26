# Engine Modernization Roadmap Design

## Summary

Helengine will complete five independent modernization projects in a fixed dependency order:

1. make every engine-owned persisted format current-version-only;
2. make editor authoring deterministic, transactional, and available through one public project-scoped API;
3. publish the current source revision and its platform registrations through one local-development workflow;
4. consolidate editor asset cooking into one dependency graph; and
5. decompose the largest editor classes after their behavioral contracts are stable.

Each project has its own design and implementation plan. Each must deliver independently testable software and be committed before the next dependent project starts.

## Global Decisions

- Breaking persisted-format changes are allowed.
- The engine carries no migration, upgrade, alias, or backward-compatibility code.
- Unsupported persisted versions fail explicitly and instruct the user to regenerate the asset or build output.
- Native authored formats embed their asset identity inside the native file.
- External source formats store identity in adjacent `.hmeta` files.
- File-backed references persist asset ID, normalized path, and SHA-256, resolved in that order.
- Duplicate identities are resolved automatically and deterministically without prompting.
- Project tooling consumes public editor APIs and must not reflect into editor application internals.
- Generated source-controlled outputs are deterministic and write nothing on a no-op second run.
- Exact engine-version pins remain authoritative for projects and platform registrations.
- Specifications and implementation plans are written and reviewed by GPT-5.6 Sol.
- Code implementation is performed exclusively by GPT-5.6 Luna workers at `xhigh` reasoning. If such a worker cannot be spawned, implementation stops; Sol must not implement as a fallback.

## Workstreams

### 1. Current-Format-Only Engine

Design: `docs/superpowers/specs/2026-08-26-current-format-only-engine-design.md`

This work removes compatibility readers, converters, aliases, deprecated overloads, compatibility tests, and migration terminology from production code. It establishes one exact version per persisted format and a repository-wide enforcement test.

### 2. Deterministic Editor Authoring

Design: `docs/superpowers/specs/2026-08-26-deterministic-editor-authoring-design.md`

This work creates one project-scoped authoring session that owns reference creation, importing, identity indexing, hashing, native writes, transactions, and repair reporting. It also makes generated writes preserve identity and skip unchanged outputs.

### 3. Local Engine and Platform Publishing

Design: `docs/superpowers/specs/2026-08-26-local-engine-platform-publishing-design.md`

This work gives source development one atomic command that publishes the current engine revision, registers matching platform payloads, validates the installation, and optionally updates a project's exact engine pin.

### 4. Unified Asset Cook Graph

Design: `docs/superpowers/specs/2026-08-26-unified-asset-cook-graph-design.md`

This work replaces duplicated packaging and transform paths with one platform-neutral dependency graph whose leaves delegate platform-specific cooking to platform builders.

### 5. Editor Modularization

Design: `docs/superpowers/specs/2026-08-26-editor-modularization-design.md`

This work extracts project lifecycle, authoring, scene workspace, build coordination, importing, and UI responsibilities from the largest classes without changing established behavior.

## Dependency Gates

Workstream 2 may begin after workstream 1 establishes exact current-format readers. Workstream 3 depends only on exact version semantics and may be prepared independently, but its final verification uses the deterministic authoring fixture. Workstream 4 consumes the public reference-resolution and authoring contracts from workstream 2 and the exact local platform registration from workstream 3. Workstream 5 begins only after the earlier contracts stop moving.

No workstream may preserve a superseded entry point solely to reduce migration work. Callers are changed in the same workstream, and the old entry point is deleted.

## End-to-End Fixture

`C:\dev\helprojs\demodisc` is the end-to-end authored-project fixture.

The fixture must prove:

- two consecutive generation passes produce no source-control changes;
- moving an asset preserves references through asset ID;
- deleting external metadata recovers through path or hash according to the reference order;
- duplicating an asset assigns an independent current ID while selecting references deterministically;
- project tooling uses public editor APIs only;
- the exact engine revision pinned in `project.heproj` has matching registered platform entries; and
- Windows cooking succeeds from the committed project without a temporary version rewrite.

Fixture regeneration is expected after intentional format breaks. No runtime or editor compatibility reader is added to avoid regenerating it.

## Delivery and Review

Each workstream is implemented from its own plan in a dedicated worktree. A fresh GPT-5.6 Luna `xhigh` worker receives only the approved design, the current plan task, repository instructions, and relevant verification commands. Sol reviews specification compliance and code quality after each task but does not write implementation code.

Every workstream ends with:

- focused tests for its contracts;
- the relevant full project test suites;
- `git diff --check`;
- a repository audit for removed surfaces;
- demodisc verification when the workstream touches authoring, persistence, platform discovery, or cooking; and
- a commit whose message describes the completed behavior rather than the migration process.

## Success Criteria

The roadmap is complete when all five workstreams are delivered, no production compatibility code remains, repeated project generation is a no-op, current source revisions can build their exact platform registrations, every target consumes the same cook graph, and the main editor composition root delegates work to focused services.
