# FPS Component Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a reusable runtime `FPSComponent` that builds its own text overlay and shows update FPS plus render FPS in the top-left corner of the viewport.

**Architecture:** The component will live in `helengine.core` alongside the other runtime UI components. It will create a child entity hierarchy in `ComponentAdded`, host two `TextComponent` instances, and refresh their content on a fixed interval using frame counters that are incremented from the engine update and draw paths. The overlay should rely on the normal entity hierarchy for enable/disable behavior so removing or disabling the parent automatically removes the text drawables from render participation.

**Tech Stack:** C# / .NET 9, xUnit, existing helengine runtime component patterns, existing test render managers.

---

## Task 1: Add failing runtime tests for the FPS overlay

**Files:**
- Create: `engine/helengine.editor.tests/FPSComponentTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the runtime FPS overlay component builds and updates its own text hierarchy.
    /// </summary>
    public class FPSComponentTests : IDisposable {
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the runtime services required by the component tests.
        /// </summary>
        public FPSComponentTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-fps-component-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputManager());
        }

        /// <summary>
        /// Deletes temporary test content after each run.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the component creates a top-left overlay host with two text children.
        /// </summary>
        [Fact]
        public void ComponentAdded_WhenAttached_BuildsTwoTextChildrenAtTopLeft() {
            Entity entity = new Entity();
            FontAsset font = CreateFont();
            FPSComponent fps = new FPSComponent(font);

            entity.AddComponent(fps);

            Entity overlayHost = Assert.Single(entity.Children);
            Assert.Equal(new float3(8f, 6f, 0f), overlayHost.LocalPosition);
            Assert.Equal(2, overlayHost.Children.Count);

            Entity updateRow = overlayHost.Children[0];
            Entity renderRow = overlayHost.Children[1];

            TextComponent updateText = Assert.Single(updateRow.Components.OfType<TextComponent>());
            TextComponent renderText = Assert.Single(renderRow.Components.OfType<TextComponent>());

            Assert.Equal("Update FPS: --", updateText.Text);
            Assert.Equal("Render FPS: --", renderText.Text);
            Assert.Equal(0f, updateRow.LocalPosition.Y);
            Assert.Equal(font.LineHeight, renderRow.LocalPosition.Y);
        }

        /// <summary>
        /// Ensures update and render frame ticks refresh both visible lines.
        /// </summary>
        [Fact]
        public void CoreUpdateAndDraw_WhenFrameTicksAdvance_RefreshesBothTextLines() {
            Entity entity = new Entity();
            FPSComponent fps = new FPSComponent(CreateFont()) {
                RefreshIntervalSeconds = 0d
            };

            entity.AddComponent(fps);
            Core.Instance.Update();
            Core.Instance.Draw();
            Core.Instance.Update();
            Core.Instance.Draw();

            Entity overlayHost = Assert.Single(entity.Children);
            TextComponent updateText = Assert.Single(overlayHost.Children[0].Components.OfType<TextComponent>());
            TextComponent renderText = Assert.Single(overlayHost.Children[1].Components.OfType<TextComponent>());

            Assert.StartsWith("Update FPS:", updateText.Text);
            Assert.StartsWith("Render FPS:", renderText.Text);
            Assert.NotEqual("Update FPS: --", updateText.Text);
            Assert.NotEqual("Render FPS: --", renderText.Text);
        }

        /// <summary>
        /// Ensures disabling the parent entity removes the text drawables from render participation.
        /// </summary>
        [Fact]
        public void ParentEntity_WhenDisabled_RemovesOverlayDrawablesFromRenderLists() {
            Entity entity = new Entity();
            FPSComponent fps = new FPSComponent(CreateFont());

            entity.AddComponent(fps);

            Entity overlayHost = Assert.Single(entity.Children);
            TextComponent updateText = Assert.Single(overlayHost.Children[0].Components.OfType<TextComponent>());
            TextComponent renderText = Assert.Single(overlayHost.Children[1].Components.OfType<TextComponent>());

            Assert.Contains(updateText, Core.Instance.ObjectManager.Drawables2D);
            Assert.Contains(renderText, Core.Instance.ObjectManager.Drawables2D);

            entity.Enabled = false;

            Assert.DoesNotContain(updateText, Core.Instance.ObjectManager.Drawables2D);
            Assert.DoesNotContain(renderText, Core.Instance.ObjectManager.Drawables2D);
            Assert.False(overlayHost.IsHierarchyEnabled);
        }

        /// <summary>
        /// Creates a deterministic font asset containing the glyphs needed by the overlay labels.
        /// </summary>
        /// <returns>Font asset with stable glyph metrics for the tests.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['U'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['R'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                [':'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                [' '] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['-'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['0'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['1'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['2'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['3'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['4'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['5'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['6'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['7'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['8'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['9'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['.'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                16f,
                64,
                64);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~FPSComponentTests" -v minimal`

Expected: compile or test failure because `FPSComponent` and its runtime wiring do not exist yet.

- [ ] **Step 3: Keep the test file as the contract for the runtime shape**

No code changes in this step. Review the assertions and keep the implementation aligned with these exact expectations:

```csharp
Assert.Equal("Update FPS: --", updateText.Text);
Assert.Equal("Render FPS: --", renderText.Text);
Assert.Contains(updateText, Core.Instance.ObjectManager.Drawables2D);
Assert.DoesNotContain(updateText, Core.Instance.ObjectManager.Drawables2D);
```

- [ ] **Step 4: Commit the test-only checkpoint if you stop here**

```bash
git add engine/helengine.editor.tests/FPSComponentTests.cs
git commit -m "Add FPS component runtime tests"
```

---

## Task 2: Implement the runtime FPS component and the core frame hooks

**Files:**
- Create: `engine/helengine.core/components/2d/FPSComponent.cs`
- Modify: `engine/helengine.core/Core.cs:158-172`

- [ ] **Step 1: Write the minimal implementation against the failing tests**

```csharp
namespace helengine {
    /// <summary>
    /// Renders a reusable two-line FPS overlay using the core 2D text pipeline.
    /// </summary>
    public class FPSComponent : UpdateComponent {
        static readonly List<FPSComponent> ActiveComponents = new List<FPSComponent>();

        readonly FontAsset Font;
        Entity OverlayHost;
        Entity UpdateRowHost;
        Entity RenderRowHost;
        TextComponent UpdateTextComponent;
        TextComponent RenderTextComponent;
        DateTime LastSampleUtc;
        int UpdateFrameCount;
        int RenderFrameCount;
        bool Initialized;

        /// <summary>
        /// Gets or sets the sampling interval used before refreshing the visible FPS values.
        /// </summary>
        public double RefreshIntervalSeconds { get; set; } = 0.5d;

        /// <summary>
        /// Gets or sets the overlay padding applied from the top-left viewport edge.
        /// </summary>
        public int2 Padding { get; set; } = new int2(8, 6);

        /// <summary>
        /// Gets or sets the render order used by the overlay text.
        /// </summary>
        public byte RenderOrder2D { get; set; } = 250;

        /// <summary>
        /// Gets the last formatted update-FPS line.
        /// </summary>
        public string UpdateFpsText { get; private set; }

        /// <summary>
        /// Gets the last formatted render-FPS line.
        /// </summary>
        public string RenderFpsText { get; private set; }

        /// <summary>
        /// Creates a new FPS overlay that renders with the provided font.
        /// </summary>
        /// <param name="font">Font used for both overlay lines.</param>
        public FPSComponent(FontAsset font) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            Font = font;
        }

        /// <summary>
        /// Builds the overlay entity hierarchy and registers the component for sampling.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            if (Initialized) {
                return;
            }

            Initialized = true;
            LastSampleUtc = DateTime.UtcNow;
            UpdateFpsText = "Update FPS: --";
            RenderFpsText = "Render FPS: --";

            entity.InitChildren();

            OverlayHost = new Entity();
            OverlayHost.LayerMask = entity.LayerMask;
            OverlayHost.InitChildren();
            OverlayHost.InitComponents();
            OverlayHost.LocalPosition = new float3(Padding.X, Padding.Y, 0f);
            entity.AddChild(OverlayHost);

            UpdateRowHost = new Entity();
            UpdateRowHost.LayerMask = entity.LayerMask;
            UpdateRowHost.InitComponents();
            UpdateRowHost.LocalPosition = new float3(0f, 0f, 0.1f);
            OverlayHost.AddChild(UpdateRowHost);

            UpdateTextComponent = new TextComponent();
            UpdateTextComponent.Font = Font;
            UpdateTextComponent.Color = new byte4(255, 255, 255, 255);
            UpdateTextComponent.RenderOrder2D = RenderOrder2D;
            UpdateTextComponent.Text = UpdateFpsText;
            UpdateRowHost.AddComponent(UpdateTextComponent);

            RenderRowHost = new Entity();
            RenderRowHost.LayerMask = entity.LayerMask;
            RenderRowHost.InitComponents();
            RenderRowHost.LocalPosition = new float3(0f, Font.LineHeight, 0.1f);
            OverlayHost.AddChild(RenderRowHost);

            RenderTextComponent = new TextComponent();
            RenderTextComponent.Font = Font;
            RenderTextComponent.Color = new byte4(255, 255, 255, 255);
            RenderTextComponent.RenderOrder2D = RenderOrder2D;
            RenderTextComponent.Text = RenderFpsText;
            RenderRowHost.AddComponent(RenderTextComponent);

            ActiveComponents.Add(this);
        }

        /// <summary>
        /// Removes the component from the active FPS registry before the entity is detached.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentRemoved(Entity entity) {
            ActiveComponents.Remove(this);
            Initialized = false;
            base.ComponentRemoved(entity);
        }

        /// <summary>
        /// Samples the current frame rate window and refreshes both visible lines when the interval has elapsed.
        /// </summary>
        public override void Update() {
            if (!Initialized) {
                throw new InvalidOperationException("FPSComponent must be added to an entity before it can sample frames.");
            }

            double elapsedSeconds = (DateTime.UtcNow - LastSampleUtc).TotalSeconds;
            if (RefreshIntervalSeconds > 0d && elapsedSeconds < RefreshIntervalSeconds) {
                return;
            }

            double safeElapsedSeconds = elapsedSeconds <= 0d ? 1d : elapsedSeconds;
            double updateFps = UpdateFrameCount / safeElapsedSeconds;
            double renderFps = RenderFrameCount / safeElapsedSeconds;

            UpdateFpsText = string.Format(CultureInfo.InvariantCulture, "Update FPS: {0:0.0}", updateFps);
            RenderFpsText = string.Format(CultureInfo.InvariantCulture, "Render FPS: {0:0.0}", renderFps);

            UpdateTextComponent.Text = UpdateFpsText;
            RenderTextComponent.Text = RenderFpsText;

            UpdateFrameCount = 0;
            RenderFrameCount = 0;
            LastSampleUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Records one update tick for every live FPS overlay.
        /// </summary>
        public static void RecordUpdateFrame() {
            for (int i = 0; i < ActiveComponents.Count; i++) {
                ActiveComponents[i].UpdateFrameCount++;
            }
        }

        /// <summary>
        /// Records one render tick for every live FPS overlay.
        /// </summary>
        public static void RecordRenderFrame() {
            for (int i = 0; i < ActiveComponents.Count; i++) {
                ActiveComponents[i].RenderFrameCount++;
            }
        }
    }
}
```

```csharp
public virtual void Update() {
    InputManager.EarlyUpdate();
    ObjectManager.Update();
    InputManager.Update();
    FPSComponent.RecordUpdateFrame();
}

public virtual void Draw() {
    RenderManager3D.Draw();
    FPSComponent.RecordRenderFrame();
}
```

- [ ] **Step 2: Run the focused test to verify the implementation passes**

Run: `dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~FPSComponentTests" -v minimal`

Expected: PASS with the three FPS overlay tests green.

- [ ] **Step 3: Keep the runtime surface aligned with the spec**

Review the implementation against the design and keep these requirements intact:

```csharp
Assert.Equal("Update FPS: --", UpdateFpsText);
Assert.Equal("Render FPS: --", RenderFpsText);
Assert.DoesNotContain(updateText, Core.Instance.ObjectManager.Drawables2D);
```

- [ ] **Step 4: Commit the runtime change if you stop here**

```bash
git add engine/helengine.core/components/2d/FPSComponent.cs engine/helengine.core/Core.cs
git commit -m "Add runtime FPS overlay component"
```

---

## Task 3: Verify the overlay behaves correctly in the broader runtime test set

**Files:**
- Modify: `engine/helengine.editor.tests/FPSComponentTests.cs` if the initial assertions need any tightening after the implementation lands

- [ ] **Step 1: Run the focused FPS component tests again**

Run: `dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~FPSComponentTests" -v minimal`

Expected: PASS.

- [ ] **Step 2: Run the adjacent runtime component tests that share the same entity and text wiring**

Run: `dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~TextBoxComponentKeyboardFocusTests|FullyQualifiedName~EntityHierarchyEnabledStateTests|FullyQualifiedName~RenderOrder2DStackTests" -v minimal`

Expected: PASS.

- [ ] **Step 3: Run a core build check**

Run: `dotnet build /mnt/c/dev/helworks/helengine/engine/helengine.core/helengine.core.csproj -v minimal`

Expected: PASS with no new compile errors from the FPS overlay.

- [ ] **Step 4: Commit the finished slice**

```bash
git add engine/helengine.core/components/2d/FPSComponent.cs engine/helengine.core/Core.cs engine/helengine.editor.tests/FPSComponentTests.cs
git commit -m "Add reusable runtime FPS overlay"
```

---

## Self-Review

1. Spec coverage: every requirement from `docs/superpowers/specs/2026-05-01-fps-component-design.md` maps to a task.
   - Reusable core-runtime component: Task 2.
   - Runtime entity creation in `ComponentAdded`: Task 1 and Task 2.
   - Two visible lines for update FPS and render FPS: Task 1 and Task 2.
   - Top-left placement using existing 2D text rendering: Task 1 and Task 2.
   - Periodic refresh from sampled frame activity: Task 2.
   - Fail-fast lifecycle behavior: Task 2.
   - Verification and regression coverage: Tasks 1 and 3.

2. Placeholder scan: no `TBD`, `TODO`, or vague “add appropriate handling” steps remain. Every task names the exact files, commands, and assertions it depends on.

3. Type consistency: the plan uses one component name (`FPSComponent`), one pair of sampling hooks (`RecordUpdateFrame` / `RecordRenderFrame`), and one pair of visible strings (`UpdateFpsText` / `RenderFpsText`) throughout the document.
