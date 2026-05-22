# Editor Generated-Core Postprocessing Removal Design

## Goal

Remove editor-side generated-core post-processing from `helengine.editor` so the editor no longer rewrites or repairs generated native output after codegen.

## Current Problem

`EditorPlatformBuildGraphRunner` still performs a final generated-core mutation phase through `FinalizeGeneratedCoreSources(...)` after codegen and runtime-native manifest generation complete.

That phase currently exists to patch generated output after the fact. It includes behavior such as:

- generated runtime component deserializer support patching
- merged shader feature-output promotion
- conversion-report-driven runtime feature manifest rewriting
- historically, generated `SceneManager.cpp` cleanup such as duplicate `delete loadResult;` normalization

This violates the boundary you set:

- generated output must come out correct from codegen or plugin-owned generation
- the editor must not rewrite generated source to make it work

## Design

### Ownership Boundary

`helengine.editor` remains responsible for:

- invoking codegen
- merging generated-core project inputs
- staging generated outputs into the build workspace
- packaging outputs for platform builds

`helengine.editor` must not:

- patch generated C++ source
- promote or rewrite generated feature metadata after codegen
- retrofit missing runtime deserializer support by mutating generated output

If generated output is wrong, the fix belongs in:

- shared codegen
- external platform generated-core inputs
- platform-native runtime/build code

### Build Graph Behavior

`EditorPlatformBuildGraphRunner.Execute(...)` will stop calling `FinalizeGeneratedCoreSources(...)`.

`FinalizeGeneratedCoreSources(...)` itself will be deleted.

This means the editor will no longer run any post-codegen generated-core mutation pass before packaging.

### Test Boundary

Tests that assert editor-side generated-core rewriting are now asserting the wrong behavior and should be removed or rewritten.

Examples include tests that validate:

- duplicate generated source cleanup
- post-hoc feature manifest rewriting
- feature promotion performed only after generation

Tests that remain valid are orchestration tests proving the editor:

- runs generation
- stages generated output
- passes output into packaging/build steps

without mutating the generated content itself.

## Approach Options

### Option 1: Remove All Editor Postprocessing Immediately

Delete the finalization phase and let broken generated output fail explicitly.

Pros:

- cleanest boundary
- no ambiguity about ownership
- reveals real upstream generation defects immediately

Cons:

- some platform builds may regress until codegen or plugin outputs are corrected

This is the recommended approach.

### Option 2: Keep Non-Source Postprocessing Only

Remove direct generated-source rewrites but keep some metadata mutation in the editor.

Pros:

- lower short-term disruption

Cons:

- still leaves the editor as a patch layer over generated output
- violates the principle that generated output should already be correct

This is not recommended.

### Option 3: Move Postprocessing First, Then Remove It

Re-home each postprocessing behavior into codegen or plugin layers before deleting the editor phase.

Pros:

- smoothest rollout

Cons:

- preserves the bad boundary longer
- adds more transition work before the editor is clean

This is not recommended for this pass.

## Testing Strategy

Run only focused validation around the build-graph and generated-core orchestration surface:

- focused `EditorPlatformBuildGraphRunnerTests`
- any focused source/feature tests that still remain valid after removing postprocessing

Delete tests that exist only to prove editor-side generated output rewrites.

## Risks

### Upstream Generation Defects Become Visible

Removing the editor patch layer may expose defects in:

- generated runtime component deserializer output
- shader feature-report generation
- platform plugin generated-core inputs
- native manifest/codegen output

This is expected and correct. Those defects must be fixed at the source, not hidden in editor postprocessing.

### Existing Tests Encode The Wrong Contract

Some existing tests may currently treat generated-output rewriting as intended behavior. Those tests should be removed or inverted to match the new rule.

## Success Criteria

This pass is complete when:

- `EditorPlatformBuildGraphRunner` no longer calls `FinalizeGeneratedCoreSources(...)`
- `FinalizeGeneratedCoreSources(...)` is deleted
- `helengine.editor` no longer rewrites generated-core output after codegen
- tests asserting generated-output rewrites are removed or rewritten
- focused validation passes for the remaining generic build-graph behavior
