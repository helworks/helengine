# Entity Dispose Ordering Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Strengthen `Entity.Dispose()` so an entity's own components finish teardown before child entities are disposed, preventing parent-owned helper hierarchies from going stale during component removal.

**Architecture:** Drive the change from focused runtime regressions. First prove the desired ordering in a dedicated entity-lifecycle test file, then minimally reorder the `Entity.Dispose()` phases in `helengine.core`. Finally, verify the stronger engine contract against the existing `FPSComponent` and `DebugComponent` overlay regressions and rerun the authored Windows navigation probe.

**Tech Stack:** C# 13 / .NET 9, xUnit, HelEngine runtime scene/entity lifecycle, Windows probe export harness

---

## File Structure

- Create: `engine/helengine.editor.tests/EntityDisposeOrderingTests.cs`
  - Focused runtime lifecycle regressions for parent-before-child disposal ordering.
- Modify: `engine/helengine.core/Entity.cs`
  - Reorder `Dispose()` so direct components are removed before child entities are disposed.
- Verify only: `engine/helengine.editor.tests/FPSComponentTests.cs`
  - Existing overlay regression proving stale-child teardown no longer crashes the user-facing component path.
- Verify only: `engine/helengine.editor.tests/DebugComponentTests.cs`
  - Existing overlay regression proving the stronger engine contract does not regress current overlay behavior.
- Verify only: `artifacts/scene-probe-build-harness/Program.cs`
  - Existing one-off harness used to build the authored navigation probe export.

### Task 1: Add Entity Disposal Ordering Regressions

**Files:**
- Create: `engine/helengine.editor.tests/EntityDisposeOrderingTests.cs`
- Test: `engine/helengine.editor.tests/EntityDisposeOrderingTests.cs`

- [ ] **Step 1: Write the failing disposal-ordering tests**

Add a new test file with focused probes that record ordering without depending on rendering or scene loading:

```csharp
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the global entity-disposal ordering contract used by runtime components that own generated child hierarchies.
    /// </summary>
    public class EntityDisposeOrderingTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the runtime test harness.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes a deterministic runtime core for entity-lifecycle tests.
        /// </summary>
        public EntityDisposeOrderingTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-entity-dispose-ordering-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            TestClockDrivenCore core = new TestClockDrivenCore(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });

            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
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
        /// Ensures direct parent components still see their generated child hierarchy during parent disposal.
        /// </summary>
        [Fact]
        public void Dispose_WhenParentOwnsGeneratedChildHierarchy_ParentComponentSeesChildBeforeChildDisposal() {
            Entity parent = CreateInitializedEntity();
            Entity child = CreateInitializedEntity();
            ParentChildVisibilityProbeComponent probe = new ParentChildVisibilityProbeComponent(child);

            parent.AddChild(child);
            parent.AddComponent(probe);

            parent.Dispose();

            Assert.True(probe.SawExpectedChildStateDuringRemoval);
        }

        /// <summary>
        /// Ensures parent component removal happens before child component removal during recursive disposal.
        /// </summary>
        [Fact]
        public void Dispose_WhenParentHasChild_ParentComponentsAreRemovedBeforeChildComponents() {
            List<string> events = new List<string>();
            Entity parent = CreateInitializedEntity();
            Entity child = CreateInitializedEntity();

            parent.AddComponent(new OrderingProbeComponent("parent", events));
            child.AddComponent(new OrderingProbeComponent("child", events));
            parent.AddChild(child);

            parent.Dispose();

            Assert.Equal(
                new[] { "parent:disabled", "parent:removed", "child:disabled", "child:removed" },
                events);
        }

        /// <summary>
        /// Creates one initialized entity ready for component lifecycle participation.
        /// </summary>
        /// <returns>Initialized entity with child and component collections.</returns>
        static Entity CreateInitializedEntity() {
            Entity entity = new Entity();
            entity.InitChildren();
            entity.InitComponents();
            entity.InitializeHierarchy();
            return entity;
        }

        /// <summary>
        /// Records whether the expected child entity is still visible during parent-component removal.
        /// </summary>
        sealed class ParentChildVisibilityProbeComponent : Component {
            /// <summary>
            /// Child entity expected to remain visible until parent-component removal completes.
            /// </summary>
            readonly Entity ExpectedChild;

            /// <summary>
            /// Gets whether the parent still owned the expected child during removal.
            /// </summary>
            public bool SawExpectedChildStateDuringRemoval { get; private set; }

            /// <summary>
            /// Initializes the probe with the expected generated child entity.
            /// </summary>
            /// <param name="expectedChild">Child entity that should still be attached during removal.</param>
            public ParentChildVisibilityProbeComponent(Entity expectedChild) {
                ExpectedChild = expectedChild ?? throw new ArgumentNullException(nameof(expectedChild));
            }

            /// <summary>
            /// Captures whether the child is still attached during parent-component removal.
            /// </summary>
            /// <param name="entity">Parent entity being disposed.</param>
            public override void ComponentRemoved(Entity entity) {
                SawExpectedChildStateDuringRemoval =
                    entity != null
                    && entity.Children != null
                    && entity.Children.Contains(ExpectedChild)
                    && ExpectedChild.Parent == entity;
                base.ComponentRemoved(entity);
            }
        }

        /// <summary>
        /// Records the disable and removal ordering for one component.
        /// </summary>
        sealed class OrderingProbeComponent : Component {
            /// <summary>
            /// Human-readable probe name.
            /// </summary>
            readonly string Name;

            /// <summary>
            /// Shared event sink that records lifecycle callbacks.
            /// </summary>
            readonly List<string> Events;

            /// <summary>
            /// Initializes the ordering probe.
            /// </summary>
            /// <param name="name">Probe name written into the shared log.</param>
            /// <param name="events">Shared event list.</param>
            public OrderingProbeComponent(string name, List<string> events) {
                Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Probe name must be provided.", nameof(name)) : name;
                Events = events ?? throw new ArgumentNullException(nameof(events));
            }

            /// <summary>
            /// Records one disable callback.
            /// </summary>
            /// <param name="newEnabled">New enabled state.</param>
            public override void ParentEnabledChange(bool newEnabled) {
                Events.Add(Name + ":" + (newEnabled ? "enabled" : "disabled"));
                base.ParentEnabledChange(newEnabled);
            }

            /// <summary>
            /// Records one removal callback.
            /// </summary>
            /// <param name="entity">Owning entity being disposed.</param>
            public override void ComponentRemoved(Entity entity) {
                Events.Add(Name + ":removed");
                base.ComponentRemoved(entity);
            }
        }
    }
}
```

- [ ] **Step 2: Run the new entity-ordering tests and verify they fail**

Run:

```powershell
dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EntityDisposeOrderingTests" --nologo -v q
```

Expected: FAIL because `Entity.Dispose()` still disposes children before removing direct parent components, so the parent probe will not see its child during `ComponentRemoved(...)`.

- [ ] **Step 3: Commit the failing test**

```powershell
git add engine/helengine.editor.tests/EntityDisposeOrderingTests.cs
git commit -m "Add entity dispose ordering regression tests"
```

### Task 2: Reorder `Entity.Dispose()`

**Files:**
- Modify: `engine/helengine.core/Entity.cs`
- Test: `engine/helengine.editor.tests/EntityDisposeOrderingTests.cs`

- [ ] **Step 1: Implement the minimal disposal-ordering change**

Change only the `Dispose()` phase order. Move the direct-component removal block ahead of the child-disposal block:

```csharp
/// <summary>
/// Recursively tears down this entity subtree, detaches its components, removes it from any parent, and unregisters it from the object manager.
/// </summary>
public void Dispose() {
    if (isDisposing) {
        return;
    }

    isDisposing = true;
    if (Components != null) {
        while (Components.Count > 0) {
            Component component = Components[Components.Count - 1];
            RemoveComponent(component);
            component.Dispose();
            NativeOwnership.Delete(component);
        }

        List<Component> components = Components;
        Components = null;
        NativeOwnership.Delete(components);
    }

    if (Children != null) {
        while (Children.Count > 0) {
            Entity child = Children[Children.Count - 1];
            RemoveChild(child);
            NativeOwnership.DisposeAndDelete(child);
        }

        List<Entity> children = Children;
        Children = null;
        NativeOwnership.Delete(children);
    }

    if (Parent != null) {
        Parent.RemoveChild(this);
    }

    Core.Instance.ObjectManager.RemoveEntity(this);
}
```

- [ ] **Step 2: Run the focused entity-ordering tests and verify they pass**

Run:

```powershell
dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EntityDisposeOrderingTests" --nologo -v q
```

Expected: PASS with both entity-ordering regressions green.

- [ ] **Step 3: Commit the disposal-ordering change**

```powershell
git add engine/helengine.core/Entity.cs engine/helengine.editor.tests/EntityDisposeOrderingTests.cs
git commit -m "Dispose entity components before child entities"
```

### Task 3: Verify Overlay Regressions and Windows Navigation Probe

**Files:**
- Verify only: `engine/helengine.editor.tests/FPSComponentTests.cs`
- Verify only: `engine/helengine.editor.tests/DebugComponentTests.cs`
- Verify only: `artifacts/scene-probe-build-harness/Program.cs`

- [ ] **Step 1: Run the overlay regression slice**

Run:

```powershell
dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~FPSComponentTests|FullyQualifiedName~DebugComponentTests" --nologo -v q
```

Expected: PASS. This confirms the stronger engine contract does not regress the existing overlay teardown protections.

- [ ] **Step 2: Rebuild the authored Windows navigation probe export**

Run:

```powershell
dotnet run --project artifacts\scene-probe-build-harness\SceneProbeBuildHarness.csproj -c Debug
```

Expected output includes:

```text
Build completed for platform 'windows': C:\dev\helprojs\output\windows_probe_nav
```

- [ ] **Step 3: Run the `mainmenu -> cube_test -> mainmenu -> cube_test -> mainmenu` soak and verify no unload crash**

Run:

```powershell
$exe = 'C:\dev\helprojs\output\windows_probe_nav\helengine_windows.exe'
$wd = 'C:\dev\helprojs\output\windows_probe_nav'
$proc = Start-Process -FilePath $exe -WorkingDirectory $wd -PassThru
for ($i = 1; $i -le 14; $i++) {
    Start-Sleep -Seconds 5
    if ($proc.HasExited) {
        throw "Probe exited early with code $($proc.ExitCode)."
    }
}
Stop-Process -Id $proc.Id -Force
$proc.WaitForExit()
```

Expected:

- the process stays alive for the full soak
- `C:\dev\helprojs\output\windows_probe_nav\helengine_windows.startup.log` contains no unhandled structured exception
- `C:\dev\helprojs\output\windows_probe_nav\helengine_windows.diagnostics.log` shows the authored scene transitions completing

- [ ] **Step 4: Commit the verified runtime contract change**

```powershell
git add engine/helengine.core/Entity.cs engine/helengine.editor.tests/EntityDisposeOrderingTests.cs
git commit -m "Strengthen entity teardown ordering"
```

## Self-Review

- Spec coverage: the plan covers the new global teardown contract, focused entity-ordering regressions, overlay regression verification, and the Windows authored navigation validation from the spec.
- Placeholder scan: no `TODO`, `TBD`, or indirect “write tests later” steps remain.
- Type consistency: `Entity.Dispose()`, `ComponentRemoved(Entity entity)`, `ParentEnabledChange(bool newEnabled)`, and the planned test helper types all use names that exist in the current codebase.
