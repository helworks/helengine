# Helengine scene arrangements

Date: 2026-10-08. Status: approved direction (Helena).

## Problem

A `helengine.video` scene can hold many layers and overlays, but each one is placed on its own: the reference image fills
the frame, a graphic overlay looks for leftover space, captions sit in their band. Elements collide (a contrast chain over
the panels of a full-frame picture) and generated images are drawn for a full 9:16 frame even when they will share it.

## Decisions

- **Arrangement**: a scene may declare `"arrangement": { "preset": "<id>", "by": ... }`. An arrangement is a named set of
  regions (normalized rectangles in the caption-free safe frame, resolved per output aspect) with roles:
  - `full`: one region `main` (today's behaviour; default when absent).
  - `stack`: `top` (graphic band, ~30 %) and `main` (picture, ~70 %) — vertical frames; on landscape becomes `split`.
  - `split`: `left` / `right` (picture and graphic side by side; on portrait becomes `stack`).
  - `take_with_graphic`: take full-frame as `background`, `band` region for graphics (top band, clear of captions).
  - `graphic_only`: `main` for the graphic, large and centered; no picture.
  Exact fractions are tuned visually; presets are built-in data in `helengine.video` (`VideoArrangementPresets`), listed in
  capabilities (`arrangements[]`: id, regions with role names, description) so planners can choose them.
- **Region assignment**: layers get `"region": "<name>"`, overlays (plain or graphic) get `"region"`. A layer in a region
  takes the region rectangle as its viewport (its `layout` preset applies inside the region; explicit `viewport` still wins).
  A graphic/overlay in a region lays out inside that rectangle (measured, scaled to fit, centered) instead of the free-area
  search; overlays without a region keep today's behaviour. Captions always keep their band; regions never include it.
- **Validation**: unknown preset, unknown region name for the scene's preset, two picture layers in one region (warning).
- **Generated media**: the product generates reference images at the aspect of the region they will fill
  (`VideoArrangementPresets.Resolve(preset, format)` exposes region pixel sizes), and image prompts ask for no rendered text.

## Flux Studio

- Planner items gain `arrangement` (enum from capabilities) and `region` for the visual layer and each overlay; guidance:
  structured overlay (graphic template) + picture → `stack`/`split`; take + graphic → `take_with_graphic`; script
  `visual.type: graphic` with a graphic overlay and no needed picture → `graphic_only`; otherwise `full`.
- Builder composes multi-layer scenes from the arrangement (take, picture, graphic) instead of one main visual.
- Image brief `size` derives from the assigned region (planner may still suggest; host overrides with region aspect),
  and the composition prompt instructions forbid lettering in images.
- Editor: arrangement picker per scene and region selects on the visual layer and overlays; lockable (`by`).

## Testing

Engine: presets resolve for 9:16/16:9/1:1, layer viewport from region, graphic inside region, validator, GPU frame of
`stack` with picture + contrast chain. Product: planner round trip with arrangement, builder multi-layer, brief size from
region, patch validation, UI tests; real-data smoke of scene `difference` as `graphic_only` and as `stack`.
