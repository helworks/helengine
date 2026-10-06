# Depth-ordered 2D

The user approved replacing authored Draw Order with depth and hierarchy, and explicitly requested its removal from internal editor UI too.

## Contract

- Remove the per-drawable `RenderOrder2D` API, inspector field, constants and persisted output. Do not replace it with another hidden sorting priority.
- Screen-space 2D uses composed entity Z: smaller Z draws first, larger Z is in front. Equal depth follows scene hierarchy (parent before descendants, siblings in their current order); components sharing an entity follow component order. Root order follows the object manager's stable entity registration order.
- Moving, reparenting, enabling or disabling content changes drawing and pointer precedence consistently without re-adding components manually. Static command caches must see ordering changes.
- World-space preview planes use camera-relative transparent back-to-front ordering and stable hierarchy ties. Rotating the scene camera must change physical overlap correctly. Existing authored 3D render order and camera pass order are outside scope.
- Internal UI uses actual entity depth and hierarchy. Keep backgrounds behind text, floating panels above docked content, popups above their owners and modal input above ordinary controls. No replacement per-drawable order field.
- Legacy scene/component documents containing RenderOrder2D still load; that obsolete property no longer controls rendering and is omitted when resaved. Do not change authored positions silently based on obsolete priority values.
- Preserve all unrelated workspace changes. No screenshots, commits or independent review sessions.

## Validation

Behavioral tests cover dynamic depth, equal-depth hierarchy, reparent/re-enable, render-cache invalidation, hit/render agreement, transparent preview ordering from opposing camera directions, legacy scene round trips, and internal UI overlap. Use synthetic renderer readback where valuable. No source-string tests. Validate changed projects and native generation compatibility without editing generated code.
