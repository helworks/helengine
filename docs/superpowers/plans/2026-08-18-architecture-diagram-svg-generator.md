# Reusable SVG Architecture Diagram Generator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a reusable TypeScript CLI that renders the approved PS2 engine architecture narrative as a scalable master SVG and optional section SVGs.

**Architecture:** Keep diagram meaning in typed TypeScript documents and keep SVG serialization in a dependency-light renderer. The CLI resolves named documents, validates IDs and references, renders the master or a selected section, and writes explicit output files. The PS2 document uses manually authored coordinates and stable IDs; it does not scrape source files or infer a dependency graph.

**Tech Stack:** Node.js, TypeScript, `node:test`, raw SVG string rendering, and the existing Git/PowerShell workflow. Runtime dependencies are zero; `typescript` and `@types/node` are development dependencies.

---

## File map

The implementation lives in the isolated `helengine` worktree:

- Create `tools/architecture-diagrams/package.json`: Node project metadata and scripts.
- Create `tools/architecture-diagrams/tsconfig.json`: strict ESM TypeScript compilation into `dist/`.
- Create `tools/architecture-diagrams/src/model.ts`: document types and structural validation.
- Create `tools/architecture-diagrams/src/model.test.ts`: model-validation tests.
- Create `tools/architecture-diagrams/src/theme.ts`: approved colors, line styles, and typography tokens.
- Create `tools/architecture-diagrams/src/renderer.ts`: deterministic SVG rendering, escaping, stable IDs, accessibility metadata, and section cropping.
- Create `tools/architecture-diagrams/src/renderer.test.ts`: renderer tests.
- Create `tools/architecture-diagrams/src/registry.ts`: named diagram registry and lookup.
- Create `tools/architecture-diagrams/src/registry.test.ts`: registry tests.
- Create `tools/architecture-diagrams/src/manifest.ts`: stable metadata export for video tooling.
- Create `tools/architecture-diagrams/src/manifest.test.ts`: manifest tests.
- Create `tools/architecture-diagrams/src/diagrams/ps2-overview.ts`: the five-section PS2 story.
- Create `tools/architecture-diagrams/src/diagrams/ps2-overview.test.ts`: PS2 content contract tests.
- Create `tools/architecture-diagrams/src/cli.ts`: `list` and `render` commands.
- Create `tools/architecture-diagrams/src/cli.test.ts`: CLI parsing and output tests.
- Create `tools/architecture-diagrams/README.md`: setup and authoring workflow.
- Create `docs/diagrams/helengine-ps2-overview.svg`: generated master artifact.
- Create `docs/diagrams/helengine-ps2-overview.manifest.json`: generated stable-ID/reveal metadata.

No engine or platform source files are modified.

### Task 1: Bootstrap the TypeScript project and document model

**Files:**
- Create: `tools/architecture-diagrams/package.json`
- Create: `tools/architecture-diagrams/tsconfig.json`
- Create: `tools/architecture-diagrams/src/model.ts`
- Test: `tools/architecture-diagrams/src/model.test.ts`

- [ ] **Step 1: Add project configuration.**

Create `package.json`:

```json
{
  "name": "helengine-architecture-diagrams",
  "private": true,
  "type": "module",
  "scripts": {
    "build": "tsc --project tsconfig.json",
    "test": "npm run build && node --test dist",
    "diagram": "npm run build && node dist/cli.js"
  },
  "devDependencies": {
    "@types/node": "^22.0.0",
    "typescript": "^5.7.0"
  }
}
```

Create `tsconfig.json` with `target: ES2022`, `module: NodeNext`, `moduleResolution: NodeNext`, `rootDir: src`, `outDir: dist`, `strict: true`, `declaration: true`, `sourceMap: true`, `verbatimModuleSyntax: true`, `esModuleInterop: true`, and `skipLibCheck: true`. Include `src/**/*.ts`.

- [ ] **Step 2: Install dependencies.**

Run from `tools/architecture-diagrams`:

```powershell
npm install
```

Expected: npm exits with code `0` and creates `package-lock.json`.

- [ ] **Step 3: Write failing model tests.**

Use `node:test` and `node:assert/strict`. The fixture must have one section, two nodes, and one edge. Cover:

```ts
test("accepts a consistent document", () => {
  assert.doesNotThrow(() => validateDocument(createValidDocument()));
});

test("rejects duplicate node ids", () => {
  const document = createValidDocument();
  document.nodes.push({ ...document.nodes[0], id: document.nodes[0].id });
  assert.throws(() => validateDocument(document), /duplicate node id/i);
});

test("rejects an edge with a missing endpoint", () => {
  const document = createValidDocument();
  document.edges[0].to = "missing-node";
  assert.throws(() => validateDocument(document), /missing node/i);
});

test("rejects non-positive section dimensions", () => {
  const document = createValidDocument();
  document.sections[0].bounds.width = 0;
  assert.throws(() => validateDocument(document), /positive/i);
});
```

- [ ] **Step 4: Run the tests and verify the expected failure.**

```powershell
npm test
```

Expected: FAIL because `model.ts` and `validateDocument` do not exist.

- [ ] **Step 5: Implement the typed model and validator.**

Define these public types:

```ts
export type DiagramRole = "csharp" | "generated-cpp" | "handwritten-cpp" | "artifact" | "ps2" | "neutral";
export type EdgeRole = "runtime" | "generation" | "packaging" | "context";
export type NodeKind = "module" | "process" | "artifact" | "hardware" | "boundary";
export interface ViewBox { x: number; y: number; width: number; height: number; }
export interface DiagramSection { id: string; title: string; subtitle?: string; bounds: ViewBox; step: number; nodeIds: string[]; edgeIds: string[]; calloutIds: string[]; }
export interface DiagramNode { id: string; title: string; subtitle?: string; lines: string[]; role: DiagramRole; kind: NodeKind; bounds: ViewBox; sectionIds: string[]; step: number; }
export interface DiagramEdge { id: string; from: string; to: string; label?: string; role: EdgeRole; sectionIds: string[]; step: number; }
export interface DiagramCallout { id: string; title: string; lines: string[]; role: DiagramRole; bounds: ViewBox; sectionIds: string[]; step: number; }
export interface DiagramDocument { title: string; subtitle?: string; viewBox: ViewBox; sections: DiagramSection[]; nodes: DiagramNode[]; edges: DiagramEdge[]; callouts: DiagramCallout[]; }
```

`validateDocument(document)` checks positive finite dimensions, unique IDs within each collection, existing section/node/callout references, edge endpoints, and matching section membership lists. Throw an `Error` naming the invalid collection/id; never silently repair invalid data.

- [ ] **Step 6: Run tests and commit the model foundation.**

```powershell
npm test
git add tools/architecture-diagrams/package.json tools/architecture-diagrams/package-lock.json tools/architecture-diagrams/tsconfig.json tools/architecture-diagrams/src/model.ts tools/architecture-diagrams/src/model.test.ts
git commit -m "feat: add architecture diagram document model"
```

Expected: all model tests pass before the commit.

### Task 2: Add the theme and deterministic SVG renderer

**Files:**
- Create: `tools/architecture-diagrams/src/theme.ts`
- Create: `tools/architecture-diagrams/src/renderer.ts`
- Test: `tools/architecture-diagrams/src/renderer.test.ts`

- [ ] **Step 1: Write failing renderer tests.**

Test that `renderDocument(validDocument)` emits an XML/SVG root with the document `viewBox`, `role="img"`, `<title>`, `<desc>`, stable IDs `section-<id>`, `node-<id>`, and `edge-<id>`, and `data-step` values. Include a title containing `&` and `<` and assert it is XML-escaped. Test a section render uses the section bounds plus a 24-unit margin and omits unrelated groups.

- [ ] **Step 2: Run tests and verify the expected failure.**

```powershell
npm test
```

Expected: FAIL because `renderer.ts` and `theme.ts` do not exist.

- [ ] **Step 3: Define theme tokens.**

Export a theme with this role mapping:

```ts
roles: {
  csharp: "#8B7CFF",
  "generated-cpp": "#F2B84B",
  "handwritten-cpp": "#41D6C3",
  artifact: "#79D98C",
  ps2: "#FF8C5A",
  neutral: "#8EA0B8"
}
```

Use charcoal `#10131A` for the background, `#F4F7FB` for foreground text, `#AAB5C5` for muted text, and role-specific edge colors. Keep line dash patterns, corner radius, typography, and marker IDs in `theme.ts`.

- [ ] **Step 4: Implement the renderer.**

Export:

```ts
export interface RenderOptions { sectionId?: string; margin?: number; theme?: DiagramTheme; }
export function renderDocument(document: DiagramDocument, options?: RenderOptions): string;
export function resolveRenderViewBox(document: DiagramDocument, sectionId?: string, margin?: number): ViewBox;
```

Call `validateDocument` first. Render the background and SVG `<defs>` once, then sections, edges, nodes, and callouts in deterministic collection order. Use stable IDs `section-<id>`, `node-<id>`, `edge-<id>`, `callout-<id>`, `data-step`, escaped text, arrow markers, role-specific dash patterns, `shape-rendering="geometricPrecision"`, `role="img"`, and accessible `<title>/<desc>`. Do not emit fixed width/height.

For a section render, include only its listed objects, keep edges only when both endpoints are included, and set the viewBox to the selected bounds expanded by the margin. Default margin is 24.

- [ ] **Step 5: Run tests and commit the renderer.**

```powershell
npm test
git add tools/architecture-diagrams/src/theme.ts tools/architecture-diagrams/src/renderer.ts tools/architecture-diagrams/src/renderer.test.ts
git commit -m "feat: render architecture diagrams as scalable SVG"
```

Expected: model and renderer tests pass.

### Task 3: Add registry and video metadata

**Files:**
- Create: `tools/architecture-diagrams/src/registry.ts`
- Create: `tools/architecture-diagrams/src/registry.test.ts`
- Create: `tools/architecture-diagrams/src/manifest.ts`
- Create: `tools/architecture-diagrams/src/manifest.test.ts`

- [ ] **Step 1: Write failing tests.**

Assert that `listDiagrams()` returns sorted names, `getDiagram("missing")` names the missing diagram, and `buildManifest(document)` returns title, viewBox, section bounds/steps, and node/edge/callout ID arrays without SVG markup.

- [ ] **Step 2: Run tests and verify the expected failure.**

```powershell
npm test
```

Expected: FAIL because registry and manifest modules do not exist.

- [ ] **Step 3: Implement the modules.**

Export:

```ts
export type DiagramFactory = () => DiagramDocument;
export function listDiagrams(): string[];
export function getDiagram(name: string): DiagramDocument;
export interface DiagramManifest { title: string; viewBox: ViewBox; sections: Array<{ id: string; bounds: ViewBox; step: number; }>; nodes: string[]; edges: string[]; callouts: string[]; }
export function buildManifest(document: DiagramDocument): DiagramManifest;
```

Use factories so each lookup returns a fresh document object. The registry must be independent from rendering.

- [ ] **Step 4: Run tests and commit.**

```powershell
npm test
git add tools/architecture-diagrams/src/registry.ts tools/architecture-diagrams/src/registry.test.ts tools/architecture-diagrams/src/manifest.ts tools/architecture-diagrams/src/manifest.test.ts
git commit -m "feat: add diagram registry and video metadata manifest"
```

### Task 4: Author and register the PS2 overview

**Files:**
- Create: `tools/architecture-diagrams/src/diagrams/ps2-overview.ts`
- Create: `tools/architecture-diagrams/src/diagrams/ps2-overview.test.ts`
- Modify: `tools/architecture-diagrams/src/registry.ts`

- [ ] **Step 1: Write the failing PS2 content contract test.**

Assert that `createPs2OverviewDocument()` validates and contains exactly these section IDs in steps 1-5: `authoring`, `shared-core`, `build-conversion`, `ps2-player`, and `ps2-hardware`. Assert node titles identify `helengine.core`, `helengine.editor`, `helengine.files`, `csharpcodegen`, generated C++, handwritten PS2 C++, `game.iso`, and the VIF/GIF/VU/GS handoff. Assert at least one generation edge and one packaging edge.

- [ ] **Step 2: Run the content test and verify the expected failure.**

```powershell
npm test
```

Expected: FAIL because the PS2 document does not exist.

- [ ] **Step 3: Implement the five-section document.**

Use a `2400 x 1500` viewBox and five 420-unit-wide panels from left to right with 30-unit gaps. Use concise video copy and explicit coordinates. Include these teaching nodes:

```text
authoring:
  project.heproj [artifact]
  helengine.editor [csharp]
  scenes and assets [artifact]
shared-core:
  helengine.core [csharp]
  Core / Entity / Component [csharp]
  SceneManager / ContentManager [csharp]
  helengine.files (read + write) [csharp]
  packaged runtime reads assets [artifact]
build-conversion:
  EditorCliBuildRunner [csharp]
  Ps2PlatformAssetBuilder [csharp]
  cook assets + write manifests [artifact]
  csharpcodegen [generated-cpp]
  selected runtime C# -> generated C++ [generated-cpp]
ps2-player:
  generated helengine core C++ [generated-cpp]
  Ps2BootHost / disc / input / audio [handwritten-cpp]
  Ps2RenderManager3D / frame planner [handwritten-cpp]
  VU microprograms [handwritten-cpp]
  helengine_ps2.elf [artifact]
ps2-hardware:
  game.iso + disc assets [artifact]
  EE runtime [ps2]
  VIF/GIF packets [ps2]
  VU1 programs [ps2]
  GS output [ps2]
```

Connect the spine with runtime, generation, packaging, and context edges. Add callouts stating “The editor and builder stay C#,” “Only the selected runtime subset crosses the codegen boundary,” and “Generated core and handwritten PS2 C++ are linked into the player.” Use section references for shared concepts.

- [ ] **Step 4: Register `ps2-overview`.**

Import the factory into `registry.ts` under the exact name `ps2-overview`.

- [ ] **Step 5: Run tests and commit.**

```powershell
npm test
git add tools/architecture-diagrams/src/diagrams/ps2-overview.ts tools/architecture-diagrams/src/diagrams/ps2-overview.test.ts tools/architecture-diagrams/src/registry.ts
git commit -m "feat: author PS2 engine architecture diagram"
```

Expected: all current tests pass.

### Task 5: Add the CLI and README

**Files:**
- Create: `tools/architecture-diagrams/src/cli.ts`
- Create: `tools/architecture-diagrams/src/cli.test.ts`
- Create: `tools/architecture-diagrams/README.md`

- [ ] **Step 1: Write failing CLI tests.**

Test:

```ts
assert.deepEqual(parseArguments(["list"]), { command: "list" });
assert.deepEqual(parseArguments(["render", "ps2-overview", "--section", "build-conversion", "--output", "out.svg"]), {
  command: "render",
  name: "ps2-overview",
  sectionId: "build-conversion",
  outputPath: "out.svg",
  manifestPath: undefined
});
assert.throws(() => parseArguments(["render"]), /diagram name/i);
assert.throws(() => parseArguments(["render", "ps2-overview"]), /output/i);
```

Also test that rendering creates a missing parent directory, writes SVG text, and writes a manifest only when `--manifest` is supplied.

- [ ] **Step 2: Run tests and verify the expected failure.**

```powershell
npm test
```

Expected: FAIL because `cli.ts` does not exist.

- [ ] **Step 3: Implement argument parsing and commands.**

Export:

```ts
export interface ListArguments { command: "list"; }
export interface RenderArguments { command: "render"; name: string; sectionId?: string; outputPath: string; manifestPath?: string; }
export type CliArguments = ListArguments | RenderArguments;
export function parseArguments(argv: string[]): CliArguments;
export async function runCli(argv: string[]): Promise<number>;
```

Support `list`, `render <name> --output <path>`, optional `--section <id>`, and optional `--manifest <path>`. Resolve relative paths from the current working directory, create parents recursively, write UTF-8 files, and return a non-zero code for clear errors.

- [ ] **Step 4: Write the README.**

Document installation, `npm test`, all CLI forms, the five PS2 sections, role colors, the C#/generated-C++/handwritten-C++ distinction, and how to add/register another document.

- [ ] **Step 5: Run tests and commit.**

```powershell
npm test
git add tools/architecture-diagrams/src/cli.ts tools/architecture-diagrams/src/cli.test.ts tools/architecture-diagrams/README.md
git commit -m "feat: add architecture diagram rendering CLI"
```

### Task 6: Generate and verify the committed SVG

**Files:**
- Create: `docs/diagrams/helengine-ps2-overview.svg`
- Create: `docs/diagrams/helengine-ps2-overview.manifest.json`

- [ ] **Step 1: Render the master artifact.**

From `tools/architecture-diagrams`:

```powershell
npm run diagram -- render ps2-overview --output ../../docs/diagrams/helengine-ps2-overview.svg --manifest ../../docs/diagrams/helengine-ps2-overview.manifest.json
```

Expected: both files exist, the SVG is non-empty, and the manifest lists five sections.

- [ ] **Step 2: Render one section crop.**

```powershell
npm run diagram -- render ps2-overview --section build-conversion --output ../../builds/architecture-diagrams/build-section.svg
```

Expected: the crop contains `section-build-conversion`, omits `section-authoring`, and has a smaller viewBox.

- [ ] **Step 3: Parse the master SVG and verify key IDs.**

From the worktree root:

```powershell
$svg = [xml](Get-Content -Raw -LiteralPath 'docs\diagrams\helengine-ps2-overview.svg')
if ($svg.svg.viewBox -ne '0 0 2400 1500') { throw 'Unexpected master viewBox.' }
$content = Get-Content -Raw -LiteralPath 'docs\diagrams\helengine-ps2-overview.svg'
foreach ($id in @('section-authoring','section-shared-core','section-build-conversion','section-ps2-player','section-ps2-hardware','node-codegen','node-generated-core','node-ps2-elf')) {
  if ($content -notmatch [regex]::Escape($id)) { throw "Missing SVG id: $id" }
}
```

Expected: the XML parse and all ID checks succeed.

- [ ] **Step 4: Run complete validation and commit artifacts.**

```powershell
Push-Location tools\architecture-diagrams
npm test
Pop-Location
git diff --check
git add docs/diagrams/helengine-ps2-overview.svg docs/diagrams/helengine-ps2-overview.manifest.json
git commit -m "docs: add PS2 architecture SVG overview"
```

### Task 7: Final review and handoff

- [ ] **Step 1: Inspect the branch.**

```powershell
git status --short
git log -6 --oneline --decorate
git diff main...HEAD --stat
```

Expected: only the diagram tool, tests/docs, plan, and generated diagram artifacts are present; no engine runtime files changed.

- [ ] **Step 2: Run a clean CLI smoke command.**

```powershell
Push-Location tools\architecture-diagrams
npm run diagram -- list
npm run diagram -- render ps2-overview --output ../../builds/architecture-diagrams/final-overview.svg
Pop-Location
```

Expected: `ps2-overview` is listed and the final SVG is written successfully.

- [ ] **Step 3: Report the worktree path, generated artifact, test command, and commit range.**

State that the generator is data-driven, the PS2 SVG is committed, and pre-existing edits in the original checkout were not modified.

## Plan self-review

- Spec coverage: the plan includes the data-driven TypeScript architecture, scalable master SVG, section crops, five PS2 narrative sections, language/conversion distinctions, stable IDs, accessibility metadata, validation, manifest metadata, tests, README, and committed SVG output.
- Placeholder scan: no unfinished or unspecified implementation step is used; each code task names files, interfaces, commands, and expected outcomes.
- Type consistency: `DiagramDocument`, `DiagramSection`, `DiagramNode`, `DiagramEdge`, `DiagramCallout`, `RenderOptions`, `DiagramManifest`, and CLI argument types are defined before later tasks consume them.
- Scope: no source scraping, browser editor, raster export, or video encoder is included in the first implementation.
