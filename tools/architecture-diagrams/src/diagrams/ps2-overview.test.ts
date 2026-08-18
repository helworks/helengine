import { strict as assert } from "node:assert";
import { test } from "node:test";
import { validateDocument } from "../model.js";
import { createPs2OverviewDocument } from "./ps2-overview.js";

test("defines the five-section PS2 narrative in reveal order", () => {
  const document = createPs2OverviewDocument();

  assert.doesNotThrow(() => validateDocument(document));
  assert.deepEqual(
    document.sections.slice().sort((left, right) => left.step - right.step).map(section => section.id),
    ["authoring", "shared-core", "build-conversion", "ps2-player", "ps2-hardware"]
  );
});

test("names the important language, build, and hardware boundaries", () => {
  const document = createPs2OverviewDocument();
  const searchableText = [
    ...document.nodes.flatMap(node => [node.title, node.subtitle ?? "", ...node.lines]),
    ...document.callouts.flatMap(callout => [callout.title, ...callout.lines])
  ].join(" ").toLowerCase();

  for (const phrase of [
    "helengine.core",
    "helengine.editor",
    "helengine.files",
    "csharpcodegen",
    "generated c++",
    "handwritten ps2 c++",
    "game.iso",
    "vif/gif packets"
  ]) {
    assert.ok(searchableText.includes(phrase), `Missing diagram phrase: ${phrase}`);
  }

});

test("does not render the editor-host explanatory callout", () => {
  const document = createPs2OverviewDocument();
  const calloutIds = document.callouts.map(callout => callout.id);
  const calloutText = document.callouts.flatMap(callout => [callout.title, ...callout.lines]).join(" ");

  assert.doesNotMatch(calloutIds.join(" "), /callout-editor-not-shipped/);
  assert.doesNotMatch(calloutText, /Editor stays on the host/);
});

test("does not render explanatory callouts", () => {
  const document = createPs2OverviewDocument();

  assert.deepEqual(document.callouts, []);
  assert.ok(document.sections.every(section => section.calloutIds.length === 0));
});

test("does not render connection lines", () => {
  const document = createPs2OverviewDocument();

  assert.deepEqual(document.edges, []);
  assert.ok(document.sections.every(section => section.edgeIds.length === 0));
});
