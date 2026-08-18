# Reusable SVG Architecture Diagram Generator

**Date:** 2026-08-18

**Status:** Approved design; implementation not started

## Goal

Create a reusable, data-driven TypeScript CLI that renders explanatory engine architecture diagrams as SVG. The first document will be a public-video-oriented overview of the Helengine PS2 path. It will be one scalable master SVG with several narrative sections, while allowing individual section SVGs to be rendered from the same model.

The diagram is a communication artifact, not an automatically generated dependency graph. Its content must be explicit enough to explain conceptual boundaries that source filenames alone cannot reliably express.

## Context

The diagram must represent the current architecture accurately:

- The shared `helengine` workspace contains the C# runtime, editor, files/content authoring side, and platform build contracts.
- `helengine-ps2` contains the C# PS2 asset/serialization and builder layers, plus the handwritten native C++ PS2 host, platform services, renderer, and VU programs.
- The editor and platform builder remain C#/.NET tools. They author projects, cook assets, write runtime manifests, select runtime modules, and orchestrate the build.
- The external `csharpcodegen` tool converts the selected runtime C# graph into generated C++ for the console runtime.
- The PS2 build stages generated core C++, handwritten PS2 C++, VU microprograms, runtime manifests, and cooked assets into the PS2 executable and disc/ISO package.

The diagram must distinguish three language/runtime categories:

1. C# authoring/editor/builder code.
2. C# runtime code selected for conversion into generated C++.
3. Handwritten C++ platform/runtime code that is compiled with the generated core.

## Visual language

The master SVG uses a configurable `viewBox` rather than a fixed physical video resolution. The first composition may be wide and cinematic, but the renderer must remain resolution-independent.

The initial theme uses:

- violet/blue for C#;
- amber for generated C++ and the conversion boundary;
- teal for handwritten C++;
- green for assets and build artifacts;
- orange for PS2 runtime/hardware boundaries;
- solid arrows for runtime or data flow;
- dashed arrows for code generation/conversion;
- dotted arrows for packaging/deployment.

Section panels use a dark charcoal background with high-contrast cards. Labels are short and explanatory. Representative folder or technology names may appear as small secondary text, but the first diagram will not become a class inventory.

The renderer emits stable SVG group and element IDs, `data-*` metadata for section/reveal order, and accessible `<title>`/`<desc>` content. This leaves room for later browser exploration, camera moves, or timeline animation without changing the source model.

## Data model

The diagram source is a TypeScript document containing meaning and layout. Rendering code must not contain the PS2 architecture facts directly.

```ts
type DiagramDocument = {
  title: string;
  subtitle?: string;
  viewBox: ViewBox;
  sections: Section[];
  nodes: DiagramNode[];
  edges: DiagramEdge[];
  callouts: Callout[];
  theme: Theme;
};
```

Nodes and edges have stable IDs, explicit coordinates, section membership, category/language tags, and concise display text. Sections reference node and edge IDs so shared concepts such as the C# runtime can appear in more than one explanatory view without duplicating their definitions. Layout and content remain separate so future diagrams can reuse the same rendering primitives with different coordinates and narrative copy.

The model includes section bounds and reveal-order metadata. Reveal order is descriptive metadata for video tooling; the first renderer remains a static SVG generator.

## First PS2 document

The composition follows one visual spine:

`Author -> Shared engine -> Build/cook -> C# conversion -> PS2 player -> PS2 disc/hardware`

It contains five sections:

### 1. Authoring surface

Show the editor, project file, scenes, assets, and editor-only tools. Explain that the editor is a C#/.NET application and is not shipped to the PS2.

### 2. Shared engine contents

Show a simplified anatomy of `helengine.core`: `Core`, entities/components, object management, scenes, content/assets, and runtime-facing input/physics/rendering contracts. Show `helengine.files` on the authoring side as the read/write asset side, and the packaged runtime core as the read-only side.

### 3. Build and conversion

Show the editor CLI and PS2 platform builder taking the authored project through prebuild commands, asset cooking, runtime manifest generation, runtime-module selection, and code generation. Make the boundary explicit: editor and builder code remains C#, while the selected runtime subset is converted to generated C++.

### 4. PS2 player

Show two distinct stacks that are compiled together:

- generated C++ core produced from the shared runtime C#;
- handwritten PS2 C++ for boot, disc I/O, input, audio, rendering, and runtime integration.

This section culminates in the PS2 executable rather than implying that the editor or builder runs on the console.

### 5. Executable to hardware

Show the PS2 Makefile combining generated core, handwritten C++, VU microprograms, runtime manifests, and cooked assets into the ELF/ISO/disc layout. Include a compact rendering inset: C++ frame planning -> VIF/GIF packets -> VU programs -> GS.

## Tool and repository layout

The reusable tool belongs in the shared `helengine` repository because the first diagram spans the shared engine and the sibling PS2 platform repository:

```text
helengine/
  tools/architecture-diagrams/
    src/
      cli.ts
      model.ts
      renderer.ts
      theme.ts
      diagrams/ps2-overview.ts
    package.json
    README.md
  docs/diagrams/
    helengine-ps2-overview.svg
```

The command-line interface supports rendering a named document, rendering one section, and listing available documents. The target usage is equivalent to:

```text
diagram render ps2-overview --output docs/diagrams/helengine-ps2-overview.svg
diagram render ps2-overview --section build --output build-section.svg
diagram list
```

The tool may accept a workspace-root argument for future context-sensitive diagrams, but the first PS2 document does not scrape source files or infer architecture from that argument.

## Outputs and validation

The master output is a valid XML/SVG file with the configured `viewBox`. A section output uses the same document and a section-specific crop. An optional manifest contains section bounds, stable IDs, reveal order, and theme tokens for video workflows.

Before writing, the renderer validates that:

- section, node, edge, and callout IDs are unique;
- every edge references existing nodes;
- section regions are valid;
- category/language tags resolve to theme tokens;
- all text is XML-escaped;
- the generated SVG includes stable IDs and accessibility metadata.

The implementation should have focused tests for model validation, SVG escaping, theme mapping, master rendering, section cropping, and a small structural smoke test that opens/parses the generated SVG as XML and checks for expected section and node IDs.

## Non-goals for the first version

- automatic dependency harvesting or repository-wide graph generation;
- raster, PDF, or video-file export;
- a browser editor for diagram authoring;
- a full PS2 renderer deep dive beyond the compact VIF/GIF/VU/GS inset;
- animation generation; only stable IDs and reveal metadata are prepared for it.

## Acceptance criteria

The first implementation is successful when a contributor can edit the PS2 diagram data, run the CLI, and obtain a scalable master SVG plus optional section SVGs without changing renderer code. The generated overview must clearly communicate what is C#, what is generated C++, what is handwritten C++, what is converted, what is cooked, and what reaches the PS2 player/disc. Adding a later engine or platform diagram should require a new data document and reusable layout primitives, not a fork of the renderer.
