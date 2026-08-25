# DemoDisc native asset format migration

## Goal

Regenerate `C:\dev\helprojs\demodisc` into the current authored asset contract without making the project depend on editor identity/index internals. DemoDisc generators should call high-level Editor APIs; the engine owns embedded identity, sidecars, canonical references, and serialization details.

## Scope

- Add/adjust public editor save APIs so generated native assets receive embedded authoring identity and authored file references are canonicalized at the editor persistence boundary.
- Update DemoDisc's direct native writers to call those APIs instead of `AssetSerializer` directly.
- Keep runtime/cache assets outside the authored `assets` tree unchanged unless they are already emitted through an authored writer.
- Preserve all pre-existing DemoDisc working-tree changes.
- Do not add old-format migration readers or compatibility branches. Existing old authored outputs will be replaced by generator output; current engine readers remain the existing source of truth.

## Implementation steps

1. Add tests for the public generated-asset writer and save-time canonicalization.
2. Implement a public engine `GeneratedAssetWriteService` that writes current native assets atomically, assigns an embedded authoring id when the in-memory asset does not have one, and preserves an already-present id.
3. Update `SceneSaveService` to canonicalize file-backed references created by editor tooling before current authored validation, including platform overrides, through `EditorAssetReferenceResolver`.
4. Replace DemoDisc authored direct serialization sites (scenes, blueprints, and generated models) with the public writer; leave cache/runtime writes on the runtime path.
5. Ensure DemoDisc's existing generated material service uses the public material writer and does not reference identity internals.
6. Build the engine/editor, run focused tests, then regenerate DemoDisc authored outputs through its registered generator commands and targeted generators.
7. Verify all authored native headers are current, embedded ids are present, external authored sources have `.hmeta`, and persisted authored file references contain asset id plus SHA-256.
8. Update `project.heproj.requiredEngineVersion` to the resulting engine version only after successful regeneration and review the final diff without touching unrelated user changes.

## Verification

- Engine focused editor tests for generated writing and scene canonicalization.
- DemoDisc source tests and editor build/regeneration commands.
- Header/metadata/reference audit over `demodisc/assets`.
- `git diff --check` and status review in both repositories.
