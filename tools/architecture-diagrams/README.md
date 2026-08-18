# Helengine architecture diagrams

This is a small, data-driven TypeScript CLI for rendering explanatory engine architecture diagrams as scalable SVG. The first document explains how the C# editor and shared runtime become generated C++ plus handwritten PS2 code.

## Setup

```powershell
npm install
npm test
```

The renderer has no runtime dependencies. TypeScript and Node type definitions are development-only dependencies.

## Commands

```powershell
npm run diagram -- list
npm run diagram -- render ps2-overview --output ../../docs/diagrams/helengine-ps2-overview.svg
npm run diagram -- render ps2-overview --section build-conversion --output build-section.svg
npm run diagram -- render ps2-overview --output overview.svg --manifest overview.manifest.json
```

The master SVG uses its document `viewBox` and can be resized for video work. A section render crops the same document to that section's bounds. The optional manifest contains stable section, node, edge, and callout IDs plus reveal steps.

## PS2 overview

The first document follows this narrative:

1. Authoring surface: `project.heproj`, `helengine.editor`, scenes, and assets.
2. Shared engine contents: `helengine.core`, entities/components, scenes, content, and `helengine.files`.
3. Build and conversion: editor CLI, `Ps2PlatformAssetBuilder`, cooking, manifests, and `csharpcodegen`.
4. PS2 player: generated C++ core, handwritten PS2 C++, VU programs, and the ELF.
5. Hardware handoff: ISO/disc assets, EE runtime, VIF/GIF packets, VU1, and GS output.

The diagram deliberately distinguishes:

- **C#** — editor, builder, and shared runtime source;
- **generated C++** — the selected runtime subset emitted by `csharpcodegen`;
- **handwritten C++** — PS2 host, platform services, renderer, and VU programs;
- **artifacts** — cooked assets, manifests, ELF, and ISO/disc media;
- **PS2** — the console execution and graphics boundary.

## Adding another diagram

1. Add a factory under `src/diagrams/` that returns a `DiagramDocument`.
2. Add a content contract test that validates the document and its required narrative IDs.
3. Register the factory in `src/registry.ts` with a stable name.
4. Render the named document with the CLI and commit the SVG when it is documentation output.

Keep architecture facts in the diagram data file. The renderer supplies reusable layout primitives, colors, arrows, stable IDs, XML escaping, and section cropping; it should not contain project-specific labels.
