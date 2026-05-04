# Rounded Rect Corner Mask Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add corner-mask support to the shared rounded-rect primitive so tabs can render with rounded top corners and square bottom corners while keeping one shared radius and the existing one-draw-call UI path.

**Architecture:** Extend the core rounded-rect contract with a `[Flags]` corner mask and let the DirectX11 rounded-rect shader interpret that mask in the existing SDF path. `ButtonComponent` will translate its styling helpers into the shared mask state, and `TabComponent` will use top-corner rounding instead of square corners. When a masked shape is drawn through a non-SDF backend, route it through the SDF path so behavior stays correct without adding composed helper shapes or extra draw calls.

**Tech Stack:** C#/.NET 9, HLSL shader code in `helengine.directx11`, xUnit.

---

### Task 1: Add the shared corner-mask primitive and make the renderer honor it

**Files:**
- Create: `engine/helengine.core/model/RoundedRectCorners.cs`
- Modify: `engine/helengine.core/model/interfaces/IRoundedRectDrawable2D.cs`
- Modify: `engine/helengine.core/components/2d/RoundedRectComponent.cs`
- Modify: `engine/helengine.directx11/materials/UIShapeShaderData.cs`
- Modify: `engine/helengine.directx11/shaders/UIShapeShader.fx`
- Modify: `engine/helengine.directx11/DirectX11Renderer2D.cs`
- Create: `engine/helengine.editor.tests/RoundedRectComponentTests.cs`
- Modify: `engine/helengine.editor.tests/TabComponentTests.cs`

- [ ] **Step 1: Write the failing test**

Add a new rounded-rect test that expects the shared primitive to expose corner selection, plus a tab test that expects tabs to request top-only rounding.

```csharp
using helengine;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the shared rounded-rect primitive exposes maskable corner selection.
    /// </summary>
    public sealed class RoundedRectComponentTests {
        /// <summary>
        /// Ensures the default rounded rectangle keeps all corners enabled and square-corner helpers clear the mask.
        /// </summary>
        [Fact]
        public void Constructor_UsesAllCorners_AndSquareCornersClearTheMask() {
            RoundedRectComponent shape = new RoundedRectComponent();

            Assert.Equal(RoundedRectCorners.All, shape.Corners);

            shape.Corners = RoundedRectCorners.None;

            Assert.Equal(RoundedRectCorners.None, shape.Corners);
        }
    }
}
```

```csharp
using helengine;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the dedicated tab wrapper keeps tab-style defaults centralized.
    /// </summary>
    public sealed class TabComponentTests {
        /// <summary>
        /// Ensures tabs request top-corner rounding instead of square corners.
        /// </summary>
        [Fact]
        public void Constructor_UsesTopCornersAndTracksSelectionState() {
            TabComponent tab = new TabComponent("Windows", new int2(96, 24), null, null);

            Assert.Equal(RoundedRectCorners.TopLeft | RoundedRectCorners.TopRight, tab.Corners);
            Assert.False(tab.IsSelected);
            Assert.False(tab.IsKeyboardFocused);

            tab.SetSelected(true);

            Assert.True(tab.IsSelected);
            Assert.True(tab.IsKeyboardFocused);
        }
    }
}
```

- [ ] **Step 2: Run the failing tests**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -nologo --filter "FullyQualifiedName~RoundedRectComponentTests|FullyQualifiedName~TabComponentTests"
```

Expected: fail because `RoundedRectCorners` and the new `Corners` property do not exist yet, and `TabComponent` still uses square corners.

- [ ] **Step 3: Write the minimal implementation**

Add the shared mask enum and plumb it through the interface, component, shader data, shader, and renderer.

```csharp
namespace helengine {
    /// <summary>
    /// Describes which corners of a rounded rectangle should remain rounded.
    /// </summary>
    [Flags]
    public enum RoundedRectCorners {
        /// <summary>
        /// No corners are rounded.
        /// </summary>
        None = 0,

        /// <summary>
        /// The top-left corner is rounded.
        /// </summary>
        TopLeft = 1,

        /// <summary>
        /// The top-right corner is rounded.
        /// </summary>
        TopRight = 2,

        /// <summary>
        /// The bottom-left corner is rounded.
        /// </summary>
        BottomLeft = 4,

        /// <summary>
        /// The bottom-right corner is rounded.
        /// </summary>
        BottomRight = 8,

        /// <summary>
        /// All corners are rounded.
        /// </summary>
        All = TopLeft | TopRight | BottomLeft | BottomRight
    }
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Describes a 2D rounded rectangle drawable.
    /// </summary>
    public interface IRoundedRectDrawable2D : IDrawable2D {
        /// <summary>
        /// Gets or sets the rounded corners that should remain active on the rectangle.
        /// </summary>
        RoundedRectCorners Corners { get; set; }

        /// <summary>
        /// Gets or sets the fill color applied to the rounded rectangle.
        /// </summary>
        byte4 FillColor { get; set; }

        /// <summary>
        /// Gets or sets the border color applied to the rounded rectangle.
        /// </summary>
        byte4 BorderColor { get; set; }

        /// <summary>
        /// Gets or sets the dimensions of the rectangle.
        /// </summary>
        int2 Size { get; set; }

        /// <summary>
        /// Gets or sets the radius of the rounded corners.
        /// </summary>
        float Radius { get; set; }

        /// <summary>
        /// Gets or sets the outline thickness.
        /// </summary>
        float BorderThickness { get; set; }

        /// <summary>
        /// Gets or sets the texture color modulation.
        /// </summary>
        byte4 Color { get; set; }

        /// <summary>
        /// Gets or sets the texture source rectangle.
        /// </summary>
        float4 SourceRect { get; set; }

        /// <summary>
        /// Gets or sets the rotation applied to the rectangle.
        /// </summary>
        float Rotation { get; set; }
    }
}
```

The shader change should keep the same fill/border pass and just branch on the corner mask in the existing SDF helper:

```hlsl
float sdRoundedRectMasked(float2 p, float2 halfSize, float r, uint corners)
{
    float2 ap = abs(p);
    float2 inner = halfSize - r;
    float2 d = ap - halfSize;
    float baseDist = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);

    if (r <= 0.0)
    {
        return baseDist;
    }

    if (ap.x <= inner.x || ap.y <= inner.y)
    {
        return baseDist;
    }

    uint cornerBit = 0;
    if (p.x < 0.0 && p.y >= 0.0)
    {
        cornerBit = 1u;
    }
    else if (p.x >= 0.0 && p.y >= 0.0)
    {
        cornerBit = 2u;
    }
    else if (p.x < 0.0 && p.y < 0.0)
    {
        cornerBit = 4u;
    }
    else
    {
        cornerBit = 8u;
    }

    if ((corners & cornerBit) == 0u)
    {
        return baseDist;
    }

    return length(ap - inner) - r;
}
```

`DirectX11Renderer2D.DrawRoundedRectSdf` should pass the mask through `UIShapeShaderData.params1.w`, and `DrawRoundedRect` should route any masked shape through the SDF path when a non-SDF backend is active so tabs and section headers still render correctly without extra geometry objects.

- [ ] **Step 4: Run the tests and verify they pass**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -nologo --filter "FullyQualifiedName~RoundedRectComponentTests|FullyQualifiedName~TabComponentTests"
```

Expected: pass once the enum, component property, and shader-backed rendering path are in place.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.core/model/RoundedRectCorners.cs engine/helengine.core/model/interfaces/IRoundedRectDrawable2D.cs engine/helengine.core/components/2d/RoundedRectComponent.cs engine/helengine.directx11/materials/UIShapeShaderData.cs engine/helengine.directx11/shaders/UIShapeShader.fx engine/helengine.directx11/DirectX11Renderer2D.cs engine/helengine.editor.tests/RoundedRectComponentTests.cs engine/helengine.editor.tests/TabComponentTests.cs
git commit -m "feat: add rounded rect corner masks"
```

### Task 2: Update button-style callers to use the shared corner-mask API

**Files:**
- Modify: `engine/helengine.core/components/2d/interactable/ButtonComponent.cs`
- Modify: `engine/helengine.core/components/2d/interactable/TabComponent.cs`
- Modify: `engine/helengine.editor/components/ui/BuildDialog.cs`
- Modify: `engine/helengine.editor/components/ui/AssetImportSettingsView.cs`
- Modify: `engine/helengine.editor.tests/BuildDialogTests.cs`

- [ ] **Step 1: Write the failing test**

Update the build-dialog tab test so it expects the tab wrapper to request top corners rather than square corners.

```csharp
List<TabComponent> platformTabs = GetPrivateField<List<TabComponent>>(dialog, "PlatformTabs");

Assert.All(platformTabs, tab => Assert.Equal(RoundedRectCorners.TopLeft | RoundedRectCorners.TopRight, tab.Corners));
Assert.True(platformTabs[0].IsSelected);
Assert.False(platformTabs[1].IsSelected);
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -nologo --filter "FullyQualifiedName~BuildDialogTests"
```

Expected: fail until `TabComponent` and `ButtonComponent` use the shared mask helpers.

- [ ] **Step 3: Write the minimal implementation**

Add helper methods on `ButtonComponent` so callers can choose full rounding, top-only rounding, or square corners. `TabComponent` should call the top-corner helper in its constructor, and the build-dialog and asset-import settings callers should continue to instantiate `TabComponent` without any manual corner styling.

```csharp
public void UseTopCorners() {
    CornerRadius = (float)(Math.Min((double)size.X, (double)size.Y) * 0.15d);
    Corners = RoundedRectCorners.TopLeft | RoundedRectCorners.TopRight;

    if (roundedRect != null) {
        roundedRect.Corners = Corners;
        roundedRect.Radius = CornerRadius;
    }
}
```

```csharp
public TabComponent(
    string text,
    int2 size,
    FontAsset font,
    Action onClickAction = null,
    float borderThickness = 2f) : base(text, size, font, onClickAction, borderThickness) {
    UseTopCorners();
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -nologo --filter "FullyQualifiedName~BuildDialogTests|FullyQualifiedName~TabComponentTests|FullyQualifiedName~RoundedRectComponentTests"
```

Expected: pass, with build-dialog tabs now reading as top-rounded tabs while the rest of the dialog continues using the shared rounded-rect primitive.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.core/components/2d/interactable/ButtonComponent.cs engine/helengine.core/components/2d/interactable/TabComponent.cs engine/helengine.editor/components/ui/BuildDialog.cs engine/helengine.editor/components/ui/AssetImportSettingsView.cs engine/helengine.editor.tests/BuildDialogTests.cs
git commit -m "feat: use shared corner masks for tabs"
```
