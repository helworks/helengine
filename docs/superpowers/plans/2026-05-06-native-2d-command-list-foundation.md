# Native 2D Command List Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Introduce a compact, codegen-safe resolved 2D command list in `helengine.core` and route the native Windows host through it for the current primitives the engine already owns today: textured quads, glyph quads, and rounded rectangles.

**Architecture:** Keep `RenderQueue2D` as the scene-facing API. Add a transient per-camera `RenderCommandList2D` built from the queue. The command list must survive generated-core translation cleanly, so it should use only simple enums, scalar/vector payloads, runtime texture references, and list-backed storage with explicit getters. Managed backends remain on the current drawable path during this pass. The native Windows host in `helengine-windows` becomes the first consumer of the resolved command path.

**Important boundary:** Do **not** include per-widget clip-stack commands in this slice. The engine currently exposes scroll state but does not expose a general 2D clip ownership seam in core, and both managed 2D backends currently only apply camera-viewport scissor. This foundation should keep camera viewport clipping only and leave widget clip-stack work for a follow-up once the engine has an actual clip model.

**Tech Stack:** C#/.NET 9 in `helengine`, generated C++ core output, native C++20 DirectX11 host in `helengine-windows`, xUnit, `rtk dotnet test`, packaged Windows build verification.

---

## Task 1: Add a codegen-safe 2D command container

**Files:**
- Create: `engine/helengine.core/managers/rendering/RenderCommand2DType.cs`
- Create: `engine/helengine.core/managers/rendering/RenderCommandList2D.cs`
- Test: `engine/helengine.editor.tests/managers/rendering/RenderCommandList2DTests.cs`

- [ ] **Step 1: Write failing tests for command order, reset, and payload access**

Cover:
- insertion order across mixed command types
- reset clearing logical command count without destroying reuse semantics
- typed payload access for:
  - textured quad
  - glyph quad
  - rounded rect

The tests should validate a full API, not a partial one. `RenderCommandList2D` must expose:
- `Count`
- `Reset()`
- `GetCommandType(int index)`
- `AddTexturedQuad(...)`
- `AddGlyphQuad(...)`
- `AddRoundedRect(...)`
- typed getters that let the backend read the payload for a logical command index

- [ ] **Step 2: Run the focused tests to confirm the types do not exist yet**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~RenderCommandList2DTests" -v minimal
```

Expected:

```text
FAIL with CS0246/CS0103 errors for RenderCommandList2D and RenderCommand2DType.
```

- [ ] **Step 3: Implement the command container using generated-core-friendly shapes**

Required design rules:
- no per-command object graph
- no tuples
- no delegates
- no local helper functions
- no clip push/pop types in this slice
- use simple `List<T>` fields and explicit getters only

Recommended storage shape:
- `List<RenderCommand2DType> CommandTypes`
- `List<int> PayloadIndices`
- `List<RuntimeTexture> QuadTextures`
- `List<float4> QuadBounds`
- `List<float4> QuadSourceRects`
- `List<byte4> QuadColors`
- `List<RuntimeTexture> GlyphTextures`
- `List<float4> GlyphBounds`
- `List<float4> GlyphSourceRects`
- `List<byte4> GlyphColors`
- `List<float4> RoundedRectBounds`
- `List<float> RoundedRectRadii`
- `List<float> RoundedRectBorderThicknesses`
- `List<RoundedRectCorners> RoundedRectCornersValues`
- `List<byte4> RoundedRectFillColors`
- `List<byte4> RoundedRectBorderColors`

`RenderCommand2DType` should contain exactly:
- `TexturedQuad`
- `GlyphQuad`
- `RoundedRect`

The command list API should return payload data through explicit getters such as:
- `GetTexturedQuadPayloadIndex(int commandIndex)`
- `GetGlyphQuadPayloadIndex(int commandIndex)`
- `GetRoundedRectPayloadIndex(int commandIndex)`
- `GetTexturedQuadTexture(int payloadIndex)`
- `GetTexturedQuadBounds(int payloadIndex)`
- `GetTexturedQuadSourceRect(int payloadIndex)`
- `GetTexturedQuadColor(int payloadIndex)`
- equivalent glyph and rounded-rect getters

- [ ] **Step 4: Run the focused tests and make them pass**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~RenderCommandList2DTests" -v minimal
```

Expected:

```text
Passed!  - Failed: 0
```

- [ ] **Step 5: Commit the command container**

```bash
git add engine/helengine.core/managers/rendering/RenderCommand2DType.cs engine/helengine.core/managers/rendering/RenderCommandList2D.cs engine/helengine.editor.tests/managers/rendering/RenderCommandList2DTests.cs
git commit -m "feat: add 2d render command list container"
```

## Task 2: Build resolved commands from the current 2D queue

**Files:**
- Create: `engine/helengine.core/managers/rendering/RenderCommandListBuilder2D.cs`
- Test: `engine/helengine.editor.tests/managers/rendering/RenderCommandListBuilder2DTests.cs`

- [ ] **Step 1: Write failing builder tests for current engine primitives**

Cover:
- a sprite produces one `TexturedQuad` command
- text produces one `GlyphQuad` command per emitted glyph
- wrapped text still resolves upstream before glyph emission
- a rounded rectangle produces one `RoundedRect` command
- disabled parents are skipped just like the current backend visitor path

Use real current engine expectations:
- `TextLayoutUtils.WrapText(...)` remains upstream in the builder
- rounded rectangles keep their native parameters instead of being flattened into solid rectangles in core

- [ ] **Step 2: Run the focused builder tests to confirm the builder does not exist yet**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~RenderCommandListBuilder2DTests" -v minimal
```

Expected:

```text
FAIL with CS0246 errors for RenderCommandListBuilder2D.
```

- [ ] **Step 3: Implement the minimal builder without changing the scene-facing queue**

Required shape:
- `RenderCommandListBuilder2D : IRenderVisitor2D`
- owns one reusable `RenderCommandList2D`
- `Build(IRenderQueue2D renderQueue)` resets and reuses the command list
- traversal still uses `renderQueue.VisitOrdered(this)`

Visitor behavior:
- `ISpriteDrawable2D` -> `AddTexturedQuad(...)`
- `ITextDrawable2D` -> wrap text when `WrapText` is enabled, then emit resolved glyph quads
- `IRoundedRectDrawable2D` -> `AddRoundedRect(...)`

Do **not** modify `IRenderVisitor2D`, `RenderList2D`, or the drawable interfaces in this slice unless a test proves it is required. This foundation should slot beside the current pipeline, not rewrite it.

- [ ] **Step 4: Run the builder tests and the baked-menu runtime regressions**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~RenderCommandListBuilder2DTests|FullyQualifiedName~RuntimeSceneLoadServiceTests.Load_WhenSceneContainsBakedDemoMenu_MaterializesTheComponent|FullyQualifiedName~RuntimeSceneLoadServiceTests.Load_WhenSceneContainsBakedDemoMenu_InitializesOnlyTheInitialPanelAsEnabled" -v minimal
```

Expected:

```text
Passed!  - Failed: 0
```

- [ ] **Step 5: Commit the builder**

```bash
git add engine/helengine.core/managers/rendering/RenderCommandListBuilder2D.cs engine/helengine.editor.tests/managers/rendering/RenderCommandListBuilder2DTests.cs
git commit -m "feat: build resolved 2d render commands"
```

## Task 3: Route the native Windows host through the command list

**Files:**
- Modify: `C:\dev\helworks\helengine-windows\src\platform\windows\win32\win32_render_bridge.hpp`
- Modify: `C:\dev\helworks\helengine-windows\src\platform\windows\win32\win32_render_bridge.cpp`
- Test: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`

- [ ] **Step 1: Add a regression that preserves the existing feature summary output**

Keep the current build-result contract covered:
- native Windows build status still reports enabled detected runtime features
- the new command path must not regress the feature summary already added to build logs

- [ ] **Step 2: Move the Win32 2D bridge from drawable interpretation to command execution**

Required runtime shape:
- `Win32RenderManager2D` owns one generated-core `RenderCommandListBuilder2D`
- `RenderCamera(ICamera* camera)` still resolves the camera viewport and sets the backend viewport/scissor from the camera
- the host builds one `RenderCommandList2D` from `camera->get_RenderQueue2D()`
- the host iterates the logical command stream and dispatches by `RenderCommand2DType`

Implementation rules:
- do not call drawable `Draw()` from the native host path anymore
- keep the existing native draw helpers, but retarget them to explicit command payloads
- no clip-stack implementation in this task

- [ ] **Step 3: Execute the three current command types**

Required coverage:
- `TexturedQuad` uses the existing textured-quad sprite pipeline
- `GlyphQuad` uses the existing atlas/textured-quad pipeline
- `RoundedRect` uses the existing native rounded-rect execution path with radius, border thickness, corners, fill color, and border color preserved

This is the key correction from the original plan:
- do **not** flatten rounded rectangles into solid rects in core for the first foundation slice
- preserve the engine primitive so the backend can keep the visual behavior it already needs now

- [ ] **Step 4: Rebuild the packaged Windows output and verify the menu still renders**

Run:

```powershell
rtk dotnet run --project helengine.ui\helengine.editor.app\helengine.editor.app.csproj --no-build -- --project C:\dev\helprojs\city\project.heproj --build windows --output C:\dev\helprojs\output\windows
```

Expected:

```text
Build completed for platform 'windows': C:\dev\helprojs\output\windows
Enabled runtime features: Render2D (AutoDetected), Shaders (AutoDetected), Sprites (AutoDetected), Text2D (AutoDetected).
```

Live verification:
- launch `C:\dev\helprojs\output\windows\helengine_windows.exe`
- verify the Demo Disc main menu still renders instead of a purple clear-only frame

- [ ] **Step 5: Commit the native backend migration**

```bash
git -C C:\dev\helworks\helengine-windows add src/platform/windows/win32/win32_render_bridge.hpp src/platform/windows/win32/win32_render_bridge.cpp
git -C C:\dev\helworks\helengine-windows commit -m "feat: execute native 2d command lists on windows"
```

## Task 4: Add coverage for the resolved command path and record the deferred clip boundary

**Files:**
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
- Create: `engine/helengine.editor.tests/managers/rendering/RenderCommandPathParityTests.cs`
- Modify: `docs/superpowers/specs/2026-05-06-native-2d-command-list-foundation-design.md`

- [ ] **Step 1: Add parity-style tests for the resolved command path**

Cover:
- sprite queue -> one textured quad command with preserved texture/source rect/tint
- text queue -> glyph commands preserve atlas source rects and color
- rounded rect queue -> rounded-rect command preserves radius, border thickness, corner mask, fill color, and border color

- [ ] **Step 2: Add one explicit note to the spec that widget clip-stack support is deferred**

Record the actual boundary discovered during plan revision:
- current core has scroll state but not a general clip ownership model
- current backends only apply camera viewport scissor
- per-widget clip push/pop is a follow-up feature, not part of this foundation

- [ ] **Step 3: Run the focused HelEngine verification slice**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~RenderCommandList2DTests|FullyQualifiedName~RenderCommandListBuilder2DTests|FullyQualifiedName~RenderCommandPathParityTests|FullyQualifiedName~RuntimeSceneLoadServiceTests.Load_WhenSceneContainsBakedDemoMenu_MaterializesTheComponent|FullyQualifiedName~RuntimeSceneLoadServiceTests.Load_WhenSceneContainsBakedDemoMenu_InitializesOnlyTheInitialPanelAsEnabled|FullyQualifiedName~EditorPlatformBuildGraphRunnerTests" -v minimal
```

Expected:

```text
Passed!  - Failed: 0
```

- [ ] **Step 4: Commit the verification coverage and spec note**

```bash
git add engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs engine/helengine.editor.tests/managers/rendering/RenderCommandPathParityTests.cs docs/superpowers/specs/2026-05-06-native-2d-command-list-foundation-design.md
git commit -m "test: cover native 2d command path foundation"
```

## Task 5: Final validation and integration status

**Files:**
- Modify: `C:\dev\helworks\helengine-windows\src\platform\windows\win32\win32_render_bridge.cpp`

- [ ] **Step 1: Remove temporary one-off diagnostics if they are no longer needed**

Keep:
- durable build feature-summary logging

Remove:
- any extra blank-screen diagnostics that were only useful during bring-up and are no longer part of the product behavior

- [ ] **Step 2: Run the final HelEngine verification slice**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~RenderCommandList2DTests|FullyQualifiedName~RenderCommandListBuilder2DTests|FullyQualifiedName~RenderCommandPathParityTests|FullyQualifiedName~RuntimeSceneLoadServiceTests.Load_WhenSceneContainsBakedDemoMenu_MaterializesTheComponent|FullyQualifiedName~RuntimeSceneLoadServiceTests.Load_WhenSceneContainsBakedDemoMenu_InitializesOnlyTheInitialPanelAsEnabled|FullyQualifiedName~EditorPlatformBuildGraphRunnerTests" -v minimal
```

Expected:

```text
Passed!  - Failed: 0
```

- [ ] **Step 3: Run the final packaged Windows verification**

Run:

```powershell
rtk dotnet run --project helengine.ui\helengine.editor.app\helengine.editor.app.csproj --no-build -- --project C:\dev\helprojs\city\project.heproj --build windows --output C:\dev\helprojs\output\windows
```

Expected:

```text
Build completed for platform 'windows': C:\dev\helprojs\output\windows
Enabled runtime features: Render2D (AutoDetected), Shaders (AutoDetected), Sprites (AutoDetected), Text2D (AutoDetected).
```

- [ ] **Step 4: Record the final integration status**

```text
HelEngine repo contains a codegen-safe resolved 2D command-list foundation for textured quads, glyph quads, and rounded rectangles.
helengine-windows consumes that command stream directly.
Managed DirectX11 and Vulkan remain on the existing drawable path by design for this pass.
Per-widget clip-stack support is intentionally deferred until core exposes a real clip ownership seam.
```
