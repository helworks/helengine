# Model Preview Bounding-Box Dimensions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (\`- [ ]\`) syntax for tracking.

**Goal:** Render the model preview bounding-box width, height, and depth as camera-facing labels using the transform-gizmo label font and material.

**Architecture:** \`ModelPreviewBoundsDimensionLabelFactory\` creates three preview-layer billboard entities from the existing transform-gizmo label mesh and material factories. \`ModelPreviewSource\` owns their lifecycle, camera-facing transforms, and box-only visibility. \`PreviewPanel\` supplies its title font, which is the same session \`uiFont\` passed to \`EditorViewport\` and its gizmo labels.

**Tech Stack:** C# / .NET 9, helengine \`EditorEntity\` and \`MeshComponent\`, runtime models/materials, xUnit.

---

## File Structure

- Create: \`engine/helengine.editor/managers/preview/ModelPreviewBoundsDimensionLabelFactory.cs\` — creates the three pre-positioned preview-layer glyph billboards and formats axis dimensions.
- Modify: \`engine/helengine.editor/managers/preview/ModelPreviewSource.cs\` — owns labels, synchronizes camera-facing transforms, and toggles visibility with bounds box mode.
- Modify: \`engine/helengine.editor/components/ui/PreviewPanel.cs\` — provides the panel’s shared gizmo font to each active model preview source.
- Modify: \`engine/helengine.editor.tests/managers/preview/ModelPreviewSourceTests.cs\` — proves label construction, placement, material binding, and box-only visibility.
- Modify: \`engine/helengine.editor.tests/components/ui/PreviewPanelTests.cs\` — proves the panel configures each source before box mode is enabled.

### Task 1: Specify the new source behavior with a failing test

**Files:**

- Modify: \`engine/helengine.editor.tests/managers/preview/ModelPreviewSourceTests.cs\`

- [ ] **Step 1: Add an axis-label font fixture and failing source test**

Add \`CreateAxisLabelFont()\` with glyphs \`0\` through \`9\`, \`.\`, and \`-\`, backed by a \`TestRuntimeTexture\`. Add this test beside the existing bounds-overlay tests:

\`\`\`csharp
[Fact]
public void ConfigureBoundsDimensionLabels_WhenBoxModeIsSelected_ShowsThreeGizmoFontBillboardsAtPositiveEdges() {
    ModelPreviewSource source = new ModelPreviewSource(CreateRuntimeModel(), Core.Instance.RenderManager3D);
    FontAsset font = CreateAxisLabelFont();

    source.ConfigureBoundsDimensionLabels(font);
    source.SetBoundsDisplayMode(ModelPreviewBoundsDisplayMode.Box);

    EditorEntity[] labels = GetPrivateField<EditorEntity[]>(source, "boundsDimensionLabelEntities");
    Assert.Equal(3, labels.Length);
    Assert.All(labels, label => Assert.True(label.Enabled));
    Assert.Equal(new float3(0f, 1f, 1f), labels[0].LocalPosition);
    Assert.Equal(new float3(1f, 0f, 1f), labels[1].LocalPosition);
    Assert.Equal(new float3(1f, 1f, 0f), labels[2].LocalPosition);
    Assert.All(labels, label => {
        MeshComponent mesh = Assert.IsType<MeshComponent>(Assert.Single(label.Components, component => component is MeshComponent));
        ShaderRuntimeMaterial material = Assert.IsType<ShaderRuntimeMaterial>(Assert.Single(mesh.Materials));
        Assert.Same(font.Texture, material.Properties.GetTexture("LabelTexture"));
    });

    source.SetBoundsDisplayMode(ModelPreviewBoundsDisplayMode.Sphere);
    Assert.All(labels, label => Assert.False(label.Enabled));
    source.Dispose();
}
\`\`\`

- [ ] **Step 2: Run the focused test to verify the red state**

Run:

\`\`\`powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~ModelPreviewSourceTests.ConfigureBoundsDimensionLabels"
\`\`\`

Expected: compilation fails because \`ModelPreviewSource.ConfigureBoundsDimensionLabels\` does not exist.

### Task 2: Build three bounds-dimension label entities with the gizmo label pipeline

**Files:**

- Create: \`engine/helengine.editor/managers/preview/ModelPreviewBoundsDimensionLabelFactory.cs\`

- [ ] **Step 1: Add a validated factory API**

Create a documented public static factory with this API:

\`\`\`csharp
public static EditorEntity[] Create(RenderManager3D render3D, FontAsset font, float3 halfExtents)
\`\`\`

Reject null renderer/font and negative half extents. Construct one shared material:

\`\`\`csharp
RuntimeMaterial labelMaterial = TransformGizmoAxisLabelMaterialFactory.Create(render3D, font);
\`\`\`

- [ ] **Step 2: Create X, Y, and Z dimension entities in a stable order**

Create exactly three \`EditorEntity\` instances, ordered X width, Y height, Z depth. Give them these positions, where \`half\` is \`halfExtents\`:

\`\`\`csharp
new float3(0f, half.Y, half.Z),
new float3(half.X, 0f, half.Z),
new float3(half.X, half.Y, 0f)
\`\`\`

For each label, generate a model through \`TransformGizmoAxisLabelModelFactory.Create(font, dimensionText)\`, upload it through \`render3D.BuildModelFromRaw\`, and attach a mesh with the shared label material. Use \`EditorLayerMasks.SceneModelPreview\`, \`InternalEntity = true\`, \`Hidden = true\`, \`Enabled = false\`, and render order \`3\`, above the bounds line render order \`2\`.

Format each full axis extent with:

\`\`\`csharp
dimension.ToString("0.##", CultureInfo.InvariantCulture)
\`\`\`

- [ ] **Step 3: Run the focused source test to verify it is still red**

Run the Task 1 command.

Expected: failure because the factory result has not been owned or enabled by \`ModelPreviewSource\`.

### Task 3: Make the preview source own, face, scale, and toggle the labels

**Files:**

- Modify: \`engine/helengine.editor/managers/preview/ModelPreviewSource.cs\`
- Modify: \`engine/helengine.editor.tests/managers/preview/ModelPreviewSourceTests.cs\`

- [ ] **Step 1: Add source state and the explicit font configuration API**

Add documented fields \`EditorEntity[] boundsDimensionLabelEntities\` and \`FontAsset boundsDimensionLabelFont\`. Add this public method:

\`\`\`csharp
public void ConfigureBoundsDimensionLabels(FontAsset font) {
    if (font == null) {
        throw new ArgumentNullException(nameof(font));
    }
    if (ReferenceEquals(boundsDimensionLabelFont, font)) {
        return;
    }
    if (boundsDimensionLabelEntities != null) {
        throw new InvalidOperationException("Model preview bounds dimension labels cannot change fonts after initialization.");
    }

    boundsDimensionLabelFont = font;
    boundsDimensionLabelEntities = ModelPreviewBoundsDimensionLabelFactory.Create(renderManager3D, font, GetBoundsHalfExtents());
    for (int labelIndex = 0; labelIndex < boundsDimensionLabelEntities.Length; labelIndex++) {
        previewEntity.AddChild(boundsDimensionLabelEntities[labelIndex]);
    }
    UpdateBoundsDimensionLabelVisibility();
    UpdateBoundsDimensionLabelBillboards();
}
\`\`\`

The first configured font remains authoritative because it is the shared session font and changing an already-created source font would require rebuilding GPU resources.

- [ ] **Step 2: Link visibility and camera updates to the dimension entities**

After assigning \`BoundsDisplayModeValue\` in \`SetBoundsDisplayMode\`, call \`UpdateBoundsDimensionLabelVisibility()\`. It must enable all three labels only for \`ModelPreviewBoundsDisplayMode.Box\`.

At the end of \`UpdateCameraTransform\`, call \`UpdateBoundsDimensionLabelBillboards()\`. For each configured label, set its orientation to \`cameraEntity.Orientation\` and use a uniform positive scale derived from the label-camera distance, preview viewport height, \`Math.PI / 4d\` vertical field of view, and the same \`1.2d\` pixel multiplier used by \`EditorViewportCameraAngleOverlayComponent\`.

- [ ] **Step 3: Extend the source test for billboard synchronization**

After box mode is selected, add:

\`\`\`csharp
source.HandleMouseDrag(new int2(12, -6));
source.Update();
Assert.All(labels, label => {
    Assert.Equal(source.PreviewCamera.Parent.Orientation, label.Orientation);
    Assert.True(label.Scale.X > 0f);
    Assert.Equal(label.Scale.X, label.Scale.Y);
    Assert.Equal(label.Scale.X, label.Scale.Z);
});
\`\`\`

- [ ] **Step 4: Run the source test to verify green**

Run:

\`\`\`powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~ModelPreviewSourceTests.ConfigureBoundsDimensionLabels"
\`\`\`

Expected: PASS.

- [ ] **Step 5: Commit the source layer**

\`\`\`powershell
git add -- engine/helengine.editor/managers/preview/ModelPreviewBoundsDimensionLabelFactory.cs engine/helengine.editor/managers/preview/ModelPreviewSource.cs engine/helengine.editor.tests/managers/preview/ModelPreviewSourceTests.cs
git commit -m "feat: add model preview bounds dimensions"
\`\`\`

### Task 4: Configure labels through the Preview panel’s shared font

**Files:**

- Modify: \`engine/helengine.editor/components/ui/PreviewPanel.cs\`
- Modify: \`engine/helengine.editor.tests/components/ui/PreviewPanelTests.cs\`

- [ ] **Step 1: Add a failing panel integration test**

Add this test next to the existing bounds-button tests:

\`\`\`csharp
[Fact]
public void SetPreviewSource_WhenBoundsBoxIsActivated_ConfiguresDimensionLabelsWithThePanelFont() {
    FontAsset font = CreateFont();
    PreviewPanel panel = new PreviewPanel(font) { Size = new int2(416, 312) };
    ModelPreviewSource source = CreateModelPreviewSource();

    panel.SetPreviewSource(source);
    panel.CycleModelBoundsDisplayMode();

    EditorEntity[] labels = GetPrivateField<EditorEntity[]>(source, "boundsDimensionLabelEntities");
    Assert.Equal(3, labels.Length);
    Assert.All(labels, label => Assert.True(label.Enabled));
    panel.ClearPreview();
}
\`\`\`

Ensure \`CreateFont()\` includes \`.\` in addition to its existing digit glyphs.

- [ ] **Step 2: Run the panel test to verify the red state**

Run:

\`\`\`powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~PreviewPanelTests.SetPreviewSource_WhenBoundsBoxIsActivated"
\`\`\`

Expected: failure because \`boundsDimensionLabelEntities\` is null.

- [ ] **Step 3: Configure the source before applying its bounds display mode**

In \`PreviewPanel.UpdateModelToolbarVisibility()\`, immediately before \`SetBoundsDisplayMode\`, add:

\`\`\`csharp
modelPreviewSource.ConfigureBoundsDimensionLabels(TitleFont);
\`\`\`

- [ ] **Step 4: Run the panel test to verify green**

Run the Task 4 focused command.

Expected: PASS.

- [ ] **Step 5: Commit the Preview-panel integration**

\`\`\`powershell
git add -- engine/helengine.editor/components/ui/PreviewPanel.cs engine/helengine.editor.tests/components/ui/PreviewPanelTests.cs
git commit -m "feat: configure model preview dimension labels"
\`\`\`

### Task 5: Verify the completed feature

**Files:**

- Verify only: \`engine/helengine.editor.tests/managers/preview/ModelPreviewSourceTests.cs\`
- Verify only: \`engine/helengine.editor.tests/components/ui/PreviewPanelTests.cs\`

- [ ] **Step 1: Run focused regression tests**

\`\`\`powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~ModelPreviewSourceTests|FullyQualifiedName~PreviewPanelTests"
\`\`\`

Expected: PASS with zero failures.

- [ ] **Step 2: Build the Visual Studio Debug launch target**

\`\`\`powershell
dotnet build helengine.ui/helengine.editor.app/helengine.editor.app.csproj --no-restore -p:BuildingInsideVisualStudio=true
\`\`\`

Expected: build succeeds with zero errors.

- [ ] **Step 3: Inspect task-owned commits and preserve unrelated work**

\`\`\`powershell
git status --short
git log --oneline -2
\`\`\`

Expected: the two feature commits are present; existing unrelated changes remain unstaged and untouched.

