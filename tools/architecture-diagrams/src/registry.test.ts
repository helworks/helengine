import { strict as assert } from "node:assert";
import { test } from "node:test";
import type { DiagramDocument } from "./model.js";
import { getDiagram, listDiagrams, registerDiagram, resetDiagramRegistry } from "./registry.js";

function createDocument(title: string): DiagramDocument {
  return {
    title,
    viewBox: { x: 0, y: 0, width: 10, height: 10 },
    sections: [],
    nodes: [],
    edges: [],
    callouts: []
  };
}

test("lists registered diagram names in sorted order", () => {
  resetDiagramRegistry();
  registerDiagram("zeta", () => createDocument("Zeta"));
  registerDiagram("alpha", () => createDocument("Alpha"));

  assert.deepEqual(listDiagrams(), ["alpha", "zeta"]);
});

test("returns a fresh document from a registered factory", () => {
  resetDiagramRegistry();
  registerDiagram("alpha", () => createDocument("Alpha"));

  const first = getDiagram("alpha");
  first.title = "Changed";

  assert.equal(getDiagram("alpha").title, "Alpha");
});

test("names an unknown diagram in its error", () => {
  resetDiagramRegistry();
  assert.throws(() => getDiagram("missing"), /missing/i);
});
