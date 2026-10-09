# Helengine graphic templates (kinetic typography)

Date: 2026-10-08. Status: approved direction (Helena chose "A": curated templates, the agent picks and times them).

## Problem

Overlays are static text. A line like `LEGALIZAR ≠ DESCRIMINALIZAR ≠ TRATAR` should build up as the speaker says each term.
Letting the AI write raw keyframes gives inconsistent, ugly motion (it cannot measure text and has no taste for easing).
Taste belongs in authored templates; the AI chooses a template, fills its slots and anchors each item to a spoken word.

## Decisions

- **Graphic template asset** (`.hgraphic`, HELE binary through helengine.files, authored in C# like `.heffect`):
  `GraphicTemplateAsset` with id, version, display name, description (for the AI), slots, typed parameters, layout and
  per-element animation. Engine built-ins in `helengine.video` (generic kinetic typography) plus project templates from
  `--project <dir>` (`assets/**/*.hgraphic`), collected in a catalog next to effects.
- **Curated first set** (built-in): `contrast_chain` (A ≠ B ≠ C, separators pop between items, earlier items may dim),
  `list_build` (items appear one by one, stacked), `highlight_word` (one phrase, a key item gets an accent color and
  emphasis), `strike_replace` (first item appears, gets struck through, second item replaces it). Each one ships with a
  vertical (9:16) and horizontal layout chosen automatically by aspect and measured width (`layout: auto|vertical|horizontal`).
- **Slots**: `items` (list of strings, min/max count, max chars), optional `separator` (string, default per template),
  optional `accent_item` (index). Parameters reuse the effect parameter descriptor kinds (float, enum, color, bool).
- **Timing**: one moment per item (`at[]`, same moment vocabulary as the edit: sec/from_end/fraction/word); `until`
  moment for the whole graphic. Element animations are offsets in seconds relative to their item's moment, with catalog
  curves (`smoothstep.v1`, `linear.v1`, plus `ease_out_back.v1` for pops if added to the media curve catalog).
- **Elements**: a template expands into text layers (one per item, one per separator, optional strike/underline rect layer
  as a solid-color media-free layer if the composition supports it, otherwise a text glyph line). Animated properties: opacity,
  scale, position (offset in layout units), color for emphasis/dim (if not animatable, two stacked layers crossfaded).
- **Layout and measurement**: the compiler places items in a box (default: centered, 84 % width safe area, respecting the
  caption reserve) using measured text. `IVideoTextMeasurer` in `VideoCompileContext` (implemented in the CLI with the same
  font loading/layout the renderer uses); without a measurer a deterministic estimate is used and a `text_measure_estimated`
  info diagnostic is raised. Uniform scale-to-fit when the measured block exceeds the box.
- **Free region** (`VideoGraphicFreeArea`): the box is the safe area minus the rectangles the scene's take and media layers
  draw (`VideoPictureBounds`: source fitted into the layout viewport, clipped when the layer clips, the whole viewport when
  it paints an opaque padding color, moved/scaled by the static transform), each grown by 1.2 % of the frame height. Every
  maximal free rectangle at least 25 % wide and 4 % tall is tried (direction and scale chosen as above, centered in the
  rectangle); the largest text wins, ties within 3 % go to the rectangle nearest the style's preferred center. When no
  picture touches the safe area nothing changes; when no free rectangle allows 40 % of the style font size (full-frame
  take or image) the whole safe area is used and the text sits over the picture as before.
- **Dimming**: an item never dims by turning see-through alone. A copy of it in `dim_color` (default mid gray) with the
  style's full outline and shadow appears under it when the next item does, and the item fades to `dim_opacity` over the
  copy, so a dimmed item settles on a muted mix of its color and the gray that stays legible on white and dark pictures.
- **Edit format**: `overlays[].graphic = { "template": "contrast_chain", "version": 1, "items": [...], "at": [<moment>...],
  "parameters": {...} }`. When `graphic` is present `text` is the plain fallback/accessibility text. Lockable as part of the
  overlay (`by`). Validator: template exists in catalog, item count/lengths, `at` count = items count, parameters typed.
- **Capabilities**: `composition capabilities` lists templates (id, version, description, slots, parameters, layouts) so
  the Flux Studio planner can offer them as an enum with typed parameters, like transitions.

## Flux Studio

- Planner overlay item gains optional `graphic` (template enum from catalog, items, `at` per item as moments, parameters).
  Guidance: use a template when the authored overlay is structured (comparison/list/correction), anchor each item to the
  word where it is spoken, otherwise keep plain text. Script overlay `style: "comparison"` hints `contrast_chain`.
- Editor: overlay section gets a template picker, item list with text + word picker per item, parameters (reuse typed
  controls), "texto simples" option.
- Preview/final render through the existing compile path.

## Testing

Engine: asset round trip, catalog with project templates, compiler expansion (layer count, timings from word anchors,
fit scaling, vertical vs horizontal), validator errors, GPU frame test of `contrast_chain` mid-build. Product: planner
round trip with a graphic overlay, patch validation, UI module tests.
