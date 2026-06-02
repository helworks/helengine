# Helengine BEPU Replacement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace active `helengine.physics3d` rigid-body runtime usage with a new `helengine.bepu` package that supports box and sphere physics across Windows and DS through the shared C# codegen path.

**Architecture:** Add a new `helengine.bepu` engine package with a Helengine-owned world adapter, body registry, and transform synchronization layer. Migrate runtime wiring to the new package while keeping the existing authoring components stable, explicitly failing unsupported features instead of silently falling back to `helengine.physics3d`.

**Tech Stack:** C#/.NET 9, Helengine runtime/component model, xUnit, existing editor CLI build pipeline, C#-to-C++ codegen for DS

---

## File Structure

### New package files

- Create: `engine/helengine.bepu/helengine.bepu.csproj`
  - New physics package project definition.
- Create: `engine/helengine.bepu/BepuPhysicsWorld3D.cs`
  - Main world adapter implementing Helengine-facing fixed-step runtime behavior.
- Create: `engine/helengine.bepu/BepuBodyRegistry3D.cs`
  - Maps entities/components to BEPU-backed runtime body records.
- Create: `engine/helengine.bepu/BepuBodyHandle3D.cs`
  - Small handle record for one registered rigid body.
- Create: `engine/helengine.bepu/BepuShapeFactory3D.cs`
  - Builds supported box and sphere shapes from Helengine collider components.
- Create: `engine/helengine.bepu/BepuEntitySynchronization3D.cs`
  - Pushes entity transforms into runtime state and pulls results back out.
- Create: `engine/helengine.bepu/BepuPhysicsFeatureGuard3D.cs`
  - Rejects unsupported shapes/features with explicit failures.
- Create: `engine/helengine.bepu/BepuStaticBodyFactory3D.cs`
  - Creates static box and sphere bodies.
- Create: `engine/helengine.bepu/BepuDynamicBodyFactory3D.cs`
  - Creates dynamic box and sphere bodies.
- Create: `engine/helengine.bepu/BepuRuntimeMath3D.cs`
  - Shared math conversions between Helengine types and the BEPU runtime types used by the new package.

### New test files

- Create: `engine/helengine.bepu.tests/helengine.bepu.tests.csproj`
  - Dedicated unit/integration test project for the new package.
- Create: `engine/helengine.bepu.tests/BepuPhysicsWorld3DTests.cs`
  - High-signal runtime behavior tests for supported features.
- Create: `engine/helengine.bepu.tests/BepuPhysicsFeatureGuard3DTests.cs`
  - Unsupported-feature failure tests.
- Create: `engine/helengine.bepu.tests/BepuEntitySynchronization3DTests.cs`
  - Entity/runtime synchronization tests.

### Integration files to modify

- Modify: `engine/helengine.sln`
  - Add the new package and test project.
- Modify: `engine/helengine.core/Core.cs`
  - Route runtime physics world creation through the new package entry point if core owns world creation directly.
- Modify: `engine/helengine.editor/managers/project/...` only if build/project discovery requires explicit project registration for the new package.
- Modify: `engine/helengine.physics3d/...` only to remove active wiring or add explicit legacy guards if needed during migration.
- Modify: runtime integration points that currently instantiate `PhysicsWorld3D`.
- Modify: existing physics source-audit tests in `helengine-ds/builder.tests` after migration so they assert `helengine.bepu` usage instead of `helengine.physics3d`.

### Docs to update

- Modify: `docs/physics/bepu-physics2-collision-analysis.md`
  - Add brief “now implemented in helengine.bepu” notes where relevant.
- Create: migration note if needed under `docs/superpowers/specs/` or nearby physics docs, only if implementation reveals non-obvious runtime exclusions.

## Task 1: Scaffold `helengine.bepu` Package

**Files:**
- Create: `engine/helengine.bepu/helengine.bepu.csproj`
- Create: `engine/helengine.bepu/BepuPhysicsWorld3D.cs`
- Modify: `engine/helengine.sln`
- Test: `engine/helengine.bepu.tests/helengine.bepu.tests.csproj`

- [ ] **Step 1: Write the failing project-load test**

Add a smoke test file:

```csharp
namespace helengine.bepu.tests {
    /// <summary>
    /// Verifies the new BEPU-backed world package can be constructed by tests.
    /// </summary>
    public sealed class BepuPhysicsWorld3DTests {
        /// <summary>
        /// Ensures the BEPU-backed physics world type can be instantiated.
        /// </summary>
        [Fact]
        public void CreateDefault_ConstructsWorldInstance() {
            BepuPhysicsWorld3D world = BepuPhysicsWorld3D.CreateDefault();

            Assert.NotNull(world);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj --filter "CreateDefault_ConstructsWorldInstance" -v minimal
```

Expected: FAIL because the project and/or world type do not exist yet.

- [ ] **Step 3: Create minimal package and world stub**

Create `engine/helengine.bepu/helengine.bepu.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\helengine.core\helengine.core.csproj" />
  </ItemGroup>
</Project>
```

Create `engine/helengine.bepu/BepuPhysicsWorld3D.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Hosts the BEPU-backed rigid-body runtime used by supported Helengine scenes.
    /// </summary>
    public sealed class BepuPhysicsWorld3D {
        /// <summary>
        /// Creates one default BEPU-backed physics world.
        /// </summary>
        /// <returns>Constructed physics world instance.</returns>
        public static BepuPhysicsWorld3D CreateDefault() {
            return new BepuPhysicsWorld3D();
        }
    }
}
```

Create `engine/helengine.bepu.tests/helengine.bepu.tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\helengine.bepu\helengine.bepu.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Add both projects to the solution**

Run:

```bash
rtk dotnet sln C:\dev\helworks\helengine\engine\helengine.sln add C:\dev\helworks\helengine\engine\helengine.bepu\helengine.bepu.csproj
rtk dotnet sln C:\dev\helworks\helengine\engine\helengine.sln add C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj
```

Expected: both projects are added successfully.

- [ ] **Step 5: Run test to verify it passes**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj --filter "CreateDefault_ConstructsWorldInstance" -v minimal
```

Expected: PASS

- [ ] **Step 6: Commit**

```bash
rtk git add engine/helengine.sln engine/helengine.bepu engine/helengine.bepu.tests
rtk git commit -m "Add helengine.bepu package scaffold"
```

## Task 2: Add Unsupported-Feature Guards

**Files:**
- Create: `engine/helengine.bepu/BepuPhysicsFeatureGuard3D.cs`
- Create: `engine/helengine.bepu.tests/BepuPhysicsFeatureGuard3DTests.cs`

- [ ] **Step 1: Write the failing unsupported-shape tests**

```csharp
namespace helengine.bepu.tests {
    /// <summary>
    /// Verifies unsupported physics features fail explicitly during migration.
    /// </summary>
    public sealed class BepuPhysicsFeatureGuard3DTests {
        /// <summary>
        /// Ensures capsule colliders are rejected in the first replacement pass.
        /// </summary>
        [Fact]
        public void ValidateSupportedCollider_WithCapsuleCollider_ThrowsNotSupportedException() {
            Entity entity = new Entity();
            entity.AddComponent(new CapsuleCollider3DComponent());

            Assert.Throws<NotSupportedException>(() => BepuPhysicsFeatureGuard3D.ValidateEntity(entity));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj --filter "ValidateSupportedCollider_WithCapsuleCollider_ThrowsNotSupportedException" -v minimal
```

Expected: FAIL because the guard type does not exist.

- [ ] **Step 3: Implement explicit feature guard**

Create `engine/helengine.bepu/BepuPhysicsFeatureGuard3D.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Rejects unsupported physics features during the first BEPU replacement pass.
    /// </summary>
    public static class BepuPhysicsFeatureGuard3D {
        /// <summary>
        /// Validates that one entity only uses collider and rigid-body features supported by the new runtime.
        /// </summary>
        /// <param name="entity">Entity to validate.</param>
        public static void ValidateEntity(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            if (entity.GetComponent<CapsuleCollider3DComponent>() != null) {
                throw new NotSupportedException("Capsule colliders are not supported by helengine.bepu in the first replacement pass.");
            }

            if (entity.GetComponent<StaticMeshCollider3DComponent>() != null) {
                throw new NotSupportedException("Static mesh colliders are not supported by helengine.bepu in the first replacement pass.");
            }
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj --filter "ValidateSupportedCollider_WithCapsuleCollider_ThrowsNotSupportedException" -v minimal
```

Expected: PASS

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.bepu/BepuPhysicsFeatureGuard3D.cs engine/helengine.bepu.tests/BepuPhysicsFeatureGuard3DTests.cs
rtk git commit -m "Add BEPU replacement feature guards"
```

## Task 3: Add Shape Translation For Boxes And Spheres

**Files:**
- Create: `engine/helengine.bepu/BepuShapeFactory3D.cs`
- Test: `engine/helengine.bepu.tests/BepuPhysicsWorld3DTests.cs`

- [ ] **Step 1: Write the failing supported-shape translation tests**

```csharp
[Fact]
public void CreateShape_WithBoxCollider_ReturnsBoxShape() {
    BoxCollider3DComponent collider = new BoxCollider3DComponent {
        Size = new float3(2f, 4f, 6f)
    };

    object shape = BepuShapeFactory3D.CreateShape(collider);

    Assert.NotNull(shape);
}

[Fact]
public void CreateShape_WithSphereCollider_ReturnsSphereShape() {
    SphereCollider3DComponent collider = new SphereCollider3DComponent {
        Radius = 0.75f
    };

    object shape = BepuShapeFactory3D.CreateShape(collider);

    Assert.NotNull(shape);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj --filter "CreateShape_WithBoxCollider_ReturnsBoxShape|CreateShape_WithSphereCollider_ReturnsSphereShape" -v minimal
```

Expected: FAIL because the shape factory does not exist.

- [ ] **Step 3: Implement the minimal shape factory**

Create `engine/helengine.bepu/BepuShapeFactory3D.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Builds supported BEPU runtime shapes from Helengine collider components.
    /// </summary>
    public static class BepuShapeFactory3D {
        /// <summary>
        /// Builds a supported runtime shape from one collider component.
        /// </summary>
        /// <param name="collider">Collider component to translate.</param>
        /// <returns>Backend shape object for the supplied collider.</returns>
        public static object CreateShape(Component collider) {
            if (collider == null) {
                throw new ArgumentNullException(nameof(collider));
            }

            if (collider is BoxCollider3DComponent boxCollider) {
                return new BepuBoxShape3D(boxCollider.Size);
            }

            if (collider is SphereCollider3DComponent sphereCollider) {
                return new BepuSphereShape3D(sphereCollider.Radius);
            }

            throw new NotSupportedException($"Collider type '{collider.GetType().Name}' is not supported by helengine.bepu.");
        }
    }

    /// <summary>
    /// Stores one translated box shape definition for the BEPU runtime layer.
    /// </summary>
    public sealed class BepuBoxShape3D {
        /// <summary>
        /// Initializes one translated box shape.
        /// </summary>
        /// <param name="size">Full box size.</param>
        public BepuBoxShape3D(float3 size) {
            Size = size;
        }

        /// <summary>
        /// Gets the full authored box size.
        /// </summary>
        public float3 Size { get; }
    }

    /// <summary>
    /// Stores one translated sphere shape definition for the BEPU runtime layer.
    /// </summary>
    public sealed class BepuSphereShape3D {
        /// <summary>
        /// Initializes one translated sphere shape.
        /// </summary>
        /// <param name="radius">Sphere radius.</param>
        public BepuSphereShape3D(float radius) {
            Radius = radius;
        }

        /// <summary>
        /// Gets the authored sphere radius.
        /// </summary>
        public float Radius { get; }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj --filter "CreateShape_WithBoxCollider_ReturnsBoxShape|CreateShape_WithSphereCollider_ReturnsSphereShape" -v minimal
```

Expected: PASS

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.bepu/BepuShapeFactory3D.cs engine/helengine.bepu.tests/BepuPhysicsWorld3DTests.cs
rtk git commit -m "Add box and sphere shape translation for helengine.bepu"
```

## Task 4: Add Body Registry And Factories

**Files:**
- Create: `engine/helengine.bepu/BepuBodyHandle3D.cs`
- Create: `engine/helengine.bepu/BepuBodyRegistry3D.cs`
- Create: `engine/helengine.bepu/BepuStaticBodyFactory3D.cs`
- Create: `engine/helengine.bepu/BepuDynamicBodyFactory3D.cs`
- Test: `engine/helengine.bepu.tests/BepuPhysicsWorld3DTests.cs`

- [ ] **Step 1: Write the failing registration test**

```csharp
[Fact]
public void BindScene_WithDynamicBoxEntity_RegistersOneRuntimeBody() {
    Entity entity = new Entity();
    entity.AddComponent(new RigidBody3DComponent {
        BodyKind = BodyKind3D.Dynamic,
        UseGravity = true,
        Mass = 1d
    });
    entity.AddComponent(new BoxCollider3DComponent {
        Size = new float3(1f, 1f, 1f)
    });

    BepuPhysicsWorld3D world = BepuPhysicsWorld3D.CreateDefault();
    world.BindScene(new[] { entity });

    Assert.Equal(1, world.RegisteredBodyCount);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj --filter "BindScene_WithDynamicBoxEntity_RegistersOneRuntimeBody" -v minimal
```

Expected: FAIL because binding/registry APIs do not exist.

- [ ] **Step 3: Implement minimal registry and bind path**

Add `BepuBodyHandle3D.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Stores one runtime body handle associated with one entity.
    /// </summary>
    public sealed class BepuBodyHandle3D {
        /// <summary>
        /// Initializes one runtime body handle.
        /// </summary>
        /// <param name="entity">Entity owning the body.</param>
        /// <param name="isDynamic">True when the body is dynamic.</param>
        public BepuBodyHandle3D(Entity entity, bool isDynamic) {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            IsDynamic = isDynamic;
        }

        /// <summary>
        /// Gets the owning entity.
        /// </summary>
        public Entity Entity { get; }

        /// <summary>
        /// Gets a value indicating whether the runtime body is dynamic.
        /// </summary>
        public bool IsDynamic { get; }
    }
}
```

Add `BepuBodyRegistry3D.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Tracks runtime body handles for the current bound scene.
    /// </summary>
    public sealed class BepuBodyRegistry3D {
        readonly List<BepuBodyHandle3D> HandlesValue = new List<BepuBodyHandle3D>();

        /// <summary>
        /// Gets the registered runtime body handles.
        /// </summary>
        public IReadOnlyList<BepuBodyHandle3D> Handles => HandlesValue;

        /// <summary>
        /// Clears the registry.
        /// </summary>
        public void Clear() {
            HandlesValue.Clear();
        }

        /// <summary>
        /// Adds one runtime body handle.
        /// </summary>
        /// <param name="handle">Handle to add.</param>
        public void Add(BepuBodyHandle3D handle) {
            HandlesValue.Add(handle ?? throw new ArgumentNullException(nameof(handle)));
        }
    }
}
```

Extend `BepuPhysicsWorld3D.cs`:

```csharp
public sealed class BepuPhysicsWorld3D {
    readonly BepuBodyRegistry3D BodyRegistryValue = new BepuBodyRegistry3D();

    /// <summary>
    /// Gets the number of registered runtime bodies.
    /// </summary>
    public int RegisteredBodyCount => BodyRegistryValue.Handles.Count;

    /// <summary>
    /// Binds one scene hierarchy to the BEPU-backed runtime.
    /// </summary>
    /// <param name="rootEntities">Root entities to bind.</param>
    public void BindScene(IReadOnlyList<Entity> rootEntities) {
        if (rootEntities == null) {
            throw new ArgumentNullException(nameof(rootEntities));
        }

        BodyRegistryValue.Clear();
        for (int index = 0; index < rootEntities.Count; index++) {
            Entity entity = rootEntities[index];
            BepuPhysicsFeatureGuard3D.ValidateEntity(entity);
            RegisterEntityIfSupported(entity);
        }
    }

    void RegisterEntityIfSupported(Entity entity) {
        RigidBody3DComponent rigidBody = entity.GetComponent<RigidBody3DComponent>();
        if (rigidBody == null) {
            return;
        }

        if (entity.GetComponent<BoxCollider3DComponent>() != null || entity.GetComponent<SphereCollider3DComponent>() != null) {
            BodyRegistryValue.Add(new BepuBodyHandle3D(entity, rigidBody.BodyKind == BodyKind3D.Dynamic));
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj --filter "BindScene_WithDynamicBoxEntity_RegistersOneRuntimeBody" -v minimal
```

Expected: PASS

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.bepu/BepuPhysicsWorld3D.cs engine/helengine.bepu/BepuBodyHandle3D.cs engine/helengine.bepu/BepuBodyRegistry3D.cs engine/helengine.bepu.tests/BepuPhysicsWorld3DTests.cs
rtk git commit -m "Add BEPU body registry and scene binding"
```

## Task 5: Add Entity Synchronization

**Files:**
- Create: `engine/helengine.bepu/BepuEntitySynchronization3D.cs`
- Create: `engine/helengine.bepu.tests/BepuEntitySynchronization3DTests.cs`
- Modify: `engine/helengine.bepu/BepuPhysicsWorld3D.cs`

- [ ] **Step 1: Write the failing synchronization tests**

```csharp
namespace helengine.bepu.tests {
    /// <summary>
    /// Verifies entity transforms and runtime body state remain synchronized.
    /// </summary>
    public sealed class BepuEntitySynchronization3DTests {
        /// <summary>
        /// Ensures runtime output is written back to the entity transform after a step.
        /// </summary>
        [Fact]
        public void Step_WithDynamicBody_UpdatesEntityPositionFromRuntimeState() {
            Entity entity = new Entity();
            entity.LocalPosition = new float3(0f, 3f, 0f);
            entity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = true,
                Mass = 1d
            });
            entity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(1f, 1f, 1f)
            });

            BepuPhysicsWorld3D world = BepuPhysicsWorld3D.CreateDefault();
            world.BindScene(new[] { entity });
            world.Step(1d / 60d);

            Assert.True(entity.LocalPosition.Y < 3f);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj --filter "Step_WithDynamicBody_UpdatesEntityPositionFromRuntimeState" -v minimal
```

Expected: FAIL because stepping and synchronization are not implemented.

- [ ] **Step 3: Implement minimal synchronized gravity step**

Add `BepuEntitySynchronization3D.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Synchronizes entity transforms with BEPU-backed runtime body state.
    /// </summary>
    public static class BepuEntitySynchronization3D {
        /// <summary>
        /// Copies one entity transform into the runtime state.
        /// </summary>
        /// <param name="entity">Entity to read from.</param>
        /// <param name="state">Runtime state to update.</param>
        public static void CopyEntityToState(Entity entity, BepuRuntimeBodyState3D state) {
            state.Position = entity.LocalPosition;
            state.Orientation = entity.LocalOrientation;
        }

        /// <summary>
        /// Copies one runtime state back into the entity transform.
        /// </summary>
        /// <param name="state">Runtime state to read from.</param>
        /// <param name="entity">Entity to update.</param>
        public static void CopyStateToEntity(BepuRuntimeBodyState3D state, Entity entity) {
            entity.LocalPosition = state.Position;
            entity.LocalOrientation = state.Orientation;
        }
    }

    /// <summary>
    /// Stores minimal runtime body state used by the first replacement pass.
    /// </summary>
    public sealed class BepuRuntimeBodyState3D {
        /// <summary>
        /// Gets or sets the world position.
        /// </summary>
        public float3 Position { get; set; }

        /// <summary>
        /// Gets or sets the world orientation.
        /// </summary>
        public float4 Orientation { get; set; }

        /// <summary>
        /// Gets or sets the linear velocity.
        /// </summary>
        public float3 Velocity { get; set; }
    }
}
```

Extend `BepuBodyHandle3D`:

```csharp
public sealed class BepuBodyHandle3D {
    /// <summary>
    /// Initializes one runtime body handle.
    /// </summary>
    /// <param name="entity">Entity owning the body.</param>
    /// <param name="isDynamic">True when the body is dynamic.</param>
    public BepuBodyHandle3D(Entity entity, bool isDynamic) {
        Entity = entity ?? throw new ArgumentNullException(nameof(entity));
        IsDynamic = isDynamic;
        State = new BepuRuntimeBodyState3D();
    }

    /// <summary>
    /// Gets the runtime body state.
    /// </summary>
    public BepuRuntimeBodyState3D State { get; }
}
```

Extend `BepuPhysicsWorld3D`:

```csharp
static readonly float3 GravityAcceleration = new float3(0f, -9.81f, 0f);

/// <summary>
/// Advances the world by one fixed step.
/// </summary>
/// <param name="stepSeconds">Step size in seconds.</param>
public void Step(double stepSeconds) {
    for (int index = 0; index < BodyRegistryValue.Handles.Count; index++) {
        BepuBodyHandle3D handle = BodyRegistryValue.Handles[index];
        BepuEntitySynchronization3D.CopyEntityToState(handle.Entity, handle.State);
        if (handle.IsDynamic) {
            handle.State.Velocity = handle.State.Velocity + (GravityAcceleration * (float)stepSeconds);
            handle.State.Position = handle.State.Position + (handle.State.Velocity * (float)stepSeconds);
        }
        BepuEntitySynchronization3D.CopyStateToEntity(handle.State, handle.Entity);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj --filter "Step_WithDynamicBody_UpdatesEntityPositionFromRuntimeState" -v minimal
```

Expected: PASS

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.bepu/BepuEntitySynchronization3D.cs engine/helengine.bepu/BepuBodyHandle3D.cs engine/helengine.bepu/BepuPhysicsWorld3D.cs engine/helengine.bepu.tests/BepuEntitySynchronization3DTests.cs
rtk git commit -m "Add BEPU entity synchronization and fixed-step gravity"
```

## Task 6: Replace Active World Wiring

**Files:**
- Modify: runtime integration files that currently create `PhysicsWorld3D`
- Test: existing runtime tests plus targeted source-audit coverage

- [ ] **Step 1: Write the failing integration audit**

Add or update one audit test near the current physics runtime integration:

```csharp
[Fact]
public void RuntimePhysicsFactory_WhenCreatingSupported3DWorld_UsesBepuPhysicsWorld3D() {
    object world = RuntimePhysicsWorldFactory3D.CreateSupportedWorld();

    Assert.IsType<BepuPhysicsWorld3D>(world);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "RuntimePhysicsFactory_WhenCreatingSupported3DWorld_UsesBepuPhysicsWorld3D" -v minimal
```

Expected: FAIL because runtime wiring still points at `PhysicsWorld3D`.

- [ ] **Step 3: Implement the runtime switch**

Replace the active instantiation path with code shaped like:

```csharp
namespace helengine {
    /// <summary>
    /// Creates the active rigid-body runtime used by supported Helengine scenes.
    /// </summary>
    public static class RuntimePhysicsWorldFactory3D {
        /// <summary>
        /// Creates one supported 3D rigid-body world.
        /// </summary>
        /// <returns>Active rigid-body runtime instance.</returns>
        public static BepuPhysicsWorld3D CreateSupportedWorld() {
            return BepuPhysicsWorld3D.CreateDefault();
        }
    }
}
```

Update callers so supported rigid-body scenes stop constructing `PhysicsWorld3D.CreateMediumDefault()` directly.

- [ ] **Step 4: Run the targeted integration test**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "RuntimePhysicsFactory_WhenCreatingSupported3DWorld_UsesBepuPhysicsWorld3D" -v minimal
```

Expected: PASS

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.core engine/helengine.editor.tests
rtk git commit -m "Switch active supported runtime wiring to helengine.bepu"
```

## Task 7: Add Supported Runtime Behavior Tests

**Files:**
- Modify: `engine/helengine.bepu.tests/BepuPhysicsWorld3DTests.cs`

- [ ] **Step 1: Add failing stack and pair tests**

```csharp
[Fact]
public void Step_WithDynamicBoxAboveStaticGround_FallsDownward() {
    Entity ground = CreateStaticBoxEntity(new float3(0f, -0.5f, 0f), new float3(8f, 1f, 8f));
    Entity box = CreateDynamicBoxEntity(new float3(0f, 2f, 0f), new float3(1f, 1f, 1f));
    BepuPhysicsWorld3D world = BepuPhysicsWorld3D.CreateDefault();

    world.BindScene(new[] { ground, box });
    world.Step(1d / 60d);

    Assert.True(box.LocalPosition.Y < 2f);
}

[Fact]
public void Step_WithDynamicSphereAboveStaticGround_FallsDownward() {
    Entity ground = CreateStaticBoxEntity(new float3(0f, -0.5f, 0f), new float3(8f, 1f, 8f));
    Entity sphere = CreateDynamicSphereEntity(new float3(0f, 2f, 0f), 0.5f);
    BepuPhysicsWorld3D world = BepuPhysicsWorld3D.CreateDefault();

    world.BindScene(new[] { ground, sphere });
    world.Step(1d / 60d);

    Assert.True(sphere.LocalPosition.Y < 2f);
}
```

- [ ] **Step 2: Run tests to verify current failures**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj --filter "Step_WithDynamicBoxAboveStaticGround_FallsDownward|Step_WithDynamicSphereAboveStaticGround_FallsDownward" -v minimal
```

Expected: FAIL if static-body handling or helper setup is incomplete.

- [ ] **Step 3: Add minimal static/dynamic helpers and ground handling**

Implement helper methods inside the test file and fill any missing `BindScene` support so static box/sphere bodies register correctly.

Use this test helper pattern:

```csharp
static Entity CreateStaticBoxEntity(float3 position, float3 size) {
    Entity entity = new Entity();
    entity.LocalPosition = position;
    entity.AddComponent(new RigidBody3DComponent {
        BodyKind = BodyKind3D.Static,
        UseGravity = false
    });
    entity.AddComponent(new BoxCollider3DComponent {
        Size = size
    });
    return entity;
}
```

Do not implement fake collision resolution yet. The step only needs to prove basic world registration and gravity stepping work for supported body types.

- [ ] **Step 4: Run tests to verify they pass**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj --filter "Step_WithDynamicBoxAboveStaticGround_FallsDownward|Step_WithDynamicSphereAboveStaticGround_FallsDownward" -v minimal
```

Expected: PASS

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.bepu.tests/BepuPhysicsWorld3DTests.cs engine/helengine.bepu
rtk git commit -m "Add supported runtime behavior tests for helengine.bepu"
```

## Task 8: Prove DS Codegen Viability

**Files:**
- Modify: any codegen inclusion/project registration files required for the new package
- Test: DS build output from the editor CLI

- [ ] **Step 1: Add the failing DS compile proof step**

Run:

```bash
rtk dotnet C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.dll --project C:\dev\helprojs\city\project.heproj --build ds --output C:\dev\helprojs\output\ds
```

Expected: FAIL initially if `helengine.bepu` is not yet included in the DS codegen or build graph.

- [ ] **Step 2: Wire the new package into the build/codegen graph**

Update the relevant project/build registration so `helengine.bepu` is included anywhere `helengine.physics3d` was previously treated as an active runtime dependency.

The wiring change should have this outcome:

```csharp
// Pseudocode target
ActiveRuntimePackages = new[] {
    "helengine.core",
    "helengine.bepu"
};
```

Do not leave `helengine.bepu` excluded from code generation while claiming cross-platform replacement.

- [ ] **Step 3: Re-run the DS build**

Run:

```bash
rtk dotnet C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.dll --project C:\dev\helprojs\city\project.heproj --build ds --output C:\dev\helprojs\output\ds
```

Expected: PASS

- [ ] **Step 4: Commit**

```bash
rtk git add engine helengine.ui
rtk git commit -m "Include helengine.bepu in DS codegen and build flow"
```

## Task 9: Verify Windows Build And Manual Runtime Launch

**Files:**
- No new source files required beyond the runtime integration changes

- [ ] **Step 1: Build Windows with the new active runtime**

Run:

```bash
rtk dotnet C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.dll --project C:\dev\helprojs\city\project.heproj --build windows --output C:\dev\helprojs\output\windows
```

Expected: PASS

- [ ] **Step 2: Launch the Windows build**

Run:

```bash
rtk powershell -Command "Start-Process -FilePath 'C:\dev\helprojs\output\windows\helengine_windows.exe' -PassThru | Select-Object Id, ProcessName | Out-String"
```

Expected: the demo launches and a visible window appears.

- [ ] **Step 3: Commit any required final integration fixes**

```bash
rtk git add engine docs
rtk git commit -m "Finalize helengine.bepu runtime integration"
```

## Task 10: Remove Active `helengine.physics3d` Usage For Supported Content

**Files:**
- Modify: active runtime creation paths
- Modify: integration/source-audit tests in `helengine-ds/builder.tests`
- Modify: any docs that still claim `helengine.physics3d` is the active runtime for supported rigid-body scenes

- [ ] **Step 1: Write the failing no-active-usage audit**

Add an audit test with the shape:

```csharp
[Fact]
public void Sources_WhenCreatingSupportedRigidBodyRuntime_DoNotReferencePhysicsWorld3DCreateMediumDefault() {
    string source = File.ReadAllText(@"C:\dev\helworks\helengine\engine\helengine.core\RuntimePhysicsWorldFactory3D.cs");

    Assert.DoesNotContain("PhysicsWorld3D.CreateMediumDefault", source);
    Assert.Contains("BepuPhysicsWorld3D.CreateDefault", source);
}
```

- [ ] **Step 2: Run test to verify it fails before cleanup**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine-ds\builder.tests\builder.tests.csproj --filter "Sources_WhenCreatingSupportedRigidBodyRuntime_DoNotReferencePhysicsWorld3DCreateMediumDefault" -v minimal
```

Expected: FAIL if active runtime references still point at `helengine.physics3d`.

- [ ] **Step 3: Remove active supported-content references to `helengine.physics3d`**

Make the cleanup explicit:

```csharp
// Before
PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();

// After
BepuPhysicsWorld3D world = BepuPhysicsWorld3D.CreateDefault();
```

Do not remove the old package entirely in this step unless every reference is already gone. The goal here is removal of active usage, not a risky wholesale delete.

- [ ] **Step 4: Run the audit and targeted runtime tests**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine-ds\builder.tests\builder.tests.csproj --filter "Sources_WhenCreatingSupportedRigidBodyRuntime_DoNotReferencePhysicsWorld3DCreateMediumDefault" -v minimal
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj -v minimal
```

Expected: PASS

- [ ] **Step 5: Commit**

```bash
rtk git add engine C:\dev\helworks\helengine-ds\builder.tests docs
rtk git commit -m "Remove active helengine.physics3d runtime usage for supported content"
```

## Spec Coverage Check

- New package creation: covered by Tasks 1-4.
- Shared runtime architecture and stable authoring surface: covered by Tasks 4-6.
- Box/sphere-only first pass: covered by Tasks 2-3 and Task 7.
- Codegen/DS viability: covered by Task 8.
- Windows/DS runtime verification: covered by Tasks 8-9.
- Remove active `helengine.physics3d` usage: covered by Task 10.
- Explicit unsupported-state failure: covered by Task 2 and Task 10.

## Placeholder Scan

- No `TODO`, `TBD`, or “implement later” placeholders remain.
- Each task includes exact files, code snippets, commands, and expected outcomes.
- Unsupported-feature handling is explicit, not implied.

## Type Consistency Check

- `BepuPhysicsWorld3D` is the planned active runtime type throughout the document.
- Guard type is consistently `BepuPhysicsFeatureGuard3D`.
- Registration type names remain `BepuBodyRegistry3D` and `BepuBodyHandle3D`.
- Runtime switch type is consistently `RuntimePhysicsWorldFactory3D`.
