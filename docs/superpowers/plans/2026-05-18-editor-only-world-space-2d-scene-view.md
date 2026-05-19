# Editor-Only World-Space 2D Scene View Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show `SpriteComponent`, `TextComponent`, and `RoundedRectComponent` in the editor 3D scene view at their real scene transform using editor-only world-space preview proxies, while preserving `ViewportComponent` as the viewport-lock exception and keeping 2D selection priority over 3D.

**Architecture:** Build one editor-only synchronization layer that discovers supported 2D scene components and creates internal 3D preview proxies for them. Each proxy mirrors its source component/entity, stays non-selectable itself, and resolves selection back to the original authored 2D entity. `ViewportComponent` remains the sole exception that can keep a subtree viewport-locked/resized in scene view. No changes are allowed in `helengine.core`.

**Tech Stack:** C#/.NET 9, HelEngine editor runtime, DirectX11 editor renderer, xUnit.

---

## File Structure

### Editor preview synchronization files

- Create: `engine/helengine.editor/components/EditorWorldSpace2DPreviewSyncComponent.cs`
- Create: `engine/helengine.editor/managers/scene/EditorWorldSpace2DPreviewRegistry.cs`
- Create: `engine/helengine.editor/managers/scene/EditorWorldSpace2DPreviewMapper.cs`

### Editor preview proxy files

- Create: `engine/helengine.editor/components/preview2d/EditorSpriteWorldPreviewComponent.cs`
- Create: `engine/helengine.editor/components/preview2d/EditorTextWorldPreviewComponent.cs`
- Create: `engine/helengine.editor/components/preview2d/EditorRoundedRectWorldPreviewComponent.cs`
- Create: `engine/helengine.editor/components/preview2d/Editor2DPreviewSourceTagComponent.cs`

### Viewport/picking files

- Modify: `engine/helengine.editor/managers/workspace/ViewportWorkspacePanelController.cs`
- Modify: `engine/helengine.editor/managers/workspace/EditorViewportWorkspaceState.cs`
- Modify: `engine/helengine.editor/components/EditorViewportPicker.cs`
- Modify: `engine/helengine.editor/managers/scene/EditorViewportDirect2DPresentationService.cs`
- Modify: `engine/helengine.editor/managers/scene/EditorViewportSceneSelectionFilter.cs`
- Modify: `engine/helengine.editor/components/EditorViewportCanvasPlanePreviewComponent.cs`

### Test files

- Create: `engine/helengine.editor.tests/EditorWorldSpace2DPreviewSyncComponentTests.cs`
- Create: `engine/helengine.editor.tests/Editor2DWorldPreviewProxyTests.cs`
- Modify: `engine/helengine.editor.tests/EditorViewportPicker2DSelectionTests.cs`
- Modify: `engine/helengine.editor.tests/ViewportAndAnchorLayoutTests.cs`

---

### Task 1: Add An Editor-Owned Registry And Mapping Layer For 2D Preview Proxies

**Files:**
- Create: `engine/helengine.editor/managers/scene/EditorWorldSpace2DPreviewRegistry.cs`
- Create: `engine/helengine.editor/managers/scene/EditorWorldSpace2DPreviewMapper.cs`
- Create: `engine/helengine.editor.tests/Editor2DWorldPreviewProxyTests.cs`

- [ ] **Step 1: Write the failing mapping tests**

Add tests that verify:

```csharp
[Fact]
public void RegisterPreviewProxy_WhenSourceEntityIsMapped_ResolvesPreviewBackToSourceEntity() {
    // create one source 2D entity and one preview entity
    // register the mapping
    // assert preview resolves back to source
}

[Fact]
public void RemovePreviewProxy_WhenSourceEntityIsRemoved_ClearsBothDirections() {
    // register one mapping, remove it, assert no stale lookup remains
}
```

- [ ] **Step 2: Run the mapping tests to verify they fail**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~Editor2DWorldPreviewProxyTests"
```

Expected: FAIL because no editor-side registry/mapping layer exists yet.

- [ ] **Step 3: Implement the minimal preview registry**

Create `EditorWorldSpace2DPreviewRegistry.cs` with:

- source entity -> preview entity lookup
- preview entity -> source entity lookup
- registration
- removal
- reset/clear support for tests

Keep it editor-only and in-memory.

- [ ] **Step 4: Implement a focused mapper helper**

Create `EditorWorldSpace2DPreviewMapper.cs` to centralize:

- whether an entity/component type is preview-supported
- how selection should resolve from proxy back to source

Do not add rendering logic here.

- [ ] **Step 5: Run the mapping tests to verify they pass**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~Editor2DWorldPreviewProxyTests"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.editor/managers/scene/EditorWorldSpace2DPreviewRegistry.cs engine/helengine.editor/managers/scene/EditorWorldSpace2DPreviewMapper.cs engine/helengine.editor.tests/Editor2DWorldPreviewProxyTests.cs
git commit -m "Add editor 2d preview proxy registry"
```

### Task 2: Add Editor-Only World-Space Preview Proxy Components

**Files:**
- Create: `engine/helengine.editor/components/preview2d/Editor2DPreviewSourceTagComponent.cs`
- Create: `engine/helengine.editor/components/preview2d/EditorSpriteWorldPreviewComponent.cs`
- Create: `engine/helengine.editor/components/preview2d/EditorTextWorldPreviewComponent.cs`
- Create: `engine/helengine.editor/components/preview2d/EditorRoundedRectWorldPreviewComponent.cs`
- Modify: `engine/helengine.editor.tests/Editor2DWorldPreviewProxyTests.cs`

- [ ] **Step 1: Write the failing proxy-component tests**

Add tests that verify:

```csharp
[Fact]
public void Update_WhenSpriteSourceMoves_PreviewProxyMirrorsWorldTransform() {
    // move source entity
    // assert preview entity/component mirrors the transform
}

[Fact]
public void Update_WhenPreviewProxyExists_ProxyEntityIsInternalAndNonSelectable() {
    // assert editor-internal flags/tags are applied
}
```

- [ ] **Step 2: Run the proxy-component tests to verify they fail**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~Editor2DWorldPreviewProxyTests"
```

Expected: FAIL because no preview proxy components exist yet.

- [ ] **Step 3: Implement the internal source-tag component**

Create `Editor2DPreviewSourceTagComponent.cs` to store the authoritative source entity/component identity used by selection and sync code.

- [ ] **Step 4: Implement sprite, text, and rounded-rect preview proxy components**

Each preview component should:

- be editor-only
- mirror source transform and enabled state
- carry only the rendering data needed by the proxy
- remain internal/non-selectable

Keep one class per preview type.

- [ ] **Step 5: Run the proxy-component tests to verify they pass**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~Editor2DWorldPreviewProxyTests"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.editor/components/preview2d/Editor2DPreviewSourceTagComponent.cs engine/helengine.editor/components/preview2d/EditorSpriteWorldPreviewComponent.cs engine/helengine.editor/components/preview2d/EditorTextWorldPreviewComponent.cs engine/helengine.editor/components/preview2d/EditorRoundedRectWorldPreviewComponent.cs engine/helengine.editor.tests/Editor2DWorldPreviewProxyTests.cs
git commit -m "Add editor world-space 2d preview proxy components"
```

### Task 3: Add One Synchronization Component That Maintains Preview Proxies

**Files:**
- Create: `engine/helengine.editor/components/EditorWorldSpace2DPreviewSyncComponent.cs`
- Modify: `engine/helengine.editor/managers/workspace/ViewportWorkspacePanelController.cs`
- Modify: `engine/helengine.editor/managers/workspace/EditorViewportWorkspaceState.cs`
- Create: `engine/helengine.editor.tests/EditorWorldSpace2DPreviewSyncComponentTests.cs`

- [ ] **Step 1: Write the failing sync tests**

Add tests that verify:

```csharp
[Fact]
public void Update_WhenSupported2DSceneEntityAppears_CreatesPreviewProxy() {
    // source sprite/text/rounded rect enters the scene
    // assert one preview proxy is created
}

[Fact]
public void Update_WhenSourceEntityIsRemoved_RemovesPreviewProxy() {
    // remove source entity
    // assert preview proxy is removed and registry is cleared
}
```

- [ ] **Step 2: Run the sync tests to verify they fail**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorWorldSpace2DPreviewSyncComponentTests"
```

Expected: FAIL because no synchronization component exists yet.

- [ ] **Step 3: Implement `EditorWorldSpace2DPreviewSyncComponent`**

The sync component should:

- scan supported 2D scene entities
- create preview proxies for supported components
- update preview state each frame
- destroy stale preview proxies
- use the registry/mapper from Task 1

This is the only central lifecycle owner.

- [ ] **Step 4: Attach the sync component in viewport workspace setup**

Modify `ViewportWorkspacePanelController.cs` and `EditorViewportWorkspaceState.cs` so each scene viewport runtime stack owns one sync component.

- [ ] **Step 5: Run the sync tests to verify they pass**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorWorldSpace2DPreviewSyncComponentTests"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.editor/components/EditorWorldSpace2DPreviewSyncComponent.cs engine/helengine.editor/managers/workspace/ViewportWorkspacePanelController.cs engine/helengine.editor/managers/workspace/EditorViewportWorkspaceState.cs engine/helengine.editor.tests/EditorWorldSpace2DPreviewSyncComponentTests.cs
git commit -m "Sync editor world-space 2d preview proxies"
```

### Task 4: Preserve `ViewportComponent` As The Only Layout Exception

**Files:**
- Modify: `engine/helengine.editor/managers/scene/EditorViewportDirect2DPresentationService.cs`
- Modify: `engine/helengine.editor/components/EditorViewportCanvasPlanePreviewComponent.cs`
- Modify: `engine/helengine.editor.tests/ViewportAndAnchorLayoutTests.cs`

- [ ] **Step 1: Write the failing viewport-exception tests**

Add tests that verify:

```csharp
[Fact]
public void SceneViewPreview_WhenSourceEntityHasNoViewportOwner_UsesRealWorldTransform() {
    // assert preview proxy mirrors raw source transform
}

[Fact]
public void SceneViewPreview_WhenSourceEntityIsInsideViewportSubtree_KeepsViewportLockBehavior() {
    // assert viewport-owned subtree still uses viewport-driven layout rules
}
```

- [ ] **Step 2: Run the viewport-exception tests to verify they fail**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~ViewportAndAnchorLayoutTests|FullyQualifiedName~EditorWorldSpace2DPreviewSyncComponentTests"
```

Expected: FAIL because the preview sync path does not yet distinguish default world-space behavior from the viewport-owned exception.

- [ ] **Step 3: Narrow `EditorViewportDirect2DPresentationService` to exception logic only**

Refactor it so:

- default path is real world transform preview
- only `ViewportComponent`-owned subtrees use viewport-lock/resizing behavior

- [ ] **Step 4: Reduce `EditorViewportCanvasPlanePreviewComponent` to compatibility-only behavior if still needed**

Do not let it remain the primary model.

- [ ] **Step 5: Run the viewport-exception tests to verify they pass**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~ViewportAndAnchorLayoutTests|FullyQualifiedName~EditorWorldSpace2DPreviewSyncComponentTests"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.editor/managers/scene/EditorViewportDirect2DPresentationService.cs engine/helengine.editor/components/EditorViewportCanvasPlanePreviewComponent.cs engine/helengine.editor.tests/ViewportAndAnchorLayoutTests.cs
git commit -m "Keep viewport component as editor 2d layout exception"
```

### Task 5: Make Picking Resolve Preview Proxies Back To The Authored 2D Entity

**Files:**
- Modify: `engine/helengine.editor/components/EditorViewportPicker.cs`
- Modify: `engine/helengine.editor/managers/scene/EditorViewportSceneSelectionFilter.cs`
- Modify: `engine/helengine.editor.tests/EditorViewportPicker2DSelectionTests.cs`

- [ ] **Step 1: Write the failing picking tests**

Add tests that verify:

```csharp
[Fact]
public void ResolveSelection_WhenPreviewProxyIsClicked_SelectsTheUnderlying2DEntity() {
    // clicking a preview proxy resolves to the source 2D entity
}

[Fact]
public void ResolveSelection_When2DPreviewAnd3DOverlap_PrefersThe2DSourceEntity() {
    // 2D preview wins over overlapping 3D content
}
```

- [ ] **Step 2: Run the picking tests to verify they fail**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorViewportPicker2DSelectionTests"
```

Expected: FAIL because picker logic does not yet resolve preview proxies back to source entities consistently.

- [ ] **Step 3: Update picker logic to resolve source entities through the preview registry**

Ensure:

- preview proxies are never the final selected entity
- underlying 2D source entity is selected
- 2D retains priority over 3D

- [ ] **Step 4: Run the picking tests to verify they pass**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorViewportPicker2DSelectionTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/EditorViewportPicker.cs engine/helengine.editor/managers/scene/EditorViewportSceneSelectionFilter.cs engine/helengine.editor.tests/EditorViewportPicker2DSelectionTests.cs
git commit -m "Resolve editor 2d preview picks to source entities"
```

### Task 6: Final Verification

**Files:**
- No code changes required unless verification exposes gaps.

- [ ] **Step 1: Run the focused editor preview and picking slices**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~Editor2DWorldPreviewProxyTests|FullyQualifiedName~EditorWorldSpace2DPreviewSyncComponentTests|FullyQualifiedName~EditorViewportPicker2DSelectionTests|FullyQualifiedName~ViewportAndAnchorLayoutTests"
```

Expected: PASS.

- [ ] **Step 2: Run one broader editor interaction slice**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorSession|FullyQualifiedName~SceneFileLoadServiceTests"
```

Expected: PASS or only unrelated known failures.

- [ ] **Step 3: Build the editor host**

Run:

```bash
dotnet build helengine.ui/helengine.editor.app/helengine.editor.app.csproj -c Debug --no-restore
```

Expected: PASS.

- [ ] **Step 4: Manual validation in the editor**

Validate:

- sprites, text, and rounded rects appear in the 3D scene view at their real scene transform
- viewport-owned subtrees still behave with viewport-lock/resizing
- clicking a previewed 2D object selects the original authored 2D entity
- when 2D and 3D overlap, 2D selection wins
- no code changes were made in `helengine.core`

- [ ] **Step 5: Commit any final follow-up fixes**

```bash
git add .
git commit -m "Finish editor-only world-space 2d scene view"
```
