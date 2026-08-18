import { strict as assert } from "node:assert";
import { test } from "node:test";
import type { DiagramDocument } from "./model.js";
import { buildManifest } from "./manifest.js";

const document: DiagramDocument = {
  title: "Manifest diagram",
  viewBox: { x: 0, y: 0, width: 200, height: 100 },
  sections: [
    {
      id: "section-a",
      title: "A",
      bounds: { x: 0, y: 0, width: 100, height: 100 },
      step: 1,
      nodeIds: ["node-a"],
      edgeIds: [],
      calloutIds: []
    }
  ],
  nodes: [
    {
      id: "node-a",
      title: "A",
      lines: [],
      role: "neutral",
      kind: "module",
      bounds: { x: 10, y: 10, width: 30, height: 20 },
      sectionIds: ["section-a"],
      step: 1
    }
  ],
  edges: [],
  callouts: []
};

test("builds stable section and element metadata without SVG markup", () => {
  const manifest = buildManifest(document);

  assert.equal(manifest.title, "Manifest diagram");
  assert.deepEqual(manifest.viewBox, document.viewBox);
  assert.deepEqual(manifest.sections, [{ id: "section-a", bounds: document.sections[0].bounds, step: 1 }]);
  assert.deepEqual(manifest.nodes, ["node-a"]);
  assert.deepEqual(manifest.edges, []);
  assert.deepEqual(manifest.callouts, []);
  assert.doesNotMatch(JSON.stringify(manifest), /<svg/i);
});
