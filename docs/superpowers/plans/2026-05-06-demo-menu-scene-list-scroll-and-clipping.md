# Demo Menu Scene List Scroll And Clipping Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the generated Demo Disc scene list clip inside its panel and scroll by rows through the existing `ScrollComponent`, with both selection-driven and mouse-wheel scrolling.

**Architecture:** Extend the baked panel structure so each menu panel gets a clipped item viewport host and a scrolling item root, then bind that authored structure into the generic menu runtime. `MenuComponent` remains responsible for selection and actions, while `ScrollComponent` owns all row-based scroll state and wheel handling.

**Tech Stack:** C#, xUnit, baked scene asset generation, `ScrollComponent`, `ClipRectComponent`, runtime menu components.

---

## File Structure

- Modify: `engine/helengine.editor/managers/menu/DemoMenuSceneAssetFactory.cs`
  - Bake a per-panel item viewport host and scrolling item root.
  - Parent item rows under the scrolling root instead of directly under the panel root.
  - Attach a `ClipRectComponent` and `ScrollComponent` in the generated structure.
- Modify: `engine/helengine.core/components/2d/menu/MenuComponent.cs`
  - Discover the panel item viewport/scroll root while binding panels.
  - Drive `ScrollComponent` to keep selected rows visible.
  - Route mouse-wheel scrolling through `ScrollComponent`.
- Modify: `engine/helengine.core/components/2d/menu/MenuPanelRuntime.cs`
  - Carry the extra authored runtime references needed for scroll/clipping.
- Modify: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`
  - Assert the generated menu structure includes viewport, scrolling root, clip, and scroll component.
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
  - Cover runtime binding and selected-row visibility for baked menu panels if there is already menu runtime coverage there.
- Modify: `engine/helengine.editor.tests/ScrollComponentTests.cs`
  - Add or reuse a focused test only if `ScrollComponent` itself needs a generic “ensure row visible” capability.

### Task 1: Lock The Generated Panel Structure In Tests

**Files:**
- Modify: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`
- Test: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`

- [ ] **Step 1: Write the failing structure test for the item viewport and scrolling root**

Add a new fact near the other generated scene composition tests:

```csharp
/// <summary>
/// Ensures generated demo menu panels bake a clipped item viewport and scrolling root instead of parenting item rows directly under the panel root.
/// </summary>
[Fact]
public void WriteAll_WhenMenuSceneIsGenerated_BakesClippedSceneListViewportAndScrollingRoot() {
    DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

    writer.WriteAll(ProjectRootPath);

    SceneAsset sceneAsset = ReadGeneratedSceneAsset();
    SceneEntityAsset menuEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
    SceneEntityAsset generatedRoot = Assert.Single(menuEntity.Children, entity => entity.Name == DemoMenuLayout.GeneratedRootEntityName);
    SceneEntityAsset sceneSelectPanel = Assert.Single(generatedRoot.Children, child => string.Equals(child.Id, "panel-scene-select", StringComparison.Ordinal));

    SceneEntityAsset itemsViewport = Assert.Single(sceneSelectPanel.Children, child => string.Equals(child.Id, "panel-scene-select-items-viewport", StringComparison.Ordinal));
    SceneEntityAsset itemsRoot = Assert.Single(itemsViewport.Children, child => string.Equals(child.Id, "panel-scene-select-items-root", StringComparison.Ordinal));

    Assert.DoesNotContain(sceneSelectPanel.Children, child => child.Id.StartsWith("item-scene-select-", StringComparison.Ordinal));
    Assert.Contains(itemsRoot.Children, child => child.Id.StartsWith("item-scene-select-", StringComparison.Ordinal));
}
```

- [ ] **Step 2: Run the new structure test to verify it fails**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DemoDiscSceneWriterTests.WriteAll_WhenMenuSceneIsGenerated_BakesClippedSceneListViewportAndScrollingRoot" -v minimal
```

Expected:

```text
FAIL
```

The failure should show that the viewport or items root entities do not exist yet, or that item rows are still direct children of the panel.

- [ ] **Step 3: Write the failing structure test for clip and scroll components**

Add a second fact immediately after the first:

```csharp
/// <summary>
/// Ensures the generated scene-list viewport owns a clip component and the scrolling root owns a scroll component.
/// </summary>
[Fact]
public void WriteAll_WhenMenuSceneIsGenerated_BakesClipAndScrollComponentsForSceneList() {
    DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

    writer.WriteAll(ProjectRootPath);

    SceneAsset sceneAsset = ReadGeneratedSceneAsset();
    SceneEntityAsset menuEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
    SceneEntityAsset generatedRoot = Assert.Single(menuEntity.Children, entity => entity.Name == DemoMenuLayout.GeneratedRootEntityName);
    SceneEntityAsset sceneSelectPanel = Assert.Single(generatedRoot.Children, child => string.Equals(child.Id, "panel-scene-select", StringComparison.Ordinal));
    SceneEntityAsset itemsViewport = Assert.Single(sceneSelectPanel.Children, child => string.Equals(child.Id, "panel-scene-select-items-viewport", StringComparison.Ordinal));
    SceneEntityAsset itemsRoot = Assert.Single(itemsViewport.Children, child => string.Equals(child.Id, "panel-scene-select-items-root", StringComparison.Ordinal));

    Assert.Contains(itemsViewport.Components, component => string.Equals(component.ComponentTypeId, ClipRectComponent.SerializedComponentTypeId, StringComparison.Ordinal));
    Assert.Contains(itemsRoot.Components, component => string.Equals(component.ComponentTypeId, ScrollComponent.SerializedComponentTypeId, StringComparison.Ordinal));
}
```

- [ ] **Step 4: Run the second structure test to verify it fails**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DemoDiscSceneWriterTests.WriteAll_WhenMenuSceneIsGenerated_BakesClipAndScrollComponentsForSceneList" -v minimal
```

Expected:

```text
FAIL
```

The failure should show that the generated entities or required components do not exist yet.

- [ ] **Step 5: Commit the failing generated-structure regressions**

```bash
git add engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs
git commit -m "test: lock demo menu scene list structure"
```

### Task 2: Bake The Clipped Viewport And Scrolling Root

**Files:**
- Modify: `engine/helengine.editor/managers/menu/DemoMenuSceneAssetFactory.cs`
- Test: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`

- [ ] **Step 1: Add generator helpers for the item viewport host and scrolling root**

In `DemoMenuSceneAssetFactory.cs`, add focused helper methods near the other entity builders so the authored structure stays readable:

```csharp
SceneEntityAsset BuildPanelItemsViewportEntityAsset(string panelId) {
    ClipRectComponent clipRectComponent = new ClipRectComponent {
        Size = new int2(DemoMenuLayout.ButtonWidth, ResolvePanelItemsViewportHeight())
    };
    SceneComponentAssetRecord clipRecord = SerializeClipRectComponent(clipRectComponent);

    return new SceneEntityAsset {
        Id = $"panel-{panelId}-items-viewport",
        Name = $"PanelItemsViewport-{panelId}",
        LocalPosition = new float3(120f, 280f, 0f),
        LocalScale = float3.One,
        LocalOrientation = float4.Identity,
        Components = new[] { clipRecord },
        Children = Array.Empty<SceneEntityAsset>()
    };
}

SceneEntityAsset BuildPanelItemsRootEntityAsset(string panelId, SceneEntityAsset[] itemChildren) {
    SceneComponentAssetRecord scrollRecord = SerializeScrollComponent(new ScrollComponent());
    return new SceneEntityAsset {
        Id = $"panel-{panelId}-items-root",
        Name = $"PanelItemsRoot-{panelId}",
        LocalPosition = float3.Zero,
        LocalScale = float3.One,
        LocalOrientation = float4.Identity,
        Components = new[] { scrollRecord },
        Children = itemChildren
    };
}
```

If `ClipRectComponent` or `ScrollComponent` do not already have persistence descriptors used by scene generation, add the exact serialization support needed in this file or the existing scene persistence registry path instead of inventing a temporary runtime-only shortcut.

- [ ] **Step 2: Reparent the baked item rows under the scrolling root**

Update `BuildPanelEntityAsset` so it:

- builds the item rows into a list first
- creates `itemsRoot` with those rows as children
- creates `itemsViewport`
- sets `itemsRoot` as the only child of `itemsViewport`
- adds `itemsViewport` to the panel children instead of adding item rows directly to the panel

The final panel-child shape should look like:

```csharp
List<SceneEntityAsset> itemChildren = new List<SceneEntityAsset>();
for (int itemIndex = 0; itemIndex < panelDefinition.Items.Length; itemIndex++) {
    MenuItemDefinition itemDefinition = panelDefinition.Items[itemIndex];
    if (!itemDefinition.Enabled) {
        continue;
    }

    itemChildren.Add(BuildItemEntityAsset(definition, panelDefinition, itemDefinition, itemInsertIndex));
    itemInsertIndex++;
}

SceneEntityAsset itemsRoot = BuildPanelItemsRootEntityAsset(panelDefinition.PanelId, itemChildren.ToArray());
SceneEntityAsset itemsViewport = BuildPanelItemsViewportEntityAsset(panelDefinition.PanelId);
itemsViewport.Children = new[] { itemsRoot };
children.Add(itemsViewport);
```

If `SceneEntityAsset.Children` is immutable after construction, create the viewport with the scrolling root in its constructor path instead.

- [ ] **Step 3: Re-run the generated-structure tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DemoDiscSceneWriterTests.WriteAll_WhenMenuSceneIsGenerated_BakesClippedSceneListViewportAndScrollingRoot|FullyQualifiedName~DemoDiscSceneWriterTests.WriteAll_WhenMenuSceneIsGenerated_BakesClipAndScrollComponentsForSceneList" -v minimal
```

Expected:

```text
PASS
```

- [ ] **Step 4: Commit the generator-side scene-list structure change**

```bash
git add engine/helengine.editor/managers/menu/DemoMenuSceneAssetFactory.cs engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs
git commit -m "feat: bake clipped demo menu scene lists"
```

### Task 3: Bind ScrollComponent Into The Runtime Menu Flow

**Files:**
- Modify: `engine/helengine.core/components/2d/menu/MenuComponent.cs`
- Modify: `engine/helengine.core/components/2d/menu/MenuPanelRuntime.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
- Test only if needed: `engine/helengine.editor.tests/ScrollComponentTests.cs`

- [ ] **Step 1: Add runtime references for the item viewport and scroll component**

Update `MenuPanelRuntime` to carry the extra references needed by runtime navigation:

```csharp
public sealed class MenuPanelRuntime {
    public MenuPanelRuntime(
        MenuPanelComponent definition,
        Entity rootEntity,
        TextComponent selectedDescriptionText,
        Entity itemsViewportEntity,
        ScrollComponent itemsScrollComponent,
        MenuItemRuntime[] items) {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        RootEntity = rootEntity ?? throw new ArgumentNullException(nameof(rootEntity));
        SelectedDescriptionText = selectedDescriptionText ?? throw new ArgumentNullException(nameof(selectedDescriptionText));
        ItemsViewportEntity = itemsViewportEntity ?? throw new ArgumentNullException(nameof(itemsViewportEntity));
        ItemsScrollComponent = itemsScrollComponent ?? throw new ArgumentNullException(nameof(itemsScrollComponent));
        Items = items ?? throw new ArgumentNullException(nameof(items));
        SelectedItemIndex = -1;
    }

    public Entity ItemsViewportEntity { get; }

    public ScrollComponent ItemsScrollComponent { get; }
}
```

Keep the rest of the type intact.

- [ ] **Step 2: Bind the authored viewport and scroll component while building panels**

In `MenuComponent.BindPanels`, resolve and pass the extra authored structure:

```csharp
Entity itemsViewportEntity = ResolveItemsViewportEntity(panelEntity);
ScrollComponent itemsScrollComponent = ResolveItemsScrollComponent(itemsViewportEntity);
MenuItemRuntime[] itemRuntimes = BindItems(panelEntity, panelComponent.PanelId);
MenuPanelRuntime panelRuntime = new MenuPanelRuntime(
    panelComponent,
    panelEntity,
    selectedDescriptionText,
    itemsViewportEntity,
    itemsScrollComponent,
    itemRuntimes);
```

Add focused helper methods:

```csharp
Entity ResolveItemsViewportEntity(Entity panelEntity) { ... }

ScrollComponent ResolveItemsScrollComponent(Entity itemsViewportEntity) { ... }
```

These should fail loudly if the authored structure is missing.

- [ ] **Step 3: Route selection changes through ScrollComponent**

In `SetSelection`, after updating `SelectedItemIdValue` and the description text, keep the selected row visible:

```csharp
panelRuntime.ItemsScrollComponent.ItemCount = panelRuntime.Items.Length;
panelRuntime.ItemsScrollComponent.VisibleItemCount = panelRuntime.Definition.VisibleItemCount;
EnsureSelectedItemIsVisible(panelRuntime, itemIndex);
```

Add a helper in `MenuComponent`:

```csharp
void EnsureSelectedItemIsVisible(MenuPanelRuntime panelRuntime, int itemIndex) {
    int currentOffset = panelRuntime.ItemsScrollComponent.ScrollOffset;
    int visibleItemCount = panelRuntime.Definition.VisibleItemCount;
    if (itemIndex < currentOffset) {
        panelRuntime.ItemsScrollComponent.ScrollTo(itemIndex);
        return;
    }

    int lastVisibleIndex = currentOffset + visibleItemCount - 1;
    if (itemIndex > lastVisibleIndex) {
        panelRuntime.ItemsScrollComponent.ScrollTo(itemIndex - visibleItemCount + 1);
    }
}
```

Keep this row-based.

- [ ] **Step 4: Route mouse wheel input through ScrollComponent**

In `HandleMouseInput`, after hover selection handling and before click release logic, scroll the active panel only when the pointer is over the active items viewport:

```csharp
if (IsPointerInsideItemsViewport(ActivePanel, inputSystem.GetMouseX(), inputSystem.GetMouseY())) {
    ActivePanel.ItemsScrollComponent.TryApplyWheelInput();
}
```

Add a focused helper:

```csharp
bool IsPointerInsideItemsViewport(MenuPanelRuntime panelRuntime, int pointerX, int pointerY) {
    float3 position = panelRuntime.ItemsViewportEntity.Position;
    ClipRectComponent clipRectComponent = FindRequiredComponent<ClipRectComponent>(panelRuntime.ItemsViewportEntity);
    return pointerX >= position.X
        && pointerX < position.X + clipRectComponent.Size.X
        && pointerY >= position.Y
        && pointerY < position.Y + clipRectComponent.Size.Y;
}
```

Do not create separate wheel logic in the menu runtime beyond this routing.

- [ ] **Step 5: Add a failing runtime regression for selected-row scrolling or extend ScrollComponent tests if needed**

Prefer an existing baked-menu runtime test file. Add coverage like:

```csharp
[Fact]
public void Load_WhenBakedMenuPanelContainsMoreRowsThanVisibleCount_SelectingLowerRowsAdvancesScrollOffset() {
    // Build or load a baked menu scene with a panel that has more items than VisibleItemCount.
    // Move selection repeatedly through the bound MenuComponent.
    // Assert that the panel scroll component offset advances once the selected index leaves the visible window.
}
```

If the easiest seam is a direct `ScrollComponent` behavior assertion instead, add only the generic missing behavior there and keep menu-specific routing tests in the menu runtime file.

- [ ] **Step 6: Run the runtime menu and scroll verification slice**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~RuntimeSceneLoadServiceTests|FullyQualifiedName~ScrollComponentTests" -v minimal
```

Expected:

```text
PASS
```

- [ ] **Step 7: Commit the runtime scroll binding**

```bash
git add engine/helengine.core/components/2d/menu/MenuComponent.cs engine/helengine.core/components/2d/menu/MenuPanelRuntime.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs engine/helengine.editor.tests/ScrollComponentTests.cs
git commit -m "feat: scroll demo menu lists through scroll component"
```

### Task 4: Regenerate And Verify The Real Demo Scene

**Files:**
- Modify: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`
- Regenerate externally for verification: `C:/dev/helprojs/city/assets/Scenes/DemoDiscMainMenu.helen`

- [ ] **Step 1: Add one end-to-end scene-writer assertion that the scene-select panel uses the authored visible row count**

Add a focused generator assertion:

```csharp
/// <summary>
/// Ensures the generated scene-select panel bakes enough rows to require clipping while still owning a scroll component.
/// </summary>
[Fact]
public void WriteAll_WhenSceneSelectPanelExceedsVisibleRowCount_BakesScrollingSceneListStructure() {
    DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

    writer.WriteAll(ProjectRootPath);

    SceneAsset sceneAsset = ReadGeneratedSceneAsset();
    SceneEntityAsset menuEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
    SceneEntityAsset generatedRoot = Assert.Single(menuEntity.Children, entity => entity.Name == DemoMenuLayout.GeneratedRootEntityName);
    SceneEntityAsset sceneSelectPanel = Assert.Single(generatedRoot.Children, child => string.Equals(child.Id, "panel-scene-select", StringComparison.Ordinal));
    SceneEntityAsset itemsViewport = Assert.Single(sceneSelectPanel.Children, child => string.Equals(child.Id, "panel-scene-select-items-viewport", StringComparison.Ordinal));
    SceneEntityAsset itemsRoot = Assert.Single(itemsViewport.Children, child => string.Equals(child.Id, "panel-scene-select-items-root", StringComparison.Ordinal));

    Assert.True(itemsRoot.Children.Count > 7);
}
```

- [ ] **Step 2: Run the full demo scene writer suite**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DemoDiscSceneWriterTests" -v minimal
```

Expected:

```text
PASS
```

- [ ] **Step 3: Regenerate the real Demo Disc main menu scene**

Run:

```bash
rtk dotnet run --project helengine.ui/helengine.editor.app/helengine.editor.app.csproj -- --project C:/dev/helprojs/city/project.heproj --editor-command menu.regenerate-demo-disc-main-menu
```

Expected:

```text
Editor command 'menu.regenerate-demo-disc-main-menu' executed successfully.
```

- [ ] **Step 4: Commit the final engine-side verification changes**

```bash
git add engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs
git commit -m "test: verify demo menu scene list scrolling"
```
