# Cooked console viewports implementation plan

> **For agentic workers:** Use superpowers:executing-plans inline in the real checkout. No worktrees.

**Goal:** Prepare every viewport at cook time on PS1, N64, Dreamcast, GameCube, PS2 and Xbox, excluding dynamic layout originals and resize listeners from their players.

**Architecture:** The platform runtime contract selects Cook-only generation and packaging together. Complete Cook and Dynamic source implementations expose the same component API, selected before C++ generation; editor and other platforms retain their current implementation. Fixed platform targets come from the backend's actual framebuffer configuration. Serialized authoring remains unchanged; the detached packaged scene receives Cook metadata and transformed geometry.

**Tech Stack:** C#, existing platform builders and C# to C++ pipeline, PS1 MIPS toolchain.

**Spec:** Helena approved this architecture in the conversation on 2026-10-21. This document records that approved specification and its execution plan.

## Global Constraints

- Exactly the six listed home consoles; leave Windows, editor, handhelds and later consoles unchanged.
- No runtime resize or retained original UI values in Cook-only players. Resolution changes require scene reload.
- Preserve component API and all scenes/menu buttons; reject unresolved cook targets and incompatible metadata explicitly.
- Do not mutate authoring scenes or silently fall back to Dynamic.
- Build from the real checkout, preserving other changes; fixed temporary/output roots under C:\temp.

## Review Focus

- Nested viewports and camera rectangles must use their own target and remain independent boundaries.
- Standalone ReferenceCanvasFit owners must also be cooked, without retaining the dynamic cache through that older component.
- Metadata and target mismatches must fail before publishing the scene.
- Runtime-created children must retain supported single-pass preparation without enabling resolution resize.
- Generation/packaging cache identities must change together with the contract and selected symbols.

### Task 1: Platform policy and packaging

**Files:** RuntimeGenerationContract.cs; EditorPlatformPreprocessorSymbolService.cs; EditorWindowsBuildScenePackager.cs; ViewportLayoutCookProcessor.cs; six platform definition factories and asset builders; ViewportLayoutCookTests.cs.

**Interfaces:** Add optional `requiresCookedViewportLayout` contract argument/property. `Process(SceneAsset, PlatformLayoutTarget, bool requiresCookedViewportLayout = false)` enforces the contract in detached components. The selected symbol is `HELENGINE_RUNTIME_COOKED_VIEWPORT_LAYOUT`.

- [x] Add and run tests for mandatory Cook on every viewport, standalone fit, unchanged optional policy and missing targets.
- [x] Implement the contract, symbol, packager propagation and six verified target providers.
- [x] Run focused packaging and policy tests; inspect cache fingerprints.

### Task 2: Generation-selected runtime implementations

**Files:** ViewportComponent.cs / ViewportComponent.Cooked.cs; ReferenceCanvasFitComponent.cs / ReferenceCanvasFitComponent.Cooked.cs; dynamic cache/state sources; cooked runtime tests.

**Interfaces:** Preserve existing public/internal layout APIs and serialized properties. Whole-file source selection isolates dynamic implementation and original-value records.

- [x] Add and run cooked runtime tests for metadata validation, zero originals, unchanged prepared geometry and no resize subscriptions.
- [x] Implement Cook-only component variants and exclude dynamic cache/state sources for the selected generation symbol.
- [x] Verify both source configurations compile; run existing Dynamic/editor layout tests.

### Task 3: Native verification and measurement

**Files:** Existing PS1 build output and evidence at C:\temp\numeric-precision\ps1; relevant repository validation documentation.

- [x] Generate the actual PS1 core and inspect that dynamic viewport/cache types and resize handlers are absent.
- [ ] Build at the existing output, verify all 15 scenes and 44 menu options, compare code/static/heap against the recorded baseline.
- [ ] Launch only through emulator MCP and inspect startup/cadence logs; report measured results and remaining limitations.
- [ ] Review the complete diff and commit only intended changes after checks pass.

## Execution rulings and evidence

- Preserve the published five-argument runtime-contract constructor ABI; the explicit six-argument overload enables the new policy.
- The GameCube build-selected video mode was separately approved: NTSC 640x480 by default, optional PAL 640x528 (`TVPal528IntDf`), including matching runtime pixel aspect.
- Standalone fits retain their established full-camera-surface semantics; camera-bound viewports use the camera rectangle.
- Review reproduced initialization-time coordinate conversion and double scaling of nested runtime-created descendants. Fixed with cooked metadata available before publication and one transient borrowed scope pointer per Core; no per-entity processed flags or original-value records.
- Managed checks passed: 12 Cook-only runtime tests, 65 existing Dynamic layout tests, 50 cook/persistence tests, 105 generation/persistence tests before the review corrections, 13 GameCube focused tests and two PS2 tests covering all 12 video modes. N64, Dreamcast and Xbox builders compiled with zero errors.
- Actual PS1 packaged content inspected: 15 scenes, 44 menu options, 35 viewports, all Cook with algorithm 2 and target 320x240. Generated core contains no dynamic cache/state files or viewport resize handlers/subscriptions.
- Native PS1 and GameCube builds were stopped at Helena's request. All 597 PS1 candidate translation units compiled, but the final resident image and runtime memory measurement were not completed. Fixed output roots remain under C:\temp\numeric-precision.
