import type { DiagramDocument } from "./model.js";

export type DiagramFactory = () => DiagramDocument;

const diagramFactories = new Map<string, DiagramFactory>();

export function registerDiagram(name: string, factory: DiagramFactory): void {
  if (diagramFactories.has(name)) {
    throw new Error(`Diagram '${name}' is already registered.`);
  }
  diagramFactories.set(name, factory);
}

export function listDiagrams(): string[] {
  return Array.from(diagramFactories.keys()).sort((left, right) => left.localeCompare(right));
}

export function getDiagram(name: string): DiagramDocument {
  const factory = diagramFactories.get(name);
  if (factory === undefined) {
    throw new Error(`Unknown diagram '${name}'. Available diagrams: ${listDiagrams().join(", ") || "none"}.`);
  }
  return factory();
}

export function resetDiagramRegistry(): void {
  diagramFactories.clear();
}
