import type { DiagramDocument, ViewBox } from "./model.js";
import { validateDocument } from "./model.js";

export interface DiagramManifest {
  title: string;
  viewBox: ViewBox;
  sections: Array<{ id: string; bounds: ViewBox; step: number }>;
  nodes: string[];
  edges: string[];
  callouts: string[];
}

export function buildManifest(document: DiagramDocument): DiagramManifest {
  validateDocument(document);
  return {
    title: document.title,
    viewBox: { ...document.viewBox },
    sections: document.sections.map(section => ({
      id: section.id,
      bounds: { ...section.bounds },
      step: section.step
    })),
    nodes: document.nodes.map(node => node.id),
    edges: document.edges.map(edge => edge.id),
    callouts: document.callouts.map(callout => callout.id)
  };
}
