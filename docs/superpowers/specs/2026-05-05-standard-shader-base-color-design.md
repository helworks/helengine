# Standard Shader Base Color Design

## Summary

This document defines the first authored color input for the built-in standard 3D material path.

Today the engine-generated standard material is backed by `EditorDefaultMesh.hlsl`, and that shader hardcodes one neutral surface color. That makes the material visible, but not authorable. Materials can opt into different shader variants and extra texture inputs, yet they cannot express the simplest per-material visual distinction: base color.

This slice renames the built-in shader to a stable standard-material name and adds one schema-driven `base-color` input that multiplies the current neutral surface tone. The default remains white so existing authored content keeps the same appearance.

## Goals

- Add one `base-color` material input to the built-in standard shader path.
- Keep the material editor schema-driven instead of adding bespoke UI.
- Preserve the current visual result for content that does not author a color.
- Rename `EditorDefaultMesh.hlsl` to a stable standard-shader name that matches its role.
- Keep the generated standard material and cooked standard material paths aligned.

## Non-Goals

- No albedo texture support in this slice.
- No generic vector or color-property framework beyond what is needed for the standard shader.
- No split "with color" and "without color" shader files in this slice.
- No changes to lighting model semantics beyond applying a base-color multiplier.
- No alpha-driven transparency behavior from `base-color`.

## Current Problem

The built-in standard material path is effectively fixed-color.

`EditorDefaultMesh.hlsl` hardcodes one surface color in the pixel shader. The generated standard material uses that shader with only shader/program/variant metadata, and the material schema currently exposes no authored color field for the DirectX11 standard material path.

That causes three concrete issues:

- authored standard materials cannot express a color difference without a custom shader
- demo content such as the physics validation scenes must carry custom shader files only to vary color
- the built-in shader name reads like an editor placeholder instead of a real standard material asset

## Proposed Architecture

### 1. Rename The Built-In Shader To A Stable Standard Name

The current file name, `EditorDefaultMesh.hlsl`, is misleading now that the shader is the canonical generated standard material path.

The shader should be renamed to:

- `StandardShader.hlsl`

All built-in shader loading, compile tests, generated-material paths, and build packaging references should move to the new file name in the same slice.

This is a semantic cleanup, not a behavior fork. There remains exactly one built-in standard shader after the rename.

### 2. Add One `BaseColorBuffer` Constant Buffer

The standard shader should gain one material-scoped constant buffer:

- `BaseColorBuffer`

It should contain:

- `float4 baseColor`

The pixel shader should multiply the current neutral built-in surface tone by `baseColor.rgb`.

Recommended behavior:

- current neutral tone remains the built-in material look foundation
- `baseColor = float4(1, 1, 1, 1)` reproduces the current look
- alpha is stored for shape consistency but ignored for shading in this slice

This keeps the change narrow and backwards-safe while making color authorable immediately.

### 3. Keep One Standard Shader Path For Now

The engine should not introduce separate "with color" and "without color" shader files or parallel schemas in this slice.

Reasons:

- multiplying by white is negligible compared with the maintenance cost of another shader path
- a second shader file would complicate generated-material caching, packaging, tests, and future compatibility work
- the existing `variant` field is already the right place for future specialization if a leaner standard path ever becomes necessary

If the engine later needs a reduced standard-material path, it should be added as a real shader variant, not as a second ad hoc built-in shader family.

### 4. Extend The Standard Material Schema With `base-color`

The standard material schema should gain one new field:

- field id: `base-color`
- display name: `Base Color`
- field kind: `Color`
- default value: `#ffffff`
- required: `false`

This field should be surfaced through the existing schema-driven material editor flow. No special-case inspector code should be added for it.

The direct effect is:

- new standard materials show a base-color control
- missing older settings resolve to white
- authored values round-trip through the current material settings model

### 5. Generated Standard Material Must Write A White Base Color

The engine-generated standard material path must continue to build one valid runtime material without requiring user-authored settings.

The generated material created by `EngineGeneratedMaterialCache` and the build-packaged generated standard material created by `SceneComponentPackagingTransformService` should both include:

- one `MaterialConstantBufferAsset` named `BaseColorBuffer`
- payload representing white `float4(1, 1, 1, 1)`

That ensures the generated material remains visually identical to the current one while using the new shader contract.

### 6. Cooked Shader-Backed Standard Materials Must Translate `base-color`

When the standard shader-backed material schema is cooked into a runtime `MaterialAsset`, the `base-color` field should be translated into the same `BaseColorBuffer` constant-buffer payload used by the generated standard material.

Rules:

- if `base-color` is present, use its authored value
- if `base-color` is absent, write white
- do not fail just because older settings do not contain `base-color`

This tolerance is appropriate here because material settings are editor-authoring data, not strict cooked runtime payloads.

### 7. Keep Existing Lighting Behavior Intact

This slice should not change the standard shader's lighting model, light evaluation, shadow behavior, or material render-state semantics.

Only the surface-color source changes:

- before: fixed built-in neutral color
- after: built-in neutral color multiplied by authored `base-color`

That keeps the visual and testing surface area narrow.

## Data Flow

### Generated Standard Material

1. The engine resolves `Engine/Materials/Standard`.
2. `EngineGeneratedMaterialCache` loads `StandardShader.hlsl`.
3. The cache creates one `MaterialAsset` with the built-in shader/program/variant metadata.
4. The cache injects `BaseColorBuffer = white`.
5. The renderer builds one runtime material.
6. The cached material is reused on later requests.

### Authored Standard Material

1. The material editor resolves the active `standard-shader` schema.
2. The editor shows `Base Color` from schema metadata.
3. The user edits the color.
4. The per-platform material settings store `base-color`.
5. The target-platform cook translates `base-color` into `BaseColorBuffer`.
6. The runtime material uses that constant buffer when drawing.

## Error Handling

Rules:

- if the standard shader is missing after the rename, generated standard material resolution should fail clearly
- if `base-color` cannot be parsed from stored settings, the cook path should fail with a clear material-settings validation error
- if `base-color` is absent, the system should write white instead of failing
- if `BaseColorBuffer` is missing from the shader or material contract unexpectedly, the shader/material build path should fail clearly rather than silently ignoring the mismatch

## Testing Requirements

Implementation must include coverage for:

1. the renamed built-in standard shader still compiles for DirectX11
2. the standard schema exposes `base-color` with default `#ffffff`
3. material settings seeded from schema metadata include `base-color = #ffffff`
4. generated standard material creation writes a white `BaseColorBuffer`
5. cooked shader-backed standard materials translate authored `base-color` into `BaseColorBuffer`
6. missing `base-color` settings still produce a white `BaseColorBuffer`
7. the runtime material build path still reports the compact standard lighting flags expected by existing tests

## Recommendation

Implement one renamed built-in standard shader, `StandardShader.hlsl`, with one authored `base-color` multiplier stored in `BaseColorBuffer`.

This solves the immediate authoring problem cleanly:

- the standard material becomes colorable
- old content keeps the same look
- the editor stays schema-driven
- generated and cooked standard materials continue to use one shared shader contract

If future optimization work proves the engine needs a leaner standard path, that should be introduced as a shader variant, not as a second built-in shader family in this slice.
