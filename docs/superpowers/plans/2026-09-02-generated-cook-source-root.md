# Generated Cook Source Root Repair Plan

## Failure

The DemoDisc GameCube build reaches `generated-core-ready`, then fails while creating the generated editor-font atlas cook item. `EditorWindowsBuildScenePackager` writes that atlas beneath the isolated build graph root, but `EditorPlatformCookWorkItemFactory` hashes it with the project-root `AssetFileHasher`. The authoring safety layer correctly rejects that build-owned path because it is outside the authored DemoDisc project root.

## Repair

1. Add a regression test that creates an authored project root and a separate build root, writes a generated atlas beneath the build root, and proves the generated cook-item path can be hashed without weakening containment for authored assets.
2. Give generated cook-item creation an explicit trusted containing root for its source file. Keep ordinary authored texture hashing on the existing project-root hasher.
3. Thread the active build root through both generated-font and generated-texture call sites. Do not bypass verified file access, broaden `AssetFileHasher` to arbitrary paths, or relax reparse-point validation.
4. Run the focused cook-work-item and scene-packager tests, then the full editor test suite. Re-run the exact DemoDisc GameCube build only after those tests are green.

## Acceptance

- The regression reproduces the old `escapes its containing root` exception and passes after the repair.
- Authored-source hashing remains restricted to the authored project root.
- Generated-source hashing is restricted to the build root supplied by the packager.
- No generated files are edited.
- The DemoDisc GameCube build advances beyond asset-cook work-item creation.
