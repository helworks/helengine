# Shared editor rendering independence Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans task-by-task. Delegation requires the user's applicable opt-in; do not launch an independent review without permission. Steps use checkbox syntax.

**Goal:** Finish the backend separation so shared editor has no concrete graphics dependency.

**Architecture:** Keep existing picking interfaces. Supply shader target, material instancing, render-target readback and CLI rendering through typed capabilities; native resources remain owned by adapters.

**Tech Stack:** C#/.NET 9, xUnit, existing engine and platform builder contracts.

**Spec:** [Platform neutrality migration design](../specs/2026-09-14-platform-neutrality-design.md)

## Global constraints

Read [the shared design](../specs/2026-09-14-platform-neutrality-design.md). All its compatibility and execution constraints apply. Paths are relative to C:/dev/helworks/helengine unless an absolute sibling-repository path is given. New paths are explicitly marked Create. Revalidate external repository instructions, branch and dirty state before implementing there.

For each task: run the named characterization tests first, add the new failing regression, implement the described change, rerun the focused tests, inspect the diff, then commit only that task's explicit files. Do not treat a zero-test filter as success. Build outputs must use a visible workspace-owned directory.

## File map

- Modify: engine/helengine.editor/rendering/IEditorPickingBackend.cs, components/EditorViewportPicker.cs, managers/workspace/ViewportWorkspacePanelController.cs, EditorSessionRendererResources.cs, EditorSession.cs.
- Modify all five backend-selecting factories: managers/gizmo/TransformGizmoGridPreviewMaterialFactory.cs, TransformGizmoPlaneMaterialFactory.cs, TransformGizmoRotationPreviewMaterialFactory.cs; managers/scene/EditorViewportGridMaterialFactory.cs, EditorViewportCanvasPlaneMaterialFactory.cs.
- Modify: managers/scene/EditorVisualMaterialFactory.cs, EditorCliBuildRunner.cs, EditorCliCommandRunner.cs.
- Move managers/rendering/DirectX11RenderTargetTextureAssetReader.cs to engine/helengine.editor.windows/rendering/.
- Create: engine/helengine.editor/rendering/EditorRenderCapabilities.cs and IEditorMaterialInstanceFactory.cs.
- Create: engine/helengine.editor.windows/rendering/DirectX11EditorMaterialInstanceFactory.cs and hosting/EditorWindowsCliComposition.cs.
- Modify host call sites in helengine.ui/helengine.editor.app, shared and Windows csproj files.
- Create: engine/helengine.editor.tests/rendering/EditorRenderCapabilitiesTests.cs and SharedEditorDependencyTests.cs.

### Task 1: Complete rendering capability injection

**Interfaces:** EditorRenderCapabilities(ShaderCompileTarget shaderTarget, IEditorPickingBackendFactory pickingFactory, IEditorMaterialInstanceFactory materialFactory, IRenderTargetTextureAssetReader readback). IEditorMaterialInstanceFactory produces `RuntimeMaterial CreateInstance(RuntimeMaterial source)`. Capabilities are explicitly supplied; each adapter documents unsupported readback/picking rather than pretending to support them.

- [ ] Characterize existing picker, gizmo and EditorVisualMaterialFactoryTests. Add a fake material factory proving shared visuals never construct backend types.
- [ ] Keep IEditorPickingBackend's int2 and byte4 neutral engine types; remove unused SharpDX imports from its interface and picker only after confirming symbol resolution.
- [ ] Replace each renderer type test with capabilities.ShaderTarget. Move DirectX material cloning to the Windows adapter; preserve shader layout, properties, lighting and render state.
- [ ] Move readback implementation unchanged to the adapter; host injects it using the existing IRenderTargetTextureAssetReader contract. Audit and migrate every constructor call.
- [ ] Verify independent viewport resources, resizing, pending readback and disposal order with existing picker tests and new capability tests.
- [ ] Run `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore -m:1 --filter "FullyQualifiedName~EditorViewportPicker|FullyQualifiedName~EditorRenderCapabilities|FullyQualifiedName~MaterialFactory"`; commit `refactor: inject editor graphics capabilities`.

### Task 2: Move CLI composition out of shared runners

**Contract:** Shared CLI runners receive host-created renderer/importer/shader services and ownership through a new engine/helengine.editor/hosting/IEditorCliHostFactory.cs. Its `IEditorCliHost Create(bool requiresRendering)` returns an IDisposable host exposing Core, IEditorProjectAuthoringSession and EditorRenderCapabilities. Define IEditorCliHost in its own file. Host owns its graph; runner borrows it only within the Run lifetime.

- [ ] Add a fake host factory recording whether a command requires rendering; add startup-failure disposal coverage.
- [ ] Move DirectX11Renderer3D construction and DirectX11ShaderBackend registration into EditorWindowsCliComposition. Separate renderer-free authoring commands from rendering-dependent commands using their actual operations, not their names.
- [ ] Preserve command-line arguments, return codes and platform shader registrations. Reuse the existing session construction/disposal ledger.
- [ ] Migrate MainForm/Program composition and every test fixture constructor; do not introduce a default DirectX factory inside the shared runner.
- [ ] Run EditorCliBuildRunnerTests and EditorCliBuildRunnerCompilationModeTests. Smoke-test existing authoring and Windows build CLI fixtures.
- [ ] Commit `refactor: move CLI graphics composition to host`.

### Task 3: Remove dependencies and prove the boundary

- [ ] Search shared sources for all concrete backend names, casts, fully qualified names and native resource signatures. Remove or migrate every executable reference.
- [ ] Remove shared ProjectReference entries for helengine.directx11 and helengine.vulkan, and System.Drawing.Common PackageReference once consumers compile without them.
- [ ] Add an assembly assertion:
```csharp
string[] forbidden = { "helengine.directx11", "helengine.vulkan", "SharpDX", "System.Drawing.Common" };
Assert.DoesNotContain(typeof(EditorSession).Assembly.GetReferencedAssemblies(),
    reference => forbidden.Any(name => reference.Name == name || reference.Name.StartsWith(name + ".", StringComparison.Ordinal)));
```
Also inspect project-reference closure; unused package references need not appear in assembly metadata.
- [ ] Run SharedEditorDependencyTests and build helengine.editor.csproj plus helengine.ui/helengine.editor.app/helengine.editor.app.csproj.
- [ ] Verify DirectX selection, grid, overlays and readback on a graphics-capable host. Verify Vulkan's existing supported preview behavior and explicit unsupported capability behavior; do not add Vulkan GPU picking as hidden scope.
- [ ] Commit `refactor: enforce neutral editor graphics dependencies`.

## Completion

A shared-editor build needs neither concrete renderer project. Native backend bridges remain functional in host composition. This supersedes unfinished tasks 2–3 of 2026-09-14-editor-01-backend-boundaries.md; the earlier picking extraction is not repeated.
