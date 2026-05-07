# Camera Clear Settings Editor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the dynamic component inspector with a type-matched custom property editor path and implement a collapsible nested `CameraClearSettings` editor for `CameraComponent.ClearSettings`.

**Architecture:** Keep the reflected inspector as the default path, but add an editor-side provider layer that can claim complex property types before primitive row mapping. `CameraClearSettings` becomes the first provider-backed nested editor, rendered inside the Camera section as a collapsible property subsection with explicit struct rebuild/write-back on every committed edit.

**Tech Stack:** C#, xUnit, existing `ComponentPropertiesView` row system, reflection metadata, RTK for verification commands.

---

### Task 1: Add Failing Custom-Editor and Clear Settings Rendering Tests

**Files:**
- Modify: `engine/helengine.editor.tests/ComponentPropertiesViewDynamicInspectorTests.cs`
- Modify: `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`
- Reference: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Reference: `engine/helengine.core/model/CameraClearSettings.cs`

- [ ] **Step 1: Add failing provider-backed Camera clear-settings rendering tests**

```csharp
[Fact]
public void ShowComponents_WhenInspectingCamera_IncludesClearSettingsNestedSection() {
    CameraComponent camera = new CameraComponent();
    EditorEntity entity = new EditorEntity();
    entity.AddComponent(camera);

    ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
    view.ShowComponents(entity);

    List<ComponentPropertyRow> rows = GetActiveRows(view);

    Assert.Contains(rows, row => string.Equals(row.Label.Text, "Clear Settings", StringComparison.Ordinal));
}

[Fact]
public void ShowComponents_WhenClearSettingsSectionIsExpanded_RendersExpectedNestedControls() {
    CameraComponent camera = new CameraComponent();
    EditorEntity entity = new EditorEntity();
    entity.AddComponent(camera);

    ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
    view.ShowComponents(entity);
    view.UpdateLayout(0, 0, 420);

    ComponentPropertyRow clearSettingsRow = GetSingleRow(view, "Clear Settings");
    InvokeNestedSectionToggle(view, clearSettingsRow);

    List<ComponentPropertyRow> rows = GetActiveRows(view);
    Assert.Contains(rows, row => string.Equals(row.Label.Text, "Clear Color Enabled", StringComparison.Ordinal));
    Assert.Contains(rows, row => string.Equals(row.Label.Text, "Clear Color", StringComparison.Ordinal));
    Assert.Contains(rows, row => string.Equals(row.Label.Text, "Clear Depth Enabled", StringComparison.Ordinal));
    Assert.Contains(rows, row => string.Equals(row.Label.Text, "Clear Depth", StringComparison.Ordinal));
    Assert.Contains(rows, row => string.Equals(row.Label.Text, "Clear Stencil Enabled", StringComparison.Ordinal));
    Assert.Contains(rows, row => string.Equals(row.Label.Text, "Clear Stencil", StringComparison.Ordinal));
}

[Fact]
public void ShowComponents_WhenClearSettingsSectionIsCollapsed_HidesNestedControls() {
    CameraComponent camera = new CameraComponent();
    EditorEntity entity = new EditorEntity();
    entity.AddComponent(camera);

    ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
    view.ShowComponents(entity);
    view.UpdateLayout(0, 0, 420);

    ComponentPropertyRow clearSettingsRow = GetSingleRow(view, "Clear Settings");
    InvokeNestedSectionToggle(view, clearSettingsRow);
    InvokeNestedSectionToggle(view, clearSettingsRow);

    List<ComponentPropertyRow> rows = GetActiveRows(view);
    Assert.DoesNotContain(rows, row => string.Equals(row.Label.Text, "Clear Color Enabled", StringComparison.Ordinal));
    Assert.DoesNotContain(rows, row => string.Equals(row.Label.Text, "Clear Color", StringComparison.Ordinal));
    Assert.DoesNotContain(rows, row => string.Equals(row.Label.Text, "Clear Depth Enabled", StringComparison.Ordinal));
    Assert.DoesNotContain(rows, row => string.Equals(row.Label.Text, "Clear Depth", StringComparison.Ordinal));
    Assert.DoesNotContain(rows, row => string.Equals(row.Label.Text, "Clear Stencil Enabled", StringComparison.Ordinal));
    Assert.DoesNotContain(rows, row => string.Equals(row.Label.Text, "Clear Stencil", StringComparison.Ordinal));
}
```

- [ ] **Step 2: Add failing Camera clear-settings write-back mutation tests**

```csharp
[Fact]
public void ShowEntityProperties_WhenClearDepthIsSubmitted_UpdatesCameraClearSettings() {
    PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
    EditorEntity entity = new EditorEntity();
    CameraComponent camera = new CameraComponent();
    camera.ClearSettings = new CameraClearSettings(true, new float4(0f, 0f, 0f, 1f), true, 1f, false, 0);
    entity.AddComponent(camera);

    panel.ShowEntityProperties(entity);

    ComponentPropertiesView view = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
    ComponentPropertyRow clearSettingsRow = GetSingleRow(view, "Clear Settings");
    InvokeNestedSectionToggle(view, clearSettingsRow);

    ComponentPropertyRow clearDepthRow = GetSingleRow(view, "Clear Depth");
    clearDepthRow.ScalarField.Text = "0.25";

    MethodInfo submitMethod = typeof(ComponentPropertiesView).GetMethod("HandleScalarSubmitted", BindingFlags.Instance | BindingFlags.NonPublic);
    submitMethod.Invoke(view, new object[] { clearDepthRow.ScalarField });

    Assert.Equal(0.25f, camera.ClearSettings.ClearDepth, 3);
}

[Fact]
public void ShowEntityProperties_WhenClearColorEnabledChanges_UpdatesCameraClearSettings() {
    PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
    EditorEntity entity = new EditorEntity();
    CameraComponent camera = new CameraComponent();
    camera.ClearSettings = new CameraClearSettings(false, new float4(0f, 0f, 0f, 1f), true, 1f, false, 0);
    entity.AddComponent(camera);

    panel.ShowEntityProperties(entity);

    ComponentPropertiesView view = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
    ComponentPropertyRow clearSettingsRow = GetSingleRow(view, "Clear Settings");
    InvokeNestedSectionToggle(view, clearSettingsRow);

    ComponentPropertyRow clearColorEnabledRow = GetSingleRow(view, "Clear Color Enabled");

    MethodInfo checkedChangedMethod = typeof(ComponentPropertiesView).GetMethod("HandleBooleanCheckedChanged", BindingFlags.Instance | BindingFlags.NonPublic);
    checkedChangedMethod.Invoke(view, new object[] { clearColorEnabledRow.CheckBoxField, true });

    Assert.True(camera.ClearSettings.ClearColorEnabled);
}
```

- [ ] **Step 3: Run the new Camera clear-settings slice to verify it fails**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ComponentPropertiesViewDynamicInspectorTests|FullyQualifiedName~ShowEntityProperties_WhenClearDepthIsSubmitted_UpdatesCameraClearSettings|FullyQualifiedName~ShowEntityProperties_WhenClearColorEnabledChanges_UpdatesCameraClearSettings"
```

Expected: FAIL because no custom property editor provider exists yet, `Clear Settings` is still excluded from the Camera inspector, and no nested write-back path is present.

- [ ] **Step 4: Commit the failing tests**

```bash
rtk git add engine/helengine.editor.tests/ComponentPropertiesViewDynamicInspectorTests.cs engine/helengine.editor.tests/PropertiesPanelMutationTests.cs
rtk git commit -m "test: cover camera clear settings editor"
```

### Task 2: Add Custom Property Editor Provider Contracts and Descriptor Shapes

**Files:**
- Create: `engine/helengine.editor/components/ui/IComponentPropertyEditorProvider.cs`
- Create: `engine/helengine.editor/components/ui/ComponentPropertyEditorDescriptor.cs`
- Modify: `engine/helengine.editor/components/ui/ReflectedComponentPropertyDescriptor.cs`
- Modify: `engine/helengine.editor/components/ui/ReflectedComponentPropertyDescriptorBuilder.cs`
- Test: `engine/helengine.editor.tests/ComponentPropertiesViewDynamicInspectorTests.cs`

- [ ] **Step 1: Add the custom property editor provider interface**

```csharp
using System.Reflection;

namespace helengine.editor {
    /// <summary>
    /// Provides custom editor descriptors for complex reflected component properties.
    /// </summary>
    public interface IComponentPropertyEditorProvider {
        /// <summary>
        /// Tries to create a custom editor descriptor for one reflected property.
        /// </summary>
        /// <param name="property">Reflected property metadata.</param>
        /// <param name="descriptor">Resolved custom editor descriptor when supported.</param>
        /// <returns>True when the property is handled by this provider.</returns>
        bool TryCreateDescriptor(PropertyInfo property, out ComponentPropertyEditorDescriptor descriptor);
    }
}
```

- [ ] **Step 2: Add the custom editor descriptor type**

```csharp
using System.Reflection;

namespace helengine.editor {
    /// <summary>
    /// Describes one provider-backed custom property editor.
    /// </summary>
    public class ComponentPropertyEditorDescriptor {
        /// <summary>
        /// Initializes a new custom property editor descriptor.
        /// </summary>
        /// <param name="property">Reflected property metadata.</param>
        /// <param name="displayName">Display label shown in the inspector.</param>
        /// <param name="editorTypeId">Stable editor type identifier.</param>
        /// <param name="order">Display order used during sorting.</param>
        public ComponentPropertyEditorDescriptor(PropertyInfo property, string displayName, string editorTypeId, int order) {
            Property = property ?? throw new ArgumentNullException(nameof(property));
            if (string.IsNullOrWhiteSpace(displayName)) {
                throw new ArgumentException("Display name must be provided.", nameof(displayName));
            }
            if (string.IsNullOrWhiteSpace(editorTypeId)) {
                throw new ArgumentException("Editor type id must be provided.", nameof(editorTypeId));
            }

            DisplayName = displayName;
            EditorTypeId = editorTypeId;
            Order = order;
        }

        /// <summary>
        /// Gets the reflected property metadata.
        /// </summary>
        public PropertyInfo Property { get; }

        /// <summary>
        /// Gets the display label shown in the inspector.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the stable editor type identifier.
        /// </summary>
        public string EditorTypeId { get; }

        /// <summary>
        /// Gets the display order used during sorting.
        /// </summary>
        public int Order { get; }
    }
}
```

- [ ] **Step 3: Extend reflected descriptors to carry either default row kinds or custom editor descriptors**

```csharp
public ReflectedComponentPropertyDescriptor(
    PropertyInfo property,
    string displayName,
    ComponentPropertyRowKind rowKind,
    int order)
```

```csharp
public ReflectedComponentPropertyDescriptor(
    PropertyInfo property,
    string displayName,
    ComponentPropertyEditorDescriptor customEditor,
    int order)
```

```csharp
public bool IsCustomEditor => CustomEditor != null;
public ComponentPropertyEditorDescriptor CustomEditor { get; }
```

- [ ] **Step 4: Update the descriptor builder to consult providers before primitive row mapping**

```csharp
readonly List<IComponentPropertyEditorProvider> Providers;

public ReflectedComponentPropertyDescriptorBuilder() {
    Providers = new List<IComponentPropertyEditorProvider> {
        new CameraClearSettingsPropertyEditorProvider()
    };
}
```

```csharp
if (TryBuildCustomEditorDescriptor(property, out ReflectedComponentPropertyDescriptor customDescriptor)) {
    descriptors.Add(customDescriptor);
    continue;
}
```

- [ ] **Step 5: Run the provider-resolution tests**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ComponentPropertiesViewDynamicInspectorTests"
```

Expected: still FAIL overall, but `Clear Settings` should now be present as a provider-backed descriptor while nested rendering/write-back remains unimplemented.

- [ ] **Step 6: Commit the provider contract layer**

```bash
rtk git add engine/helengine.editor/components/ui/IComponentPropertyEditorProvider.cs engine/helengine.editor/components/ui/ComponentPropertyEditorDescriptor.cs engine/helengine.editor/components/ui/ReflectedComponentPropertyDescriptor.cs engine/helengine.editor/components/ui/ReflectedComponentPropertyDescriptorBuilder.cs
rtk git commit -m "feat: add custom component property editor providers"
```

### Task 3: Add the CameraClearSettings Provider and Nested Row Kinds

**Files:**
- Create: `engine/helengine.editor/components/ui/CameraClearSettingsPropertyEditorProvider.cs`
- Modify: `engine/helengine.editor/components/ui/ComponentPropertyRowKind.cs`
- Modify: `engine/helengine.editor/components/ui/ComponentPropertyRow.cs`
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Test: `engine/helengine.editor.tests/ComponentPropertiesViewDynamicInspectorTests.cs`

- [ ] **Step 1: Add the CameraClearSettings provider**

```csharp
using System.Reflection;

namespace helengine.editor {
    /// <summary>
    /// Resolves the custom nested editor used for CameraClearSettings properties.
    /// </summary>
    public class CameraClearSettingsPropertyEditorProvider : IComponentPropertyEditorProvider {
        /// <summary>
        /// Stable editor type identifier for CameraClearSettings.
        /// </summary>
        public const string EditorTypeId = "CameraClearSettings";

        /// <summary>
        /// Tries to create the nested editor descriptor for one reflected property.
        /// </summary>
        /// <param name="property">Reflected property metadata.</param>
        /// <param name="descriptor">Resolved custom editor descriptor when supported.</param>
        /// <returns>True when the property type is CameraClearSettings.</returns>
        public bool TryCreateDescriptor(PropertyInfo property, out ComponentPropertyEditorDescriptor descriptor) {
            if (property == null) {
                throw new ArgumentNullException(nameof(property));
            }

            descriptor = null;
            if (property.PropertyType != typeof(CameraClearSettings)) {
                return false;
            }

            descriptor = new ComponentPropertyEditorDescriptor(
                property,
                ResolveDisplayName(property),
                EditorTypeId,
                ResolveOrder(property));
            return true;
        }
    }
}
```

- [ ] **Step 2: Add nested/custom row kinds needed by the clear-settings editor**

```csharp
/// <summary>
/// Collapsible nested section row used by provider-backed custom editors.
/// </summary>
CustomSection,
/// <summary>
/// Editable row for a Vector4 value.
/// </summary>
Vector4
```

- [ ] **Step 3: Extend ComponentPropertyRow to carry nested editor state**

```csharp
public bool IsExpanded { get; set; }
public string CustomEditorTypeId { get; set; }
public EditorEntity HeaderBackgroundHost { get; set; }
public NineSliceComponent HeaderBackground { get; set; }
public EditorEntity HeaderInteractableHost { get; set; }
public ButtonInteractableComponent HeaderInteractable { get; set; }
public EditorEntity[] Vector4FieldHosts { get; set; }
public TextBoxComponent[] Vector4Fields { get; set; }
public string[] Vector4Cache { get; set; }
```

- [ ] **Step 4: Build the nested custom section row and the Vector4 row skeletons**

```csharp
case ComponentPropertyRowKind.CustomSection:
    BuildCustomSectionRow(row, rowEntity);
    break;
case ComponentPropertyRowKind.Vector4:
    BuildVector4Row(row, rowEntity);
    break;
```

- [ ] **Step 5: Run the rendering tests again**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ShowComponents_WhenInspectingCamera_IncludesClearSettingsNestedSection|FullyQualifiedName~ShowComponents_WhenClearSettingsSectionIsExpanded_RendersExpectedNestedControls|FullyQualifiedName~ShowComponents_WhenClearSettingsSectionIsCollapsed_HidesNestedControls"
```

Expected: still FAIL in part, but now because layout/state/write-back are incomplete rather than because the provider or row kinds are missing.

- [ ] **Step 6: Commit the provider-backed row scaffolding**

```bash
rtk git add engine/helengine.editor/components/ui/CameraClearSettingsPropertyEditorProvider.cs engine/helengine.editor/components/ui/ComponentPropertyRowKind.cs engine/helengine.editor/components/ui/ComponentPropertyRow.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs
rtk git commit -m "feat: add camera clear settings editor row scaffolding"
```

### Task 4: Implement Nested Clear Settings Layout, Collapse State, and Struct Write-Back

**Files:**
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Test: `engine/helengine.editor.tests/ComponentPropertiesViewDynamicInspectorTests.cs`
- Test: `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`

- [ ] **Step 1: Add stable nested expansion state to ComponentPropertiesView**

```csharp
readonly Dictionary<string, bool> CustomEditorExpandedStates;
```

```csharp
string BuildCustomEditorStateKey(Component component, PropertyInfo property) {
    return $"{component.GetHashCode().ToString(CultureInfo.InvariantCulture)}::{property.Name}";
}
```

- [ ] **Step 2: Expand provider-backed properties into nested sub-rows**

```csharp
void AddCustomEditorRows(ComponentSectionView section, Component component, ReflectedComponentPropertyDescriptor descriptor) {
    ComponentPropertyRow sectionRow = AcquireRow(ComponentPropertyRowKind.CustomSection);
    BindCustomSectionRow(sectionRow, component, descriptor);
    section.Rows.Add(sectionRow);
    ActiveRows.Add(sectionRow);

    if (!sectionRow.IsExpanded) {
        return;
    }

    AddCameraClearSettingsRows(section, sectionRow, component, descriptor.Property);
}
```

- [ ] **Step 3: Implement the CameraClearSettings nested sub-row creation**

```csharp
void AddCameraClearSettingsRows(ComponentSectionView section, ComponentPropertyRow parentRow, Component component, PropertyInfo property) {
    AddNestedBooleanRow(section, parentRow, component, property, "Clear Color Enabled");
    AddNestedVector4Row(section, parentRow, component, property, "Clear Color");
    AddNestedBooleanRow(section, parentRow, component, property, "Clear Depth Enabled");
    AddNestedScalarRow(section, parentRow, component, property, "Clear Depth");
    AddNestedBooleanRow(section, parentRow, component, property, "Clear Stencil Enabled");
    AddNestedScalarRow(section, parentRow, component, property, "Clear Stencil");
}
```

- [ ] **Step 4: Implement struct rebuild/write-back helpers**

```csharp
CameraClearSettings ReadCameraClearSettings(ComponentPropertyRow row) {
    object rawValue = GetPropertyValue(row);
    if (rawValue is CameraClearSettings settings) {
        return settings;
    }

    throw new InvalidOperationException("Camera clear settings row requires a CameraClearSettings value.");
}
```

```csharp
void WriteCameraClearSettings(ComponentPropertyRow row, CameraClearSettings settings) {
    row.Property.SetValue(row.TargetComponent, settings);
    EditorSceneMutationService.MarkSceneMutated();
}
```

```csharp
void UpdateCameraClearDepth(ComponentPropertyRow row, float value) {
    CameraClearSettings settings = ReadCameraClearSettings(row);
    settings.ClearDepth = value;
    WriteCameraClearSettings(row, settings);
}
```

- [ ] **Step 5: Implement the Vector4 editor handling for Clear Color**

```csharp
void HandleVector4Submitted(TextBoxComponent field) {
    // Resolve owning row, parse RGBA, rebuild CameraClearSettings.ClearColor, assign full struct back.
}
```

- [ ] **Step 6: Run the focused Camera clear-settings test slice**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ComponentPropertiesViewDynamicInspectorTests|FullyQualifiedName~ShowEntityProperties_WhenClearDepthIsSubmitted_UpdatesCameraClearSettings|FullyQualifiedName~ShowEntityProperties_WhenClearColorEnabledChanges_UpdatesCameraClearSettings"
```

Expected: PASS, including nested section rendering, collapse behavior, and struct write-back.

- [ ] **Step 7: Commit the working clear-settings editor**

```bash
rtk git add engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor.tests/ComponentPropertiesViewDynamicInspectorTests.cs engine/helengine.editor.tests/PropertiesPanelMutationTests.cs
rtk git commit -m "feat: add camera clear settings property editor"
```

### Task 5: Run Verification and Record Known Unrelated Failures

**Files:**
- Review: `engine/helengine.editor/components/ui/ReflectedComponentPropertyDescriptorBuilder.cs`
- Review: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Review: `engine/helengine.editor/components/ui/CameraClearSettingsPropertyEditorProvider.cs`
- Review: `engine/helengine.editor.tests/ComponentPropertiesViewDynamicInspectorTests.cs`
- Review: `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`

- [ ] **Step 1: Run the focused final verification pass**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ComponentPropertiesViewDynamicInspectorTests|FullyQualifiedName~ComponentPropertiesViewGeneratedAssetTests|FullyQualifiedName~ComponentPropertiesViewScenePersistenceTests|FullyQualifiedName~PropertiesPanelMutationTests"
```

Expected: PASS.

- [ ] **Step 2: Run the broader related slice**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~PropertiesPanelComponentShellTests|FullyQualifiedName~EditorSessionModelAssetSelectionTests|FullyQualifiedName~EditorSessionPreviewSelectionTests|FullyQualifiedName~CameraComponentLayerMaskTests"
```

Expected: the same two unrelated pre-existing `PropertiesPanelComponentShellTests` failures may still remain:
- `HandleAddComponentClicked_WhenDialogIsVisible_BlocksViewportInputUntilHidden`
- `HandleAddComponentClicked_WhenRowIsActivated_SelectsItWithoutClosing`

Everything else in the slice should stay green.

- [ ] **Step 3: Inspect final worktree state**

Run:

```powershell
rtk git status --short
```

Expected: only intended feature files are modified, plus any known unrelated worktree files that must be left untouched.

- [ ] **Step 4: Commit any final test adjustments if needed**

```bash
rtk git add engine/helengine.editor.tests/ComponentPropertiesViewDynamicInspectorTests.cs engine/helengine.editor.tests/PropertiesPanelMutationTests.cs
rtk git commit -m "test: verify camera clear settings editor"
```
