# Properties Platform Override Indicators Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add override borders and `Revert` actions to transform rows, component property rows, and component section headers so platform-authored state is visible and can be reverted back to inherited `common`.

**Architecture:** Extend the existing transform and component platform editing services with override detection and revert operations, then wire a shared inspector chrome pattern into `PropertiesPanel` and `ComponentPropertiesView`. Keep override storage sparse and editor-authored; revert must remove override metadata instead of copying common values into platform payloads.

**Tech Stack:** C#, xUnit, existing Helengine editor UI components (`EditorEntity`, `SpriteComponent`, `ButtonComponent`, `TextBoxComponent`), existing platform override services.

---

### Task 1: Add Transform Override Detection And Revert Support

**Files:**
- Modify: `engine/helengine.editor/managers/scene/EntityPlatformTransformEditingService.cs`
- Test: `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`

- [ ] **Step 1: Write the failing transform revert test**

Add this test to `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`:

```csharp
/// <summary>
/// Ensures reverting one platform-authored transform row removes the override and restores inherited common values.
/// </summary>
[Fact]
public void ShowEntityProperties_WhenPs2PositionOverrideIsReverted_PositionReturnsToCommonAndFollowsLaterCommonEdits() {
    PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
    EditorEntity entity = new EditorEntity {
        Name = "PlatformEntity",
        Position = new float3(1f, 2f, 3f),
        Scale = float3.One,
        Orientation = float4.Identity
    };

    panel.ShowEntityProperties(entity, new[] { "ps2" });
    SelectInspectorPlatform(panel, "ps2");

    TextBoxComponent[] positionFields = GetPrivateField<TextBoxComponent[]>(panel, "PositionFields");
    positionFields[0].Text = "10";
    positionFields[1].Text = "20";
    positionFields[2].Text = "30";
    SetPrivateField(panel, "ApplyTransformRequested", true);
    InvokePrivate(panel, "UpdateTransformEdits");

    InvokePrivate(panel, "HandleRevertPositionOverrideRequested");
    Assert.Equal(new float3(1f, 2f, 3f), entity.Position);

    SelectInspectorPlatform(panel, "common");
    entity.Position = new float3(7f, 8f, 9f);

    SelectInspectorPlatform(panel, "ps2");
    Assert.Equal(new float3(7f, 8f, 9f), entity.Position);
}
```

- [ ] **Step 2: Run the focused transform revert test and verify it fails**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~PropertiesPanelMutationTests.ShowEntityProperties_WhenPs2PositionOverrideIsReverted_PositionReturnsToCommonAndFollowsLaterCommonEdits -v minimal
```

Expected: FAIL because `HandleRevertPositionOverrideRequested` and transform-row override clearing do not exist yet.

- [ ] **Step 3: Add minimal transform override detection and clear operations**

Update `engine/helengine.editor/managers/scene/EntityPlatformTransformEditingService.cs` to add row-level detection and clearing methods:

```csharp
/// <summary>
/// Returns whether the active platform currently overrides the entity position row.
/// </summary>
/// <param name="saveComponent">Hidden save component that stores transform override metadata.</param>
/// <returns>True when the active platform contains a position override.</returns>
public bool IsPositionOverrideActive(EntitySaveComponent saveComponent) {
    if (saveComponent == null) {
        throw new ArgumentNullException(nameof(saveComponent));
    }

    string activePlatformId = saveComponent.ActiveTransformPlatformId;
    if (string.IsNullOrWhiteSpace(activePlatformId) ||
        string.Equals(activePlatformId, CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
        return false;
    }

    return saveComponent.TryGetTransformPlatformOverride(activePlatformId, out SceneEntityPlatformTransformOverrideAsset overrideState)
        && overrideState.HasLocalPositionOverride;
}

/// <summary>
/// Clears the active platform position override and reapplies the inherited common value to the live entity.
/// </summary>
/// <param name="entity">Entity whose live position should be restored.</param>
/// <param name="saveComponent">Hidden save component that stores transform override metadata.</param>
public void ClearPositionOverride(Entity entity, EntitySaveComponent saveComponent) {
    if (entity == null) {
        throw new ArgumentNullException(nameof(entity));
    } else if (saveComponent == null) {
        throw new ArgumentNullException(nameof(saveComponent));
    }

    string activePlatformId = saveComponent.ActiveTransformPlatformId;
    if (string.IsNullOrWhiteSpace(activePlatformId) ||
        string.Equals(activePlatformId, CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
        return;
    }

    if (!saveComponent.TryGetTransformPlatformOverride(activePlatformId, out SceneEntityPlatformTransformOverrideAsset overrideState)) {
        return;
    }

    overrideState.HasLocalPositionOverride = false;
    overrideState.LocalPosition = saveComponent.CommonLocalPositionSnapshot;
    entity.LocalPosition = saveComponent.CommonLocalPositionSnapshot;
}
```

Repeat the same pattern in the same file for:

- `IsRotationOverrideActive(...)`
- `IsScaleOverrideActive(...)`
- `ClearRotationOverride(...)`
- `ClearScaleOverride(...)`

Use:

- `HasLocalOrientationOverride` / `CommonLocalOrientationSnapshot` / `entity.LocalOrientation`
- `HasLocalScaleOverride` / `CommonLocalScaleSnapshot` / `entity.LocalScale`

- [ ] **Step 4: Run the focused transform revert test and verify it still fails for missing panel wiring**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~PropertiesPanelMutationTests.ShowEntityProperties_WhenPs2PositionOverrideIsReverted_PositionReturnsToCommonAndFollowsLaterCommonEdits -v minimal
```

Expected: FAIL because the panel button handler is still missing.

- [ ] **Step 5: Commit the service-only transform override detection slice**

```bash
git add engine/helengine.editor/managers/scene/EntityPlatformTransformEditingService.cs engine/helengine.editor.tests/PropertiesPanelMutationTests.cs
git commit -m "Add transform override detection helpers"
```

### Task 2: Add Transform Row Override Chrome And Revert Buttons

**Files:**
- Modify: `engine/helengine.editor/components/ui/PropertiesPanel.cs`
- Modify: `engine/helengine.editor/components/ui/PropertiesPanelUpdater.cs`
- Test: `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`

- [ ] **Step 1: Write the failing transform override indicator test**

Add this test to `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`:

```csharp
/// <summary>
/// Ensures active platform-authored transform rows display override chrome only on non-common tabs.
/// </summary>
[Fact]
public void ShowEntityProperties_WhenPs2PositionOverrideExists_PositionRowShowsOverrideChromeOnlyOnPs2() {
    PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
    EditorEntity entity = new EditorEntity {
        Name = "PlatformEntity",
        Position = new float3(1f, 2f, 3f),
        Scale = float3.One,
        Orientation = float4.Identity
    };

    panel.ShowEntityProperties(entity, new[] { "ps2" });
    SelectInspectorPlatform(panel, "ps2");

    TextBoxComponent[] positionFields = GetPrivateField<TextBoxComponent[]>(panel, "PositionFields");
    positionFields[0].Text = "10";
    positionFields[1].Text = "20";
    positionFields[2].Text = "30";
    SetPrivateField(panel, "ApplyTransformRequested", true);
    InvokePrivate(panel, "UpdateTransformEdits");

    SpriteComponent positionOverrideBorder = GetPrivateField<SpriteComponent>(panel, "PositionRowOverrideBorder");
    ButtonComponent positionRevertButton = GetPrivateField<ButtonComponent>(panel, "PositionRowRevertButton");

    Assert.True(positionOverrideBorder.Enabled);
    Assert.True(positionRevertButton.Enabled);

    SelectInspectorPlatform(panel, "common");
    Assert.False(positionOverrideBorder.Enabled);
    Assert.False(positionRevertButton.Enabled);
}
```

- [ ] **Step 2: Run the focused transform chrome test and verify it fails**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~PropertiesPanelMutationTests.ShowEntityProperties_WhenPs2PositionOverrideExists_PositionRowShowsOverrideChromeOnlyOnPs2 -v minimal
```

Expected: FAIL because the row border/button fields and refresh logic do not exist yet.

- [ ] **Step 3: Add minimal transform row override chrome and revert handlers**

In `engine/helengine.editor/components/ui/PropertiesPanel.cs`:

1. Add new fields:

```csharp
readonly SpriteComponent PositionRowOverrideBorder;
readonly SpriteComponent RotationRowOverrideBorder;
readonly SpriteComponent ScaleRowOverrideBorder;
readonly ButtonComponent PositionRowRevertButton;
readonly ButtonComponent RotationRowRevertButton;
readonly ButtonComponent ScaleRowRevertButton;
readonly EditorEntity PositionRowRevertHost;
readonly EditorEntity RotationRowRevertHost;
readonly EditorEntity ScaleRowRevertHost;
```

2. Create the chrome during constructor initialization after transform-row creation:

```csharp
PositionRowOverrideBorder = CreateTransformOverrideBorder(PositionRow);
RotationRowOverrideBorder = CreateTransformOverrideBorder(RotationRow);
ScaleRowOverrideBorder = CreateTransformOverrideBorder(ScaleRow);

PositionRowRevertHost = CreateTransformRevertButtonHost(PositionRow, out PositionRowRevertButton, HandleRevertPositionOverrideRequested);
RotationRowRevertHost = CreateTransformRevertButtonHost(RotationRow, out RotationRowRevertButton, HandleRevertRotationOverrideRequested);
ScaleRowRevertHost = CreateTransformRevertButtonHost(ScaleRow, out ScaleRowRevertButton, HandleRevertScaleOverrideRequested);
```

3. Add methods:

```csharp
void HandleRevertPositionOverrideRequested() {
    if (SelectedEntity == null) {
        return;
    }

    EntitySaveComponent saveComponent = FindEntitySaveComponent(SelectedEntity);
    if (saveComponent == null) {
        return;
    }

    TransformPlatformEditingService.ClearPositionOverride(SelectedEntity, saveComponent);
    SyncTransformFields(SelectedEntity);
    RefreshTransformOverrideVisualState();
    EditorSceneMutationService.MarkSceneMutated();
}
```

Add the same pattern for rotation and scale.

4. Add a `RefreshTransformOverrideVisualState()` method that enables/disables border and button visibility from `TransformPlatformEditingService`.

5. Call `RefreshTransformOverrideVisualState()` from:

- `ShowEntityProperties(...)`
- `HandleComponentPlatformTabChanged(...)`
- `UpdateTransformEdits()` after `PersistSelectedEntityTransformPlatform()`

6. Update row layout so the revert button sits at the far right and the border uses an existing theme accent:

```csharp
overrideBorder.BorderColor = ThemeManager.Colors.AccentTertiary;
overrideBorder.BorderThickness = 1f;
```

- [ ] **Step 4: Run the two focused transform tests and verify they pass**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~PropertiesPanelMutationTests.ShowEntityProperties_WhenPs2PositionOverrideIsReverted_PositionReturnsToCommonAndFollowsLaterCommonEdits -v minimal
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~PropertiesPanelMutationTests.ShowEntityProperties_WhenPs2PositionOverrideExists_PositionRowShowsOverrideChromeOnlyOnPs2 -v minimal
```

Expected: PASS

- [ ] **Step 5: Commit the transform row override UI slice**

```bash
git add engine/helengine.editor/components/ui/PropertiesPanel.cs engine/helengine.editor/components/ui/PropertiesPanelUpdater.cs engine/helengine.editor.tests/PropertiesPanelMutationTests.cs
git commit -m "Show and revert transform platform overrides"
```

### Task 3: Add Component Property Override Indicators And Revert

**Files:**
- Modify: `engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs`
- Modify: `engine/helengine.editor/components/ui/ComponentPropertyRow.cs`
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Test: `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`

- [ ] **Step 1: Write the failing component property revert test**

Add this test to `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`:

```csharp
/// <summary>
/// Ensures reverting one platform-authored component property removes the override and resumes inheritance from common.
/// </summary>
[Fact]
public void ShowEntityProperties_WhenWindowsComponentPropertyOverrideIsReverted_ValueReturnsToCommonAndFollowsLaterCommonEdits() {
    PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
    EditorEntity entity = new EditorEntity {
        Name = "Camera"
    };
    CameraComponent camera = new CameraComponent {
        FarPlaneDistance = 100f
    };
    entity.AddComponent(camera);

    panel.ShowEntityProperties(entity, new[] { "windows" });
    SelectInspectorPlatform(panel, "windows");

    ComponentPropertiesView view = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
    ComponentPropertyRow farPlaneRow = GetSingleRow(view, "Far Plane Distance");
    farPlaneRow.ScalarField.Text = "200";
    typeof(ComponentPropertiesView).GetMethod("HandleScalarSubmitted", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, new object[] { farPlaneRow.ScalarField });

    typeof(ComponentPropertiesView).GetMethod("HandleRevertPropertyOverridePressed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, new object[] { farPlaneRow.ActionButton });
    Assert.Equal("100", farPlaneRow.ScalarField.Text);

    SelectInspectorPlatform(panel, "common");
    camera.FarPlaneDistance = 300f;
    panel.ShowEntityProperties(entity, new[] { "windows" });
    SelectInspectorPlatform(panel, "windows");

    view = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
    farPlaneRow = GetSingleRow(view, "Far Plane Distance");
    Assert.Equal("300", farPlaneRow.ScalarField.Text);
}
```

- [ ] **Step 2: Run the focused component property revert test and verify it fails**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~PropertiesPanelMutationTests.ShowEntityProperties_WhenWindowsComponentPropertyOverrideIsReverted_ValueReturnsToCommonAndFollowsLaterCommonEdits -v minimal
```

Expected: FAIL because property-level override detection/revert does not exist.

- [ ] **Step 3: Add minimal component property override queries and clearing**

In `engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs`, add methods:

```csharp
/// <summary>
/// Returns whether one editable component property is overridden for the requested platform.
/// </summary>
public bool IsPropertyOverrideActive(Component commonComponent, EntitySaveComponent saveComponent, string platformId, PropertyInfo property) {
    if (commonComponent == null) {
        throw new ArgumentNullException(nameof(commonComponent));
    } else if (saveComponent == null) {
        throw new ArgumentNullException(nameof(saveComponent));
    } else if (string.IsNullOrWhiteSpace(platformId)) {
        throw new ArgumentException("Platform id must be provided.", nameof(platformId));
    } else if (property == null) {
        throw new ArgumentNullException(nameof(property));
    }

    if (string.Equals(platformId, CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
        return false;
    }

    Component effectiveComponent = ResolveEditableComponent(commonComponent, saveComponent, platformId);
    object commonValue = property.GetValue(commonComponent);
    object platformValue = property.GetValue(effectiveComponent);
    return !Equals(commonValue, platformValue);
}
```

Also add:

```csharp
/// <summary>
/// Reverts one property override by rebuilding the editable platform component from common without the targeted property delta.
/// </summary>
public void ClearPropertyOverride(Component commonComponent, EntitySaveComponent saveComponent, string platformId, PropertyInfo property) {
    if (commonComponent == null) {
        throw new ArgumentNullException(nameof(commonComponent));
    } else if (saveComponent == null) {
        throw new ArgumentNullException(nameof(saveComponent));
    } else if (string.IsNullOrWhiteSpace(platformId)) {
        throw new ArgumentException("Platform id must be provided.", nameof(platformId));
    } else if (property == null) {
        throw new ArgumentNullException(nameof(property));
    }

    if (string.Equals(platformId, CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
        return;
    }

    Component editableComponent = EnsurePlatformOverrideComponent(commonComponent, saveComponent, platformId);
    property.SetValue(editableComponent, property.GetValue(commonComponent));
    PersistPlatformOverride(commonComponent, editableComponent, saveComponent, platformId);
}
```

In `engine/helengine.editor/components/ui/ComponentPropertyRow.cs`, add row UI state:

```csharp
public SpriteComponent OverrideBorder { get; set; }
public EditorEntity RevertButtonHost { get; set; }
```

In `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`:

- initialize per-row override border and revert button when rows are acquired
- add `RefreshRowOverrideState(ComponentPropertyRow row)` to enable border/button only when the active platform is not `common` and the row property is overridden
- add `HandleRevertPropertyOverridePressed(ButtonComponent button)` to clear the property override, rebuild the component view, and relayout

- [ ] **Step 4: Run the focused component property revert test and verify it passes**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~PropertiesPanelMutationTests.ShowEntityProperties_WhenWindowsComponentPropertyOverrideIsReverted_ValueReturnsToCommonAndFollowsLaterCommonEdits -v minimal
```

Expected: PASS

- [ ] **Step 5: Commit the component property override slice**

```bash
git add engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs engine/helengine.editor/components/ui/ComponentPropertyRow.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor.tests/PropertiesPanelMutationTests.cs
git commit -m "Show and revert component property overrides"
```

### Task 4: Add Component Existence Override Header Indicators And Revert

**Files:**
- Modify: `engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs`
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Test: `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`

- [ ] **Step 1: Write the failing component existence revert test**

Add this test to `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`:

```csharp
/// <summary>
/// Ensures a platform-only component shows header override chrome and reverting it removes the component from that platform.
/// </summary>
[Fact]
public void ShowEntityProperties_WhenPlatformOnlyComponentIsReverted_ComponentSectionDisappearsOnThatPlatform() {
    PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
    EditorEntity entity = new EditorEntity {
        Name = "PlatformEntity"
    };

    panel.ShowEntityProperties(entity, new[] { "ps2" });
    SelectInspectorPlatform(panel, "ps2");

    ComponentPropertiesView view = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
    typeof(PropertiesPanel).GetMethod("HandleAddComponentSelected", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, new object[] { typeof(CameraComponent).FullName });

    view = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
    ComponentSectionView cameraSection = GetSingleSection(view, "Camera Component");
    Assert.True(cameraSection.HeaderBackground.BorderThickness > 0f);

    typeof(ComponentPropertiesView).GetMethod("HandleRevertComponentExistenceOverridePressed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, new object[] { cameraSection.RemoveButton });

    view = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
    Assert.DoesNotContain(GetSections(view), section => string.Equals(section.Title.Text, "Camera Component", StringComparison.Ordinal));
}
```

- [ ] **Step 2: Run the focused component existence revert test and verify it fails**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~PropertiesPanelMutationTests.ShowEntityProperties_WhenPlatformOnlyComponentIsReverted_ComponentSectionDisappearsOnThatPlatform -v minimal
```

Expected: FAIL because section-level existence override detection and revert handling do not exist.

- [ ] **Step 3: Add minimal existence override detection and header revert behavior**

In `engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs`, add:

```csharp
/// <summary>
/// Returns whether one component section existence differs from common on the supplied platform.
/// </summary>
public bool IsComponentExistenceOverrideActive(Component commonComponent, EntitySaveComponent saveComponent, string platformId) {
    if (commonComponent == null) {
        throw new ArgumentNullException(nameof(commonComponent));
    } else if (saveComponent == null) {
        throw new ArgumentNullException(nameof(saveComponent));
    } else if (string.IsNullOrWhiteSpace(platformId)) {
        throw new ArgumentException("Platform id must be provided.", nameof(platformId));
    }

    return !string.Equals(platformId, CommonPlatformId, StringComparison.OrdinalIgnoreCase)
        && saveComponent.TryGetComponentState(commonComponent, out EntityComponentSaveState saveState)
        && saveState.HasPlatformOverride(platformId);
}
```

Also add:

```csharp
/// <summary>
/// Clears one component existence override for the supplied platform.
/// </summary>
public void ClearComponentExistenceOverride(Component commonComponent, EntitySaveComponent saveComponent, string platformId) {
    if (commonComponent == null) {
        throw new ArgumentNullException(nameof(commonComponent));
    } else if (saveComponent == null) {
        throw new ArgumentNullException(nameof(saveComponent));
    } else if (string.IsNullOrWhiteSpace(platformId)) {
        throw new ArgumentException("Platform id must be provided.", nameof(platformId));
    }

    if (string.Equals(platformId, CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
        return;
    }

    if (saveComponent.TryGetComponentState(commonComponent, out EntityComponentSaveState saveState)) {
        saveState.RemovePlatformOverride(platformId);
    }
}
```

In `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`:

- add section-header override border/button refresh logic
- add `HandleRevertComponentExistenceOverridePressed(ButtonComponent button)`
- after revert:
  - rebuild visible sections from current entity/platform
  - relayout
  - mark scene mutated

- [ ] **Step 4: Run the focused component existence revert test and verify it passes**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~PropertiesPanelMutationTests.ShowEntityProperties_WhenPlatformOnlyComponentIsReverted_ComponentSectionDisappearsOnThatPlatform -v minimal
```

Expected: PASS

- [ ] **Step 5: Commit the component existence override slice**

```bash
git add engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor.tests/PropertiesPanelMutationTests.cs
git commit -m "Show and revert component existence overrides"
```

### Task 5: Run Focused Regression Verification

**Files:**
- Modify: `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`
- Modify: `engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs`

- [ ] **Step 1: Add a common-tab no-chrome regression**

Add this test to `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`:

```csharp
/// <summary>
/// Ensures common platform editing never shows override chrome or revert actions.
/// </summary>
[Fact]
public void ShowEntityProperties_WhenCommonTabIsActive_NoOverrideChromeIsVisible() {
    PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
    EditorEntity entity = new EditorEntity {
        Name = "PlatformEntity",
        Position = new float3(1f, 2f, 3f)
    };

    panel.ShowEntityProperties(entity, new[] { "ps2" });

    SpriteComponent positionOverrideBorder = GetPrivateField<SpriteComponent>(panel, "PositionRowOverrideBorder");
    ButtonComponent positionRevertButton = GetPrivateField<ButtonComponent>(panel, "PositionRowRevertButton");

    Assert.False(positionOverrideBorder.Enabled);
    Assert.False(positionRevertButton.Enabled);
}
```

- [ ] **Step 2: Run the focused properties test bundle**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~PropertiesPanelMutationTests -v minimal
```

Expected: PASS

- [ ] **Step 3: Run the shell/layout smoke test bundle**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~PropertiesPanelComponentShellTests -v minimal
```

Expected: PASS

- [ ] **Step 4: Run the final editor test-project build**

Run:

```powershell
rtk dotnet build engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore
```

Expected: `0 errors`

- [ ] **Step 5: Commit the final verification sweep**

```bash
git add engine/helengine.editor.tests/PropertiesPanelMutationTests.cs engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs
git commit -m "Verify properties platform override indicators"
```
