import { strict as assert } from "node:assert";
import { test } from "node:test";
import type { DiagramDocument } from "./model.js";
import { renderDocument } from "./renderer.js";

function createRendererDocument(): DiagramDocument {
  return {
    title: "Renderer & test diagram",
    subtitle: "A scalable document",
    viewBox: { x: 0, y: 0, width: 320, height: 180 },
    sections: [
      {
        id: "section-a",
        title: "Section A",
        subtitle: "First section",
        bounds: { x: 0, y: 0, width: 140, height: 120 },
        step: 1,
        nodeIds: ["node-a", "node-b"],
        edgeIds: ["edge-a"],
        calloutIds: []
      },
      {
        id: "section-b",
        title: "Section B",
        bounds: { x: 160, y: 0, width: 140, height: 120 },
        step: 2,
        nodeIds: ["node-c"],
        edgeIds: [],
        calloutIds: []
      }
    ],
    nodes: [
      {
        id: "node-a",
        title: "C# & <runtime>",
        subtitle: "Managed",
        lines: ["Core"],
        role: "csharp",
        kind: "module",
        bounds: { x: 20, y: 30, width: 50, height: 30 },
        sectionIds: ["section-a"],
        step: 1
      },
      {
        id: "node-b",
        title: "Generated C++",
        lines: [],
        role: "generated-cpp",
        kind: "artifact",
        bounds: { x: 80, y: 70, width: 50, height: 30 },
        sectionIds: ["section-a"],
        step: 2
      },
      {
        id: "node-c",
        title: "PS2 hardware",
        lines: [],
        role: "ps2",
        kind: "hardware",
        bounds: { x: 180, y: 30, width: 70, height: 30 },
        sectionIds: ["section-b"],
        step: 3
      }
    ],
    edges: [
      {
        id: "edge-a",
        from: "node-a",
        to: "node-b",
        label: "convert",
        role: "generation",
        sectionIds: ["section-a"],
        step: 2
      }
    ],
    callouts: []
  };
}

test("renders accessible SVG with stable ids and escaped text", () => {
  const output = renderDocument(createRendererDocument());

  assert.match(output, /<svg[^>]+viewBox="0 0 320 180"/);
  assert.match(output, /role="img"/);
  assert.match(output, /<title>Renderer &amp; test diagram<\/title>/);
  assert.match(output, /<desc>A scalable document<\/desc>/);
  assert.match(output, /id="section-section-a"[^>]+data-step="1"/);
  assert.match(output, /id="node-node-a"/);
  assert.match(output, /id="edge-edge-a"[^>]+data-step="2"/);
  assert.match(output, /<path[^>]+d="M 45 45 C 75 45 75 85 105 85"[^>]+opacity="0.4"/);
  assert.match(output, /C# &amp; &lt;runtime&gt;/);
  assert.doesNotMatch(output, /<marker\b/);
  assert.doesNotMatch(output, /marker-end=/);
});

test("renders a selected section using a cropped viewBox", () => {
  const output = renderDocument(createRendererDocument(), { sectionId: "section-a" });

  assert.match(output, /viewBox="-24 -24 188 168"/);
  assert.match(output, /id="section-section-a"/);
  assert.doesNotMatch(output, /id="section-section-b"/);
  assert.doesNotMatch(output, /id="node-node-c"/);
});
