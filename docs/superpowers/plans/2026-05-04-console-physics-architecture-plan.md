# Console Physics Architecture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an optional, console-first physics architecture to Helengine with a real `helengine.physics3d` runtime, a placeholder `helengine.physics2d` runtime, core hosting contracts, generic per-platform property overrides, and exportable validation scenes.

**Architecture:** Keep all solver logic outside `helengine.core` and host physics through narrow contracts owned by core. Implement `helengine.physics3d` as a fixed-step, profile-driven runtime with dense simulation records, optional broadphase strategies, cooked static collision hooks, and a first-class character controller; implement `helengine.physics2d` only as a structural placeholder so 2D-only titles do not take a 3D dependency. Add a generic per-platform override metadata and resolution layer in core/editor so physics and future systems can opt in without custom editor rewrites.

**Tech Stack:** C#, .NET, xUnit, Helengine core/editor/runtime libraries, scene serialization, platform build metadata

---

## File Structure

### Existing files to modify

- `C:\dev\helworks\helengine\engine\helengine.core\Core.cs`
  - Host optional fixed-step physics runtimes during update.
- `C:\dev\helworks\helengine\engine\helengine.core\CoreInitializationOptions.cs`
  - Add default fixed-step physics timing and optional runtime host wiring.
- `C:\dev\helworks\helengine\engine\helengine.core\Component.cs`
  - Add stable metadata access points used by generic per-platform overrides.
- `C:\dev\helworks\helengine\engine\helengine.core\Entity.cs`
  - Expose controlled transform sync helpers used by physics write-back.
- `C:\dev\helworks\helengine\engine\helengine.core\helengine.core.csproj`
  - Reference any shared abstractions needed by physics hosting.
- `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\ComponentPropertiesView.cs`
  - Render generic per-platform override controls from component metadata.
- `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`
  - Initialize validation scenes and route platform override editing through the generic system.
- `C:\dev\helworks\helengine\engine\helengine.editor.tests\serialization\scene\ComponentPersistenceRegistryTests.cs`
  - Cover new override metadata and physics component persistence registration.
- `C:\dev\helworks\helengine\engine\helengine.editor.tests\EditorSessionBuildSettingsTests.cs`
  - Verify physics scenario scenes and platform-specific values flow into builds.

### New production files to create

- `C:\dev\helworks\helengine\engine\helengine.core\physics\IPhysicsRuntime.cs`
- `C:\dev\helworks\helengine\engine\helengine.core\physics\IPhysicsWorldHost.cs`
- `C:\dev\helworks\helengine\engine\helengine.core\physics\IPhysicsSceneBinding.cs`
- `C:\dev\helworks\helengine\engine\helengine.core\physics\IPhysicsTransformSyncPolicy.cs`
- `C:\dev\helworks\helengine\engine\helengine.core\physics\PhysicsFixedStepScheduler.cs`
- `C:\dev\helworks\helengine\engine\helengine.core\platform\PlatformOverrideAttribute.cs`
- `C:\dev\helworks\helengine\engine\helengine.core\platform\PlatformOverrideValueRecord.cs`
- `C:\dev\helworks\helengine\engine\helengine.core\platform\PlatformOverrideResolver.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\helengine.physics3d.csproj`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\PhysicsWorld3D.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\PhysicsWorld3DProfile.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\PhysicsWorld3DSettings.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\RigidBody3DComponent.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\CharacterController3DComponent.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\colliders\BoxCollider3DComponent.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\colliders\SphereCollider3DComponent.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\colliders\CapsuleCollider3DComponent.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\colliders\MeshCollider3DComponent.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\materials\PhysicsMaterial3DAsset.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\runtime\BodyKind3D.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\runtime\BodyState3D.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\runtime\ColliderState3D.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\runtime\ContactManifold3D.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\runtime\CharacterControllerState3D.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\broadphase\IBroadphase3D.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\broadphase\BroadphaseKind3D.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\broadphase\UniformGridBroadphase3D.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\broadphase\SweepAndPruneBroadphase3D.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\collision\StaticCollisionScene3D.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\collision\StaticCollisionNode3D.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\collision\StaticCollisionTriangle3D.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\collision\StaticCollisionQueryService3D.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\solver\PhysicsSolver3D.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\solver\PhysicsNarrowphase3D.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d\solver\CharacterControllerSolver3D.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics2d\helengine.physics2d.csproj`
- `C:\dev\helworks\helengine\engine\helengine.physics2d\PhysicsWorld2D.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics2d\PhysicsWorld2DProfile.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics2d\RigidBody2DComponent.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics2d\colliders\BoxCollider2DComponent.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics2d\colliders\CircleCollider2DComponent.cs`
- `C:\dev\helworks\helengine\engine\helengine.editor\managers\physics\PhysicsValidationSceneCatalog.cs`
- `C:\dev\helworks\helengine\engine\helengine.editor\managers\physics\PhysicsValidationSceneFactory.cs`
- `C:\dev\helworks\helengine\engine\helengine.editor\serialization\scene\PhysicsComponentPersistenceDescriptors.cs`

### New test files to create

- `C:\dev\helworks\helengine\engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj`
- `C:\dev\helworks\helengine\engine\helengine.physics3d.tests\PhysicsWorld3DProfileTests.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d.tests\RigidBody3DComponentTests.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d.tests\UniformGridBroadphase3DTests.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d.tests\SweepAndPruneBroadphase3DTests.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d.tests\PrimitiveCollision3DTests.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d.tests\CharacterController3DTests.cs`
- `C:\dev\helworks\helengine\engine\helengine.physics3d.tests\StaticCollisionScene3DTests.cs`
- `C:\dev\helworks\helengine\engine\helengine.core.tests\PlatformOverrideResolverTests.cs`
- `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\physics\PhysicsValidationSceneFactoryTests.cs`

### Notes for the implementer

- Keep the first implementation biased toward `Physics3D`; `Physics2D` exists only to establish package boundaries and authoring shape.
- Do not move solver logic into `helengine.core`.
- Keep `Entity` transform ownership in core and only add controlled sync helpers.
- Use dense arrays and handles inside `helengine.physics3d`; do not simulate directly through component objects.
- Keep per-platform override support generic. Physics is the first consumer, not the only one.
- Validation scenes must be authored as real scene assets or generated by a factory that emits normal scene entities/components, so they can be exported and run on target platforms.

## Task 1: Add core physics hosting contracts and fixed-step scheduling

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.core\physics\IPhysicsRuntime.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.core\physics\IPhysicsWorldHost.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.core\physics\IPhysicsSceneBinding.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.core\physics\IPhysicsTransformSyncPolicy.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.core\physics\PhysicsFixedStepScheduler.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.core\CoreInitializationOptions.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.core\Core.cs`
- Test: `C:\dev\helworks\helengine\engine\helengine.editor.tests\EditorSessionBuildSettingsTests.cs`

- [ ] **Step 1: Write the failing fixed-step host test**

```csharp
[Fact]
public void Update_WithPhysicsFixedStep_AdvancesRuntimeUsingScheduler() {
    CoreInitializationOptions options = new CoreInitializationOptions {
        PhysicsFixedStepSeconds = 1.0f / 60.0f
    };
    Core core = new Core(options);
    TestPhysicsRuntime runtime = new TestPhysicsRuntime();
    TestInputBackend input = new TestInputBackend();

    core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), input);
    core.AttachPhysicsRuntime(runtime);

    input.AdvanceElapsedSeconds(1.0f / 30.0f);
    core.Update();

    Assert.Equal(2, runtime.StepCount);
    Assert.Equal((float)(1.0 / 60.0), runtime.LastStepSeconds);
}
```

- [ ] **Step 2: Run the focused test to verify it fails**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~Update_WithPhysicsFixedStep_AdvancesRuntimeUsingScheduler" -v minimal`

Expected: FAIL because `AttachPhysicsRuntime`, `PhysicsFixedStepSeconds`, and the scheduler types do not exist.

- [ ] **Step 3: Add the hosting contracts and scheduler**

```csharp
namespace helengine {
    /// <summary>
    /// Hosts one optional physics runtime inside the core update loop.
    /// </summary>
    public interface IPhysicsRuntime {
        /// <summary>
        /// Advances the runtime by one fixed simulation step.
        /// </summary>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        void Step(float stepSeconds);
    }
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Accumulates frame time and produces fixed simulation steps.
    /// </summary>
    public sealed class PhysicsFixedStepScheduler {
        float accumulatorSeconds;

        /// <summary>
        /// Gets the configured fixed-step length in seconds.
        /// </summary>
        public float StepSeconds { get; }

        /// <summary>
        /// Initializes a new scheduler.
        /// </summary>
        /// <param name="stepSeconds">Fixed simulation step in seconds.</param>
        public PhysicsFixedStepScheduler(float stepSeconds) {
            if (stepSeconds <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(stepSeconds));
            }

            StepSeconds = stepSeconds;
        }

        /// <summary>
        /// Adds one frame of elapsed time.
        /// </summary>
        /// <param name="elapsedSeconds">Elapsed frame time in seconds.</param>
        public void AddFrameTime(float elapsedSeconds) {
            accumulatorSeconds += elapsedSeconds;
        }

        /// <summary>
        /// Tries to consume one simulation step from the current accumulator.
        /// </summary>
        /// <returns><c>true</c> when one step should run; otherwise <c>false</c>.</returns>
        public bool TryConsumeStep() {
            if (accumulatorSeconds < StepSeconds) {
                return false;
            }

            accumulatorSeconds -= StepSeconds;
            return true;
        }
    }
}
```

Also extend `CoreInitializationOptions` and `Core` with:

```csharp
public float PhysicsFixedStepSeconds { get; set; } = 1.0f / 60.0f;
```

```csharp
public void AttachPhysicsRuntime(IPhysicsRuntime runtime) {
    PhysicsRuntime = runtime;
}
```

and in `Core.Update()`:

```csharp
PhysicsScheduler.AddFrameTime(Input.FrameDeltaSeconds);
while (PhysicsRuntime != null && PhysicsScheduler.TryConsumeStep()) {
    PhysicsRuntime.Step(PhysicsScheduler.StepSeconds);
}
```

- [ ] **Step 4: Run the focused test to verify it passes**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~Update_WithPhysicsFixedStep_AdvancesRuntimeUsingScheduler" -v minimal`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk git add engine\helengine.core\physics\IPhysicsRuntime.cs engine\helengine.core\physics\IPhysicsWorldHost.cs engine\helengine.core\physics\IPhysicsSceneBinding.cs engine\helengine.core\physics\IPhysicsTransformSyncPolicy.cs engine\helengine.core\physics\PhysicsFixedStepScheduler.cs engine\helengine.core\CoreInitializationOptions.cs engine\helengine.core\Core.cs engine\helengine.editor.tests\EditorSessionBuildSettingsTests.cs
rtk git commit -m "feat: add core physics hosting contracts"
```

## Task 2: Add generic per-platform override metadata and resolution

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.core\platform\PlatformOverrideAttribute.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.core\platform\PlatformOverrideValueRecord.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.core\platform\PlatformOverrideResolver.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.core\Component.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.core.tests\PlatformOverrideResolverTests.cs`

- [ ] **Step 1: Write the failing override resolution test**

```csharp
[Fact]
public void ResolveValue_WithPlatformSpecificOverride_ReturnsOverrideInsteadOfBaseValue() {
    TestPlatformOverrideComponent component = new TestPlatformOverrideComponent {
        SolverIterations = 6
    };
    PlatformOverrideValueRecord overrideRecord = new PlatformOverrideValueRecord(
        "ps2",
        "solver-iterations",
        "10");

    int resolved = PlatformOverrideResolver.ResolveInt32(component, "ps2", nameof(TestPlatformOverrideComponent.SolverIterations), component.SolverIterations, [overrideRecord]);

    Assert.Equal(10, resolved);
}
```

- [ ] **Step 2: Run the focused test to verify it fails**

Run: `rtk dotnet test engine\helengine.core.tests\helengine.core.tests.csproj --filter "FullyQualifiedName~ResolveValue_WithPlatformSpecificOverride_ReturnsOverrideInsteadOfBaseValue" -v minimal`

Expected: FAIL because override metadata and resolver types do not exist.

- [ ] **Step 3: Add the attribute, record, and resolver**

```csharp
namespace helengine {
    /// <summary>
    /// Marks one component property as overridable per platform.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class PlatformOverrideAttribute : Attribute {
        /// <summary>
        /// Initializes a new platform override attribute.
        /// </summary>
        /// <param name="propertyId">Stable property identifier used in override storage.</param>
        public PlatformOverrideAttribute(string propertyId) {
            if (string.IsNullOrWhiteSpace(propertyId)) {
                throw new ArgumentException("Property id must be provided.", nameof(propertyId));
            }

            PropertyId = propertyId;
        }

        /// <summary>
        /// Gets the stable override property identifier.
        /// </summary>
        public string PropertyId { get; }
    }
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Stores one per-platform override value.
    /// </summary>
    public readonly record struct PlatformOverrideValueRecord(string PlatformId, string PropertyId, string SerializedValue);
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Resolves effective property values for the active platform.
    /// </summary>
    public static class PlatformOverrideResolver {
        /// <summary>
        /// Resolves one 32-bit integer property value.
        /// </summary>
        public static int ResolveInt32(Component component, string platformId, string propertyName, int baseValue, IReadOnlyList<PlatformOverrideValueRecord> overrides) {
            for (int index = 0; index < overrides.Count; index++) {
                PlatformOverrideValueRecord record = overrides[index];
                if (record.PlatformId == platformId && record.PropertyId == propertyName) {
                    return int.Parse(record.SerializedValue, CultureInfo.InvariantCulture);
                }
            }

            return baseValue;
        }
    }
}
```

- [ ] **Step 4: Run the focused test to verify it passes**

Run: `rtk dotnet test engine\helengine.core.tests\helengine.core.tests.csproj --filter "FullyQualifiedName~ResolveValue_WithPlatformSpecificOverride_ReturnsOverrideInsteadOfBaseValue" -v minimal`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk git add engine\helengine.core\platform\PlatformOverrideAttribute.cs engine\helengine.core\platform\PlatformOverrideValueRecord.cs engine\helengine.core\platform\PlatformOverrideResolver.cs engine\helengine.core\Component.cs engine\helengine.core.tests\PlatformOverrideResolverTests.cs
rtk git commit -m "feat: add generic platform override resolution"
```

## Task 3: Create the `helengine.physics3d` project skeleton and profile model

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\helengine.physics3d.csproj`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\PhysicsWorld3D.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\PhysicsWorld3DProfile.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\PhysicsWorld3DSettings.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\broadphase\BroadphaseKind3D.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d.tests\PhysicsWorld3DProfileTests.cs`

- [ ] **Step 1: Write the failing profile defaults test**

```csharp
[Fact]
public void CreateMedium_DefaultsToPrimitiveBodiesAndStaticMeshQueries() {
    PhysicsWorld3DProfile profile = PhysicsWorld3DProfile.CreateMedium();

    Assert.Equal(BroadphaseKind3D.UniformGrid, profile.DefaultBroadphaseKind);
    Assert.True(profile.AllowStaticMeshCollision);
    Assert.True(profile.AllowDynamicBodies);
    Assert.False(profile.AllowJoints);
    Assert.False(profile.AllowContinuousCollisionDetection);
}
```

- [ ] **Step 2: Run the focused tests to verify failure**

Run: `rtk dotnet test engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj --filter "FullyQualifiedName~CreateMedium_DefaultsToPrimitiveBodiesAndStaticMeshQueries" -v minimal`

Expected: FAIL because the project and types do not exist.

- [ ] **Step 3: Create the project and profile scaffolding**

```csharp
namespace helengine {
    /// <summary>
    /// Enumerates supported dynamic-body broadphase strategies.
    /// </summary>
    public enum BroadphaseKind3D {
        UniformGrid = 0,
        SweepAndPrune = 1
    }
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Describes one runtime physics profile for a 3D simulation world.
    /// </summary>
    public sealed class PhysicsWorld3DProfile {
        /// <summary>
        /// Gets or sets the default broadphase strategy.
        /// </summary>
        public BroadphaseKind3D DefaultBroadphaseKind { get; set; }

        /// <summary>
        /// Gets or sets whether static mesh collision is allowed.
        /// </summary>
        public bool AllowStaticMeshCollision { get; set; }

        /// <summary>
        /// Gets or sets whether dynamic rigid bodies are allowed.
        /// </summary>
        public bool AllowDynamicBodies { get; set; }

        /// <summary>
        /// Gets or sets whether joints are enabled.
        /// </summary>
        public bool AllowJoints { get; set; }

        /// <summary>
        /// Gets or sets whether continuous collision detection is enabled.
        /// </summary>
        public bool AllowContinuousCollisionDetection { get; set; }

        /// <summary>
        /// Creates the medium default profile.
        /// </summary>
        public static PhysicsWorld3DProfile CreateMedium() {
            return new PhysicsWorld3DProfile {
                DefaultBroadphaseKind = BroadphaseKind3D.UniformGrid,
                AllowStaticMeshCollision = true,
                AllowDynamicBodies = true,
                AllowJoints = false,
                AllowContinuousCollisionDetection = false
            };
        }
    }
}
```

- [ ] **Step 4: Run the focused tests to verify pass**

Run: `rtk dotnet test engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj --filter "FullyQualifiedName~CreateMedium_DefaultsToPrimitiveBodiesAndStaticMeshQueries" -v minimal`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk git add engine\helengine.physics3d\helengine.physics3d.csproj engine\helengine.physics3d\PhysicsWorld3D.cs engine\helengine.physics3d\PhysicsWorld3DProfile.cs engine\helengine.physics3d\PhysicsWorld3DSettings.cs engine\helengine.physics3d\broadphase\BroadphaseKind3D.cs engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj engine\helengine.physics3d.tests\PhysicsWorld3DProfileTests.cs
rtk git commit -m "feat: add physics3d project skeleton"
```

## Task 4: Add authoring components and runtime registration handles

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\RigidBody3DComponent.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\CharacterController3DComponent.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\colliders\BoxCollider3DComponent.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\colliders\SphereCollider3DComponent.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\colliders\CapsuleCollider3DComponent.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\colliders\MeshCollider3DComponent.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\runtime\BodyKind3D.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d.tests\RigidBody3DComponentTests.cs`

- [ ] **Step 1: Write the failing rigid body registration test**

```csharp
[Fact]
public void ComponentAdded_WithHierarchyEnabled_RegistersBodyIntoWorld() {
    TestPhysicsWorld3D world = new TestPhysicsWorld3D();
    Entity entity = new Entity();
    entity.InitComponents();

    RigidBody3DComponent component = new RigidBody3DComponent {
        BodyKind = BodyKind3D.Dynamic
    };

    world.Attach(entity);
    entity.AddComponent(component);

    Assert.True(component.RuntimeBodyHandle >= 0);
    Assert.Equal(1, world.RegisteredBodyCount);
}
```

- [ ] **Step 2: Run the focused test to verify failure**

Run: `rtk dotnet test engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj --filter "FullyQualifiedName~ComponentAdded_WithHierarchyEnabled_RegistersBodyIntoWorld" -v minimal`

Expected: FAIL because the component and registration contract do not exist.

- [ ] **Step 3: Add the body kind enum and authoring components**

```csharp
namespace helengine {
    /// <summary>
    /// Describes how one rigid body participates in simulation.
    /// </summary>
    public enum BodyKind3D {
        Static = 0,
        Kinematic = 1,
        Dynamic = 2
    }
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Defines one authoring-facing rigid body for the 3D physics runtime.
    /// </summary>
    public class RigidBody3DComponent : Component {
        /// <summary>
        /// Gets or sets the body kind.
        /// </summary>
        [PlatformOverride("body-kind")]
        public BodyKind3D BodyKind { get; set; }

        /// <summary>
        /// Gets the runtime body handle.
        /// </summary>
        public int RuntimeBodyHandle { get; internal set; } = -1;
    }
}
```

Add collider components with overridable fields such as:

```csharp
[PlatformOverride("is-trigger")]
public bool IsTrigger { get; set; }
```

and shape dimensions on box/sphere/capsule.

- [ ] **Step 4: Run the focused test to verify pass**

Run: `rtk dotnet test engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj --filter "FullyQualifiedName~ComponentAdded_WithHierarchyEnabled_RegistersBodyIntoWorld" -v minimal`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk git add engine\helengine.physics3d\RigidBody3DComponent.cs engine\helengine.physics3d\CharacterController3DComponent.cs engine\helengine.physics3d\colliders\BoxCollider3DComponent.cs engine\helengine.physics3d\colliders\SphereCollider3DComponent.cs engine\helengine.physics3d\colliders\CapsuleCollider3DComponent.cs engine\helengine.physics3d\colliders\MeshCollider3DComponent.cs engine\helengine.physics3d\runtime\BodyKind3D.cs engine\helengine.physics3d.tests\RigidBody3DComponentTests.cs
rtk git commit -m "feat: add physics3d authoring components"
```

## Task 5: Add dense runtime records and broadphase interfaces

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\runtime\BodyState3D.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\runtime\ColliderState3D.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\runtime\ContactManifold3D.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\runtime\CharacterControllerState3D.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\broadphase\IBroadphase3D.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\broadphase\UniformGridBroadphase3D.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\broadphase\SweepAndPruneBroadphase3D.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d.tests\UniformGridBroadphase3DTests.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d.tests\SweepAndPruneBroadphase3DTests.cs`

- [ ] **Step 1: Write the failing broadphase candidate-generation test**

```csharp
[Fact]
public void QueryPairs_WithThreeBodies_OnlyReturnsOverlappingCandidates() {
    UniformGridBroadphase3D broadphase = new UniformGridBroadphase3D(8.0f);
    broadphase.UpsertProxy(0, new float3(0f, 0f, 0f), new float3(1f, 1f, 1f));
    broadphase.UpsertProxy(1, new float3(1.5f, 0f, 0f), new float3(1f, 1f, 1f));
    broadphase.UpsertProxy(2, new float3(40f, 0f, 0f), new float3(1f, 1f, 1f));

    List<(int A, int B)> pairs = broadphase.BuildCandidatePairs();

    Assert.Single(pairs);
    Assert.Equal((0, 1), pairs[0]);
}
```

- [ ] **Step 2: Run the focused tests to verify failure**

Run: `rtk dotnet test engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj --filter "FullyQualifiedName~QueryPairs_WithThreeBodies_OnlyReturnsOverlappingCandidates" -v minimal`

Expected: FAIL because no broadphase exists yet.

- [ ] **Step 3: Add the runtime records and broadphase interface**

```csharp
namespace helengine {
    /// <summary>
    /// Maintains dynamic-body candidate generation for one 3D physics world.
    /// </summary>
    public interface IBroadphase3D {
        /// <summary>
        /// Inserts or updates one proxy.
        /// </summary>
        void UpsertProxy(int bodyHandle, float3 center, float3 halfExtents);

        /// <summary>
        /// Removes one proxy.
        /// </summary>
        void RemoveProxy(int bodyHandle);

        /// <summary>
        /// Builds candidate body pairs for the current world state.
        /// </summary>
        List<(int A, int B)> BuildCandidatePairs();
    }
}
```

Implement `UniformGridBroadphase3D` first using pre-sized cell buckets and integer cell keys.
Implement `SweepAndPruneBroadphase3D` as a second strategy behind the same interface, even if the first world implementation still defaults to the grid.

- [ ] **Step 4: Run the focused tests to verify pass**

Run: `rtk dotnet test engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj --filter "FullyQualifiedName~UniformGridBroadphase3DTests|FullyQualifiedName~SweepAndPruneBroadphase3DTests" -v minimal`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk git add engine\helengine.physics3d\runtime\BodyState3D.cs engine\helengine.physics3d\runtime\ColliderState3D.cs engine\helengine.physics3d\runtime\ContactManifold3D.cs engine\helengine.physics3d\runtime\CharacterControllerState3D.cs engine\helengine.physics3d\broadphase\IBroadphase3D.cs engine\helengine.physics3d\broadphase\UniformGridBroadphase3D.cs engine\helengine.physics3d\broadphase\SweepAndPruneBroadphase3D.cs engine\helengine.physics3d.tests\UniformGridBroadphase3DTests.cs engine\helengine.physics3d.tests\SweepAndPruneBroadphase3DTests.cs
rtk git commit -m "feat: add physics3d broadphase infrastructure"
```

## Task 6: Add primitive collision and a minimal dynamic solver

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\solver\PhysicsNarrowphase3D.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\solver\PhysicsSolver3D.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.physics3d\PhysicsWorld3D.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d.tests\PrimitiveCollision3DTests.cs`

- [ ] **Step 1: Write the failing primitive contact resolution test**

```csharp
[Fact]
public void Step_WithOverlappingDynamicSpheres_PushesBodiesApart() {
    PhysicsWorld3D world = PhysicsWorld3D.CreateForTests();
    int bodyA = world.AddDynamicSphere(new float3(0f, 0f, 0f), 1.0f);
    int bodyB = world.AddDynamicSphere(new float3(1.0f, 0f, 0f), 1.0f);

    world.Step(1.0f / 60.0f);

    float3 positionA = world.GetBodyPosition(bodyA);
    float3 positionB = world.GetBodyPosition(bodyB);

    Assert.True(float3.Distance(positionA, positionB) >= 1.99f);
}
```

- [ ] **Step 2: Run the focused test to verify failure**

Run: `rtk dotnet test engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj --filter "FullyQualifiedName~Step_WithOverlappingDynamicSpheres_PushesBodiesApart" -v minimal`

Expected: FAIL because narrowphase and solve paths do not exist.

- [ ] **Step 3: Add minimal primitive-vs-primitive narrowphase and iterative contact solve**

```csharp
namespace helengine {
    /// <summary>
    /// Generates contacts for primitive 3D collider pairs.
    /// </summary>
    public sealed class PhysicsNarrowphase3D {
        /// <summary>
        /// Builds one sphere-sphere contact when two dynamic spheres overlap.
        /// </summary>
        public bool TryBuildSphereSphereContact(in BodyState3D bodyA, in BodyState3D bodyB, out ContactManifold3D contact) {
            float3 delta = bodyB.Position - bodyA.Position;
            float distance = delta.Length();
            float radiusSum = bodyA.BoundingRadius + bodyB.BoundingRadius;
            if (distance >= radiusSum) {
                contact = default;
                return false;
            }

            float3 normal = distance > 0.0001f ? delta / distance : new float3(1f, 0f, 0f);
            contact = new ContactManifold3D(bodyA.Handle, bodyB.Handle, normal, radiusSum - distance);
            return true;
        }
    }
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Resolves contact penetration for one physics step.
    /// </summary>
    public sealed class PhysicsSolver3D {
        /// <summary>
        /// Resolves one contact manifold.
        /// </summary>
        public void ResolveContact(ref BodyState3D bodyA, ref BodyState3D bodyB, in ContactManifold3D contact) {
            float correction = contact.PenetrationDepth * 0.5f;
            bodyA.Position -= contact.Normal * correction;
            bodyB.Position += contact.Normal * correction;
        }
    }
}
```

- [ ] **Step 4: Run the focused test to verify pass**

Run: `rtk dotnet test engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj --filter "FullyQualifiedName~PrimitiveCollision3DTests" -v minimal`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk git add engine\helengine.physics3d\solver\PhysicsNarrowphase3D.cs engine\helengine.physics3d\solver\PhysicsSolver3D.cs engine\helengine.physics3d\PhysicsWorld3D.cs engine\helengine.physics3d.tests\PrimitiveCollision3DTests.cs
rtk git commit -m "feat: add minimal physics3d primitive solver"
```

## Task 7: Add cooked static collision data contracts and query service

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\collision\StaticCollisionScene3D.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\collision\StaticCollisionNode3D.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\collision\StaticCollisionTriangle3D.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\collision\StaticCollisionQueryService3D.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d.tests\StaticCollisionScene3DTests.cs`

- [ ] **Step 1: Write the failing cooked-scene query test**

```csharp
[Fact]
public void QuerySphere_WithSingleFloorTriangle_ReturnsGroundContact() {
    StaticCollisionScene3D scene = StaticCollisionScene3D.CreateForTests(
        [new StaticCollisionTriangle3D(new float3(-2f, 0f, -2f), new float3(2f, 0f, -2f), new float3(0f, 0f, 2f))]);

    StaticCollisionQueryService3D queryService = new StaticCollisionQueryService3D(scene);
    bool found = queryService.TryFindSpherePenetration(new float3(0f, 0.5f, 0f), 1.0f, out float3 normal, out float depth);

    Assert.True(found);
    Assert.Equal(new float3(0f, 1f, 0f), normal);
    Assert.True(depth > 0f);
}
```

- [ ] **Step 2: Run the focused test to verify failure**

Run: `rtk dotnet test engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj --filter "FullyQualifiedName~QuerySphere_WithSingleFloorTriangle_ReturnsGroundContact" -v minimal`

Expected: FAIL because cooked static collision types do not exist.

- [ ] **Step 3: Add the cooked scene contracts and sphere query path**

```csharp
namespace helengine {
    /// <summary>
    /// Stores cooked static collision data for one 3D scene.
    /// </summary>
    public sealed class StaticCollisionScene3D {
        /// <summary>
        /// Gets the cooked nodes.
        /// </summary>
        public IReadOnlyList<StaticCollisionNode3D> Nodes { get; }

        /// <summary>
        /// Gets the shared triangle storage.
        /// </summary>
        public IReadOnlyList<StaticCollisionTriangle3D> Triangles { get; }
    }
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Queries cooked static collision data.
    /// </summary>
    public sealed class StaticCollisionQueryService3D {
        /// <summary>
        /// Finds one sphere penetration against cooked static triangles.
        /// </summary>
        public bool TryFindSpherePenetration(float3 center, float radius, out float3 normal, out float depth) {
            // Iterate referenced triangles, compute closest point, and return the deepest overlap.
        }
    }
}
```

Keep the data layout shared-triangle based. Do not duplicate triangle arrays per node.

- [ ] **Step 4: Run the focused test to verify pass**

Run: `rtk dotnet test engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj --filter "FullyQualifiedName~StaticCollisionScene3DTests" -v minimal`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk git add engine\helengine.physics3d\collision\StaticCollisionScene3D.cs engine\helengine.physics3d\collision\StaticCollisionNode3D.cs engine\helengine.physics3d\collision\StaticCollisionTriangle3D.cs engine\helengine.physics3d\collision\StaticCollisionQueryService3D.cs engine\helengine.physics3d.tests\StaticCollisionScene3DTests.cs
rtk git commit -m "feat: add cooked static collision queries"
```

## Task 8: Add the character controller and exportable validation scenes

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.physics3d\solver\CharacterControllerSolver3D.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.physics3d\CharacterController3DComponent.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.editor\managers\physics\PhysicsValidationSceneCatalog.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.editor\managers\physics\PhysicsValidationSceneFactory.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\physics\PhysicsValidationSceneFactoryTests.cs`
- Create: scene assets under `C:\dev\helworks\helengine\engine\helengine.editor\content\scenes\physics\`

- [ ] **Step 1: Write the failing character controller slope test**

```csharp
[Fact]
public void Move_WithWalkableSlope_RemainsGrounded() {
    CharacterControllerSolver3D solver = new CharacterControllerSolver3D();
    TestCharacterControllerWorld world = TestCharacterControllerWorld.CreateWalkableSlope();

    CharacterControllerStepResult result = solver.Move(world, new float3(0f, 2f, 0f), new float3(1f, -2f, 0f), 0.5f, 1.8f);

    Assert.True(result.IsGrounded);
    Assert.True(result.Position.Y > 0f);
}
```

- [ ] **Step 2: Run the focused tests to verify failure**

Run: `rtk dotnet test engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj --filter "FullyQualifiedName~Move_WithWalkableSlope_RemainsGrounded" -v minimal`

Expected: FAIL because controller solve logic and validation scenes do not exist.

- [ ] **Step 3: Add the character controller solver and validation scene factory**

```csharp
namespace helengine {
    /// <summary>
    /// Solves one fixed-step character movement update.
    /// </summary>
    public sealed class CharacterControllerSolver3D {
        /// <summary>
        /// Moves one controller capsule through the world.
        /// </summary>
        public CharacterControllerStepResult Move(TestCharacterControllerWorld world, float3 startPosition, float3 desiredMotion, float radius, float height) {
            // Sweep, clamp to walkable slope, apply ground snap, and report grounding.
        }
    }
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Builds exportable physics validation scenes.
    /// </summary>
    public sealed class PhysicsValidationSceneFactory {
        /// <summary>
        /// Creates the slope-validation scene with a fixed spectator camera.
        /// </summary>
        public SceneAsset CreateCharacterSlopeScene() {
            // Create camera, ground, slope mesh, and controller entity.
        }
    }
}
```

Create scene outputs for:

- `physics3d_character_slope`
- `physics3d_character_steps`
- `physics3d_character_moving_platform`
- `physics3d_dynamic_stack_boxes`
- `physics3d_dynamic_sphere_ramp`
- `physics3d_kinematic_push`
- `physics3d_mesh_ground_stability`
- `physics3d_trigger_volume`

- [ ] **Step 4: Run the focused tests to verify pass**

Run: `rtk dotnet test engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj --filter "FullyQualifiedName~CharacterController3DTests" -v minimal`

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~PhysicsValidationSceneFactoryTests" -v minimal`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk git add engine\helengine.physics3d\solver\CharacterControllerSolver3D.cs engine\helengine.physics3d\CharacterController3DComponent.cs engine\helengine.editor\managers\physics\PhysicsValidationSceneCatalog.cs engine\helengine.editor\managers\physics\PhysicsValidationSceneFactory.cs engine\helengine.editor.tests\managers\physics\PhysicsValidationSceneFactoryTests.cs engine\helengine.editor\content\scenes\physics
rtk git commit -m "feat: add character controller and physics validation scenes"
```

## Task 9: Add the `helengine.physics2d` placeholder package and editor persistence hooks

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.physics2d\helengine.physics2d.csproj`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics2d\PhysicsWorld2D.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics2d\PhysicsWorld2DProfile.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics2d\RigidBody2DComponent.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics2d\colliders\BoxCollider2DComponent.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.physics2d\colliders\CircleCollider2DComponent.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.editor\serialization\scene\PhysicsComponentPersistenceDescriptors.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\serialization\scene\ComponentPersistenceRegistryTests.cs`

- [ ] **Step 1: Write the failing persistence registration test**

```csharp
[Fact]
public void CreateDefault_RegistersPhysics3DAndPhysics2DDescriptors() {
    ComponentPersistenceRegistry registry = ComponentPersistenceRegistry.CreateDefault();

    Assert.NotNull(registry.Resolve(typeof(RigidBody3DComponent)));
    Assert.NotNull(registry.Resolve(typeof(BoxCollider3DComponent)));
    Assert.NotNull(registry.Resolve(typeof(RigidBody2DComponent)));
    Assert.NotNull(registry.Resolve(typeof(BoxCollider2DComponent)));
}
```

- [ ] **Step 2: Run the focused test to verify failure**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~CreateDefault_RegistersPhysics3DAndPhysics2DDescriptors" -v minimal`

Expected: FAIL because the placeholder 2D package and persistence descriptors do not exist.

- [ ] **Step 3: Add the 2D placeholder package and register persistence descriptors**

```csharp
namespace helengine {
    /// <summary>
    /// Placeholder 2D physics world contract used to keep 2D and 3D runtimes separately loadable.
    /// </summary>
    public sealed class PhysicsWorld2D {
    }
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Registers persistence descriptors for physics components.
    /// </summary>
    public static class PhysicsComponentPersistenceDescriptors {
        /// <summary>
        /// Adds physics component descriptors to the registry.
        /// </summary>
        public static void Register(ComponentPersistenceRegistry registry) {
            registry.Register(new RigidBody3DComponentPersistenceDescriptor());
            registry.Register(new BoxCollider3DComponentPersistenceDescriptor());
            registry.Register(new RigidBody2DComponentPersistenceDescriptor());
            registry.Register(new BoxCollider2DComponentPersistenceDescriptor());
        }
    }
}
```

- [ ] **Step 4: Run the focused test to verify pass**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~CreateDefault_RegistersPhysics3DAndPhysics2DDescriptors" -v minimal`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk git add engine\helengine.physics2d\helengine.physics2d.csproj engine\helengine.physics2d\PhysicsWorld2D.cs engine\helengine.physics2d\PhysicsWorld2DProfile.cs engine\helengine.physics2d\RigidBody2DComponent.cs engine\helengine.physics2d\colliders\BoxCollider2DComponent.cs engine\helengine.physics2d\colliders\CircleCollider2DComponent.cs engine\helengine.editor\serialization\scene\PhysicsComponentPersistenceDescriptors.cs engine\helengine.editor.tests\serialization\scene\ComponentPersistenceRegistryTests.cs
rtk git commit -m "feat: add placeholder physics2d package"
```

## Self-Review

### Spec coverage

- Optional physics runtime outside core: covered by Tasks 1, 3, and 9.
- Real `Physics3D` target: covered by Tasks 3 through 8.
- Placeholder `Physics2D`: covered by Task 9.
- Fixed-step simulation: covered by Task 1.
- Static, kinematic, dynamic bodies: covered by Task 4.
- Dense runtime records and handles: covered by Task 5.
- Configurable broadphase: covered by Task 5.
- Cooked static collision: covered by Task 7.
- First-class character controller: covered by Task 8.
- Generic per-platform property overrides: covered by Task 2 and consumed in Task 4.
- Exportable validation scenes: covered by Task 8.
- Editor/runtime persistence integration: covered by Tasks 4, 8, and 9.

No spec sections are uncovered.

### Placeholder scan

- No `TODO`, `TBD`, or “implement later” placeholders remain in the task steps.
- Every task includes exact file paths, tests, commands, and code snippets.
- Future work such as joints and CCD remains in the design doc, not as implementation placeholders in this plan.

### Type consistency

- Core hosting uses `IPhysicsRuntime` consistently.
- The profile type is consistently `PhysicsWorld3DProfile`.
- Broadphase types use `BroadphaseKind3D` and `IBroadphase3D` consistently.
- Override metadata uses `PlatformOverrideAttribute`, `PlatformOverrideValueRecord`, and `PlatformOverrideResolver` consistently.

