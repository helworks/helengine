export type DiagramRole = "csharp" | "generated-cpp" | "handwritten-cpp" | "artifact" | "ps2" | "neutral";

export type EdgeRole = "runtime" | "generation" | "packaging" | "context";

export type NodeKind = "module" | "process" | "artifact" | "hardware" | "boundary";

export interface ViewBox {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface DiagramSection {
  id: string;
  title: string;
  subtitle?: string;
  bounds: ViewBox;
  step: number;
  nodeIds: string[];
  edgeIds: string[];
  calloutIds: string[];
}

export interface DiagramNode {
  id: string;
  title: string;
  subtitle?: string;
  lines: string[];
  role: DiagramRole;
  kind: NodeKind;
  bounds: ViewBox;
  sectionIds: string[];
  step: number;
}

export interface DiagramEdge {
  id: string;
  from: string;
  to: string;
  label?: string;
  role: EdgeRole;
  sectionIds: string[];
  step: number;
}

export interface DiagramCallout {
  id: string;
  title: string;
  lines: string[];
  role: DiagramRole;
  bounds: ViewBox;
  sectionIds: string[];
  step: number;
}

export interface DiagramDocument {
  title: string;
  subtitle?: string;
  viewBox: ViewBox;
  sections: DiagramSection[];
  nodes: DiagramNode[];
  edges: DiagramEdge[];
  callouts: DiagramCallout[];
}

export function validateDocument(document: DiagramDocument): void {
  if (!document || typeof document !== "object") {
    throw new Error("Diagram document is required.");
  }

  assertPositiveViewBox(document.viewBox, "document viewBox");

  const sectionIds = assertUniqueIds(document.sections, "section");
  const nodeIds = assertUniqueIds(document.nodes, "node");
  const edgeIds = assertUniqueIds(document.edges, "edge");
  const calloutIds = assertUniqueIds(document.callouts, "callout");

  const sectionsById = new Map(document.sections.map(section => [section.id, section]));

  for (const section of document.sections) {
    assertPositiveViewBox(section.bounds, `section '${section.id}' bounds`);
    assertReferencedIds(section.nodeIds, nodeIds, `section '${section.id}' node`);
    assertReferencedIds(section.edgeIds, edgeIds, `section '${section.id}' edge`);
    assertReferencedIds(section.calloutIds, calloutIds, `section '${section.id}' callout`);
  }

  for (const node of document.nodes) {
    assertPositiveViewBox(node.bounds, `node '${node.id}' bounds`);
    assertSectionMembership(node.sectionIds, sectionsById, sectionId => {
      const section = sectionsById.get(sectionId);
      return section !== undefined && section.nodeIds.includes(node.id);
    }, `node '${node.id}'`);
  }

  for (const edge of document.edges) {
    if (!nodeIds.has(edge.from) || !nodeIds.has(edge.to)) {
      throw new Error(`Edge '${edge.id}' references a missing node.`);
    }

    assertSectionMembership(edge.sectionIds, sectionsById, sectionId => {
      const section = sectionsById.get(sectionId);
      return section !== undefined && section.edgeIds.includes(edge.id);
    }, `edge '${edge.id}'`);
  }

  for (const callout of document.callouts) {
    assertPositiveViewBox(callout.bounds, `callout '${callout.id}' bounds`);
    assertSectionMembership(callout.sectionIds, sectionsById, sectionId => {
      const section = sectionsById.get(sectionId);
      return section !== undefined && section.calloutIds.includes(callout.id);
    }, `callout '${callout.id}'`);
  }

  for (const section of document.sections) {
    assertReverseMembership(section.nodeIds, document.nodes, node => node.sectionIds, section.id, "node", section.id);
    assertReverseMembership(section.edgeIds, document.edges, edge => edge.sectionIds, section.id, "edge", section.id);
    assertReverseMembership(section.calloutIds, document.callouts, callout => callout.sectionIds, section.id, "callout", section.id);
  }
}

function assertPositiveViewBox(bounds: ViewBox, description: string): void {
  if (!bounds || ![bounds.x, bounds.y, bounds.width, bounds.height].every(Number.isFinite) || bounds.width <= 0 || bounds.height <= 0) {
    throw new Error(`${description} must have finite coordinates and positive dimensions.`);
  }
}

function assertUniqueIds<T extends { id: string }>(items: T[], label: string): Set<string> {
  const ids = new Set<string>();
  for (const item of items) {
    if (ids.has(item.id)) {
      throw new Error(`Duplicate ${label} id '${item.id}'.`);
    }
    ids.add(item.id);
  }
  return ids;
}

function assertReferencedIds(ids: string[], knownIds: Set<string>, label: string): void {
  for (const id of ids) {
    if (!knownIds.has(id)) {
      throw new Error(`The ${label} '${id}' is missing.`);
    }
  }
}

function assertSectionMembership(
  sectionIds: string[],
  sectionsById: Map<string, DiagramSection>,
  containsReference: (sectionId: string) => boolean,
  description: string
): void {
  for (const sectionId of sectionIds) {
    if (!sectionsById.has(sectionId)) {
      throw new Error(`${description} references a missing section '${sectionId}'.`);
    }
    if (!containsReference(sectionId)) {
      throw new Error(`${description} is not listed by section '${sectionId}'.`);
    }
  }
}

function assertReverseMembership<T extends { id: string }>(
  ids: string[],
  items: T[],
  getSectionIds: (item: T) => string[],
  sectionId: string,
  label: string,
  sectionDescription: string
): void {
  for (const id of ids) {
    const item = items.find(candidate => candidate.id === id);
    if (!item || !getSectionIds(item).includes(sectionId)) {
      throw new Error(`Section '${sectionDescription}' lists ${label} '${id}' without matching membership.`);
    }
  }
}
