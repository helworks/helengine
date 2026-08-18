import { mkdir, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { buildManifest } from "./manifest.js";
import { getDiagram, listDiagrams } from "./registry.js";
import { renderDocument } from "./renderer.js";

export interface ListArguments {
  command: "list";
}

export interface RenderArguments {
  command: "render";
  name: string;
  sectionId?: string;
  outputPath: string;
  manifestPath?: string;
}

export type CliArguments = ListArguments | RenderArguments;

export function parseArguments(argv: string[]): CliArguments {
  const command = argv[0];
  if (command === "list") {
    if (argv.length !== 1) {
      throw new Error("The list command does not accept additional arguments.");
    }
    return { command: "list" };
  }

  if (command !== "render") {
    throw new Error("Expected command 'list' or 'render'.");
  }

  const name = argv[1];
  if (name === undefined || name.startsWith("--")) {
    throw new Error("A diagram name is required for render.");
  }

  let sectionId: string | undefined;
  let outputPath: string | undefined;
  let manifestPath: string | undefined;

  for (let index = 2; index < argv.length; index++) {
    const option = argv[index];
    const value = argv[index + 1];
    if (value === undefined || value.startsWith("--")) {
      throw new Error(`Option '${option}' requires a value.`);
    }

    if (option === "--section") {
      sectionId = value;
    } else if (option === "--output") {
      outputPath = value;
    } else if (option === "--manifest") {
      manifestPath = value;
    } else {
      throw new Error(`Unknown option '${option}'.`);
    }
    index++;
  }

  if (outputPath === undefined) {
    throw new Error("An output path is required for render.");
  }

  return { command: "render", name, sectionId, outputPath, manifestPath };
}

export async function runCli(argv: string[]): Promise<number> {
  const argumentsValue = parseArguments(argv);
  if (argumentsValue.command === "list") {
    process.stdout.write(`${listDiagrams().join("\n")}\n`);
    return 0;
  }

  const document = getDiagram(argumentsValue.name);
  const svgPath = resolve(argumentsValue.outputPath);
  await writeTextFile(svgPath, renderDocument(document, { sectionId: argumentsValue.sectionId }));

  if (argumentsValue.manifestPath !== undefined) {
    await writeTextFile(resolve(argumentsValue.manifestPath), `${JSON.stringify(buildManifest(document), null, 2)}\n`);
  }

  return 0;
}

async function writeTextFile(path: string, contents: string): Promise<void> {
  await mkdir(dirname(path), { recursive: true });
  await writeFile(path, contents, "utf8");
}

function isMainModule(): boolean {
  if (process.argv[1] === undefined) {
    return false;
  }
  return import.meta.url === pathToFileURL(resolve(process.argv[1])).href;
}

if (isMainModule()) {
  runCli(process.argv.slice(2)).then(
    code => {
      process.exitCode = code;
    },
    error => {
      process.stderr.write(`Error: ${error instanceof Error ? error.message : String(error)}\n`);
      process.exitCode = 1;
    }
  );
}
