# Depth-ordered 2D Implementation Plan

**Goal:** Remove per-drawable 2D Draw Order everywhere and use physical depth with stable hierarchy ties.

**Architecture:** One core depth/hierarchy comparator serves 2D queues and pointer hit precedence. Screen-space queues sort current transforms before consumption; world-space transparent submissions sort for each camera. Editor widgets express stacking through their existing entity transforms and hierarchy.

**Tech Stack:** C#/.NET 9, DirectX11/Vulkan, engine native code generation.

**Spec:** ../specs/2026-09-28-depth-ordered-2d-design.md

## Ownership and tasks

- [ ] Root: core drawable contract, RenderList2D/comparator, pointer resolver, queue cache behavior, transparent frame ordering, core rendering/input regression tests.
- [ ] UI worker: all core 2D components and editor UI construction; remove per-drawable order, migrate stacking to actual entity Z/hierarchy, update UI/stack tests.
- [ ] Persistence worker: scene serialization and compatibility, preview source order metadata/copies, native/runtime consumers and non-UI remaining references, regression tests.
- [ ] Root integration: targeted managed suites, editor/backend builds, bounded native generation checks, no active RenderOrder2D references except explicit legacy read compatibility/tests/docs.

## Review focus

- Equal Z must not fall back to registration order after reparenting or enable toggles.
- Static text cache must invalidate when only depth/hierarchy changes.
- Hit precedence must match visual composition even with transparent input shields.
- UI layout updates must not overwrite new Z placements.
- Legacy unknown-property preservation must not restore the removed field on save.

Each owner adds or adjusts behavioral regression tests before production changes when feasible, records failures and focused green checks under the workspace build directory, and never edits another owner's files without coordination. The user's current explicit scope approval authorizes execution; no additional approval loop or commit is requested.
