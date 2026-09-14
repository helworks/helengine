# Target font ownership and architecture guards Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans task-by-task. Delegation requires the user's applicable opt-in; do not launch an independent review without permission. Steps use checkbox syntax.

**Goal:** Move or retire the DS-only debug-font helper and enforce the new boundaries in tests.

**Architecture:** Only retain a platform font tool when a real caller needs it. Architecture checks inspect both dependency closure and executable source patterns, with no blanket prohibition on legitimate platform metadata.

**Tech Stack:** C#/.NET 9, xUnit, existing platform builder contracts and native host APIs.

**Spec:** [Platform neutrality migration design](../specs/2026-09-14-platform-neutrality-design.md)

## Global constraints

Read [the shared design](../specs/2026-09-14-platform-neutrality-design.md). All its compatibility and execution constraints apply. Paths are relative to C:/dev/helworks/helengine unless an absolute sibling-repository path is given. New paths are explicitly marked Create. Revalidate external repository instructions, branch and dirty state before implementing there.

For each task: run the named characterization tests first, add the new failing regression, implement the described change, rerun the focused tests, inspect the diff, then commit only that task's explicit files. Do not treat a zero-test filter as success. Build outputs must use a visible workspace-owned directory.

## File map

- Remove after caller audit: helengine.ui/helengine.editor.app/NintendoDsDebugFontFactory.cs.
- Conditional Create only for a verified consumer: C:/dev/helworks/helengine-ds/tools/debug-font/helengine.ds.debugfont.csproj and Program.cs.
- Conditional Modify: the verified project-generation caller, recorded by absolute path during execution.
- Create: engine/helengine.architecture.tests/helengine.architecture.tests.csproj, SharedAssemblyDependencyTests.cs, SharedSourceBoundaryTests.cs, RepositorySourceLocator.cs.
- Create: scripts/tests/Test-PlatformNeutrality.ps1.
- Modify existing CI test orchestration after locating the tracked workflow/script. Do not invent a second unrelated test pipeline.

### Task 1: Resolve the font helper's ownership from actual usage

Current tracked C# source search found only the helper declaration and its own CreateBottomOverlayFont method. Its body imports Consolas at 8 pixels through GDIFontProcessor; this alone does not establish an active consumer.

- [ ] Search all tracked source, project compile items, reflection/config strings, scripts and relevant project-generator sources for NintendoDsDebugFontFactory and CreateBottomOverlayFont. Record findings.
- [ ] If still unused, remove the app file and build the desktop app. Do not create an unused replacement tool simply to relocate dead code.
- [ ] If a consumer exists, place a command-line font generation tool in the DS repository. The tool may target net9.0-windows and use the GDI importer; DS recipe constants live in that tool, never shared editor.
- [ ] Give the tool explicit output-path arguments and use the existing FontAsset serializer. Preserve Consolas, 8-pixel size, atlas metrics, glyph coverage and tile-packing input. Reject missing required font rather than silently substituting one.
- [ ] Update the real caller, compare font asset metrics and payloads, and smoke-test the DS debug overlay. Shared code receives an ordinary FontAsset.
- [ ] Commit engine cleanup and any required platform-tool migration separately.

### Task 2: Add enforceable dependency and source rules

**Tests:** Create a net9.0 xUnit test project following existing repository test package versions. RepositorySourceLocator finds the repository by walking upward for the expected engine project files, then requires explicit include roots. Exclude obj/bin/vendor/generated output and docs. Never scan another checkout.

- [ ] Add dependency-closure assertions for core/baseplatform/editor and production project files. Shared editor must not transitively depend on SharpDX, helengine.directx11, helengine.vulkan, System.Drawing.Common or native host implementation projects.
- [ ] Add executable-source checks for DemoDisc scene constants in shared production, kernel32/libc imports in editor, concrete backend construction/casts in shared editor, and platform-name dispatch in the audited core content/overlay classes.
- [ ] Use Roslyn syntax analysis for executable identifiers/calls, with the repository's existing Roslyn version if available; otherwise pin a compatible version in the new test project. Avoid failing on comments, shader target metadata, diagnostic text or test fixtures.
- [ ] Define a negative fixture showing a forbidden import/type reference fails and a positive fixture where ShaderCompileTarget metadata and platform selection data pass.
- [ ] Example dependency assertion:
```csharp
Assert.DoesNotContain(typeof(Core).Assembly.GetReferencedAssemblies(),
    reference => reference.Name.StartsWith("SharpDX", StringComparison.Ordinal));
```
Pair metadata tests with evaluated project dependency/package closure checks so an unused forbidden reference also fails. Invoke MSBuild evaluation for configurations under test; do not rely solely on literal XML parsing.
- [ ] Run `dotnet test engine/helengine.architecture.tests/helengine.architecture.tests.csproj --no-restore`. Add guards as each migration lands rather than enabling known-failing rules or maintaining permanent exceptions.
- [ ] Commit `test: enforce shared engine platform boundaries`.

### Task 3: Wire verification and close the migration checklist

- [ ] Add Test-PlatformNeutrality.ps1 to build/test the architecture project with failures propagated; include it in the repository's actual existing validation path.
- [ ] Run core tests, affected editor tests, projectfile tests, shared/desktop builds and the platform-specific checks required by plans 01–06. Record exact commands, selected counts and platform smoke outcomes in the index.
- [ ] Confirm no broad full-suite pass is claimed if a suite hangs or native checks cannot run.
- [ ] Confirm all removed special cases now have a configured real caller or were proven unused. Search for wrapper classes still forwarding to the original platform branch.
- [ ] Mark each plan complete only when its production integration and cleanup gates pass. Commit `test: integrate platform neutrality validation`.

## Completion

The DS-only app helper is either deleted as unused or owned by active DS tooling. Shared dependency and behavior regressions fail automated checks. All seven migration statuses reflect actual verified production changes, not interface creation.
