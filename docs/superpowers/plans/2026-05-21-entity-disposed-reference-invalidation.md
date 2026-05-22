# Entity Disposed Reference Invalidation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make disposed `Entity` and `Component` objects fail fast across the engine and remove the hidden FPS/debug overlay lifetime sentinel components.

**Architecture:** The implementation adds a strict disposed-state contract to `Entity` and `Component`, treating disposal-begin as already invalid for external callers. After the ownership layer throws `ObjectDisposedException` consistently, `FPSComponent` and `DebugComponent` can rely on ordinary entity teardown instead of attaching hidden cleanup components to generated overlay roots.

**Tech Stack:** C#, xUnit, existing runtime test harness in `engine/helengine.editor.tests`

---

## File Map

### Engine ownership layer

- Modify: `engine/helengine.core/Component.cs`
  - Add explicit disposed-state tracking and shared guard helpers for live component access.
- Modify: `engine/helengine.core/Entity.cs`
  - Add explicit disposed-state surface and enforce fail-fast guards on scene-graph operations and externally visible state access.

### Overlay cleanup consumers

- Modify: `engine/helengine.core/components/2d/FPSComponent.cs`
  - Remove hidden overlay sentinel usage and rely on the new ownership contract.
- Delete: `engine/helengine.core/components/2d/FPSComponentOverlayLifetimeComponent.cs`
  - Remove the fake cleanup component entirely.
- Modify: `engine/helengine.core/components/2d/DebugComponent.cs`
  - Remove hidden overlay sentinel usage and rely on the new ownership contract.
- Delete: `engine/helengine.core/components/2d/DebugComponentOverlayLifetimeComponent.cs`
  - Remove the fake cleanup component entirely.

### Focused tests

- Create: `engine/helengine.editor.tests/EntityDisposedInvalidationTests.cs`
  - Add direct ownership-contract tests for disposed entity and component access.
- Modify: `engine/helengine.editor.tests/EntityDisposeOrderingTests.cs`
  - Keep disposal-order coverage valid under the new throwing semantics.
- Modify: `engine/helengine.editor.tests/FPSComponentTests.cs`
  - Replace sentinel-based stale-overlay tests with disposed-reference and teardown behavior that matches the new rule.
- Modify: `engine/helengine.editor.tests/DebugComponentTests.cs`
  - Replace sentinel-based stale-overlay tests with disposed-reference and teardown behavior that matches the new rule.

## Task 1: Lock the Ownership Contract With Failing Tests

**Files:**
- Create: `engine/helengine.editor.tests/EntityDisposedInvalidationTests.cs`
- Modify: `engine/helengine.editor.tests/FPSComponentTests.cs`
- Modify: `engine/helengine.editor.tests/DebugComponentTests.cs`

- [ ] **Step 1: Write the failing disposed-entity and disposed-component tests**

Create `engine/helengine.editor.tests/EntityDisposedInvalidationTests.cs` with this content:

```csharp
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies disposed runtime entities and components fail fast instead of behaving like live scene objects.
    /// </summary>
    public sealed class EntityDisposedInvalidationTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the runtime harness.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes one deterministic runtime core for ownership-lifetime tests.
        /// </summary>
        public EntityDisposedInvalidationTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-entity-disposed-invalidation-tests", Guid.NewGuid().ToString("N"));
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
        /// Ensures disposed entities reject later scene-graph mutation.
        /// </summary>
        [Fact]
        public void AddChild_WhenReceiverEntityWasDisposed_ThrowsObjectDisposedException() {
            Entity parent = CreateInitializedEntity();
            Entity child = CreateInitializedEntity();

            parent.Dispose();

            ObjectDisposedException exception = Assert.Throws<ObjectDisposedException>(() => parent.AddChild(child));
            Assert.Contains(nameof(Entity), exception.ObjectName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures disposed entities reject externally visible state reads.
        /// </summary>
        [Fact]
        public void ParentProperty_WhenEntityWasDisposed_ThrowsObjectDisposedException() {
            Entity parent = CreateInitializedEntity();
            Entity child = CreateInitializedEntity();
            parent.AddChild(child);

            child.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _ = child.Parent);
        }

        /// <summary>
        /// Ensures disposed components reject later parent access.
        /// </summary>
        [Fact]
        public void ParentProperty_WhenComponentWasDisposed_ThrowsObjectDisposedException() {
            Entity entity = CreateInitializedEntity();
            ProbeComponent component = new ProbeComponent();
            entity.AddComponent(component);

            entity.RemoveComponent(component);
            component.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _ = component.Parent);
        }

        /// <summary>
        /// Ensures disposed components cannot be reattached.
        /// </summary>
        [Fact]
        public void AddComponent_WhenSuppliedComponentWasDisposed_ThrowsObjectDisposedException() {
            Entity firstEntity = CreateInitializedEntity();
            Entity secondEntity = CreateInitializedEntity();
            ProbeComponent component = new ProbeComponent();
            firstEntity.AddComponent(component);

            firstEntity.RemoveComponent(component);
            component.Dispose();

            Assert.Throws<ObjectDisposedException>(() => secondEntity.AddComponent(component));
        }

        /// <summary>
        /// Creates one initialized entity ready for runtime lifecycle participation.
        /// </summary>
        /// <returns>Initialized entity with component and child collections.</returns>
        static Entity CreateInitializedEntity() {
            Entity entity = new Entity();
            entity.InitChildren();
            entity.InitComponents();
            entity.InitializeHierarchy();
            return entity;
        }

        /// <summary>
        /// Minimal lifecycle probe used by disposed-component tests.
        /// </summary>
        sealed class ProbeComponent : Component {
        }
    }
}
```

- [ ] **Step 2: Replace the FPS sentinel-specific test with the new disposed-reference expectation**

Update `engine/helengine.editor.tests/FPSComponentTests.cs` by deleting `ParentEnabledChange_WhenOverlaySubtreeWasDisposedExternally_TearsDownStaleOverlayReferences` and adding this test in its place:

```csharp
        /// <summary>
        /// Ensures externally disposed overlay entities become invalid references instead of requiring a hidden cleanup sentinel.
        /// </summary>
        [Fact]
        public void OverlayHost_WhenDisposedExternally_ThrowsObjectDisposedExceptionOnLaterAccess() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent {
                Font = CreateFont()
            };

            entity.AddComponent(fps);

            Entity overlayHost = Assert.IsType<Entity>(GetPrivateFieldValue(fps, "OverlayHost"));
            overlayHost.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _ = overlayHost.Parent);
        }
```

- [ ] **Step 3: Replace the Debug sentinel-specific test with the new disposed-reference expectation**

Update `engine/helengine.editor.tests/DebugComponentTests.cs` by deleting `ParentEnabledChange_WhenOverlaySubtreeWasDisposedExternally_TearsDownStaleOverlayReferences` and adding this test in its place:

```csharp
        /// <summary>
        /// Ensures externally disposed overlay entities become invalid references instead of requiring a hidden cleanup sentinel.
        /// </summary>
        [Fact]
        public void OverlayHost_WhenDisposedExternally_ThrowsObjectDisposedExceptionOnLaterAccess() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            DebugComponent debug = new DebugComponent {
                Font = CreateFont()
            };

            entity.AddComponent(debug);

            Entity overlayHost = Assert.IsType<Entity>(GetPrivateFieldValue(debug, "OverlayHost"));
            overlayHost.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _ = overlayHost.Parent);
        }
```

- [ ] **Step 4: Run the focused tests to verify they fail for the right reason**

Run:

```bash
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\\helengine.editor.tests\\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~EntityDisposedInvalidationTests|FullyQualifiedName~OverlayHost_WhenDisposedExternally_ThrowsObjectDisposedExceptionOnLaterAccess' 2>&1 | Select-Object -Last 180 | Out-String -Width 240 | Write-Output"
```

Expected:

- `EntityDisposedInvalidationTests` fails because `Entity` and `Component` do not yet throw on disposed access
- the new FPS/debug tests fail because disposed overlay entities still appear readable

- [ ] **Step 5: Commit the red tests**

```bash
git add engine/helengine.editor.tests/EntityDisposedInvalidationTests.cs engine/helengine.editor.tests/FPSComponentTests.cs engine/helengine.editor.tests/DebugComponentTests.cs
git commit -m "Add disposed runtime ownership regression tests"
```

## Task 2: Implement the Core Entity/Component Disposed Contract

**Files:**
- Modify: `engine/helengine.core/Component.cs`
- Modify: `engine/helengine.core/Entity.cs`
- Modify: `engine/helengine.editor.tests/EntityDisposeOrderingTests.cs`

- [ ] **Step 1: Add failing disposal-order assertions that survive the stricter contract**

Update `engine/helengine.editor.tests/EntityDisposeOrderingTests.cs` to add this test:

```csharp
        /// <summary>
        /// Ensures disposing entities become externally invalid as soon as disposal begins while recursive teardown still completes.
        /// </summary>
        [Fact]
        public void Dispose_WhenEntityHasChildHierarchy_BecomesExternallyInvalidAfterDisposalCompletes() {
            Entity parent = CreateInitializedEntity();
            Entity child = CreateInitializedEntity();

            parent.AddChild(child);

            parent.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _ = parent.Children);
            Assert.Throws<ObjectDisposedException>(() => _ = child.Parent);
        }
```

- [ ] **Step 2: Run the disposal-order test to confirm the contract still fails before implementation**

Run:

```bash
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\\helengine.editor.tests\\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~Dispose_WhenEntityHasChildHierarchy_BecomesExternallyInvalidAfterDisposalCompletes' 2>&1 | Select-Object -Last 140 | Out-String -Width 240 | Write-Output"
```

Expected: `FAIL` because disposed `Entity` state is not yet enforced on property reads.

- [ ] **Step 3: Implement disposed-state tracking and guards in `Component.cs`**

Update `engine/helengine.core/Component.cs` so it looks like this:

```csharp
namespace helengine {
    /// <summary>
    /// Base class for entity components that participate in the engine lifecycle.
    /// </summary>
    public class Component : IDisposable {
        /// <summary>
        /// Parent entity that owns the component while it is attached and live.
        /// </summary>
        Entity parent;

        /// <summary>
        /// Tracks whether disposal has begun for this component.
        /// </summary>
        bool isDisposing;

        /// <summary>
        /// Gets the entity this component is attached to.
        /// </summary>
        public Entity Parent {
            get {
                ThrowIfDisposed();
                return parent;
            }
            private set {
                parent = value;
            }
        }

        /// <summary>
        /// Gets whether disposal has begun for this component.
        /// </summary>
        public bool IsDisposed => isDisposing;

        /// <summary>
        /// Gets whether this component is the editor-owned suppression marker that disables gameplay update execution during scene authoring.
        /// </summary>
        public virtual bool IsEditorUpdateExecutionSuppressionMarker => false;

        /// <summary>
        /// Associates the component with one entity before any runtime lifecycle callbacks are considered.
        /// </summary>
        /// <param name="entity">Entity receiving the component.</param>
        internal void AttachToEntity(Entity entity) {
            ThrowIfDisposed();
            parent = entity;
        }

        /// <summary>
        /// Clears the parent association after the component has finished its detach lifecycle.
        /// </summary>
        internal void DetachFromEntity() {
            parent = null;
        }

        /// <summary>
        /// Throws when the component is being disposed or has already been disposed.
        /// </summary>
        protected void ThrowIfDisposed() {
            if (isDisposing) {
                throw new ObjectDisposedException(nameof(Component));
            }
        }

        /// <summary>
        /// Marks the component disposed before native deletion completes.
        /// </summary>
        public virtual void Dispose() {
            isDisposing = true;
        }
    }
}
```

- [ ] **Step 4: Implement disposed-state guards in `Entity.cs`**

Update `engine/helengine.core/Entity.cs` with these concrete additions:

```csharp
        /// <summary>
        /// Gets the parent entity in the current scene hierarchy.
        /// </summary>
        public Entity Parent {
            get {
                ThrowIfDisposed();
                return parent;
            }
            private set {
                parent = value;
            }
        }

        /// <summary>
        /// Gets the direct child entities owned by this entity.
        /// </summary>
        public List<Entity> Children {
            get {
                ThrowIfDisposed();
                return children;
            }
            private set {
                children = value;
            }
        }

        /// <summary>
        /// Gets the directly attached components owned by this entity.
        /// </summary>
        public List<Component> Components {
            get {
                ThrowIfDisposed();
                return components;
            }
            private set {
                components = value;
            }
        }

        /// <summary>
        /// Gets whether disposal has begun for this entity.
        /// </summary>
        public bool IsDisposed => isDisposing;

        /// <summary>
        /// Throws when the entity is disposing or has already been disposed.
        /// </summary>
        void ThrowIfDisposed() {
            if (isDisposing) {
                throw new ObjectDisposedException(nameof(Entity));
            }
        }

        /// <summary>
        /// Throws when the supplied entity argument is disposing or has already been disposed.
        /// </summary>
        /// <param name="entity">Entity argument to validate.</param>
        /// <param name="argumentName">Argument name for diagnostics.</param>
        static void ThrowIfDisposed(Entity entity, string argumentName) {
            if (entity != null && entity.IsDisposed) {
                throw new ObjectDisposedException(argumentName);
            }
        }

        /// <summary>
        /// Throws when the supplied component argument is disposing or has already been disposed.
        /// </summary>
        /// <param name="component">Component argument to validate.</param>
        /// <param name="argumentName">Argument name for diagnostics.</param>
        static void ThrowIfDisposed(Component component, string argumentName) {
            if (component != null && component.IsDisposed) {
                throw new ObjectDisposedException(argumentName);
            }
        }
```

Then update these methods to guard external use before proceeding:

```csharp
        public void AddChild(Entity entity) {
            ThrowIfDisposed();
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            ThrowIfDisposed(entity, nameof(entity));
            if (entity.Parent != null) {
                throw new Exception("Parent is not empty");
            }
            ...
        }

        public void RemoveChild(Entity entity) {
            ThrowIfDisposed();
            ...
            ThrowIfDisposed(entity, nameof(entity));
            ...
        }

        public void InitChildren() {
            ThrowIfDisposed();
            Children = new List<Entity>();
        }

        public void InitComponents() {
            ThrowIfDisposed();
            Components = new List<Component>();
        }

        public void AddComponent(Component comp) {
            ThrowIfDisposed();
            if (comp == null) {
                throw new ArgumentNullException(nameof(comp));
            }
            ThrowIfDisposed(comp, nameof(comp));
            Components.Add(comp);
            comp.AttachToEntity(this);
            ...
        }

        public void RemoveComponent(Component comp) {
            ThrowIfDisposed();
            ...
            ThrowIfDisposed(comp, nameof(comp));
            ...
        }
```

Keep the current recursive `Dispose()` ordering intact. Do not route the internal teardown loop through any new public guard that would break recursive disposal.

- [ ] **Step 5: Run the ownership tests and disposal-order tests to verify they pass**

Run:

```bash
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\\helengine.editor.tests\\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~EntityDisposedInvalidationTests|FullyQualifiedName~EntityDisposeOrderingTests' 2>&1 | Select-Object -Last 180 | Out-String -Width 240 | Write-Output"
```

Expected: `PASS` for the new invalidation tests and the disposal-order suite.

- [ ] **Step 6: Commit the ownership-layer contract**

```bash
git add engine/helengine.core/Component.cs engine/helengine.core/Entity.cs engine/helengine.editor.tests/EntityDisposeOrderingTests.cs
git commit -m "Add fail-fast disposed entity and component guards"
```

## Task 3: Remove FPS Overlay Lifetime Sentinel

**Files:**
- Modify: `engine/helengine.core/components/2d/FPSComponent.cs`
- Delete: `engine/helengine.core/components/2d/FPSComponentOverlayLifetimeComponent.cs`
- Modify: `engine/helengine.editor.tests/FPSComponentTests.cs`

- [ ] **Step 1: Add a failing regression that proves FPS teardown works without the sentinel**

Add this test to `engine/helengine.editor.tests/FPSComponentTests.cs`:

```csharp
        /// <summary>
        /// Ensures disposing the generated overlay subtree does not require a hidden lifetime component to keep FPS teardown safe.
        /// </summary>
        [Fact]
        public void RemoveComponent_WhenOverlayWasDisposedExternally_DoesNotLeaveGeneratedChildrenAttached() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            FPSComponent fps = new FPSComponent {
                Font = CreateFont()
            };
            entity.AddComponent(fps);

            Entity overlayHost = Assert.IsType<Entity>(GetPrivateFieldValue(fps, "OverlayHost"));
            overlayHost.Dispose();

            entity.RemoveComponent(fps);

            Assert.Empty(entity.Children);
            Assert.DoesNotContain(fps, GetActiveComponents());
        }
```

- [ ] **Step 2: Run the focused FPS tests to verify the sentinel removal path is still red**

Run:

```bash
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\\helengine.editor.tests\\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~FPSComponentTests' 2>&1 | Select-Object -Last 200 | Out-String -Width 240 | Write-Output"
```

Expected: a failure remains while `FPSComponent` still builds and caches the sentinel component.

- [ ] **Step 3: Remove the sentinel from `FPSComponent.cs`**

Make these exact changes in `engine/helengine.core/components/2d/FPSComponent.cs`:

```csharp
        /// <summary>
        /// Root entity that positions the overlay in viewport space.
        /// </summary>
        Entity OverlayHost;
```

Delete this field entirely:

```csharp
        /// <summary>
        /// Lifetime sentinel attached to the overlay root so stale cached references are cleared when the overlay subtree is disposed externally.
        /// </summary>
        FPSComponentOverlayLifetimeComponent OverlayLifetimeComponent;
```

In `BuildOverlay()`, remove:

```csharp
            OverlayLifetimeComponent = new FPSComponentOverlayLifetimeComponent(this);
            OverlayHost.AddComponent(OverlayLifetimeComponent);
```

In `ReleaseOverlayReferences()`, remove:

```csharp
            OverlayLifetimeComponent = null;
```

Delete this method entirely:

```csharp
        internal void ReleaseOverlayReferencesFromDisposedHierarchy() {
            ReleaseOverlayReferences();
        }
```

Delete the file `engine/helengine.core/components/2d/FPSComponentOverlayLifetimeComponent.cs`.

- [ ] **Step 4: Run the focused FPS suite again**

Run:

```bash
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\\helengine.editor.tests\\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~FPSComponentTests' 2>&1 | Select-Object -Last 200 | Out-String -Width 240 | Write-Output"
```

Expected: `PASS`

- [ ] **Step 5: Commit the FPS cleanup**

```bash
git add engine/helengine.core/components/2d/FPSComponent.cs engine/helengine.editor.tests/FPSComponentTests.cs
git rm engine/helengine.core/components/2d/FPSComponentOverlayLifetimeComponent.cs
git commit -m "Remove FPS overlay lifetime sentinel"
```

## Task 4: Remove Debug Overlay Lifetime Sentinel

**Files:**
- Modify: `engine/helengine.core/components/2d/DebugComponent.cs`
- Delete: `engine/helengine.core/components/2d/DebugComponentOverlayLifetimeComponent.cs`
- Modify: `engine/helengine.editor.tests/DebugComponentTests.cs`

- [ ] **Step 1: Add a failing regression that proves Debug teardown works without the sentinel**

Add this test to `engine/helengine.editor.tests/DebugComponentTests.cs`:

```csharp
        /// <summary>
        /// Ensures disposing the generated overlay subtree does not require a hidden lifetime component to keep debug teardown safe.
        /// </summary>
        [Fact]
        public void RemoveComponent_WhenOverlayWasDisposedExternally_DoesNotLeaveGeneratedChildrenAttached() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();

            DebugComponent debug = new DebugComponent {
                Font = CreateFont()
            };
            entity.AddComponent(debug);

            Entity overlayHost = Assert.IsType<Entity>(GetPrivateFieldValue(debug, "OverlayHost"));
            overlayHost.Dispose();

            entity.RemoveComponent(debug);

            Assert.Empty(entity.Children);
        }
```

- [ ] **Step 2: Run the focused Debug tests to verify the sentinel removal path is still red**

Run:

```bash
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\\helengine.editor.tests\\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~DebugComponentTests' 2>&1 | Select-Object -Last 220 | Out-String -Width 240 | Write-Output"
```

Expected: a failure remains while `DebugComponent` still builds and caches the sentinel component.

- [ ] **Step 3: Remove the sentinel from `DebugComponent.cs`**

Make these exact changes in `engine/helengine.core/components/2d/DebugComponent.cs`:

Delete this field entirely:

```csharp
        /// <summary>
        /// Lifetime sentinel attached to the overlay root so stale cached references are cleared when the overlay subtree is disposed externally.
        /// </summary>
        DebugComponentOverlayLifetimeComponent OverlayLifetimeComponent;
```

In `BuildOverlay()`, remove:

```csharp
            OverlayLifetimeComponent = new DebugComponentOverlayLifetimeComponent(this);
            OverlayHost.AddComponent(OverlayLifetimeComponent);
```

In `ReleaseOverlayReferences()`, remove:

```csharp
            OverlayLifetimeComponent = null;
```

Delete this method entirely:

```csharp
        internal void ReleaseOverlayReferencesFromDisposedHierarchy() {
            ReleaseOverlayReferences();
        }
```

Delete the file `engine/helengine.core/components/2d/DebugComponentOverlayLifetimeComponent.cs`.

- [ ] **Step 4: Run the focused Debug suite again**

Run:

```bash
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\\helengine.editor.tests\\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~DebugComponentTests' 2>&1 | Select-Object -Last 220 | Out-String -Width 240 | Write-Output"
```

Expected: `PASS`

- [ ] **Step 5: Commit the Debug cleanup**

```bash
git add engine/helengine.core/components/2d/DebugComponent.cs engine/helengine.editor.tests/DebugComponentTests.cs
git rm engine/helengine.core/components/2d/DebugComponentOverlayLifetimeComponent.cs
git commit -m "Remove debug overlay lifetime sentinel"
```

## Task 5: Final Focused Verification

**Files:**
- No additional code changes expected

- [ ] **Step 1: Run the combined ownership and overlay regression suite**

Run:

```bash
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\\helengine.editor.tests\\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~EntityDisposedInvalidationTests|FullyQualifiedName~EntityDisposeOrderingTests|FullyQualifiedName~FPSComponentTests|FullyQualifiedName~DebugComponentTests' 2>&1 | Select-Object -Last 260 | Out-String -Width 240 | Write-Output"
```

Expected:

- disposed entity/component tests pass
- disposal-order tests still pass
- FPS overlay tests pass without `FPSComponentOverlayLifetimeComponent`
- Debug overlay tests pass without `DebugComponentOverlayLifetimeComponent`

- [ ] **Step 2: Audit the source tree for removed sentinel references**

Run:

```bash
rtk proxy powershell -NoProfile -Command "rg -n --no-messages 'FPSComponentOverlayLifetimeComponent|DebugComponentOverlayLifetimeComponent|ReleaseOverlayReferencesFromDisposedHierarchy' engine\\helengine.core engine\\helengine.editor.tests 2>&1 | Out-String -Width 240 | Write-Output"
```

Expected: no matches

- [ ] **Step 3: Commit the final verification checkpoint**

```bash
git add -A
git commit -m "Verify disposed runtime invalidation cleanup"
```

## Self-Review

### Spec coverage

- General engine rule for disposed `Entity`/`Component` references: covered by Task 2
- Throw immediately on later use: covered by Tasks 1 and 2
- No new event system: preserved throughout
- Remove FPS/debug lifetime sentinels: covered by Tasks 3 and 4
- Focused validation only: covered by Task 5

### Placeholder scan

- No `TODO`/`TBD` placeholders remain
- Each task includes exact files, code, commands, and expected outcomes
- All commit messages are explicit

### Type consistency

- Uses existing type names `Entity`, `Component`, `FPSComponent`, `DebugComponent`
- Uses the same sentinel class names currently present in source
- Uses `IsDisposed` consistently as the public disposed-state surface
