import type { DiagramDocument, DiagramRole, EdgeRole, ViewBox } from "./model.js";
import { validateDocument } from "./model.js";
import { defaultTheme } from "./theme.js";

export interface DiagramManifest {
  title: string;
  viewBox: ViewBox;
  sections: Array<{ id: string; bounds: ViewBox; step: number }>;
  nodes: Array<{ id: string; step: number; role: DiagramRole }>;
  edges: Array<{ id: string; step: number; role: EdgeRole }>;
  callouts: Array<{ id: string; step: number; role: DiagramRole }>;
  theme: { roles: Record<DiagramRole, string> };
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
    nodes: document.nodes.map(node => ({ id: node.id, step: node.step, role: node.role })),
    edges: document.edges.map(edge => ({ id: edge.id, step: edge.step, role: edge.role })),
    callouts: document.callouts.map(callout => ({ id: callout.id, step: callout.step, role: callout.role })),
    theme: { roles: { ...defaultTheme.roles } }
  };
}
