import type { DiagramRole, EdgeRole } from "./model.js";

export interface DiagramTheme {
  background: string;
  foreground: string;
  muted: string;
  sectionFill: string;
  sectionStroke: string;
  roles: Record<DiagramRole, string>;
  edgeRoles: Record<EdgeRole, string>;
  edgeDash: Record<EdgeRole, string>;
  edgeOpacity: number;
  fontFamily: string;
  titleSize: number;
  subtitleSize: number;
  bodySize: number;
  cornerRadius: number;
}

export const defaultTheme: DiagramTheme = {
  background: "#10131A",
  foreground: "#F4F7FB",
  muted: "#AAB5C5",
  sectionFill: "#151B25",
  sectionStroke: "#334155",
  roles: {
    csharp: "#8B7CFF",
    "generated-cpp": "#F2B84B",
    "handwritten-cpp": "#41D6C3",
    artifact: "#79D98C",
    ps2: "#FF8C5A",
    neutral: "#8EA0B8"
  },
  edgeRoles: {
    runtime: "#D7E0EA",
    generation: "#F2B84B",
    packaging: "#79D98C",
    context: "#718096"
  },
  edgeDash: {
    runtime: "",
    generation: "9 7",
    packaging: "2 7",
    context: "5 5"
  },
  edgeOpacity: 0.4,
  fontFamily: "Inter, Segoe UI, sans-serif",
  titleSize: 22,
  subtitleSize: 13,
  bodySize: 12,
  cornerRadius: 14
};
