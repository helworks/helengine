# Modal Content Root Contract Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Introduce a shared modal content-root contract in `EditorDialogBase` and migrate `PlatformsDialog` so its checkbox rows render immediately under a dedicated modal-owned subtree instead of leaking into global editor space.

**Architecture:** Add one dedicated content root under the modal panel shell and one shared show-time visible-state helper in `EditorDialogBase`. Migrate `PlatformsDialog` so all dialog-owned content is parented under that root and laid out through one shared content-layout hook that runs both on `Show(...)` and later host resizes.

**Tech Stack:** C#/.NET 9, xUnit, helengine editor UI entities/components, `EditorDialogBase`, `PlatformsDialog`

---

### Task 1: Lock the Platforms Dialog Failure in Tests

**Files:**
- Modify: `engine/helengine.editor.tests/PlatformsDialogTests.cs`
- Test: `engine/helengine.editor.tests/PlatformsDialogTests.cs`

- [ ] **Step 1: Write the failing immediate-show ownership and layout regressions**

Add these tests to `engine/helengine.editor.tests/PlatformsDialogTests.cs`:

```csharp
/// <summary>
/// Ensures platform rows are parented under the modal content root and positioned immediately during Show.
/// </summary>
[Fact]
public void Show_WhenOpened_ParentsPlatformRowsUnderDialogContentRootAndLaysThemOutImmediately() {
    PlatformsDialog dialog = new PlatformsDialog(CreateFont());

    dialog.Show(
        new[] { "windows", "ps2", "linux" },
        new[] { "windows", "ps2" },
        "ps2");

    EditorEntity dialogContentRoot = GetProtectedProperty<EditorEntity>(dialog, "DialogContentRoot");
    List<EditorEntity> platformCheckBoxHosts = GetPrivateField<List<EditorEntity>>(dialog, "PlatformCheckBoxHosts");
    List<EditorEntity> platformLabelHosts = GetPrivateField<List<EditorEntity>>(dialog, "PlatformLabelHosts");
    EditorEntity platformsLabelHost = GetPrivateField<EditorEntity>(dialog, "PlatformsLabelHost");
    EditorEntity activePlatformComboBoxHost = GetPrivateField<EditorEntity>(dialog, "ActivePlatformComboBoxHost");

    Assert.Equal(3, platformCheckBoxHosts.Count);
    Assert.Equal(3, platformLabelHosts.Count);
    Assert.Same(dialogContentRoot, platformsLabelHost.Parent);
    Assert.Same(dialogContentRoot, activePlatformComboBoxHost.Parent);
    Assert.All(platformCheckBoxHosts, host => Assert.Same(dialogContentRoot, host.Parent));
    Assert.All(platformLabelHosts, host => Assert.Same(dialogContentRoot, host.Parent));
    Assert.All(platformCheckBoxHosts, host => Assert.True(host.LocalPosition.Y > 0f));
    Assert.All(platformLabelHosts, host => Assert.True(host.LocalPosition.Y > 0f));
}

/// <summary>
/// Ensures platform rows do not require a later UpdateLayout pass to leave origin coordinates.
/// </summary>
[Fact]
public void Show_WhenOpened_DoesNotLeavePlatformRowsAtDefaultOriginUntilLaterLayout() {
    PlatformsDialog dialog = new PlatformsDialog(CreateFont());

    dialog.Show(
        new[] { "windows", "ps2" },
        new[] { "windows" },
        "windows");

    List<EditorEntity> platformCheckBoxHosts = GetPrivateField<List<EditorEntity>>(dialog, "PlatformCheckBoxHosts");
    List<EditorEntity> platformLabelHosts = GetPrivateField<List<EditorEntity>>(dialog, "PlatformLabelHosts");

    Assert.All(platformCheckBoxHosts, host => Assert.NotEqual(float3.Zero, host.LocalPosition));
    Assert.All(platformLabelHosts, host => Assert.NotEqual(float3.Zero, host.LocalPosition));
}
```

Add this helper near the existing reflection helpers in the same file:

```csharp
/// <summary>
/// Reads one inherited non-public or protected instance property and casts it to the requested type.
/// </summary>
/// <typeparam name="T">Expected property type.</typeparam>
/// <param name="target">Object that owns the property.</param>
/// <param name="propertyName">Name of the property to read.</param>
/// <returns>Property value cast to the requested type.</returns>
T GetProtectedProperty<T>(object target, string propertyName) {
    Type currentType = target.GetType();

    while (currentType != null) {
        PropertyInfo property = currentType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (property != null) {
            return Assert.IsType<T>(property.GetValue(target));
        }

        currentType = currentType.BaseType;
    }

    throw new InvalidOperationException($"Property '{propertyName}' was not found on type '{target.GetType().FullName}'.");
}
```

- [ ] **Step 2: Run the Platforms dialog tests to verify the new regressions fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~PlatformsDialogTests"
```

Expected: FAIL because `DialogContentRoot` does not exist yet and/or the platform row hosts still remain at default positions until a later `UpdateLayout(...)` pass.

- [ ] **Step 3: Commit the red tests**

```bash
git add engine/helengine.editor.tests/PlatformsDialogTests.cs
git commit -m "test: cover immediate platforms dialog modal ownership"
```

### Task 2: Add the Shared Modal Content Root Contract in `EditorDialogBase`

**Files:**
- Modify: `engine/helengine.editor/components/ui/EditorDialogBase.cs`
- Test: `engine/helengine.editor.tests/PlatformsDialogTests.cs`

- [ ] **Step 1: Add the dedicated dialog content root and a protected accessor**

Update `engine/helengine.editor/components/ui/EditorDialogBase.cs` to add a new content-root field and accessor separate from shared chrome:

```csharp
/// <summary>
/// Root entity that owns dialog-specific content beneath the shared panel shell.
/// </summary>
readonly EditorEntity ContentRoot;
```

Construct it under `PanelRoot` after the shared chrome is created:

```csharp
ContentRoot = new EditorEntity {
    LayerMask = LayerMask,
    Position = float3.Zero,
    InternalEntity = true
};
PanelRoot.AddChild(ContentRoot);
```

Expose it to derived dialogs:

```csharp
/// <summary>
/// Gets the root entity that owns dialog-specific content beneath the shared shell.
/// </summary>
protected EditorEntity DialogContentRoot => ContentRoot;
```

- [ ] **Step 2: Add a shared show-time helper that leaves the shell render-ready immediately**

Add this helper to `EditorDialogBase.cs`:

```csharp
/// <summary>
/// Applies the visible dialog shell state immediately after a dialog becomes visible.
/// </summary>
protected void ShowDialogImmediately() {
    CenterDialogIfNeeded();
    ApplyVisibleDialogState();
}
```

Do not remove the existing `HandleDialogLayoutChanged()` hook. Keep `ApplyVisibleDialogState()` as the single path that invokes shell layout and then the derived content-layout hook.

- [ ] **Step 3: Run the Platforms dialog tests again**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~PlatformsDialogTests"
```

Expected: still FAIL, but now the failures should be limited to `PlatformsDialog` still parenting content under `DialogPanelRoot` and still not laying rows out during `Show(...)`.

- [ ] **Step 4: Commit the base contract change**

```bash
git add engine/helengine.editor/components/ui/EditorDialogBase.cs
git commit -m "refactor: add shared modal content root contract"
```

### Task 3: Migrate `PlatformsDialog` to the Shared Modal Content Root

**Files:**
- Modify: `engine/helengine.editor/components/ui/PlatformsDialog.cs`
- Test: `engine/helengine.editor.tests/PlatformsDialogTests.cs`

- [ ] **Step 1: Reparent dialog-owned static content under `DialogContentRoot`**

In `engine/helengine.editor/components/ui/PlatformsDialog.cs`, move the static dialog-owned controls to the shared content root:

```csharp
PlatformsLabelHost = CreateInternalHost();
DialogContentRoot.AddChild(PlatformsLabelHost);

ActivePlatformLabelHost = CreateInternalHost();
DialogContentRoot.AddChild(ActivePlatformLabelHost);

ActivePlatformComboBoxHost = CreateInternalHost();
DialogContentRoot.AddChild(ActivePlatformComboBoxHost);

StatusHost = CreateInternalHost();
DialogContentRoot.AddChild(StatusHost);

CancelButtonHost = CreateInternalHost();
DialogContentRoot.AddChild(CancelButtonHost);

SaveButtonHost = CreateInternalHost();
DialogContentRoot.AddChild(SaveButtonHost);
```

Do not move shared shell chrome such as the panel background, header, or close button.

- [ ] **Step 2: Reparent dynamic platform rows under `DialogContentRoot`**

Update `CreatePlatformRow(...)` and `ClearPlatformRows()` so the dynamic rows are owned by the dedicated content root:

```csharp
EditorEntity checkBoxHost = CreateInternalHost();
DialogContentRoot.AddChild(checkBoxHost);
PlatformCheckBoxHosts.Add(checkBoxHost);

CheckBoxComponent checkBox = new CheckBoxComponent(GetPlatformCheckBoxSize(), DialogFont, isChecked);
checkBox.CheckedChanged += HandlePlatformCheckBoxChanged;
checkBox.SetRenderOrders(DialogTextOrder, DialogTextOrder);
checkBoxHost.AddComponent(checkBox);
PlatformCheckBoxes.Add(checkBox);

EditorEntity labelHost = CreateInternalHost();
DialogContentRoot.AddChild(labelHost);
PlatformLabelHosts.Add(labelHost);
```

And during cleanup:

```csharp
for (int index = 0; index < PlatformLabelHosts.Count; index++) {
    DialogContentRoot.RemoveChild(PlatformLabelHosts[index]);
}
for (int index = 0; index < PlatformCheckBoxHosts.Count; index++) {
    DialogContentRoot.RemoveChild(PlatformCheckBoxHosts[index]);
}
```

- [ ] **Step 3: Make `Show(...)` apply visible shell state and content layout immediately**

Update `Show(...)` so it becomes render-ready before returning:

```csharp
public void Show(IReadOnlyList<string> availablePlatformIds, IReadOnlyList<string> supportedPlatformIds, string activePlatformId) {
    if (availablePlatformIds == null) {
        throw new ArgumentNullException(nameof(availablePlatformIds));
    }
    if (supportedPlatformIds == null) {
        throw new ArgumentNullException(nameof(supportedPlatformIds));
    }
    if (string.IsNullOrWhiteSpace(activePlatformId)) {
        throw new ArgumentException("Active platform id must be provided.", nameof(activePlatformId));
    }

    ResetDialogPositioning();
    Enabled = true;
    StatusText.Text = string.Empty;

    RebuildPlatformRows(availablePlatformIds, supportedPlatformIds);
    RebuildActivePlatformItems(activePlatformId);
    ShowDialogImmediately();
}
```

Override the base content-layout hook so both `ShowDialogImmediately()` and later `UpdateLayout(...)` share the same content path:

```csharp
/// <summary>
/// Reapplies Platforms dialog content layout after the shared shell layout changes.
/// </summary>
protected override void HandleDialogLayoutChanged() {
    LayoutContent();
}
```

Then simplify `UpdateLayout(...)` so it relies on the shared hook:

```csharp
public void UpdateLayout(int windowWidth, int windowHeight) {
    if (!IsInitialized) {
        return;
    }
    if (!UpdateDialogFrame(windowWidth, windowHeight)) {
        return;
    }
}
```

- [ ] **Step 4: Run the Platforms dialog tests to verify they pass**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~PlatformsDialogTests"
```

Expected: PASS. The new tests should prove immediate layout and owned parenting, while the existing active-platform validation test should remain green.

- [ ] **Step 5: Commit the Platforms dialog migration**

```bash
git add engine/helengine.editor/components/ui/PlatformsDialog.cs engine/helengine.editor.tests/PlatformsDialogTests.cs
git commit -m "fix: parent platforms dialog content under modal root"
```

### Task 4: Verify Backward Compatibility for Existing Modal Behavior

**Files:**
- Modify: none
- Test: `engine/helengine.editor.tests/BuildSettingsDialogTests.cs`
- Test: `engine/helengine.editor.tests/BuildDialogTests.cs`

- [ ] **Step 1: Run focused modal regression suites**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildSettingsDialogTests|FullyQualifiedName~BuildDialogTests"
```

Expected: PASS. `BuildSettingsDialog` should remain backward-compatible even though it still uses `DialogPanelRoot`, and `BuildDialog` should keep its existing modal layout behavior.

- [ ] **Step 2: Review git diff for scope**

Run:

```bash
git diff -- engine/helengine.editor/components/ui/EditorDialogBase.cs engine/helengine.editor/components/ui/PlatformsDialog.cs engine/helengine.editor.tests/PlatformsDialogTests.cs
```

Expected: the diff is limited to the shared content-root contract, the `PlatformsDialog` migration, and the new regressions.

- [ ] **Step 3: Commit the verification checkpoint**

```bash
git commit --allow-empty -m "chore: verify modal content root contract regressions"
```

## Self-Review Checklist

- Spec coverage:
  - Shared modal content root contract: Task 2
  - Immediate show-time render readiness: Task 3
  - `PlatformsDialog` migration: Task 3
  - Regression coverage for ownership and immediate layout: Task 1 and Task 3
  - Backward compatibility against existing modals: Task 4

- Placeholder scan:
  - No `TODO`, `TBD`, or “appropriate handling” placeholders remain.
  - Every code-changing step includes concrete code snippets.
  - Every verification step includes an exact command and expected outcome.

- Type consistency:
  - Base accessor name is `DialogContentRoot`.
  - Base show-time helper name is `ShowDialogImmediately()`.
  - Derived hook remains `HandleDialogLayoutChanged()`.
  - `PlatformsDialog` methods referenced here match existing method names: `Show(...)`, `UpdateLayout(...)`, `LayoutContent()`, `CreatePlatformRow(...)`, `ClearPlatformRows()`.
