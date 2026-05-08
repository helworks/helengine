# Demo Menu Generator Static Copy Removal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the static panel subtitle text and left accent bar from regenerated Demo Disc menu panels while preserving the dynamic selected-item description and runtime menu behavior.

**Architecture:** Keep the change generator-side only by updating `DemoMenuSceneAssetFactory` so baked panel subtrees stop authoring the two unwanted entities and reclaim the recovered space with modest position adjustments. Lock the behavior with scene-writer regressions that inspect the generated scene asset rather than patching serialized output by hand.

**Tech Stack:** C#, xUnit, existing scene asset serialization/deserialization, demo menu generation services.

---

## File Structure

- Modify: `engine/helengine.editor/managers/menu/DemoMenuSceneAssetFactory.cs`
  - Remove authored static panel-description entity creation.
  - Remove authored left accent bar entity creation.
  - Adjust remaining panel child positions so the list and bottom selected-description use the recovered space cleanly.
- Modify: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`
  - Add regressions that assert regenerated menu panels no longer contain the static description entity or accent entity.
  - Add assertions that selected-description entities still exist so the runtime path remains intact.
- Inspect only if needed during execution: `engine/helengine.editor/managers/menu/EditorMenuSceneRegenerationService.cs`
  - No behavior change expected.
  - Use only to confirm regeneration still routes through `DemoMenuSceneBuildService` after the generator update.

### Task 1: Lock The New Panel Composition In Tests

**Files:**
- Modify: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`
- Test: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`

- [ ] **Step 1: Write the failing test for removing the static panel description entity**

Add a new fact near the other generated-scene composition tests:

```csharp
/// <summary>
/// Ensures regenerated demo menu panels no longer author the static subtitle entity beneath the heading.
/// </summary>
[Fact]
public void WriteAll_WhenMenuSceneIsGenerated_DoesNotBakeStaticPanelDescriptionEntities() {
    DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

    writer.WriteAll(ProjectRootPath);

    SceneAsset sceneAsset = ReadGeneratedSceneAsset();
    SceneEntityAsset menuEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
    SceneEntityAsset generatedRoot = Assert.Single(menuEntity.Children, entity => entity.Name == DemoMenuLayout.GeneratedRootEntityName);

    foreach (SceneEntityAsset panelEntity in generatedRoot.Children.Where(child => child.Name.StartsWith("Panel-", StringComparison.Ordinal))) {
        Assert.DoesNotContain(
            panelEntity.Children,
            child => child.Id.Contains("-description", StringComparison.Ordinal) && !child.Id.StartsWith("selected-description-", StringComparison.Ordinal));
    }
}
```

- [ ] **Step 2: Run the new test to verify it fails**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DemoDiscSceneWriterTests.WriteAll_WhenMenuSceneIsGenerated_DoesNotBakeStaticPanelDescriptionEntities" -v minimal
```

Expected:

```text
FAIL
```

The failure should show that one panel still contains a child entity whose id ends with `-description`.

- [ ] **Step 3: Write the failing test for removing the left accent bar while preserving the selected-description entity**

Add one more fact immediately after the previous test:

```csharp
/// <summary>
/// Ensures regenerated demo menu panels no longer author the decorative left accent bar and still keep the selected-description entity.
/// </summary>
[Fact]
public void WriteAll_WhenMenuSceneIsGenerated_RemovesPanelAccentBarAndKeepsSelectedDescriptionEntity() {
    DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

    writer.WriteAll(ProjectRootPath);

    SceneAsset sceneAsset = ReadGeneratedSceneAsset();
    SceneEntityAsset menuEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
    SceneEntityAsset generatedRoot = Assert.Single(menuEntity.Children, entity => entity.Name == DemoMenuLayout.GeneratedRootEntityName);

    foreach (SceneEntityAsset panelEntity in generatedRoot.Children.Where(child => child.Name.StartsWith("Panel-", StringComparison.Ordinal))) {
        Assert.DoesNotContain(panelEntity.Children, child => child.Id.EndsWith("-accent", StringComparison.Ordinal));
        Assert.Contains(panelEntity.Children, child => child.Id.StartsWith("selected-description-", StringComparison.Ordinal));
    }
}
```

- [ ] **Step 4: Run the second new test to verify it fails**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DemoDiscSceneWriterTests.WriteAll_WhenMenuSceneIsGenerated_RemovesPanelAccentBarAndKeepsSelectedDescriptionEntity" -v minimal
```

Expected:

```text
FAIL
```

The failure should show that each panel still contains the `panel-<id>-accent` entity.

- [ ] **Step 5: Commit the failing tests**

```bash
git add engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs
git commit -m "test: lock demo menu panel cleanup"
```

### Task 2: Remove The Static Panel Copy And Accent At The Generator

**Files:**
- Modify: `engine/helengine.editor/managers/menu/DemoMenuSceneAssetFactory.cs`
- Test: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`

- [ ] **Step 1: Remove the authored static description and accent entities from the panel factory**

In `BuildPanelEntityAsset`, replace the current child list setup:

```csharp
children.Add(BuildBackgroundEntityAsset($"panel-{panelDefinition.PanelId}-surface", new float3(88f, 190f, 0f), new int2(DemoMenuLayout.PanelWidth, DemoMenuLayout.PanelHeight), 18f, 3f, definition.SurfaceColor, definition.SurfaceBorderColor, 30));
children.Add(BuildBackgroundEntityAsset($"panel-{panelDefinition.PanelId}-accent", new float3(88f, 190f, 0f), new int2(DemoMenuLayout.PanelWidth, 18), 9f, 0f, definition.AccentColor, definition.AccentColor, 31));
children.Add(BuildTextEntityAsset($"panel-{panelDefinition.PanelId}-heading", new float3(120f, 220f, 0.1f), panelDefinition.Heading, definition.BodyFontPath, definition.TextColor, new int2(420, 36), 41));
children.Add(BuildTextEntityAsset($"panel-{panelDefinition.PanelId}-description", new float3(120f, 258f, 0.1f), panelDefinition.Description, definition.BodyFontPath, definition.MutedTextColor, new int2(430, 52), 41));
children.Add(BuildSelectedDescriptionEntityAsset(panelDefinition.PanelId, new float3(120f, 600f, 0.1f), firstItem.Description, definition.BodyFontPath, definition.MutedTextColor));
```

with:

```csharp
children.Add(BuildBackgroundEntityAsset($"panel-{panelDefinition.PanelId}-surface", new float3(88f, 190f, 0f), new int2(DemoMenuLayout.PanelWidth, DemoMenuLayout.PanelHeight), 18f, 3f, definition.SurfaceColor, definition.SurfaceBorderColor, 30));
children.Add(BuildBackgroundEntityAsset($"panel-{panelDefinition.PanelId}-top-band", new float3(88f, 190f, 0f), new int2(DemoMenuLayout.PanelWidth, 18), 9f, 0f, definition.AccentColor, definition.AccentColor, 31));
children.Add(BuildTextEntityAsset($"panel-{panelDefinition.PanelId}-heading", new float3(120f, 220f, 0.1f), panelDefinition.Heading, definition.BodyFontPath, definition.TextColor, new int2(420, 36), 41));
children.Add(BuildSelectedDescriptionEntityAsset(panelDefinition.PanelId, new float3(120f, 600f, 0.1f), firstItem.Description, definition.BodyFontPath, definition.MutedTextColor));
```

This preserves the existing top band and removes only the dedicated panel accent child and the static subtitle entity.

- [ ] **Step 2: Reclaim the recovered vertical space for menu items**

In `BuildItemEntityAsset`, shift the item rows upward so the list uses the removed subtitle space:

```csharp
LocalPosition = new float3(120f, 280f + (visibleIndex * (DemoMenuLayout.ButtonHeight + DemoMenuLayout.ButtonSpacing)), 0f),
```

This replaces the current `320f` Y origin. Keep the X origin at `120f` so the conservative layout change only removes the dead subtitle gap in this pass.

- [ ] **Step 3: Run the targeted scene-writer tests to verify the generator fix**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DemoDiscSceneWriterTests.WriteAll_WhenMenuSceneIsGenerated_DoesNotBakeStaticPanelDescriptionEntities|FullyQualifiedName~DemoDiscSceneWriterTests.WriteAll_WhenMenuSceneIsGenerated_RemovesPanelAccentBarAndKeepsSelectedDescriptionEntity|FullyQualifiedName~DemoDiscSceneWriterTests.WriteAll_WhenMenuSceneIsGenerated_BakesTheMenuHierarchyIntoTheScene" -v minimal
```

Expected:

```text
PASS
```

- [ ] **Step 4: Commit the generator cleanup**

```bash
git add engine/helengine.editor/managers/menu/DemoMenuSceneAssetFactory.cs engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs
git commit -m "feat: simplify demo menu panel generation"
```

### Task 3: Verify Regeneration Still Produces The Intended Scene

**Files:**
- Modify: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`
- Inspect: `engine/helengine.editor/managers/menu/EditorMenuSceneRegenerationService.cs`

- [ ] **Step 1: Add one test that confirms selected-description entities still survive alongside the cleaned panel body**

Add a focused assertion over the generated scene:

```csharp
/// <summary>
/// Ensures regenerated demo menu panels still keep the dynamic selected-description text target after static cleanup.
/// </summary>
[Fact]
public void WriteAll_WhenMenuSceneIsGenerated_KeepsSelectedDescriptionEntityPerPanel() {
    DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

    writer.WriteAll(ProjectRootPath);

    SceneAsset sceneAsset = ReadGeneratedSceneAsset();
    SceneEntityAsset menuEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
    SceneEntityAsset generatedRoot = Assert.Single(menuEntity.Children, entity => entity.Name == DemoMenuLayout.GeneratedRootEntityName);

    foreach (SceneEntityAsset panelEntity in generatedRoot.Children.Where(child => child.Name.StartsWith("Panel-", StringComparison.Ordinal))) {
        Assert.Single(panelEntity.Children.Where(child => child.Id.StartsWith("selected-description-", StringComparison.Ordinal)));
    }
}
```

- [ ] **Step 2: Run the focused regression to verify it passes**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DemoDiscSceneWriterTests.WriteAll_WhenMenuSceneIsGenerated_KeepsSelectedDescriptionEntityPerPanel" -v minimal
```

Expected:

```text
PASS
```

- [ ] **Step 3: Run the whole demo scene writer suite**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DemoDiscSceneWriterTests" -v minimal
```

Expected:

```text
PASS
```

- [ ] **Step 4: Regenerate the real demo menu scene from the editor command path**

Run:

```bash
rtk dotnet run --project helengine.ui/helengine.editor.app/helengine.editor.app.csproj -- --project C:/dev/helprojs/city/project.heproj --editor-command menu.regenerate-demo-disc-main-menu
```

Expected:

```text
Editor command 'menu.regenerate-demo-disc-main-menu' executed successfully.
```

- [ ] **Step 5: Commit the final verification-backed scene regeneration changes**

```bash
git add engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs C:/dev/helprojs/city/assets/Scenes/DemoDiscMainMenu.helen
git commit -m "test: verify cleaned demo menu regeneration"
```
