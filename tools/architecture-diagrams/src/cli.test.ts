import { strict as assert } from "node:assert";
import { mkdtemp, readFile, rm } from "node:fs/promises";
import { test } from "node:test";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { parseArguments, runCli } from "./cli.js";

test("parses the list command", () => {
  assert.deepEqual(parseArguments(["list"]), { command: "list" });
});

test("parses a render command with section and manifest options", () => {
  assert.deepEqual(
    parseArguments(["render", "ps2-overview", "--section", "build-conversion", "--output", "out.svg"]),
    {
      command: "render",
      name: "ps2-overview",
      sectionId: "build-conversion",
      outputPath: "out.svg",
      manifestPath: undefined
    }
  );
});

test("rejects render commands without a diagram name or output", () => {
  assert.throws(() => parseArguments(["render"]), /diagram name/i);
  assert.throws(() => parseArguments(["render", "ps2-overview"]), /output/i);
});

test("writes SVG and optional manifest output while creating parent directories", async () => {
  const root = await mkdtemp(join(tmpdir(), "helengine-diagram-cli-"));
  const outputPath = join(root, "nested", "overview.svg");
  const manifestPath = join(root, "nested", "overview.manifest.json");

  try {
    const result = await runCli(["render", "ps2-overview", "--output", outputPath, "--manifest", manifestPath]);

    assert.equal(result, 0);
    assert.match(await readFile(outputPath, "utf8"), /<svg/);
    assert.match(await readFile(manifestPath, "utf8"), /ps2-overview|How Helengine reaches/);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});
