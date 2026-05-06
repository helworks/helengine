# EditorDialogBase Modal Lifecycle Sweep Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every direct `EditorDialogBase` modal reach a visually valid state by the end of `Show(...)`, with content relayout driven by the shared dialog lifecycle instead of deferred to a later `UpdateLayout(...)` pass.

**Architecture:** Keep `EditorDialogBase` as the owner of shell state, backdrop state, content-root parenting, and relayout callbacks. Fixed-size dialogs should end `Show(...)` with `ShowDialogImmediately()` and move content placement into `HandleDialogLayoutChanged()`, while `OpenFileDialog` keeps its host-sized behavior through an explicit dialog-level immediate-show helper that still uses the shared shell contract.

**Tech Stack:** C#, xUnit, existing editor UI/dialog infrastructure, `rtk dotnet test`

---

## File Structure

- Modify: `engine/helengine.editor/components/ui/BuildDialogCopySettingsDialog.cs`
  - Replace manual show-time shell work with the shared immediate-show contract.
- Modify: `engine/helengine.editor/components/ui/BuildSettingsDialog.cs`
  - Apply the shared immediate-show contract after dynamic platform rows are rebuilt.
- Modify: `engine/helengine.editor/components/ui/ComponentAddDialog.cs`
  - Apply immediate show-time layout after the filtered component list is rebuilt.
- Modify: `engine/helengine.editor/components/ui/EditorPreferencesDialog.cs`
  - Apply immediate show-time layout for fixed controls.
- Modify: `engine/helengine.editor/components/ui/ProfilesDialog.cs`
  - Apply immediate show-time layout after selector/tab/content state is prepared.
- Modify: `engine/helengine.editor/components/ui/ReparentEntityDialog.cs`
  - Apply immediate show-time layout after hierarchy content is populated.
- Modify: `engine/helengine.editor/components/ui/RemoveComponentDialog.cs`
  - Apply immediate show-time layout for fixed confirmation content.
- Modify: `engine/helengine.editor/components/ui/UnsavedChangesDialog.cs`
  - Apply immediate show-time layout for fixed confirmation content.
- Modify: `engine/helengine.editor/components/ui/asset/OpenFileDialog.cs`
  - Add a host-aware immediate-show path that resolves initial size and then applies the shared shell/content lifecycle.
- Modify: `engine/helengine.editor/tests/BuildDialogCopySettingsDialogTests.cs`
  - Add show-time layout regressions for the copy-settings chooser.
- Modify: `engine/helengine.editor/tests/BuildSettingsDialogTests.cs`
  - Add show-time layout regressions for dynamic platform rows.
- Create: `engine/helengine.editor.tests/ComponentAddDialogTests.cs`
  - Add direct show-time layout regressions for the component picker.
- Modify: `engine/helengine.editor.tests/EditorPreferencesDialogTests.cs`
  - Add show-time layout regressions for preferences controls.
- Modify: `engine/helengine.editor.tests/OpenFileDialogTests.cs`
  - Add show-time layout regressions for browser/status/footer placement.
- Modify: `engine/helengine.editor.tests/ProfilesDialogTests.cs`
  - Add show-time layout regressions for selector/tab/content positioning.
- Modify: `engine/helengine.editor.tests/ReparentEntityDialogTests.cs`
  - Add show-time layout regressions for hierarchy picker positioning.
- Modify: `engine/helengine.editor.tests/RemoveComponentDialogTests.cs`
  - Add show-time layout regressions for confirmation content.
- Modify: `engine/helengine.editor.tests/UnsavedChangesDialogTests.cs`
  - Add show-time layout regressions for confirmation content without touching the unrelated stale resize assertions.

### Task 1: Fixed-Layout Modal Contract

**Files:**
- Modify: `engine/helengine.editor/components/ui/BuildDialogCopySettingsDialog.cs`
- Modify: `engine/helengine.editor/components/ui/EditorPreferencesDialog.cs`
- Modify: `engine/helengine.editor/components/ui/RemoveComponentDialog.cs`
- Modify: `engine/helengine.editor/components/ui/UnsavedChangesDialog.cs`
- Test: `engine/helengine.editor.tests/BuildDialogCopySettingsDialogTests.cs`
- Test: `engine/helengine.editor.tests/EditorPreferencesDialogTests.cs`
- Test: `engine/helengine.editor.tests/RemoveComponentDialogTests.cs`
- Test: `engine/helengine.editor.tests/UnsavedChangesDialogTests.cs`

- [ ] **Step 1: Write the failing show-time layout regressions**

```csharp
[Fact]
public void Show_WhenOpened_PositionsContentImmediately() {
    BuildDialogCopySettingsDialog dialog = new BuildDialogCopySettingsDialog(CreateFont());

    dialog.Show(new[] { "windows", "ps2" });

    EditorEntity sourceLabelHost = GetPrivateField<EditorEntity>(dialog, "SourceLabelHost");
    EditorEntity sourceComboHost = GetPrivateField<EditorEntity>(dialog, "SourceComboHost");
    EditorEntity copyButtonHost = GetPrivateField<EditorEntity>(dialog, "CopyButtonHost");
    EditorEntity cancelButtonHost = GetPrivateField<EditorEntity>(dialog, "CancelButtonHost");

    Assert.NotEqual(float3.Zero, sourceLabelHost.LocalPosition);
    Assert.NotEqual(float3.Zero, sourceComboHost.LocalPosition);
    Assert.NotEqual(float3.Zero, copyButtonHost.LocalPosition);
    Assert.NotEqual(float3.Zero, cancelButtonHost.LocalPosition);
}

[Fact]
public void Show_WhenOpened_PositionsControlsImmediately() {
    EditorPreferencesDialog dialog = new EditorPreferencesDialog(CreateFont(), EditorUiMetrics.Default);

    dialog.Show(new EditorUiScaleSettings(EditorUiScaleMode.Override, 150));

    EditorEntity scaleModeComboBoxHost = GetPrivateField<EditorEntity>(dialog, "ScaleModeComboBoxHost");
    EditorEntity scalePercentComboBoxHost = GetPrivateField<EditorEntity>(dialog, "ScalePercentComboBoxHost");
    EditorEntity applyButtonHost = GetPrivateField<EditorEntity>(dialog, "ApplyButtonHost");

    Assert.NotEqual(float3.Zero, scaleModeComboBoxHost.LocalPosition);
    Assert.NotEqual(float3.Zero, scalePercentComboBoxHost.LocalPosition);
    Assert.NotEqual(float3.Zero, applyButtonHost.LocalPosition);
}

[Fact]
public void Show_WhenOpened_PositionsMessageAndFooterImmediately() {
    RemoveComponentDialog dialog = new RemoveComponentDialog(CreateFont());

    dialog.Show("Camera", "TransformComponent");

    EditorEntity messageHost = GetPrivateField<EditorEntity>(dialog, "MessageHost");
    EditorEntity removeButtonHost = GetPrivateField<EditorEntity>(dialog, "RemoveButtonHost");

    Assert.NotEqual(float3.Zero, messageHost.LocalPosition);
    Assert.NotEqual(float3.Zero, removeButtonHost.LocalPosition);
}

[Fact]
public void Show_WhenOpened_PositionsMessageAndFooterImmediately() {
    UnsavedChangesDialog dialog = new UnsavedChangesDialog(CreateFont());

    dialog.Show();

    EditorEntity messageHost = GetPrivateField<EditorEntity>(dialog, "MessageHost");
    EditorEntity footerHost = GetPrivateField<EditorEntity>(dialog, "FooterHost");

    Assert.NotEqual(float3.Zero, messageHost.LocalPosition);
    Assert.NotEqual(float3.Zero, footerHost.LocalPosition);
}
```

- [ ] **Step 2: Run the focused tests to verify they fail for deferred layout**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogCopySettingsDialogTests.Show_WhenOpened_PositionsContentImmediately|FullyQualifiedName~EditorPreferencesDialogTests.Show_WhenOpened_PositionsControlsImmediately|FullyQualifiedName~RemoveComponentDialogTests.Show_WhenOpened_PositionsMessageAndFooterImmediately|FullyQualifiedName~UnsavedChangesDialogTests.Show_WhenOpened_PositionsMessageAndFooterImmediately" -v minimal`

Expected: FAIL because the hosts still sit at `float3.Zero` until a later `UpdateLayout(...)` call.

- [ ] **Step 3: Move the fixed-layout dialogs onto the shared immediate-show contract**

```csharp
public void Show(IReadOnlyList<string> sourcePlatformIds) {
    if (sourcePlatformIds == null) {
        throw new ArgumentNullException(nameof(sourcePlatformIds));
    }

    SourcePlatformIds.Clear();
    for (int index = 0; index < sourcePlatformIds.Count; index++) {
        string sourcePlatformId = sourcePlatformIds[index];
        if (string.IsNullOrWhiteSpace(sourcePlatformId)) {
            throw new ArgumentException("Source platform ids must not be blank.", nameof(sourcePlatformIds));
        }

        SourcePlatformIds.Add(sourcePlatformId);
    }

    ResetDialogPositioning();
    Enabled = true;
    SourceComboBox.IsOpen = false;
    RebuildSourceItems();
    ShowDialogImmediately();
}

public void Show(EditorUiScaleSettings settings) {
    if (settings == null) {
        throw new ArgumentNullException(nameof(settings));
    }

    CurrentSettings = settings;
    ResetDialogPositioning();
    SetScaleModeSelection(settings.Mode);
    SetScalePercentSelection(settings.OverridePercent);
    UpdateScalePercentEnabled(settings.Mode == EditorUiScaleMode.Override);
    Enabled = true;
    ShowDialogImmediately();
}

public void Show(string entityName, string componentName) {
    if (string.IsNullOrWhiteSpace(entityName)) {
        throw new ArgumentException("An entity name is required.", nameof(entityName));
    }
    if (string.IsNullOrWhiteSpace(componentName)) {
        throw new ArgumentException("A component name is required.", nameof(componentName));
    }

    ResetDialogPositioning();
    MessageText.Text = $"Remove {componentName} from {entityName}?";
    Enabled = true;
    ShowDialogImmediately();
}

public void Show() {
    ResetDialogPositioning();
    Enabled = true;
    ShowDialogImmediately();
}

protected override void HandleDialogLayoutChanged() {
    LayoutContent();
}

protected override void HandleDialogLayoutChanged() {
    LayoutMessage();
    LayoutButtons();
}
```

- [ ] **Step 4: Run the fixed-layout suites**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogCopySettingsDialogTests|FullyQualifiedName~EditorPreferencesDialogTests|FullyQualifiedName~RemoveComponentDialogTests|FullyQualifiedName~UnsavedChangesDialogTests.Show_WhenOpened_" -v minimal`

Expected: PASS for the new show-time layout tests and the full touched suites, except that the two known unrelated resize tests in `UnsavedChangesDialogTests` remain intentionally excluded by the filter.

- [ ] **Step 5: Commit the fixed-layout modal sweep**

```bash
git add engine/helengine.editor/components/ui/BuildDialogCopySettingsDialog.cs \
        engine/helengine.editor/components/ui/EditorPreferencesDialog.cs \
        engine/helengine.editor/components/ui/RemoveComponentDialog.cs \
        engine/helengine.editor/components/ui/UnsavedChangesDialog.cs \
        engine/helengine.editor.tests/BuildDialogCopySettingsDialogTests.cs \
        engine/helengine.editor.tests/EditorPreferencesDialogTests.cs \
        engine/helengine.editor.tests/RemoveComponentDialogTests.cs \
        engine/helengine.editor.tests/UnsavedChangesDialogTests.cs
git commit -m "fix: align fixed editor dialogs with modal lifecycle"
```

### Task 2: Dynamic Platform and Profile Dialogs

**Files:**
- Modify: `engine/helengine.editor/components/ui/BuildSettingsDialog.cs`
- Modify: `engine/helengine.editor/components/ui/ProfilesDialog.cs`
- Test: `engine/helengine.editor.tests/BuildSettingsDialogTests.cs`
- Test: `engine/helengine.editor.tests/ProfilesDialogTests.cs`

- [ ] **Step 1: Write the failing regressions for dynamic rows and tab content**

```csharp
[Fact]
public void Show_WhenOpened_ParentsPlatformRowsUnderDialogContentRootAndLaysThemOutImmediately() {
    BuildSettingsDialog dialog = new BuildSettingsDialog(CreateFont());

    dialog.Show(
        new[] {
            new AvailablePlatformDescriptor("windows", "Windows", true),
            new AvailablePlatformDescriptor("ps2", "PlayStation 2", false)
        },
        new[] { "windows" });

    EditorEntity dialogContentRoot = GetProtectedProperty<EditorEntity>(dialog, "DialogContentRoot");
    List<EditorEntity> platformCheckBoxHosts = GetPrivateField<List<EditorEntity>>(dialog, "PlatformCheckBoxHosts");
    List<EditorEntity> platformLabelHosts = GetPrivateField<List<EditorEntity>>(dialog, "PlatformLabelHosts");

    Assert.All(platformCheckBoxHosts, host => Assert.Same(dialogContentRoot, host.Parent));
    Assert.All(platformLabelHosts, host => Assert.Same(dialogContentRoot, host.Parent));
    Assert.All(platformCheckBoxHosts, host => Assert.NotEqual(float3.Zero, host.LocalPosition));
}

[Fact]
public void Show_WhenOpened_PositionsSelectorTabsAndActiveContentImmediately() {
    ProfilesDialog dialog = new ProfilesDialog(CreateFont(), EditorUiMetrics.Default);

    dialog.Show(CreateProfilesDocument(), new[] { "windows", "ps2" }, "windows", CreateSelectionResolver());

    EditorEntity platformComboBoxHost = GetPrivateField<EditorEntity>(dialog, "PlatformComboBoxHost");
    EditorEntity buildTabButtonHost = GetPrivateField<EditorEntity>(dialog, "BuildTabButtonHost");
    EditorEntity buildContentHost = GetPrivateField<EditorEntity>(dialog, "BuildContentHost");

    Assert.NotEqual(float3.Zero, platformComboBoxHost.LocalPosition);
    Assert.NotEqual(float3.Zero, buildTabButtonHost.LocalPosition);
    Assert.NotEqual(float3.Zero, buildContentHost.LocalPosition);
}
```

- [ ] **Step 2: Run the focused tests to verify they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildSettingsDialogTests.Show_WhenOpened_ParentsPlatformRowsUnderDialogContentRootAndLaysThemOutImmediately|FullyQualifiedName~ProfilesDialogTests.Show_WhenOpened_PositionsSelectorTabsAndActiveContentImmediately" -v minimal`

Expected: FAIL because row/tab hosts are still positioned only after `UpdateLayout(...)`.

- [ ] **Step 3: Apply the immediate-show and base-relayout contract**

```csharp
public void Show(IReadOnlyList<AvailablePlatformDescriptor> availablePlatforms, IReadOnlyList<string> supportedPlatforms) {
    if (availablePlatforms == null) {
        throw new ArgumentNullException(nameof(availablePlatforms));
    }
    if (supportedPlatforms == null) {
        throw new ArgumentNullException(nameof(supportedPlatforms));
    }

    ResetDialogPositioning();
    Enabled = true;
    StatusText.Text = string.Empty;

    RebuildPlatformRows(availablePlatforms, supportedPlatforms);
    ApplyEmptyPlatformMessage();
    ShowDialogImmediately();
}

public void Show(EditorProfileSettingsDocument document, IReadOnlyList<string> supportedPlatforms, string activePlatformId, Func<string, EditorPlatformBuildSelectionModel> selectionModelResolver) {
    if (document == null) {
        throw new ArgumentNullException(nameof(document));
    }
    if (supportedPlatforms == null) {
        throw new ArgumentNullException(nameof(supportedPlatforms));
    }
    if (string.IsNullOrWhiteSpace(activePlatformId)) {
        throw new ArgumentException("Active platform id must be provided.", nameof(activePlatformId));
    }

    int activeIndex = ResolvePlatformIndex(supportedPlatforms, activePlatformId);
    CurrentDocument = CloneProfileSettingsDocument(document);
    CurrentPlatformId = supportedPlatforms[activeIndex];
    SelectionModelResolver = selectionModelResolver;
    ActivePlatformSelectionModel = ResolveSelectionModelForPlatform(CurrentPlatformId);
    SelectedTabIndex = 0;

    SupportedPlatformIds.Clear();
    for (int i = 0; i < supportedPlatforms.Count; i++) {
        SupportedPlatformIds.Add(supportedPlatforms[i]);
    }

    ResetDialogPositioning();
    Enabled = true;
    StatusText.Text = string.Empty;

    IsInitializingSelection = true;
    PlatformComboBox.SetItems(SupportedPlatformIds, activeIndex);
    IsInitializingSelection = false;
    LoadSelectedPlatformIntoFields(CurrentPlatformId);
    RefreshTabVisibility();
    ShowDialogImmediately();
}

protected override void HandleDialogLayoutChanged() {
    LayoutPlatformRows();
    LayoutTableHeader();
    LayoutStatus();
    LayoutButtons();
}

protected override void HandleDialogLayoutChanged() {
    LayoutPlatformSelector();
    LayoutTabs();
    LayoutSettingsSections();
    LayoutStatus();
    LayoutButtons();
}
```

- [ ] **Step 4: Run the dialog suites**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildSettingsDialogTests|FullyQualifiedName~ProfilesDialogTests" -v minimal`

Expected: PASS for the new show-time regressions and the full dialog suites.

- [ ] **Step 5: Commit the dynamic platform/profile modal sweep**

```bash
git add engine/helengine.editor/components/ui/BuildSettingsDialog.cs \
        engine/helengine.editor/components/ui/ProfilesDialog.cs \
        engine/helengine.editor.tests/BuildSettingsDialogTests.cs \
        engine/helengine.editor.tests/ProfilesDialogTests.cs
git commit -m "fix: align platform and profile dialogs with modal lifecycle"
```

### Task 3: Picker Dialogs with Dynamic Content

**Files:**
- Modify: `engine/helengine.editor/components/ui/ComponentAddDialog.cs`
- Modify: `engine/helengine.editor/components/ui/ReparentEntityDialog.cs`
- Create: `engine/helengine.editor.tests/ComponentAddDialogTests.cs`
- Test: `engine/helengine.editor.tests/ReparentEntityDialogTests.cs`

- [ ] **Step 1: Write the failing picker regressions**

```csharp
[Fact]
public void Show_WhenOpened_PositionsSearchListAndFooterImmediately() {
    ComponentAddDialog dialog = new ComponentAddDialog(CreateFont());
    EditorEntity entity = new EditorEntity();

    dialog.Show(entity, Array.Empty<EditorComponentAddDescriptor>());

    EditorEntity searchFieldHost = GetPrivateField<EditorEntity>(dialog, "SearchFieldHost");
    EditorEntity listHost = GetPrivateField<EditorEntity>(dialog, "ListHost");
    EditorEntity footerHost = GetPrivateField<EditorEntity>(dialog, "FooterHost");

    Assert.NotEqual(float3.Zero, searchFieldHost.LocalPosition);
    Assert.NotEqual(float3.Zero, listHost.LocalPosition);
    Assert.NotEqual(float3.Zero, footerHost.LocalPosition);
}

[Fact]
public void Show_WhenOpened_PositionsHierarchyAndFooterImmediately() {
    ReparentEntityDialog dialog = new ReparentEntityDialog(CreateFont(), EditorUiMetrics.Default);
    EditorEntity target = new EditorEntity { Name = "Player" };

    dialog.Show(target, new Entity[] { target });

    EditorEntity targetHost = GetPrivateField<EditorEntity>(dialog, "TargetHost");
    SceneHierarchyPickerView parentHierarchyView = GetPrivateField<SceneHierarchyPickerView>(dialog, "ParentHierarchyView");
    EditorEntity applyButtonHost = GetPrivateField<EditorEntity>(dialog, "ApplyButtonHost");

    Assert.NotEqual(float3.Zero, targetHost.LocalPosition);
    Assert.NotEqual(float3.Zero, parentHierarchyView.Entity.LocalPosition);
    Assert.NotEqual(float3.Zero, applyButtonHost.LocalPosition);
}
```

- [ ] **Step 2: Run the focused tests to verify they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ComponentAddDialogTests.Show_WhenOpened_PositionsSearchListAndFooterImmediately|FullyQualifiedName~ReparentEntityDialogTests.Show_WhenOpened_PositionsHierarchyAndFooterImmediately" -v minimal`

Expected: FAIL because the hosts still depend on a later `UpdateLayout(...)`.

- [ ] **Step 3: Apply the immediate-show contract to picker dialogs**

```csharp
public void Show(EditorEntity entity, IReadOnlyList<EditorComponentAddDescriptor> scriptDescriptors) {
    if (entity == null) {
        throw new ArgumentNullException(nameof(entity));
    }

    TargetEntity = entity;
    ScriptDescriptors.Clear();
    if (scriptDescriptors != null) {
        for (int i = 0; i < scriptDescriptors.Count; i++) {
            EditorComponentAddDescriptor descriptor = scriptDescriptors[i];
            if (descriptor != null) {
                ScriptDescriptors.Add(descriptor);
            }
        }
    }

    ResetDialogPositioning();
    ListScrollComponent.ResetScrollOffset();
    ResetActivationTracking();
    Enabled = true;
    SearchField.Text = string.Empty;
    RefreshAvailableComponents();
    ClearSelection();
    SearchField.IsFocused = true;
    ShowDialogImmediately();
}

public void Show(Entity targetEntity, IReadOnlyList<Entity> parentEntities) {
    if (targetEntity == null) {
        throw new ArgumentNullException(nameof(targetEntity));
    }
    if (parentEntities == null) {
        throw new ArgumentNullException(nameof(parentEntities));
    }

    ResetDialogPositioning();
    TargetEntity = targetEntity;
    SelectedParentEntity = targetEntity.Parent;
    StatusText.Text = string.Empty;
    TargetText.Text = GetEntityDisplayName(targetEntity);
    CopyAvailableParentEntities(parentEntities);
    ParentHierarchyView.Show(targetEntity, parentEntities, SelectedParentEntity);
    Enabled = true;
    ShowDialogImmediately();
}

protected override void HandleDialogLayoutChanged() {
    UpdateSearchLayout();
    UpdateListLayout();
    UpdateFooterLayout();
}

protected override void HandleDialogLayoutChanged() {
    LayoutTarget();
    LayoutParentHierarchy();
    LayoutStatus();
    LayoutFooter();
}
```

- [ ] **Step 4: Run the picker suites**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ComponentAddDialogTests|FullyQualifiedName~ReparentEntityDialogTests" -v minimal`

Expected: PASS for the new direct picker regressions and the reparent suite.

- [ ] **Step 5: Commit the picker modal sweep**

```bash
git add engine/helengine.editor/components/ui/ComponentAddDialog.cs \
        engine/helengine.editor/components/ui/ReparentEntityDialog.cs \
        engine/helengine.editor.tests/ComponentAddDialogTests.cs \
        engine/helengine.editor.tests/ReparentEntityDialogTests.cs
git commit -m "fix: align picker dialogs with modal lifecycle"
```

### Task 4: Host-Sized OpenFileDialog Immediate Show Path

**Files:**
- Modify: `engine/helengine.editor/components/ui/asset/OpenFileDialog.cs`
- Test: `engine/helengine.editor.tests/OpenFileDialogTests.cs`

- [ ] **Step 1: Write the failing host-sized dialog regression**

```csharp
[Fact]
public void Show_WhenOpened_PositionsBrowserStatusAndFooterImmediately() {
    OpenFileDialog dialog = new OpenFileDialog(CreateFont(), EditorUiMetrics.Default, ProjectPath);

    dialog.Show("scenes");

    AssetBrowserView browserView = GetPrivateField<AssetBrowserView>(dialog, "BrowserView");
    EditorEntity statusHost = GetPrivateField<EditorEntity>(dialog, "StatusHost");
    EditorEntity openButtonHost = GetPrivateField<EditorEntity>(dialog, "OpenButtonHost");

    Assert.NotEqual(float3.Zero, browserView.Entity.LocalPosition);
    Assert.NotEqual(float3.Zero, statusHost.LocalPosition);
    Assert.NotEqual(float3.Zero, openButtonHost.LocalPosition);
}
```

- [ ] **Step 2: Run the focused test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~OpenFileDialogTests.Show_WhenOpened_PositionsBrowserStatusAndFooterImmediately" -v minimal`

Expected: FAIL because the browser/status/footer placement still depends on `UpdateLayout(...)`.

- [ ] **Step 3: Add the host-aware immediate-show path**

```csharp
public void Show(string initialRelativeDirectory) {
    ResetDialogPositioning();
    SelectedEntry = null;
    LastActivatedTicks = 0;
    BrowserView.ClearSelection();
    StatusText.Text = string.Empty;
    Enabled = true;

    if (!BrowserView.TryNavigateTo(initialRelativeDirectory)) {
        if (!BrowserView.TryNavigateTo(SceneSavePathResolver.DefaultSceneDirectory)) {
            BrowserView.TryNavigateTo(string.Empty);
        }
    }

    BrowserView.RefreshEntries();
    ApplyImmediateShowState();
}

void ApplyImmediateShowState() {
    int safeHostWidth = Math.Max(1, DialogHostSize.X);
    int safeHostHeight = Math.Max(1, DialogHostSize.Y);
    if (!DialogIsUserPositioned) {
        int maxWidth = Math.Max(GetMinimumPanelWidthPixels(), safeHostWidth - GetPanelPaddingPixels() * 2);
        int maxHeight = Math.Max(GetMinimumPanelHeightPixels(), safeHostHeight - GetPanelPaddingPixels() * 2);
        int panelWidth = Math.Min(GetMaximumPanelWidthPixels(), Math.Min(maxWidth, safeHostWidth));
        int panelHeight = Math.Min(GetMaximumPanelHeightPixels(), Math.Min(maxHeight, safeHostHeight));
        SetDialogSize(panelWidth, panelHeight);
    }

    ShowDialogImmediately();
}

protected override void HandleDialogLayoutChanged() {
    PanelSize = DialogPanelBackground.Size;
    PanelPosition = DialogPanelPosition;
    LayoutBrowser();
    LayoutStatus();
    LayoutFooter();
}
```

- [ ] **Step 4: Run the OpenFileDialog suite**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~OpenFileDialogTests" -v minimal`

Expected: PASS for the new show-time regression and the existing open-file coverage.

- [ ] **Step 5: Commit the host-sized dialog sweep**

```bash
git add engine/helengine.editor/components/ui/asset/OpenFileDialog.cs \
        engine/helengine.editor.tests/OpenFileDialogTests.cs
git commit -m "fix: align open file dialog with modal lifecycle"
```

### Task 5: Broad Verification and Final Review

**Files:**
- Modify: `engine/helengine.editor/components/ui/BuildDialogCopySettingsDialog.cs`
- Modify: `engine/helengine.editor/components/ui/BuildSettingsDialog.cs`
- Modify: `engine/helengine.editor/components/ui/ComponentAddDialog.cs`
- Modify: `engine/helengine.editor/components/ui/EditorPreferencesDialog.cs`
- Modify: `engine/helengine.editor/components/ui/ProfilesDialog.cs`
- Modify: `engine/helengine.editor/components/ui/ReparentEntityDialog.cs`
- Modify: `engine/helengine.editor/components/ui/RemoveComponentDialog.cs`
- Modify: `engine/helengine.editor/components/ui/UnsavedChangesDialog.cs`
- Modify: `engine/helengine.editor/components/ui/asset/OpenFileDialog.cs`
- Test: `engine/helengine.editor.tests/BuildDialogCopySettingsDialogTests.cs`
- Test: `engine/helengine.editor.tests/BuildDialogTests.cs`
- Test: `engine/helengine.editor.tests/BuildSettingsDialogTests.cs`
- Test: `engine/helengine.editor.tests/ComponentAddDialogTests.cs`
- Test: `engine/helengine.editor.tests/EditorPreferencesDialogTests.cs`
- Test: `engine/helengine.editor.tests/OpenFileDialogTests.cs`
- Test: `engine/helengine.editor.tests/PlatformsDialogTests.cs`
- Test: `engine/helengine.editor.tests/ProfilesDialogTests.cs`
- Test: `engine/helengine.editor.tests/ReparentEntityDialogTests.cs`
- Test: `engine/helengine.editor.tests/RemoveComponentDialogTests.cs`
- Test: `engine/helengine.editor.tests/UnsavedChangesDialogTests.cs`

- [ ] **Step 1: Run the broad modal regression filters**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogCopySettingsDialogTests|FullyQualifiedName~BuildDialogTests|FullyQualifiedName~BuildSettingsDialogTests|FullyQualifiedName~ComponentAddDialogTests|FullyQualifiedName~EditorPreferencesDialogTests|FullyQualifiedName~OpenFileDialogTests|FullyQualifiedName~PlatformsDialogTests|FullyQualifiedName~ProfilesDialogTests|FullyQualifiedName~ReparentEntityDialogTests|FullyQualifiedName~RemoveComponentDialogTests|FullyQualifiedName~UnsavedChangesDialogTests.Show_WhenOpened_" -v minimal`

Expected: PASS for all touched dialog suites, with the command intentionally targeting only the new `UnsavedChangesDialog` show-time regression because the two unrelated resize assertions in that suite are known stale failures.

- [ ] **Step 2: Review the diff for accidental lifecycle drift**

Run: `git diff --stat HEAD~4..HEAD`

Expected: Only the direct `EditorDialogBase` modal files and their corresponding test files changed, plus the new `ComponentAddDialogTests.cs` file.

- [ ] **Step 3: Commit any final cleanup**

```bash
git add engine/helengine.editor/components/ui/BuildDialogCopySettingsDialog.cs \
        engine/helengine.editor/components/ui/BuildSettingsDialog.cs \
        engine/helengine.editor/components/ui/ComponentAddDialog.cs \
        engine/helengine.editor/components/ui/EditorPreferencesDialog.cs \
        engine/helengine.editor/components/ui/ProfilesDialog.cs \
        engine/helengine.editor/components/ui/ReparentEntityDialog.cs \
        engine/helengine.editor/components/ui/RemoveComponentDialog.cs \
        engine/helengine.editor/components/ui/UnsavedChangesDialog.cs \
        engine/helengine.editor/components/ui/asset/OpenFileDialog.cs \
        engine/helengine.editor.tests/BuildDialogCopySettingsDialogTests.cs \
        engine/helengine.editor.tests/BuildSettingsDialogTests.cs \
        engine/helengine.editor.tests/ComponentAddDialogTests.cs \
        engine/helengine.editor.tests/EditorPreferencesDialogTests.cs \
        engine/helengine.editor.tests/OpenFileDialogTests.cs \
        engine/helengine.editor.tests/ProfilesDialogTests.cs \
        engine/helengine.editor.tests/ReparentEntityDialogTests.cs \
        engine/helengine.editor.tests/RemoveComponentDialogTests.cs \
        engine/helengine.editor.tests/UnsavedChangesDialogTests.cs
git commit -m "fix: align editor dialogs with modal lifecycle"
```
