# Bézier Architecture Diagram Edges

## Goal

Render architecture-diagram connections as smooth cubic Bézier curves instead of straight SVG lines, while preserving the current 40% edge opacity and arrowheads.

## Design

The renderer will continue to derive each edge from the centers of its source and destination node bounds. It will emit an SVG `<path>` using a cubic Bézier command:

```text
M sourceX,sourceY C midpointX,sourceY midpointX,destinationY destinationX,destinationY
```

Using the horizontal midpoint for both control points gives every edge horizontal tangents at its endpoints and a consistent S-shaped flow. The edge remains grouped under its stable `edge-*` id and keeps its role-specific stroke, dash pattern, `opacity="0.4"`, and `marker-end` arrowhead.

The edge label remains a separate text element at the midpoint of the endpoint centers, so it is not affected by edge opacity.

## Scope and compatibility

- No changes to the diagram document model or manifest schema.
- No per-edge control-point configuration.
- Existing section filtering, stable ids, accessibility attributes, roles, dashes, and arrow markers remain unchanged.
- The generated PS2 overview SVG will be regenerated after the renderer change.

## Testing

- Update the renderer test to require a cubic Bézier path and retain the 40% opacity and generation marker assertions.
- Run the complete TypeScript diagram test suite.
- Parse the regenerated SVG as XML and verify all generated connection paths contain cubic Bézier commands and `opacity="0.4"`.
