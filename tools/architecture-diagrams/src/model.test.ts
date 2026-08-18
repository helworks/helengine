import { strict as assert } from "node:assert";
import { test } from "node:test";
import { validateDocument } from "./model.js";
import type { DiagramDocument } from "./model.js";

function createValidDocument(): DiagramDocument {
  return {
    title: "Test diagram",
    viewBox: { x: 0, y: 0, width: 200, height: 100 },
    sections: [
      {
        id: "section-a",
        title: "Section A",
        bounds: { x: 0, y: 0, width: 200, height: 100 },
        step: 1,
        nodeIds: ["node-a", "node-b"],
        edgeIds: ["edge-a"],
        calloutIds: []
      }
    ],
    nodes: [
      {
        id: "node-a",
        title: "Node A",
        lines: [],
        role: "csharp",
        kind: "module",
        bounds: { x: 20, y: 20, width: 60, height: 30 },
        sectionIds: ["section-a"],
        step: 1
      },
      {
        id: "node-b",
        title: "Node B",
        lines: [],
        role: "artifact",
        kind: "artifact",
        bounds: { x: 120, y: 20, width: 60, height: 30 },
        sectionIds: ["section-a"],
        step: 2
      }
    ],
    edges: [
      {
        id: "edge-a",
        from: "node-a",
        to: "node-b",
        role: "runtime",
        sectionIds: ["section-a"],
        step: 2
      }
    ],
    callouts: []
  };
}

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
