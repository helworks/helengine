import type { DiagramCallout, DiagramDocument, DiagramEdge, DiagramNode, DiagramSection, ViewBox } from "./model.js";
import { validateDocument } from "./model.js";
import { defaultTheme, type DiagramTheme } from "./theme.js";

export interface RenderOptions {
  sectionId?: string;
  margin?: number;
  theme?: DiagramTheme;
}

const defaultSectionMargin = 24;

export function renderDocument(document: DiagramDocument, options: RenderOptions = {}): string {
  validateDocument(document);

  const theme = options.theme ?? defaultTheme;
  const selectedSections = resolveSelectedSections(document, options.sectionId);
  const selectedNodeIds = new Set(selectedSections.flatMap(section => section.nodeIds));
  const selectedEdgeIds = new Set(selectedSections.flatMap(section => section.edgeIds));
  const selectedCalloutIds = new Set(selectedSections.flatMap(section => section.calloutIds));
  const nodesById = new Map(document.nodes.map(node => [node.id, node]));
  const viewBox = resolveRenderViewBox(document, options.sectionId, options.margin);
  const output: string[] = [];

  output.push("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
  output.push(`<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"${formatViewBox(viewBox)}\" role=\"img\" aria-label=\"${escapeXml(document.title)}\">`);
  output.push(`<title>${escapeXml(document.title)}</title>`);
  output.push(`<desc>${escapeXml(document.subtitle ?? "Architecture diagram")}</desc>`);
  output.push(renderDefinitions(theme));
  output.push(`<rect id=\"diagram-background\" x=\"${formatNumber(viewBox.x)}\" y=\"${formatNumber(viewBox.y)}\" width=\"${formatNumber(viewBox.width)}\" height=\"${formatNumber(viewBox.height)}\" fill=\"${theme.background}\"/>`);
  output.push(renderHeader(document, viewBox, theme));

  for (const section of selectedSections) {
    output.push(renderSection(section, theme));
  }

  for (const edge of document.edges) {
    if (!selectedEdgeIds.has(edge.id) || !selectedNodeIds.has(edge.from) || !selectedNodeIds.has(edge.to)) {
      continue;
    }
    output.push(renderEdge(edge, nodesById.get(edge.from), nodesById.get(edge.to), theme));
  }

  for (const node of document.nodes) {
    if (selectedNodeIds.has(node.id)) {
      output.push(renderNode(node, theme));
    }
  }

  for (const callout of document.callouts) {
    if (selectedCalloutIds.has(callout.id)) {
      output.push(renderCallout(callout, theme));
    }
  }

  output.push("</svg>");
  return output.join("\n");
}

export function resolveRenderViewBox(document: DiagramDocument, sectionId?: string, margin = defaultSectionMargin): ViewBox {
  if (sectionId === undefined) {
    return document.viewBox;
  }

  if (!Number.isFinite(margin) || margin < 0) {
    throw new Error("Render margin must be a finite non-negative number.");
  }

  const section = document.sections.find(candidate => candidate.id === sectionId);
  if (section === undefined) {
    throw new Error(`Unknown section '${sectionId}'.`);
  }

  return {
    x: section.bounds.x - margin,
    y: section.bounds.y - margin,
    width: section.bounds.width + margin * 2,
    height: section.bounds.height + margin * 2
  };
}

function resolveSelectedSections(document: DiagramDocument, sectionId?: string): DiagramSection[] {
  if (sectionId === undefined) {
    return document.sections;
  }

  const section = document.sections.find(candidate => candidate.id === sectionId);
  if (section === undefined) {
    throw new Error(`Unknown section '${sectionId}'.`);
  }
  return [section];
}

function renderDefinitions(theme: DiagramTheme): string {
  const markers = ["runtime", "generation", "packaging", "context"].map(role => {
    const color = theme.edgeRoles[role as keyof typeof theme.edgeRoles];
    return `<marker id=\"arrow-${role}\" markerWidth=\"8\" markerHeight=\"8\" refX=\"7\" refY=\"4\" orient=\"auto\" markerUnits=\"strokeWidth\"><path d=\"M0,0 L8,4 L0,8 z\" fill=\"${color}\"/></marker>`;
  });
  return `<defs>${markers.join("")}</defs>`;
}

function renderHeader(document: DiagramDocument, viewBox: ViewBox, theme: DiagramTheme): string {
  const x = viewBox.x + 32;
  const y = viewBox.y + 44;
  const subtitle = document.subtitle === undefined ? "" : `<text x=\"${formatNumber(x)}\" y=\"${formatNumber(y + 28)}\" fill=\"${theme.muted}\" font-family=\"${theme.fontFamily}\" font-size=\"${theme.subtitleSize}\">${escapeXml(document.subtitle)}</text>`;
  return `<g id=\"diagram-header\"><text x=\"${formatNumber(x)}\" y=\"${formatNumber(y)}\" fill=\"${theme.foreground}\" font-family=\"${theme.fontFamily}\" font-size=\"${theme.titleSize}\" font-weight=\"700\">${escapeXml(document.title)}</text>${subtitle}</g>`;
}

function renderSection(section: DiagramSection, theme: DiagramTheme): string {
  const bounds = section.bounds;
  const subtitle = section.subtitle === undefined ? "" : `<text x=\"${formatNumber(bounds.x + 20)}\" y=\"${formatNumber(bounds.y + 50)}\" fill=\"${theme.muted}\" font-family=\"${theme.fontFamily}\" font-size=\"${theme.subtitleSize}\">${escapeXml(section.subtitle)}</text>`;
  return `<g id=\"section-${escapeXml(section.id)}\" data-step=\"${section.step}\" data-kind=\"section\"><rect x=\"${formatNumber(bounds.x)}\" y=\"${formatNumber(bounds.y)}\" width=\"${formatNumber(bounds.width)}\" height=\"${formatNumber(bounds.height)}\" rx=\"${theme.cornerRadius}\" fill=\"${theme.sectionFill}\" stroke=\"${theme.sectionStroke}\" stroke-width=\"2\"/><text x=\"${formatNumber(bounds.x + 20)}\" y=\"${formatNumber(bounds.y + 30)}\" fill=\"${theme.foreground}\" font-family=\"${theme.fontFamily}\" font-size=\"${theme.subtitleSize + 2}\" font-weight=\"700\">${escapeXml(section.title)}</text>${subtitle}</g>`;
}

function renderEdge(edge: DiagramEdge, from: DiagramNode | undefined, to: DiagramNode | undefined, theme: DiagramTheme): string {
  if (from === undefined || to === undefined) {
    return "";
  }

  const fromPoint = centerOf(from.bounds);
  const toPoint = centerOf(to.bounds);
  const color = theme.edgeRoles[edge.role];
  const dash = theme.edgeDash[edge.role];
  const dashAttribute = dash === "" ? "" : ` stroke-dasharray=\"${dash}\"`;
  const label = edge.label === undefined ? "" : `<text x=\"${formatNumber((fromPoint.x + toPoint.x) / 2)}\" y=\"${formatNumber((fromPoint.y + toPoint.y) / 2 - 8)}\" text-anchor=\"middle\" fill=\"${theme.muted}\" font-family=\"${theme.fontFamily}\" font-size=\"${theme.bodySize}\">${escapeXml(edge.label)}</text>`;
  return `<g id=\"edge-${escapeXml(edge.id)}\" data-step=\"${edge.step}\" data-role=\"${edge.role}\"><line x1=\"${formatNumber(fromPoint.x)}\" y1=\"${formatNumber(fromPoint.y)}\" x2=\"${formatNumber(toPoint.x)}\" y2=\"${formatNumber(toPoint.y)}\" stroke=\"${color}\" stroke-width=\"3\"${dashAttribute} marker-end=\"url(#arrow-${edge.role})\"/>${label}</g>`;
}

function renderNode(node: DiagramNode, theme: DiagramTheme): string {
  const bounds = node.bounds;
  const color = theme.roles[node.role];
  const title = `<text x=\"${formatNumber(bounds.x + 16)}\" y=\"${formatNumber(bounds.y + 28)}\" fill=\"${theme.foreground}\" font-family=\"${theme.fontFamily}\" font-size=\"${theme.subtitleSize}\" font-weight=\"700\">${escapeXml(node.title)}</text>`;
  const subtitle = node.subtitle === undefined ? "" : `<text x=\"${formatNumber(bounds.x + 16)}\" y=\"${formatNumber(bounds.y + 46)}\" fill=\"${theme.muted}\" font-family=\"${theme.fontFamily}\" font-size=\"${theme.bodySize}\">${escapeXml(node.subtitle)}</text>`;
  const lines = node.lines.map((line, index) => `<text x=\"${formatNumber(bounds.x + 16)}\" y=\"${formatNumber(bounds.y + 64 + index * 16)}\" fill=\"${theme.muted}\" font-family=\"${theme.fontFamily}\" font-size=\"${theme.bodySize}\">${escapeXml(line)}</text>`).join("");
  return `<g id=\"node-${escapeXml(node.id)}\" data-step=\"${node.step}\" data-role=\"${node.role}\" data-kind=\"${node.kind}\"><rect x=\"${formatNumber(bounds.x)}\" y=\"${formatNumber(bounds.y)}\" width=\"${formatNumber(bounds.width)}\" height=\"${formatNumber(bounds.height)}\" rx=\"${theme.cornerRadius - 4}\" fill=\"${color}\" fill-opacity=\"0.18\" stroke=\"${color}\" stroke-width=\"2\"/>${title}${subtitle}${lines}</g>`;
}

function renderCallout(callout: DiagramCallout, theme: DiagramTheme): string {
  const bounds = callout.bounds;
  const color = theme.roles[callout.role];
  const title = `<text x=\"${formatNumber(bounds.x + 16)}\" y=\"${formatNumber(bounds.y + 26)}\" fill=\"${theme.foreground}\" font-family=\"${theme.fontFamily}\" font-size=\"${theme.bodySize}\" font-weight=\"700\">${escapeXml(callout.title)}</text>`;
  const lines = callout.lines.map((line, index) => `<text x=\"${formatNumber(bounds.x + 16)}\" y=\"${formatNumber(bounds.y + 48 + index * 16)}\" fill=\"${theme.muted}\" font-family=\"${theme.fontFamily}\" font-size=\"${theme.bodySize}\">${escapeXml(line)}</text>`).join("");
  return `<g id=\"callout-${escapeXml(callout.id)}\" data-step=\"${callout.step}\" data-role=\"${callout.role}\"><rect x=\"${formatNumber(bounds.x)}\" y=\"${formatNumber(bounds.y)}\" width=\"${formatNumber(bounds.width)}\" height=\"${formatNumber(bounds.height)}\" rx=\"${theme.cornerRadius - 6}\" fill=\"${color}\" fill-opacity=\"0.08\" stroke=\"${color}\" stroke-dasharray=\"4 5\"/>${title}${lines}</g>`;
}

function centerOf(bounds: ViewBox): { x: number; y: number } {
  return { x: bounds.x + bounds.width / 2, y: bounds.y + bounds.height / 2 };
}

function formatViewBox(viewBox: ViewBox): string {
  return [viewBox.x, viewBox.y, viewBox.width, viewBox.height].map(formatNumber).join(" ");
}

function formatNumber(value: number): string {
  const rounded = Number(value.toFixed(3));
  return Object.is(rounded, -0) ? "0" : String(rounded);
}

function escapeXml(value: string): string {
  return value.replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&apos;");
}
