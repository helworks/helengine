# Viewport settings numeric input design

## Scope

Replace the read-only values beside the four viewport settings sliders with editable numeric textboxes:

- Pixels / Unit
- Near Plane
- Far Plane
- Camera Speed

No other editor sliders are changed.

## Interaction

Each textbox displays the slider's current formatted value. Editing is committed when Enter is pressed or focus leaves the field. A committed value uses the existing slider range and projection validation, so values clamp exactly as drag-based input does. Invalid or incomplete text restores the current formatted value without changing the setting.

Slider changes update the textbox immediately, and successful textbox commits update the corresponding slider. The existing keyboard focus order remains unchanged, except each numeric field participates in the row's normal editing interaction.

## Verification

Tests will cover a valid typed value committing to the setting, an invalid value restoring the prior setting, and slider-to-textbox synchronization. Existing viewport overlay interaction tests remain green.
