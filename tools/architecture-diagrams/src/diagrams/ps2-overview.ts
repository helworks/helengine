import type { DiagramCallout, DiagramDocument, DiagramEdge, DiagramNode, DiagramSection, NodeKind, ViewBox, DiagramRole, EdgeRole } from "../model.js";

const viewBox: ViewBox = { x: 0, y: 0, width: 2400, height: 1500 };

const sectionBounds = {
  authoring: { x: 60, y: 170, width: 420, height: 1080 },
  "shared-core": { x: 510, y: 170, width: 420, height: 1080 },
  "build-conversion": { x: 960, y: 170, width: 420, height: 1080 },
  "ps2-player": { x: 1410, y: 170, width: 420, height: 1080 },
  "ps2-hardware": { x: 1860, y: 170, width: 420, height: 1080 }
} satisfies Record<string, ViewBox>;

export function createPs2OverviewDocument(): DiagramDocument {
  const sections: DiagramSection[] = [
    section("authoring", "Authoring surface", "What exists on the development machine", sectionBounds.authoring, 1, ["node-project", "node-editor", "node-scenes-assets"], ["edge-project-editor", "edge-editor-scenes"], []),
    section("shared-core", "Shared engine contents", "The reusable C# runtime boundary", sectionBounds["shared-core"], 2, ["node-core", "node-core-anatomy", "node-files", "node-runtime-reads"], ["edge-scenes-files", "edge-files-runtime"], ["callout-core-boundary"]),
    section("build-conversion", "Build and conversion", "How authored content becomes a console runtime", sectionBounds["build-conversion"], 3, ["node-editor-cli", "node-ps2-builder", "node-cook", "node-codegen", "node-generated-core"], ["edge-editor-cli", "edge-cli-builder", "edge-builder-cook", "edge-cook-codegen", "edge-core-codegen", "edge-codegen-generated-core"], ["callout-codegen-boundary"]),
    section("ps2-player", "The PS2 player", "Generated core meets handwritten platform code", sectionBounds["ps2-player"], 4, ["node-player-core", "node-boot-host", "node-render-manager", "node-vu-programs", "node-ps2-elf"], ["edge-generated-player-core", "edge-host-elf", "edge-render-vu", "edge-vu-elf", "edge-builder-elf"], ["callout-player-link"]),
    section("ps2-hardware", "Executable to hardware", "What finally reaches the PS2", sectionBounds["ps2-hardware"], 5, ["node-game-iso", "node-ee-runtime", "node-packets", "node-vu1", "node-gs"], ["edge-elf-iso", "edge-iso-ee", "edge-ee-packets", "edge-packets-vu", "edge-vu-gs"], ["callout-hardware-flow"])
  ];

  const nodes: DiagramNode[] = [
    node("node-project", "project.heproj", "Authored project", "artifact", "artifact", { x: 90, y: 270, width: 160, height: 90 }, ["Scenes, settings, platform choices"], 1),
    node("node-editor", "helengine.editor", "C# / .NET editor", "module", "csharp", { x: 290, y: 270, width: 160, height: 90 }, ["Viewport, inspectors, commands"], 1),
    node("node-scenes-assets", "Scenes + assets", "Authoring data", "artifact", "artifact", { x: 90, y: 410, width: 360, height: 100 }, ["Maps, models, textures, materials"], 1),

    node("node-core", "helengine.core", "Shared runtime", "module", "csharp", { x: 540, y: 260, width: 360, height: 100 }, ["C# gameplay/runtime model"], 2),
    node("node-core-anatomy", "Core / Entity / Component", "ObjectManager + SceneManager", "module", "csharp", { x: 540, y: 400, width: 360, height: 150 }, ["Update, draw, lifecycle, scenes", "ContentManager reads runtime assets"], 2),
    node("node-files", "helengine.files", "Read + write authoring side", "module", "csharp", { x: 540, y: 630, width: 170, height: 105 }, ["Serialization / cook input"], 2),
    node("node-runtime-reads", "Packaged runtime", "Read-only asset side", "boundary", "artifact", { x: 730, y: 630, width: 170, height: 105 }, ["Consumes cooked payloads"], 2),

    node("node-editor-cli", "EditorCliBuildRunner", "C# build orchestration", "process", "csharp", { x: 990, y: 250, width: 170, height: 100 }, ["Prebuild + runtime-only cook"], 3),
    node("node-ps2-builder", "Ps2PlatformAssetBuilder", "C# platform builder", "process", "csharp", { x: 1180, y: 250, width: 170, height: 100 }, ["PS2 cook + package steps"], 3),
    node("node-cook", "Cook assets + manifests", "Build artifacts", "artifact", "artifact", { x: 990, y: 390, width: 360, height: 95 }, ["Runtime scene catalog, asset paths"], 3),
    node("node-codegen", "csharpcodegen", "C# -> generated C++", "process", "generated-cpp", { x: 990, y: 535, width: 170, height: 105 }, ["Selected runtime graph"], 3),
    node("node-generated-core", "Generated C++ core", "Converted shared runtime", "artifact", "generated-cpp", { x: 1180, y: 535, width: 170, height: 105 }, ["helengine_core_amalgamated.cpp"], 3),

    node("node-player-core", "Generated core in player", "Converted C# runtime", "module", "generated-cpp", { x: 1440, y: 250, width: 360, height: 100 }, ["Generated C++ linked into PS2 runtime"], 4),
    node("node-boot-host", "Handwritten PS2 C++", "Ps2BootHost / disc / input / audio", "module", "handwritten-cpp", { x: 1440, y: 400, width: 170, height: 135 }, ["Platform startup and services"], 4),
    node("node-render-manager", "Handwritten PS2 C++", "Ps2RenderManager3D / frame planner", "module", "handwritten-cpp", { x: 1630, y: 400, width: 170, height: 135 }, ["Render proxies and packets"], 4),
    node("node-vu-programs", "VU microprograms", "Handwritten PS2 C++ / VSM", "module", "handwritten-cpp", { x: 1440, y: 580, width: 360, height: 95 }, ["Opaque draw, textured draw, pretransformed draw"], 4),
    node("node-ps2-elf", "helengine_ps2.elf", "Linked PS2 player", "artifact", "artifact", { x: 1530, y: 735, width: 180, height: 105 }, ["EE executable"], 4),

    node("node-game-iso", "game.iso + disc assets", "Packaged media", "artifact", "artifact", { x: 1890, y: 250, width: 360, height: 100 }, ["SYSTEM.CNF, HELENGIN.ELF, cooked data"], 5),
    node("node-ee-runtime", "EE runtime", "PS2 player process", "hardware", "ps2", { x: 1890, y: 405, width: 160, height: 105 }, ["Boot, update, draw"], 5),
    node("node-packets", "VIF/GIF packets", "Graphics command stream", "hardware", "ps2", { x: 2070, y: 405, width: 180, height: 105 }, ["DMA-fed packet data"], 5),
    node("node-vu1", "VU1 programs", "Vector unit work", "hardware", "ps2", { x: 1890, y: 565, width: 160, height: 105 }, ["Transform + shade"], 5),
    node("node-gs", "GS output", "PlayStation 2 graphics synthesizer", "hardware", "ps2", { x: 2070, y: 565, width: 180, height: 105 }, ["Framebuffer"], 5)
  ];

  const edges: DiagramEdge[] = [
    edge("edge-project-editor", "node-project", "node-editor", "open", "context", ["authoring"], 1),
    edge("edge-editor-scenes", "node-editor", "node-scenes-assets", "author", "runtime", ["authoring"], 1),
    edge("edge-scenes-files", "node-scenes-assets", "node-files", "serialize", "context", ["shared-core"], 2),
    edge("edge-files-runtime", "node-files", "node-runtime-reads", "cook/read boundary", "runtime", ["shared-core"], 2),
    edge("edge-editor-cli", "node-editor", "node-editor-cli", "build", "context", ["build-conversion"], 3),
    edge("edge-cli-builder", "node-editor-cli", "node-ps2-builder", "request", "runtime", ["build-conversion"], 3),
    edge("edge-builder-cook", "node-ps2-builder", "node-cook", "cook", "packaging", ["build-conversion"], 3),
    edge("edge-cook-codegen", "node-cook", "node-codegen", "runtime inputs", "generation", ["build-conversion"], 3),
    edge("edge-core-codegen", "node-core", "node-codegen", "selected runtime C#", "generation", ["build-conversion"], 3),
    edge("edge-codegen-generated-core", "node-codegen", "node-generated-core", "generated C++", "generation", ["build-conversion"], 3),
    edge("edge-generated-player-core", "node-generated-core", "node-player-core", "stage", "generation", ["ps2-player"], 4),
    edge("edge-host-elf", "node-boot-host", "node-ps2-elf", "link", "packaging", ["ps2-player"], 4),
    edge("edge-render-vu", "node-render-manager", "node-vu-programs", "dispatch", "runtime", ["ps2-player"], 4),
    edge("edge-vu-elf", "node-vu-programs", "node-ps2-elf", "link", "packaging", ["ps2-player"], 4),
    edge("edge-builder-elf", "node-ps2-builder", "node-ps2-elf", "package inputs", "packaging", ["ps2-player"], 4),
    edge("edge-elf-iso", "node-ps2-elf", "node-game-iso", "package", "packaging", ["ps2-hardware"], 5),
    edge("edge-iso-ee", "node-game-iso", "node-ee-runtime", "boot", "runtime", ["ps2-hardware"], 5),
    edge("edge-ee-packets", "node-ee-runtime", "node-packets", "draw", "runtime", ["ps2-hardware"], 5),
    edge("edge-packets-vu", "node-packets", "node-vu1", "execute", "runtime", ["ps2-hardware"], 5),
    edge("edge-vu-gs", "node-vu1", "node-gs", "rasterize", "runtime", ["ps2-hardware"], 5)
  ];

  const callouts: DiagramCallout[] = [
    callout("callout-core-boundary", "Shared C# runtime", ["Core owns update, scenes, entities, content", "Runtime reads cooked assets; files writes them"], "csharp", { x: 540, y: 805, width: 360, height: 125 }, ["shared-core"], 2),
    callout("callout-codegen-boundary", "Only the runtime subset crosses", ["The editor and builder stay C#", "Selected runtime C# -> generated C++", "The player receives generated core, not the editor"], "generated-cpp", { x: 990, y: 700, width: 360, height: 165 }, ["build-conversion"], 3),
    callout("callout-player-link", "Two C++ sources become one player", ["Generated core + handwritten PS2 C++", "The linker produces the EE executable"], "handwritten-cpp", { x: 1440, y: 900, width: 360, height: 130 }, ["ps2-player"], 4),
    callout("callout-hardware-flow", "VIF/GIF/VU/GS", ["C++ frame planning becomes packets", "VU1 transforms and shades; GS produces the image"], "ps2", { x: 1890, y: 745, width: 360, height: 145 }, ["ps2-hardware"], 5)
  ];

  return { title: "How Helengine reaches the PlayStation 2", subtitle: "A C# editor and shared runtime become generated C++ plus handwritten PS2 code", viewBox, sections, nodes, edges, callouts };
}

function section(id: string, title: string, subtitle: string, bounds: ViewBox, step: number, nodeIds: string[], edgeIds: string[], calloutIds: string[]): DiagramSection {
  return { id, title, subtitle, bounds, step, nodeIds, edgeIds, calloutIds };
}

function node(id: string, title: string, subtitle: string, kind: NodeKind, role: DiagramRole, bounds: ViewBox, lines: string[], step: number): DiagramNode {
  const sectionId = resolveSectionId(bounds);
  return { id, title, subtitle, lines, role, kind, bounds, sectionIds: [sectionId], step };
}

function edge(id: string, from: string, to: string, label: string, role: EdgeRole, sectionIds: string[], step: number): DiagramEdge {
  return { id, from, to, label, role, sectionIds, step };
}

function callout(id: string, title: string, lines: string[], role: DiagramRole, bounds: ViewBox, sectionIds: string[], step: number): DiagramCallout {
  return { id, title, lines, role, bounds, sectionIds, step };
}

function resolveSectionId(bounds: ViewBox): string {
  if (bounds.x < sectionBounds["shared-core"].x) {
    return "authoring";
  }
  if (bounds.x < sectionBounds["build-conversion"].x) {
    return "shared-core";
  }
  if (bounds.x < sectionBounds["ps2-player"].x) {
    return "build-conversion";
  }
  if (bounds.x < sectionBounds["ps2-hardware"].x) {
    return "ps2-player";
  }
  return "ps2-hardware";
}
