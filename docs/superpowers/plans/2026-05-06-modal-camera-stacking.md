# Modal Camera Stacking Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Establish a shared editor camera-tier rule so modal dialogs always render after panel-owned secondary UI content cameras such as Scene Hierarchy.

**Architecture:** Introduce shared camera draw-order constants in editor infrastructure, move the shared editor UI camera and `SceneHierarchyPanel` content camera to those explicit tiers, and add regression tests that validate the tier relationship instead of relying on panel-specific suppression. The fix stays at the camera ordering layer, which is the actual stacking boundary across different UI cameras.

**Tech Stack:** C#, xUnit, helengine editor UI, editor camera setup in `EditorSession`, panel-owned content camera in `SceneHierarchyPanel`.

---

### Task 1: Add RED tests for shared camera-tier ordering

**Files:**
- Modify: `engine/helengine.editor.tests/SceneHierarchyPanelTests.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs` or `engine/helengine.editor.tests/EditorSessionPlatformsTests.cs` only if a session-level modal scenario is required
- Test: `engine/helengine.editor.tests/SceneHierarchyPanelTests.cs`

- [ ] **Step 1: Add a test that pins the Scene Hierarchy content camera below the modal UI tier**

Add this test near the existing content-camera assertions in `engine/helengine.editor.tests/SceneHierarchyPanelTests.cs`:

```csharp
/// <summary>
/// Ensures the Scene Hierarchy content camera renders below the shared modal UI camera tier.
/// </summary>
[Fact]
public void RefreshHierarchy_UsesPanelContentCameraTierBelowModalUiTier() {
    new EditorEntity {
        Name = "Hierarchy Camera Tier Entity"
    };
    SceneHierarchyPanel panel = new SceneHierarchyPanel(CreateFont()) {
        Position = new float3(40f, 64f, 0f),
        Size = new int2(320, 176)
    };

    panel.RefreshHierarchy();

    CameraComponent contentCamera = GetPrivateField<CameraComponent>(panel, "contentCameraComponent");

    Assert.True(contentCamera.CameraDrawOrder < EditorUiCameraDrawOrders.ModalUi);
    Assert.Equal(EditorUiCameraDrawOrders.PanelContent, contentCamera.CameraDrawOrder);
}
```

- [ ] **Step 2: Add a test that pins the shared editor UI camera to the modal tier**

If there is no existing test fixture exposing `EditorSession` camera setup cleanly, add the assertion to the most relevant existing session test file with editor initialization helpers. The new test should look like:

```csharp
/// <summary>
/// Ensures the shared editor UI camera renders on the modal UI tier above panel-owned content cameras.
/// </summary>
[Fact]
public void Constructor_ConfiguresSharedEditorUiCameraOnModalUiTier() {
    EditorSession session = CreateSession(...);

    CameraComponent uiCameraComponent = GetPrivateField<CameraComponent>(session, "uiCameraComponent");

    Assert.Equal(EditorUiCameraDrawOrders.ModalUi, uiCameraComponent.CameraDrawOrder);
    Assert.True(uiCameraComponent.CameraDrawOrder > EditorUiCameraDrawOrders.PanelContent);
}
```

- [ ] **Step 3: Run the new camera-tier tests to verify RED**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneHierarchyPanelTests.RefreshHierarchy_UsesPanelContentCameraTierBelowModalUiTier|FullyQualifiedName~EditorSession.*ConfiguresSharedEditorUiCameraOnModalUiTier"
```

Expected: FAIL because the shared camera-tier constants and assignments do not exist yet, and both cameras currently effectively use `255`.

- [ ] **Step 4: Commit the RED tests**

```powershell
git add engine/helengine.editor.tests/SceneHierarchyPanelTests.cs engine/helengine.editor.tests/<session-test-file>.cs
git commit -m "test: cover modal camera stacking tiers"
```

### Task 2: Implement shared editor camera-tier constants

**Files:**
- Create: `engine/helengine.editor/EditorUiCameraDrawOrders.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/components/ui/SceneHierarchyPanel.cs`

- [ ] **Step 1: Add a shared camera draw-order definition file**

Create `engine/helengine.editor/EditorUiCameraDrawOrders.cs` with:

```csharp
namespace helengine.editor {
    /// <summary>
    /// Defines shared camera draw-order tiers used by editor UI rendering.
    /// </summary>
    public static class EditorUiCameraDrawOrders {
        /// <summary>
        /// Draw order used by panel-owned secondary UI content cameras.
        /// </summary>
        public const byte PanelContent = 254;

        /// <summary>
        /// Draw order used by the shared editor UI camera that renders modal dialogs.
        /// </summary>
        public const byte ModalUi = 255;
    }
}
```

- [ ] **Step 2: Move `EditorSession` shared UI camera setup onto the shared modal tier**

Update `engine/helengine.editor/EditorSession.cs` from:

```csharp
uiCameraComponent.CameraDrawOrder = 255;
```

to:

```csharp
uiCameraComponent.CameraDrawOrder = EditorUiCameraDrawOrders.ModalUi;
```

- [ ] **Step 3: Move `SceneHierarchyPanel` content camera onto the shared panel-content tier**

Update `engine/helengine.editor/components/ui/SceneHierarchyPanel.cs` from:

```csharp
contentCameraComponent = new CameraComponent {
    LayerMask = EditorLayerMasks.SceneHierarchyContent,
    CameraDrawOrder = 255,
    ClearSettings = new CameraClearSettings(false, new float4(0f, 0f, 0f, 0f), false, 1.0f, false, 0)
};
```

to:

```csharp
contentCameraComponent = new CameraComponent {
    LayerMask = EditorLayerMasks.SceneHierarchyContent,
    CameraDrawOrder = EditorUiCameraDrawOrders.PanelContent,
    ClearSettings = new CameraClearSettings(false, new float4(0f, 0f, 0f, 0f), false, 1.0f, false, 0)
};
```

- [ ] **Step 4: Verify the minimal implementation against the focused camera-tier tests**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneHierarchyPanelTests.RefreshHierarchy_UsesPanelContentCameraTierBelowModalUiTier|FullyQualifiedName~EditorSession.*ConfiguresSharedEditorUiCameraOnModalUiTier"
```

Expected: PASS with both tier-contract tests green.

- [ ] **Step 5: Commit the shared camera-tier implementation**

```powershell
git add engine/helengine.editor/EditorUiCameraDrawOrders.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor/components/ui/SceneHierarchyPanel.cs
git commit -m "fix: define shared editor modal camera tiers"
```

### Task 3: Add GREEN regression coverage for modal-over-hierarchy behavior

**Files:**
- Modify: `engine/helengine.editor.tests/SceneHierarchyPanelTests.cs`
- Modify: `engine/helengine.editor.tests/BuildDialogTests.cs` only if a modal fixture helper is needed
- Test: `engine/helengine.editor.tests/SceneHierarchyPanelTests.cs`

- [ ] **Step 1: Add a direct regression test for the modal-over-hierarchy ordering contract**

Add a behavior test that verifies the modal tier outranks the Scene Hierarchy content tier through concrete camera values:

```csharp
/// <summary>
/// Ensures modal UI rendering always uses a later camera tier than Scene Hierarchy content.
/// </summary>
[Fact]
public void RefreshHierarchy_WhenModalUiTierIsCompared_RendersBelowModalDialogs() {
    new EditorEntity {
        Name = "Modal Comparison Entity"
    };
    SceneHierarchyPanel panel = new SceneHierarchyPanel(CreateFont()) {
        Position = new float3(24f, 32f, 0f),
        Size = new int2(320, 176)
    };

    panel.RefreshHierarchy();

    CameraComponent contentCamera = GetPrivateField<CameraComponent>(panel, "contentCameraComponent");

    Assert.True(EditorUiCameraDrawOrders.ModalUi > contentCamera.CameraDrawOrder);
}
```

This test is intentionally camera-tier based, because that is the real stacking contract across cameras.

- [ ] **Step 2: Run the Scene Hierarchy suite**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneHierarchyPanelTests"
```

Expected: PASS, proving the new tier contract does not break hierarchy viewport or row behavior.

- [ ] **Step 3: Run the adjacent modal suite that motivated the bug report**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogTests"
```

Expected: PASS, proving modal dialog behavior still works after the camera-tier change.

- [ ] **Step 4: Run one relevant session suite to validate camera setup integration**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionBuildQueueTests|FullyQualifiedName~EditorSessionPlatformsTests"
```

Expected: PASS, proving the updated shared UI camera tier integrates cleanly with editor-session modal flows.

- [ ] **Step 5: Commit the regression coverage**

```powershell
git add engine/helengine.editor.tests/SceneHierarchyPanelTests.cs engine/helengine.editor.tests/<session-test-file>.cs
git commit -m "test: verify modal ui camera outranks hierarchy content"
```
