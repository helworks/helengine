# Dynamic Camera Properties Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a metadata-driven default component inspector, migrate `CameraComponent` to it, and hide unsupported/runtime-facing Camera properties until custom editors exist.

**Architecture:** Keep `ComponentPropertiesView` as the row renderer, but move reflected property eligibility into a focused metadata-driven path. `helengine.core` owns property-level editor attributes on component types, while `helengine.editor` owns descriptor discovery, supported-row selection, and the binding/rendering logic that consumes those descriptors.

**Tech Stack:** C#, xUnit, existing `ComponentPropertiesView` row system, reflection-based editor metadata, RTK for verification commands.

---

### Task 1: Add Failing Dynamic-Inspector Coverage

**Files:**
- Create: `engine/helengine.editor.tests/ComponentPropertiesViewDynamicInspectorTests.cs`
- Modify: `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`
- Reference: `engine/helengine.editor.tests/ComponentPropertiesViewGeneratedAssetTests.cs`
- Reference: `engine/helengine.editor.tests/testing/TestRuntimeTexture.cs`

- [ ] **Step 1: Write the failing reflected-inspector discovery tests**

```csharp
using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies metadata-driven reflected inspector behavior.
    /// </summary>
    public class ComponentPropertiesViewDynamicInspectorTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the test content manager.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes core services required by component property rows.
        /// </summary>
        public ComponentPropertiesViewDynamicInspectorTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-dynamic-inspector-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
            EditorSceneMutationService.Reset();
        }

        /// <summary>
        /// Cleans temporary test content.
        /// </summary>
        public void Dispose() {
            EditorSceneMutationService.Reset();
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures metadata hides unsupported Camera runtime properties from the default inspector.
        /// </summary>
        [Fact]
        public void ShowComponents_WhenInspectingCamera_HidesRuntimeAndUnsupportedProperties() {
            CameraComponent camera = new CameraComponent();
            EditorEntity entity = new EditorEntity();
            entity.AddComponent(camera);

            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
            view.ShowComponents(entity);

            List<ComponentPropertyRow> rows = GetActiveRows(view);
            Assert.Contains(rows, row => string.Equals(row.Label.Text, "Draw Order", StringComparison.Ordinal));
            Assert.Contains(rows, row => string.Equals(row.Label.Text, "Layer Mask", StringComparison.Ordinal));
            Assert.Contains(rows, row => string.Equals(row.Label.Text, "Near Plane Distance", StringComparison.Ordinal));
            Assert.Contains(rows, row => string.Equals(row.Label.Text, "Far Plane Distance", StringComparison.Ordinal));

            Assert.DoesNotContain(rows, row => string.Equals(row.Label.Text, "ClearSettings", StringComparison.Ordinal));
            Assert.DoesNotContain(rows, row => string.Equals(row.Label.Text, "RenderSettings", StringComparison.Ordinal));
            Assert.DoesNotContain(rows, row => string.Equals(row.Label.Text, "RenderQueue2D", StringComparison.Ordinal));
            Assert.DoesNotContain(rows, row => string.Equals(row.Label.Text, "RenderQueue3D", StringComparison.Ordinal));
            Assert.DoesNotContain(rows, row => string.Equals(row.Label.Text, "RenderTarget", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures metadata ordering controls the rendered Camera row order.
        /// </summary>
        [Fact]
        public void ShowComponents_WhenInspectingCamera_UsesMetadataOrderForRows() {
            CameraComponent camera = new CameraComponent();
            EditorEntity entity = new EditorEntity();
            entity.AddComponent(camera);

            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
            view.ShowComponents(entity);

            List<ComponentPropertyRow> rows = GetActiveRows(view);
            Assert.Collection(
                rows,
                row => Assert.Equal("Draw Order", row.Label.Text),
                row => Assert.Equal("Layer Mask", row.Label.Text),
                row => Assert.Equal("Near Plane Distance", row.Label.Text),
                row => Assert.Equal("Far Plane Distance", row.Label.Text));
        }

        /// <summary>
        /// Ensures unsupported complex properties are excluded instead of falling back to noisy read-only rows.
        /// </summary>
        [Fact]
        public void ShowComponents_WhenPropertyTypeIsUnsupported_DoesNotCreateReadOnlyFallbackRow() {
            CameraComponent camera = new CameraComponent();
            EditorEntity entity = new EditorEntity();
            entity.AddComponent(camera);

            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
            view.ShowComponents(entity);

            List<ComponentPropertyRow> rows = GetActiveRows(view);
            Assert.DoesNotContain(rows, row => row.Kind == ComponentPropertyRowKind.ReadOnly);
        }

        List<ComponentPropertyRow> GetActiveRows(ComponentPropertiesView view) {
            FieldInfo activeRowsField = typeof(ComponentPropertiesView).GetField("ActiveRows", BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<List<ComponentPropertyRow>>(activeRowsField.GetValue(view));
        }

        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['D'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                [' '] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['O'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['L'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['N'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture { Width = 64, Height = 64 },
                characters,
                16f,
                64,
                64);
        }
    }
}
```

- [ ] **Step 2: Write the failing Camera write-back mutation test**

```csharp
[Fact]
public void ShowEntityProperties_WhenCameraScalarFieldIsSubmitted_UpdatesTheCameraComponent() {
    PropertiesPanel panel = CreatePanel();
    EditorEntity entity = new EditorEntity();
    CameraComponent camera = new CameraComponent();
    camera.NearPlaneDistance = 0.1f;
    entity.AddComponent(camera);

    panel.ShowEntityProperties(entity);

    ComponentPropertiesView view = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
    ComponentPropertyRow nearPlaneRow = GetSingleRow(view, "Near Plane Distance");
    nearPlaneRow.ScalarField.Text = "2.5";

    MethodInfo submitMethod = typeof(ComponentPropertiesView).GetMethod("HandleScalarSubmitted", BindingFlags.Instance | BindingFlags.NonPublic);
    submitMethod.Invoke(view, new object[] { nearPlaneRow.ScalarField, "2.5" });

    Assert.Equal(2.5f, camera.NearPlaneDistance, 3);
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ComponentPropertiesViewDynamicInspectorTests|FullyQualifiedName~ShowEntityProperties_WhenCameraScalarFieldIsSubmitted_UpdatesTheCameraComponent"
```

Expected: FAIL because Camera properties still use raw CLR labels/order, unsupported rows can still leak through, and no metadata attributes exist yet.

- [ ] **Step 4: Commit the failing tests**

```bash
rtk git add engine/helengine.editor.tests/ComponentPropertiesViewDynamicInspectorTests.cs engine/helengine.editor.tests/PropertiesPanelMutationTests.cs
rtk git commit -m "test: cover dynamic camera property inspection"
```

### Task 2: Add Core Editor-Property Metadata and Annotate CameraComponent

**Files:**
- Create: `engine/helengine.core/components/editor/EditorPropertyHiddenAttribute.cs`
- Create: `engine/helengine.core/components/editor/EditorPropertyDisplayNameAttribute.cs`
- Create: `engine/helengine.core/components/editor/EditorPropertyOrderAttribute.cs`
- Modify: `engine/helengine.core/components/CameraComponent.cs`
- Test: `engine/helengine.editor.tests/ComponentPropertiesViewDynamicInspectorTests.cs`

- [ ] **Step 1: Add the editor metadata attribute types**

```csharp
namespace helengine {
    /// <summary>
    /// Hides one component property from the default reflected editor inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class EditorPropertyHiddenAttribute : Attribute {
    }
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Provides the display label used by the default reflected editor inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class EditorPropertyDisplayNameAttribute : Attribute {
        /// <summary>
        /// Initializes a new display-name attribute.
        /// </summary>
        /// <param name="displayName">Display label shown in the editor.</param>
        public EditorPropertyDisplayNameAttribute(string displayName) {
            if (string.IsNullOrWhiteSpace(displayName)) {
                throw new ArgumentException("Display name must be provided.", nameof(displayName));
            }

            DisplayName = displayName;
        }

        /// <summary>
        /// Gets the display label shown in the editor.
        /// </summary>
        public string DisplayName { get; }
    }
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Controls the display order used by the default reflected editor inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class EditorPropertyOrderAttribute : Attribute {
        /// <summary>
        /// Initializes a new order attribute.
        /// </summary>
        /// <param name="order">Order value used when sorting editor rows.</param>
        public EditorPropertyOrderAttribute(int order) {
            Order = order;
        }

        /// <summary>
        /// Gets the sort order used when rendering the property.
        /// </summary>
        public int Order { get; }
    }
}
```

- [ ] **Step 2: Annotate CameraComponent properties for the first dynamic pass**

```csharp
[EditorPropertyDisplayName("Draw Order")]
[EditorPropertyOrder(0)]
public byte CameraDrawOrder {
```

```csharp
[EditorPropertyHidden]
public float4 Viewport { get; set; }
```

```csharp
[EditorPropertyOrder(1)]
[EditorPropertyDisplayName("Layer Mask")]
public ushort LayerMask {
```

```csharp
[EditorPropertyDisplayName("Near Plane Distance")]
[EditorPropertyOrder(2)]
public float NearPlaneDistance {
```

```csharp
[EditorPropertyDisplayName("Far Plane Distance")]
[EditorPropertyOrder(3)]
public float FarPlaneDistance {
```

```csharp
[EditorPropertyHidden]
public RenderTarget RenderTarget { get; set; }

[EditorPropertyHidden]
public CameraClearSettings ClearSettings { get; set; }

[EditorPropertyHidden]
public CameraRenderSettings RenderSettings {
```

```csharp
[EditorPropertyHidden]
public IRenderQueue2D RenderQueue2D { get { return renderList2D; } }

[EditorPropertyHidden]
public IRenderQueue3D RenderQueue3D { get { return renderList3D; } }
```

- [ ] **Step 3: Run the metadata tests again**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ComponentPropertiesViewDynamicInspectorTests"
```

Expected: still FAIL, but now failures should be down to `ComponentPropertiesView` not honoring the new metadata rules yet.

- [ ] **Step 4: Commit the metadata layer**

```bash
rtk git add engine/helengine.core/components/editor/EditorPropertyHiddenAttribute.cs engine/helengine.core/components/editor/EditorPropertyDisplayNameAttribute.cs engine/helengine.core/components/editor/EditorPropertyOrderAttribute.cs engine/helengine.core/components/CameraComponent.cs
rtk git commit -m "feat: add editor property metadata for camera"
```

### Task 3: Implement Metadata-Driven Reflected Property Discovery

**Files:**
- Create: `engine/helengine.editor/components/ui/ReflectedComponentPropertyDescriptor.cs`
- Create: `engine/helengine.editor/components/ui/ReflectedComponentPropertyDescriptorBuilder.cs`
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Test: `engine/helengine.editor.tests/ComponentPropertiesViewDynamicInspectorTests.cs`

- [ ] **Step 1: Add a focused descriptor type for reflected property rows**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Describes one property that can be rendered by the default reflected inspector.
    /// </summary>
    public class ReflectedComponentPropertyDescriptor {
        /// <summary>
        /// Initializes a new reflected property descriptor.
        /// </summary>
        public ReflectedComponentPropertyDescriptor(
            PropertyInfo property,
            string displayName,
            ComponentPropertyRowKind rowKind,
            int order) {
            Property = property ?? throw new ArgumentNullException(nameof(property));
            if (string.IsNullOrWhiteSpace(displayName)) {
                throw new ArgumentException("Display name must be provided.", nameof(displayName));
            }

            DisplayName = displayName;
            RowKind = rowKind;
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
        /// Gets the row kind used to render the property.
        /// </summary>
        public ComponentPropertyRowKind RowKind { get; }

        /// <summary>
        /// Gets the explicit display order.
        /// </summary>
        public int Order { get; }
    }
}
```

- [ ] **Step 2: Add the descriptor builder that applies metadata rules**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Builds metadata-driven descriptors for the default reflected component inspector.
    /// </summary>
    public class ReflectedComponentPropertyDescriptorBuilder {
        /// <summary>
        /// Builds reflected descriptors for the supplied component type.
        /// </summary>
        /// <param name="componentType">Component type being inspected.</param>
        /// <returns>Ordered descriptors eligible for the default inspector.</returns>
        public List<ReflectedComponentPropertyDescriptor> Build(Type componentType) {
            PropertyInfo[] properties = componentType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            List<ReflectedComponentPropertyDescriptor> descriptors = new List<ReflectedComponentPropertyDescriptor>(properties.Length);

            for (int i = 0; i < properties.Length; i++) {
                PropertyInfo property = properties[i];
                if (!ShouldInclude(property)) {
                    continue;
                }

                if (!TryMapRowKind(property, out ComponentPropertyRowKind rowKind)) {
                    continue;
                }

                descriptors.Add(new ReflectedComponentPropertyDescriptor(
                    property,
                    ResolveDisplayName(property),
                    rowKind,
                    ResolveOrder(property)));
            }

            descriptors.Sort(CompareDescriptors);
            return descriptors;
        }
    }
}
```

- [ ] **Step 3: Change ComponentPropertiesView to consume descriptors instead of raw property loops**

```csharp
readonly ReflectedComponentPropertyDescriptorBuilder DescriptorBuilder;
```

```csharp
DescriptorBuilder = new ReflectedComponentPropertyDescriptorBuilder();
```

```csharp
void AddPropertyRows(ComponentSectionView section, Component component) {
    List<ReflectedComponentPropertyDescriptor> descriptors = DescriptorBuilder.Build(component.GetType());
    for (int i = 0; i < descriptors.Count; i++) {
        ReflectedComponentPropertyDescriptor descriptor = descriptors[i];
        ComponentPropertyRow row = AcquireRow(descriptor.RowKind);
        BindPropertyRow(row, component, descriptor);
        UpdateRowValue(row);
        section.Rows.Add(row);
        ActiveRows.Add(row);
    }
}
```

```csharp
void BindPropertyRow(ComponentPropertyRow row, Component component, ReflectedComponentPropertyDescriptor descriptor) {
    row.TargetComponent = component;
    row.Property = descriptor.Property;
    row.Label.Text = descriptor.DisplayName;
    row.Label.Color = ThemeManager.Colors.InputForegroundPrimary;
    row.Entity.Enabled = true;
}
```

```csharp
void UpdateRowValue(ComponentPropertyRow row) {
    switch (row.Kind) {
        case ComponentPropertyRowKind.Vector3:
            UpdateVectorRow(row);
            break;
        case ComponentPropertyRowKind.Material:
            UpdateMaterialRow(row);
            break;
        case ComponentPropertyRowKind.Font:
            UpdateFontRow(row);
            break;
        case ComponentPropertyRowKind.Model:
            UpdateModelRow(row);
            break;
        case ComponentPropertyRowKind.Boolean:
            UpdateBooleanRow(row);
            break;
        case ComponentPropertyRowKind.Scalar:
            UpdateScalarRow(row);
            break;
        case ComponentPropertyRowKind.ReadOnly:
            UpdateReadOnlyRow(row);
            break;
    }
}
```

- [ ] **Step 4: Run the reflected-inspector tests and keep the unsupported-type behavior strict**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ComponentPropertiesViewDynamicInspectorTests"
```

Expected: PASS, with Camera rows labeled and ordered from metadata and unsupported hidden/runtime properties excluded from the default inspector.

- [ ] **Step 5: Commit the descriptor-driven inspector path**

```bash
rtk git add engine/helengine.editor/components/ui/ReflectedComponentPropertyDescriptor.cs engine/helengine.editor/components/ui/ReflectedComponentPropertyDescriptorBuilder.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs
rtk git commit -m "feat: add metadata-driven reflected component inspector"
```

### Task 4: Verify Camera Editing Through the Real Properties Panel

**Files:**
- Modify: `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`
- Test: `engine/helengine.editor.tests/ComponentPropertiesViewDynamicInspectorTests.cs`
- Reference: `engine/helengine.editor/components/ui/PropertiesPanel.cs`

- [ ] **Step 1: Finish any helper assertions needed by the panel mutation test**

```csharp
ComponentPropertyRow GetSingleRow(ComponentPropertiesView view, string label) {
    FieldInfo activeRowsField = typeof(ComponentPropertiesView).GetField("ActiveRows", BindingFlags.Instance | BindingFlags.NonPublic);
    List<ComponentPropertyRow> rows = Assert.IsType<List<ComponentPropertyRow>>(activeRowsField.GetValue(view));
    return Assert.Single(rows, row => string.Equals(row.Label.Text, label, StringComparison.Ordinal));
}
```

- [ ] **Step 2: Run the panel mutation test together with the reflected-inspector tests**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ComponentPropertiesViewDynamicInspectorTests|FullyQualifiedName~ShowEntityProperties_WhenCameraScalarFieldIsSubmitted_UpdatesTheCameraComponent"
```

Expected: PASS, proving the real panel path can render the dynamic Camera row and write the changed scalar value back to the live component.

- [ ] **Step 3: Run the nearby regression slice to catch row-system regressions**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ComponentPropertiesViewGeneratedAssetTests|FullyQualifiedName~ComponentPropertiesViewScenePersistenceTests|FullyQualifiedName~PropertiesPanelMutationTests|FullyQualifiedName~PropertiesPanelComponentShellTests"
```

Expected: PASS, except for any pre-existing unrelated failures already known in `PropertiesPanelComponentShellTests`. If those known unrelated failures appear, record them explicitly and verify the new Camera/property tests are green.

- [ ] **Step 4: Commit the panel verification coverage**

```bash
rtk git add engine/helengine.editor.tests/PropertiesPanelMutationTests.cs engine/helengine.editor.tests/ComponentPropertiesViewDynamicInspectorTests.cs
rtk git commit -m "test: verify dynamic camera properties panel editing"
```

### Task 5: Final Verification and Cleanup

**Files:**
- Review: `engine/helengine.core/components/CameraComponent.cs`
- Review: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Review: `engine/helengine.editor/components/ui/ReflectedComponentPropertyDescriptor.cs`
- Review: `engine/helengine.editor/components/ui/ReflectedComponentPropertyDescriptorBuilder.cs`
- Review: `engine/helengine.editor.tests/ComponentPropertiesViewDynamicInspectorTests.cs`
- Review: `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`

- [ ] **Step 1: Run the focused verification pass**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ComponentPropertiesViewDynamicInspectorTests|FullyQualifiedName~ComponentPropertiesViewGeneratedAssetTests|FullyQualifiedName~ComponentPropertiesViewScenePersistenceTests|FullyQualifiedName~PropertiesPanelMutationTests"
```

Expected: PASS.

- [ ] **Step 2: Run one broader editor regression slice that exercises Camera inspector consumers**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~PropertiesPanelComponentShellTests|FullyQualifiedName~EditorSessionModelAssetSelectionTests|FullyQualifiedName~EditorSessionPreviewSelectionTests|FullyQualifiedName~CameraComponentLayerMaskTests"
```

Expected: PASS, except for any explicitly pre-existing unrelated failures that must be called out if they still exist.

- [ ] **Step 3: Inspect git status**

Run:

```powershell
rtk git status --short
```

Expected: only the intended implementation files are modified, plus any known unrelated worktree changes that must be left untouched.

- [ ] **Step 4: Commit the finished feature**

```bash
rtk git add engine/helengine.core/components/CameraComponent.cs engine/helengine.core/components/editor/EditorPropertyHiddenAttribute.cs engine/helengine.core/components/editor/EditorPropertyDisplayNameAttribute.cs engine/helengine.core/components/editor/EditorPropertyOrderAttribute.cs engine/helengine.editor/components/ui/ReflectedComponentPropertyDescriptor.cs engine/helengine.editor/components/ui/ReflectedComponentPropertyDescriptorBuilder.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor.tests/ComponentPropertiesViewDynamicInspectorTests.cs engine/helengine.editor.tests/PropertiesPanelMutationTests.cs
rtk git commit -m "feat: add dynamic camera properties"
```
