# Remove DS Generated Font From Shared Engine Paths

## Summary

Delete the generated `editor:ds-debug-font` reference from shared `engine/helengine.editor` code. Shared engine and editor infrastructure should no longer know about Nintendo DS-specific font assets, factories, or generated reference ids. Scenes and tests that still depend on that generated reference will stop working until they are regenerated or rewritten to use normal file-backed font references.

## Problem

Shared engine/editor code still contains platform-specific logic for a Nintendo DS debug font:

- `EditorWindowsBuildScenePackager` rewrites `editor:ds-debug-font` into `cooked/fonts/ds-debug.hefont`.
- `EditorSceneAssetReferenceResolver` resolves that generated id by reflecting into `helengine.editor.app.NintendoDsDebugFontFactory`.
- `TextComponentSpriteBakeService` also reflects into `helengine.editor.app` for the same DS-only font.
- `FontAssetScenePersistenceSupport` can still mint the generated DS debug-font reference.
- `SceneComponentPackagingTransformService` still rewrites that generated font reference in automatic component save-state rewriting.

This violates the boundary that shared engine/editor code should remain platform-agnostic. It also creates brittle cross-assembly dependencies from shared code into app-host code.

## Decision

Remove the generated DS debug-font concept entirely from shared engine/editor code with no backward compatibility.

Specifically:

- Remove recognition of `AssetId = "ds-debug-font"` from shared engine/editor code.
- Remove any reflection-based loading of `helengine.editor.app.NintendoDsDebugFontFactory` from shared engine/editor code.
- Remove any helper that manufactures `generated/editor/fonts/ds-debug.hefont` references in shared engine/editor code.
- Rewrite or delete tests that only exist to preserve the old DS generated-font behavior.

Old scenes or tests still carrying that generated reference are allowed to fail until regenerated or rewritten.

## Scope

In scope:

- `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- `engine/helengine.editor/managers/project/TextComponentSpriteBakeService.cs`
- `engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs`
- `engine/helengine.editor/serialization/scene/FontAssetScenePersistenceSupport.cs`
- tests in `engine/helengine.editor.tests` that still author or expect `ds-debug-font`

Out of scope:

- removing `NintendoDsDebugFontFactory` from app-side code
- preserving compatibility for existing authored scenes that still use the generated DS font reference
- replacing the DS font with a new engine-level abstraction

## Target Behavior

After the change:

- Shared engine/editor code treats `editor:ds-debug-font` as unsupported.
- Shared engine/editor code only handles the editor default generated font plus ordinary file-backed font references.
- Text sprite baking, editor-time scene reference resolution, and packaging no longer depend on `helengine.editor.app` for DS-specific font generation.
- Tests for debug/fps generic runtime continue to pass using ordinary font references.

## Expected Test Changes

- Delete or rewrite the DS-generated-font packaging test in `EditorWindowsBuildScenePackagerTests`.
- Delete or rewrite the DS-generated-font transform test in `SceneComponentPackagingTransformServiceTests`.
- Keep the ordinary debug/fps font-reference tests, but back them with normal file-backed or editor-default font references.

## Risks

### Scene breakage

Any scene still serialized with `generated/editor/fonts/ds-debug.hefont` will stop resolving. This is intentional.

### Hidden dependency surface

There may be additional DS-font references outside the already identified shared engine/editor paths. A final source sweep for `ds-debug-font` is required before completion.

### Test drift

Some tests currently encode the old special-case behavior. They must be rewritten to assert the new unsupported behavior or removed if they only existed to pin the old path.
