# BEPU Static Mesh Cook Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add static world-mesh collision support to `helengine.bepu` by cooking BEPU mesh payloads ahead of time and binding only prebuilt mesh data at runtime.

**Architecture:** Keep authored static-mesh collision data generic in `helengine.physics`, add one opaque cooked-runtime payload to the shared collider contract, expose a generic cook-processor registry from the editor packaging path, and let a BEPU-owned processor populate that payload. At runtime, `helengine.bepu` will deserialize the cooked payload into a BEPU `Mesh`, register it as a static, and explicitly reject missing or invalid mesh payloads.

**Tech Stack:** C#/.NET 9, Helengine reflected scene persistence, `helengine.editor` scene packaging, `helengine.bepu`, upstream BEPU `Mesh` serialization, xUnit.

---

## File Structure

### Shared physics contract

- Create: `engine/helengine.physics/StaticMeshCollisionRuntimeData3D.cs`
  - Opaque cooked-runtime payload for static mesh colliders.
- Create: `engine/helengine.physics/IStaticMeshCollisionCookProcessor3D.cs`
  - Generic cook-time processor contract implemented by runtime plugins.
- Modify: `engine/helengine.physics/StaticMeshCollider3DComponent.cs`
  - Add the cooked-runtime payload property alongside the existing generic collision data.

### Editor packaging seam

- Create: `engine/helengine.editor/managers/project/StaticMeshCollisionCookProcessorRegistry.cs`
  - Generic registry that stores cook processors without hardcoding BEPU.
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
  - Detect static mesh colliders during reflected packaging, invoke the registered processor, and write the cooked payload back into the serialized component.
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildScenePackager.cs`
  - Pass the shared registry into the transform service.

### Editor host registration

- Create: `helengine.ui/helengine.editor.app/EditorHostStaticMeshCollisionCookProcessorRegistration.cs`
  - Register BEPU’s cook processor into the generic editor registry at host startup.
- Modify: `helengine.ui/helengine.editor.app/Program.cs`
  - Call the registration helper before GUI and CLI build flows.
- Modify: `helengine.ui/helengine.editor.app/helengine.editor.app.csproj`
  - Reference `helengine.bepu`.

### BEPU cook/runtime support

- Create: `engine/helengine.bepu/BepuStaticMeshCollisionCookProcessor3D.cs`
  - Convert generic vertices/indices into serialized BEPU mesh bytes.
- Create: `engine/helengine.bepu/BepuOwnedMeshShape3D.cs`
  - Own one deserialized BEPU mesh and return pooled resources during teardown.
- Modify: `engine/helengine.bepu/BepuShapeFactory3D.cs`
  - Add BEPU mesh payload deserialization helper.
- Modify: `engine/helengine.bepu/BepuBodyHandle3D.cs`
  - Track static-mesh collider handles and owned mesh resources.
- Modify: `engine/helengine.bepu/BepuPhysicsFeatureGuard3D.cs`
  - Replace unconditional static-mesh rejection with strict static-only payload validation.
- Modify: `engine/helengine.bepu/BepuPhysicsWorld3D.cs`
  - Register static mesh statics, own mesh resources, and dispose them on reset.

### Tests

- Create: `engine/helengine.editor.tests/serialization/scene/StaticMeshColliderGenericPersistenceTests.cs`
  - Shared reflected-persistence coverage for the new cooked-runtime payload.
- Modify: `engine/helengine.editor.tests/SceneComponentPackagingTransformServiceTests.cs`
  - Verify stub and real cook processors populate packaged static mesh payloads.
- Modify: `engine/helengine.editor.tests/helengine.editor.tests.csproj`
  - Reference `helengine.bepu` for the real-processor packaging test.
- Create: `engine/helengine.bepu.tests/BepuStaticMeshCollisionCookProcessorTests.cs`
  - Verify BEPU cook payloads round-trip into BEPU `Mesh`.
- Modify: `engine/helengine.bepu.tests/BepuPhysicsFeatureGuard3DTests.cs`
  - Cover unsupported dynamic/kinematic and missing-payload mesh cases.
- Modify: `engine/helengine.bepu.tests/BepuPhysicsWorld3DTests.cs`
  - Cover static mesh runtime binding and a simple sphere-on-mesh contact case.

## Task 1: Add The Shared Static-Mesh Runtime Payload Contract

**Files:**
- Create: `engine/helengine.physics/StaticMeshCollisionRuntimeData3D.cs`
- Modify: `engine/helengine.physics/StaticMeshCollider3DComponent.cs`
- Create: `engine/helengine.editor.tests/serialization/scene/StaticMeshColliderGenericPersistenceTests.cs`

- [ ] **Step 1: Write the failing reflected-persistence test**

Create `engine/helengine.editor.tests/serialization/scene/StaticMeshColliderGenericPersistenceTests.cs`:

```csharp
using helengine.editor;
using helengine.editor.tests.testing;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies static-mesh colliders persist through the generic reflected descriptor.
    /// </summary>
    public sealed class StaticMeshColliderGenericPersistenceTests {
        /// <summary>
        /// Ensures generic collision data and one cooked runtime payload both round-trip through the automatic reflected persistence path.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenStaticMeshColliderUsesCookedRuntimePayload_RoundTripsGenericPayload() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            StaticMeshCollider3DComponent collider = new StaticMeshCollider3DComponent {
                CollisionData = new StaticMeshCollisionData3D(
                    [
                        new float3(-1f, 0f, -1f),
                        new float3(1f, 0f, -1f),
                        new float3(-1f, 0f, 1f)
                    ],
                    [0, 1, 2]),
                CookedRuntimeData = new StaticMeshCollisionRuntimeData3D(
                    "helengine.bepu.static-mesh",
                    [0x10, 0x20, 0x30, 0x40])
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(collider, 0, saveComponent.GetOrCreateComponentState(collider));
            StaticMeshCollider3DComponent restored = Assert.IsType<StaticMeshCollider3DComponent>(descriptor.DeserializeComponent(record, saveComponent, resolver));

            Assert.Equal(3, restored.CollisionData.Vertices.Length);
            Assert.Equal(new float3(-1f, 0f, -1f), restored.CollisionData.Vertices[0]);
            Assert.Equal(new[] { 0, 1, 2 }, restored.CollisionData.Indices);
            Assert.Equal("helengine.bepu.static-mesh", restored.CookedRuntimeData.FormatId);
            Assert.Equal(new byte[] { 0x10, 0x20, 0x30, 0x40 }, restored.CookedRuntimeData.Data);
        }
    }
}
```

- [ ] **Step 2: Run the new test to verify it fails**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~StaticMeshColliderGenericPersistenceTests" -v minimal
```

Expected: FAIL because `StaticMeshCollider3DComponent` does not expose `CookedRuntimeData` and `StaticMeshCollisionRuntimeData3D` does not exist.

- [ ] **Step 3: Add the new shared payload type and wire it onto the collider**

Create `engine/helengine.physics/StaticMeshCollisionRuntimeData3D.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Stores one opaque cooked runtime payload for a static mesh collider.
    /// </summary>
    public sealed class StaticMeshCollisionRuntimeData3D {
        /// <summary>
        /// Backing field for the runtime payload format identifier.
        /// </summary>
        string FormatIdValue;

        /// <summary>
        /// Backing field for the opaque cooked runtime bytes.
        /// </summary>
        byte[] DataValue;

        /// <summary>
        /// Initializes one empty runtime payload for reflected scene materialization.
        /// </summary>
        public StaticMeshCollisionRuntimeData3D() {
        }

        /// <summary>
        /// Initializes one runtime payload with the supplied format id and cooked bytes.
        /// </summary>
        /// <param name="formatId">Stable runtime payload format identifier.</param>
        /// <param name="data">Opaque cooked runtime bytes.</param>
        public StaticMeshCollisionRuntimeData3D(string formatId, byte[] data) {
            FormatId = formatId;
            Data = data;
        }

        /// <summary>
        /// Gets or sets the stable runtime payload format identifier.
        /// </summary>
        public string FormatId {
            get { return FormatIdValue ?? throw new InvalidOperationException("Static mesh runtime payload format id must be initialized before use."); }
            set { FormatIdValue = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Static mesh runtime payload format id must be provided.", nameof(value)) : value; }
        }

        /// <summary>
        /// Gets or sets the opaque cooked runtime bytes.
        /// </summary>
        public byte[] Data {
            get { return DataValue ?? throw new InvalidOperationException("Static mesh runtime payload bytes must be initialized before use."); }
            set {
                if (value == null) {
                    throw new ArgumentNullException(nameof(value));
                } else if (value.Length == 0) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Static mesh runtime payload bytes must not be empty.");
                }

                DataValue = [.. value];
            }
        }
    }
}
```

Modify `engine/helengine.physics/StaticMeshCollider3DComponent.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Defines one authored cooked static mesh collider consumed by 3D physics runtimes.
    /// </summary>
    public sealed class StaticMeshCollider3DComponent : Collider3DComponent {
        /// <summary>
        /// Backing field for the cooked collision data blob.
        /// </summary>
        StaticMeshCollisionData3D CollisionDataValue;

        /// <summary>
        /// Backing field for the optional cooked runtime payload.
        /// </summary>
        StaticMeshCollisionRuntimeData3D CookedRuntimeDataValue;

        /// <summary>
        /// Gets or sets the cooked static collision data queried by the runtime.
        /// </summary>
        public StaticMeshCollisionData3D CollisionData {
            get { return CollisionDataValue; }
            set { CollisionDataValue = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets the optional cooked runtime payload generated for the active physics backend.
        /// </summary>
        public StaticMeshCollisionRuntimeData3D CookedRuntimeData {
            get { return CookedRuntimeDataValue; }
            set { CookedRuntimeDataValue = value; }
        }
    }
}
```

- [ ] **Step 4: Run the reflected-persistence test again**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~StaticMeshColliderGenericPersistenceTests" -v minimal
```

Expected: PASS

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.physics/StaticMeshCollisionRuntimeData3D.cs engine/helengine.physics/StaticMeshCollider3DComponent.cs engine/helengine.editor.tests/serialization/scene/StaticMeshColliderGenericPersistenceTests.cs
rtk git commit -m "Add shared static mesh runtime payload contract"
```

## Task 2: Add The Generic Static-Mesh Cook Processor Seam To Scene Packaging

**Files:**
- Create: `engine/helengine.physics/IStaticMeshCollisionCookProcessor3D.cs`
- Create: `engine/helengine.editor/managers/project/StaticMeshCollisionCookProcessorRegistry.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildScenePackager.cs`
- Modify: `engine/helengine.editor.tests/SceneComponentPackagingTransformServiceTests.cs`

- [ ] **Step 1: Write the failing packaging test with one stub cook processor**

Add this test and helper to `engine/helengine.editor.tests/SceneComponentPackagingTransformServiceTests.cs`:

```csharp
[Fact]
public void TryTransform_WhenStaticMeshColliderUsesRegisteredCookProcessor_WritesCookedRuntimePayload() {
    StaticMeshCollisionCookProcessorRegistry registry = new StaticMeshCollisionCookProcessorRegistry();
    registry.RegisterProcessor(new StubStaticMeshCollisionCookProcessor3D());
    SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService(), registry);
    SceneComponentAssetRecord record = CreateStaticMeshColliderRecord();

    bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

    Assert.True(transformed);
    Assert.NotNull(transformedRecord);
    StaticMeshCollider3DComponent restored = DeserializeAutomaticComponent<StaticMeshCollider3DComponent>(transformedRecord);
    Assert.NotNull(restored.CookedRuntimeData);
    Assert.Equal("test.static-mesh", restored.CookedRuntimeData.FormatId);
    Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, restored.CookedRuntimeData.Data);
}

static SceneComponentAssetRecord CreateStaticMeshColliderRecord() {
    AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
    StaticMeshCollider3DComponent component = new StaticMeshCollider3DComponent {
        CollisionData = new StaticMeshCollisionData3D(
            [
                new float3(-1f, 0f, -1f),
                new float3(1f, 0f, -1f),
                new float3(-1f, 0f, 1f)
            ],
            [0, 1, 2])
    };

    return descriptor.SerializeComponent(component, 0, new EntityComponentSaveState());
}

static TComponent DeserializeAutomaticComponent<TComponent>(SceneComponentAssetRecord record) where TComponent : Component {
    AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
    return Assert.IsType<TComponent>(descriptor.DeserializeComponent(record, new EntitySaveComponent(), new TestSceneAssetReferenceResolver()));
}

sealed class StubStaticMeshCollisionCookProcessor3D : IStaticMeshCollisionCookProcessor3D {
    public StaticMeshCollisionRuntimeData3D Cook(StaticMeshCollisionData3D collisionData) {
        return new StaticMeshCollisionRuntimeData3D("test.static-mesh", [0x01, 0x02, 0x03]);
    }
}
```

Update `CreateService(...)` in the same file so it accepts the registry:

```csharp
SceneComponentPackagingTransformService CreateService(ITextComponentSpriteBakeService bakeService, StaticMeshCollisionCookProcessorRegistry staticMeshCookProcessorRegistry = null) {
    ContentManager contentManager = new ContentManager(ProjectRootPath);
    AssetImportManager assetImportManager = new AssetImportManager(ProjectRootPath, contentManager);
    assetImportManager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), [".ttf"]));
    assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"]));
    EditorFileSystemModelResolver fileSystemModelResolver = new EditorFileSystemModelResolver(assetImportManager);

    return new SceneComponentPackagingTransformService(
        Path.Combine(ProjectRootPath, "assets"),
        contentManager,
        assetImportManager,
        fileSystemModelResolver,
        new List<string>(),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        "windows",
        null,
        string.Empty,
        string.Empty,
        null,
        null,
        null,
        bakeService,
        staticMeshCookProcessorRegistry);
}
```

- [ ] **Step 2: Run the packaging test to verify it fails**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~TryTransform_WhenStaticMeshColliderUsesRegisteredCookProcessor_WritesCookedRuntimePayload" -v minimal
```

Expected: FAIL because the cook processor interface, registry, and constructor hook do not exist.

- [ ] **Step 3: Add the generic cook processor contract and editor registry**

Create `engine/helengine.physics/IStaticMeshCollisionCookProcessor3D.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Cooks generic static mesh collision data into one backend-owned runtime payload.
    /// </summary>
    public interface IStaticMeshCollisionCookProcessor3D {
        /// <summary>
        /// Cooks one static mesh collision blob into one runtime payload.
        /// </summary>
        /// <param name="collisionData">Generic collision data to convert.</param>
        /// <returns>Cooked runtime payload.</returns>
        StaticMeshCollisionRuntimeData3D Cook(StaticMeshCollisionData3D collisionData);
    }
}
```

Create `engine/helengine.editor/managers/project/StaticMeshCollisionCookProcessorRegistry.cs`:

```csharp
namespace helengine.editor {
    /// <summary>
    /// Stores generic static-mesh collision cook processors used during scene packaging.
    /// </summary>
    public sealed class StaticMeshCollisionCookProcessorRegistry {
        /// <summary>
        /// Shared registry used by the editor host.
        /// </summary>
        public static readonly StaticMeshCollisionCookProcessorRegistry Shared = new StaticMeshCollisionCookProcessorRegistry();

        /// <summary>
        /// Registered processors in discovery order.
        /// </summary>
        readonly List<IStaticMeshCollisionCookProcessor3D> ProcessorsValue;

        /// <summary>
        /// Initializes one empty processor registry.
        /// </summary>
        public StaticMeshCollisionCookProcessorRegistry() {
            ProcessorsValue = new List<IStaticMeshCollisionCookProcessor3D>();
        }

        /// <summary>
        /// Gets the registered processors.
        /// </summary>
        public IReadOnlyList<IStaticMeshCollisionCookProcessor3D> Processors => ProcessorsValue;

        /// <summary>
        /// Registers one processor.
        /// </summary>
        /// <param name="processor">Processor to register.</param>
        public void RegisterProcessor(IStaticMeshCollisionCookProcessor3D processor) {
            if (processor == null) {
                throw new ArgumentNullException(nameof(processor));
            }

            ProcessorsValue.Add(processor);
        }
    }
}
```

- [ ] **Step 4: Hook the registry into packaging and cook static mesh colliders**

Modify the `SceneComponentPackagingTransformService` constructor signature and field list in `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`:

```csharp
readonly StaticMeshCollisionCookProcessorRegistry StaticMeshCookProcessorRegistry;

public SceneComponentPackagingTransformService(
    string assetsRootPath,
    ContentManager projectContentManager,
    AssetImportManager assetImportManager,
    EditorFileSystemModelResolver fileSystemModelResolver,
    List<string> referencedShaderAssetIds,
    HashSet<string> referencedShaderAssetIdsSet,
    string targetPlatformId = "",
    IPlatformAssetBuilder materialBuilder = null,
    string selectedBuildProfileId = "",
    string selectedGraphicsProfileId = "",
    IScriptTypeResolver scriptTypeResolver = null,
    Action<PlatformCookWorkItem> platformCookWorkItemSink = null,
    PlatformDefinition platformDefinition = null,
    ITextComponentSpriteBakeService textComponentSpriteBakeService = null,
    StaticMeshCollisionCookProcessorRegistry staticMeshCookProcessorRegistry = null) {
    // existing assignments...
    StaticMeshCookProcessorRegistry = staticMeshCookProcessorRegistry ?? StaticMeshCollisionCookProcessorRegistry.Shared;
}
```

Add this helper inside the same file and call it from `TryRewriteAutomaticComponentRecord(...)` after the component is deserialized but before it is reserialized:

```csharp
void ApplyStaticMeshCookedRuntimeData(Component component) {
    if (component is not StaticMeshCollider3DComponent staticMeshCollider) {
        return;
    } else if (staticMeshCollider.CollisionData == null) {
        throw new InvalidOperationException("Static mesh collider packaging requires collision data before backend cooking can run.");
    }

    IReadOnlyList<IStaticMeshCollisionCookProcessor3D> processors = StaticMeshCookProcessorRegistry.Processors;
    if (processors.Count == 0) {
        return;
    } else if (processors.Count > 1) {
        throw new InvalidOperationException("Static mesh collider packaging requires exactly one registered cook processor.");
    }

    staticMeshCollider.CookedRuntimeData = processors[0].Cook(staticMeshCollider.CollisionData);
}
```

The automatic rewrite flow should call it like:

```csharp
Component component = (Component)AutomaticScriptComponentDescriptor.DeserializeComponent(record, saveComponent, resolver);
ApplyStaticMeshCookedRuntimeData(component);
transformedRecord = AutomaticScriptComponentDescriptor.SerializeComponent(component, record.ComponentIndex, saveState);
return true;
```

Modify `engine/helengine.editor/managers/project/EditorPlatformBuildScenePackager.cs` so the transform service uses the shared registry:

```csharp
TransformService = new SceneComponentPackagingTransformService(
    AssetsRootPath,
    ProjectContentManager,
    AssetImportManager,
    FileSystemModelResolver,
    ReferencedShaderAssetIds,
    ReferencedShaderAssetIdsSet,
    TargetPlatformId,
    MaterialBuilder,
    SelectedBuildProfileId,
    SelectedGraphicsProfileId,
    scriptTypeResolver,
    RememberPlatformCookWorkItem,
    PlatformDefinition,
    textComponentSpriteBakeService,
    StaticMeshCollisionCookProcessorRegistry.Shared);
```

- [ ] **Step 5: Run the packaging test again**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~TryTransform_WhenStaticMeshColliderUsesRegisteredCookProcessor_WritesCookedRuntimePayload" -v minimal
```

Expected: PASS

- [ ] **Step 6: Commit**

```bash
rtk git add engine/helengine.physics/IStaticMeshCollisionCookProcessor3D.cs engine/helengine.editor/managers/project/StaticMeshCollisionCookProcessorRegistry.cs engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor/managers/project/EditorPlatformBuildScenePackager.cs engine/helengine.editor.tests/SceneComponentPackagingTransformServiceTests.cs
rtk git commit -m "Add generic static mesh cook processor seam"
```

## Task 3: Add The BEPU Static-Mesh Cook Processor And Host Registration

**Files:**
- Create: `engine/helengine.bepu/BepuStaticMeshCollisionCookProcessor3D.cs`
- Modify: `engine/helengine.editor.tests/helengine.editor.tests.csproj`
- Modify: `engine/helengine.editor.tests/SceneComponentPackagingTransformServiceTests.cs`
- Create: `helengine.ui/helengine.editor.app/EditorHostStaticMeshCollisionCookProcessorRegistration.cs`
- Modify: `helengine.ui/helengine.editor.app/Program.cs`
- Modify: `helengine.ui/helengine.editor.app/helengine.editor.app.csproj`
- Create: `engine/helengine.bepu.tests/BepuStaticMeshCollisionCookProcessorTests.cs`

- [ ] **Step 1: Write the failing BEPU cook-processor round-trip test**

Create `engine/helengine.bepu.tests/BepuStaticMeshCollisionCookProcessorTests.cs`:

```csharp
using BepuPhysics.Collidables;
using BepuUtilities.Memory;

namespace helengine.bepu.tests {
    /// <summary>
    /// Verifies BEPU static-mesh cook payloads round-trip into BEPU mesh shapes.
    /// </summary>
    public sealed class BepuStaticMeshCollisionCookProcessorTests {
        /// <summary>
        /// Ensures one cooked BEPU payload can be deserialized back into a BEPU mesh.
        /// </summary>
        [Fact]
        public void Cook_WhenCollisionDataIsValid_ProducesRoundTrippableMeshPayload() {
            BepuStaticMeshCollisionCookProcessor3D processor = new BepuStaticMeshCollisionCookProcessor3D();
            StaticMeshCollisionData3D collisionData = new StaticMeshCollisionData3D(
                [
                    new float3(-1f, 0f, -1f),
                    new float3(1f, 0f, -1f),
                    new float3(-1f, 0f, 1f),
                    new float3(1f, 0f, 1f)
                ],
                [0, 1, 2, 2, 1, 3]);

            StaticMeshCollisionRuntimeData3D payload = processor.Cook(collisionData);

            Assert.Equal("helengine.bepu.static-mesh", payload.FormatId);
            Assert.NotEmpty(payload.Data);

            BufferPool pool = new BufferPool();
            Mesh mesh = new Mesh(payload.Data, pool);
            try {
                Assert.Equal(2, mesh.ChildCount);
            } finally {
                mesh.Dispose(pool);
            }
        }
    }
}
```

- [ ] **Step 2: Run the BEPU cook-processor test to verify it fails**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj --filter "FullyQualifiedName~BepuStaticMeshCollisionCookProcessorTests" -v minimal
```

Expected: FAIL because the processor does not exist.

- [ ] **Step 3: Implement the BEPU cook processor**

Create `engine/helengine.bepu/BepuStaticMeshCollisionCookProcessor3D.cs`:

```csharp
using BepuPhysics.Collidables;
using BepuUtilities.Memory;
using System.Numerics;

namespace helengine {
    /// <summary>
    /// Cooks generic static mesh collision data into serialized BEPU mesh payloads.
    /// </summary>
    public sealed class BepuStaticMeshCollisionCookProcessor3D : IStaticMeshCollisionCookProcessor3D {
        /// <summary>
        /// Stable format identifier used by cooked BEPU mesh payloads.
        /// </summary>
        public const string FormatIdValue = "helengine.bepu.static-mesh";

        /// <summary>
        /// Cooks one generic collision blob into one serialized BEPU mesh payload.
        /// </summary>
        /// <param name="collisionData">Generic collision data to convert.</param>
        /// <returns>Serialized BEPU mesh payload.</returns>
        public StaticMeshCollisionRuntimeData3D Cook(StaticMeshCollisionData3D collisionData) {
            if (collisionData == null) {
                throw new ArgumentNullException(nameof(collisionData));
            }

            BufferPool pool = new BufferPool();
            pool.Take(collisionData.TriangleCount, out BepuUtilities.Memory.Buffer<Triangle> triangles);
            try {
                for (int triangleIndex = 0; triangleIndex < collisionData.TriangleCount; triangleIndex++) {
                    int firstIndex = collisionData.Indices[triangleIndex * 3];
                    int secondIndex = collisionData.Indices[(triangleIndex * 3) + 1];
                    int thirdIndex = collisionData.Indices[(triangleIndex * 3) + 2];
                    float3 first = collisionData.Vertices[firstIndex];
                    float3 second = collisionData.Vertices[secondIndex];
                    float3 third = collisionData.Vertices[thirdIndex];
                    triangles[triangleIndex] = new Triangle(
                        new Vector3(first.X, first.Y, first.Z),
                        new Vector3(second.X, second.Y, second.Z),
                        new Vector3(third.X, third.Y, third.Z));
                }

                Mesh mesh = new Mesh(triangles, new Vector3(1f, 1f, 1f), pool);
                try {
                    byte[] serialized = new byte[mesh.GetSerializedByteCount()];
                    mesh.Serialize(serialized);
                    return new StaticMeshCollisionRuntimeData3D(FormatIdValue, serialized);
                } finally {
                    mesh.Dispose(pool);
                }
            } finally {
                pool.Return(ref triangles);
            }
        }
    }
}
```

- [ ] **Step 4: Add one real packaging integration test and one editor-host registration helper**

Add a direct BEPU reference to `engine/helengine.editor.tests/helengine.editor.tests.csproj`:

```xml
<ProjectReference Include="..\helengine.bepu\helengine.bepu.csproj" SkipGetTargetFrameworkProperties="true" />
```

Add this real-processor test to `engine/helengine.editor.tests/SceneComponentPackagingTransformServiceTests.cs`:

```csharp
[Fact]
public void TryTransform_WhenStaticMeshColliderUsesRealBepuCookProcessor_WritesBepuPayload() {
    StaticMeshCollisionCookProcessorRegistry registry = new StaticMeshCollisionCookProcessorRegistry();
    registry.RegisterProcessor(new BepuStaticMeshCollisionCookProcessor3D());
    SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService(), registry);

    bool transformed = service.TryTransform(CreateStaticMeshColliderRecord(), BuildRootPath, out SceneComponentAssetRecord transformedRecord);

    Assert.True(transformed);
    StaticMeshCollider3DComponent restored = DeserializeAutomaticComponent<StaticMeshCollider3DComponent>(transformedRecord);
    Assert.Equal(BepuStaticMeshCollisionCookProcessor3D.FormatIdValue, restored.CookedRuntimeData.FormatId);
    Assert.NotEmpty(restored.CookedRuntimeData.Data);
}
```

Create `helengine.ui/helengine.editor.app/EditorHostStaticMeshCollisionCookProcessorRegistration.cs`:

```csharp
using helengine.editor;

namespace helengine.editor.app {
    /// <summary>
    /// Registers editor-host static-mesh cook processors exposed by runtime plugins.
    /// </summary>
    internal static class EditorHostStaticMeshCollisionCookProcessorRegistration {
        /// <summary>
        /// Registers the default editor-host static-mesh cook processors.
        /// </summary>
        public static void RegisterDefaults() {
            if (StaticMeshCollisionCookProcessorRegistry.Shared.Processors.Count > 0) {
                return;
            }

            StaticMeshCollisionCookProcessorRegistry.Shared.RegisterProcessor(new BepuStaticMeshCollisionCookProcessor3D());
        }
    }
}
```

Modify `helengine.ui/helengine.editor.app/Program.cs`:

```csharp
static int Main(string[] args) {
    EditorHostStaticMeshCollisionCookProcessorRegistration.RegisterDefaults();

    if (TryRunEditorCommandMode(args, out int commandExitCode)) {
        return commandExitCode;
    }

    if (TryRunBuildMode(args, out int buildExitCode)) {
        return buildExitCode;
    }

    // existing startup...
}
```

Modify `helengine.ui/helengine.editor.app/helengine.editor.app.csproj`:

```xml
<ProjectReference Include="..\..\engine\helengine.bepu\helengine.bepu.csproj" SkipGetTargetFrameworkProperties="true" />
```

- [ ] **Step 5: Run the focused BEPU cook tests**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj --filter "FullyQualifiedName~BepuStaticMeshCollisionCookProcessorTests" -v minimal
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~TryTransform_WhenStaticMeshColliderUsesRealBepuCookProcessor_WritesBepuPayload" -v minimal
```

Expected: PASS

- [ ] **Step 6: Commit**

```bash
rtk git add engine/helengine.bepu/BepuStaticMeshCollisionCookProcessor3D.cs engine/helengine.bepu.tests/BepuStaticMeshCollisionCookProcessorTests.cs engine/helengine.editor.tests/SceneComponentPackagingTransformServiceTests.cs engine/helengine.editor.tests/helengine.editor.tests.csproj helengine.ui/helengine.editor.app/EditorHostStaticMeshCollisionCookProcessorRegistration.cs helengine.ui/helengine.editor.app/Program.cs helengine.ui/helengine.editor.app/helengine.editor.app.csproj
rtk git commit -m "Add BEPU static mesh cook processor"
```

## Task 4: Add Static-Mesh Runtime Support To `helengine.bepu`

**Files:**
- Create: `engine/helengine.bepu/BepuOwnedMeshShape3D.cs`
- Modify: `engine/helengine.bepu/BepuShapeFactory3D.cs`
- Modify: `engine/helengine.bepu/BepuBodyHandle3D.cs`
- Modify: `engine/helengine.bepu/BepuPhysicsFeatureGuard3D.cs`
- Modify: `engine/helengine.bepu/BepuPhysicsWorld3D.cs`
- Modify: `engine/helengine.bepu.tests/BepuPhysicsFeatureGuard3DTests.cs`
- Modify: `engine/helengine.bepu.tests/BepuPhysicsWorld3DTests.cs`

- [ ] **Step 1: Write the failing BEPU runtime tests**

Add these tests to `engine/helengine.bepu.tests/BepuPhysicsFeatureGuard3DTests.cs`:

```csharp
[Fact]
public void ValidateSupportedCollider_WithStaticMeshColliderAndDynamicBody_ThrowsNotSupportedException() {
    Entity entity = new Entity();
    entity.InitComponents();
    entity.AddComponent(new RigidBody3DComponent {
        BodyKind = BodyKind3D.Dynamic,
        Mass = 1d
    });
    entity.AddComponent(new StaticMeshCollider3DComponent {
        CollisionData = new StaticMeshCollisionData3D(
            [
                new float3(-1f, 0f, -1f),
                new float3(1f, 0f, -1f),
                new float3(-1f, 0f, 1f)
            ],
            [0, 1, 2])
    });

    Assert.Throws<NotSupportedException>(() => BepuPhysicsFeatureGuard3D.ValidateEntity(entity));
}

[Fact]
public void ValidateSupportedCollider_WithStaticMeshColliderAndMissingCookedRuntimeData_ThrowsNotSupportedException() {
    Entity entity = new Entity();
    entity.InitComponents();
    entity.AddComponent(new RigidBody3DComponent {
        BodyKind = BodyKind3D.Static
    });
    entity.AddComponent(new StaticMeshCollider3DComponent {
        CollisionData = new StaticMeshCollisionData3D(
            [
                new float3(-1f, 0f, -1f),
                new float3(1f, 0f, -1f),
                new float3(-1f, 0f, 1f)
            ],
            [0, 1, 2])
    });

    Assert.Throws<NotSupportedException>(() => BepuPhysicsFeatureGuard3D.ValidateEntity(entity));
}
```

Add this runtime test and helpers to `engine/helengine.bepu.tests/BepuPhysicsWorld3DTests.cs`:

```csharp
[Fact]
public void Step_WithDynamicSphereAboveStaticMeshGround_ResolvesGroundContact() {
    Entity groundEntity = CreateStaticMeshGroundEntity();
    Entity sphereEntity = CreateDynamicSphereEntity(new float3(0f, 2f, 0f), 0.5f);
    BepuPhysicsWorld3D world = BepuPhysicsWorld3D.CreateDefault();

    world.BindScene(new[] { groundEntity, sphereEntity });
    for (int index = 0; index < 180; index++) {
        world.Step(1.0 / 60.0);
    }

    Assert.True(sphereEntity.LocalPosition.Y < 2f);
    Assert.InRange(sphereEntity.LocalPosition.Y, 0.49f, 0.56f);
}

static Entity CreateStaticMeshGroundEntity() {
    BepuStaticMeshCollisionCookProcessor3D processor = new BepuStaticMeshCollisionCookProcessor3D();
    StaticMeshCollisionData3D collisionData = new StaticMeshCollisionData3D(
        [
            new float3(-4f, 0f, -4f),
            new float3(4f, 0f, -4f),
            new float3(-4f, 0f, 4f),
            new float3(4f, 0f, 4f)
        ],
        [0, 1, 2, 2, 1, 3]);

    Entity entity = new Entity();
    entity.InitComponents();
    entity.AddComponent(new RigidBody3DComponent {
        BodyKind = BodyKind3D.Static,
        UseGravity = false
    });
    entity.AddComponent(new StaticMeshCollider3DComponent {
        CollisionData = collisionData,
        CookedRuntimeData = processor.Cook(collisionData)
    });
    return entity;
}
```

- [ ] **Step 2: Run the new BEPU runtime tests to verify they fail**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj --filter "FullyQualifiedName~ValidateSupportedCollider_WithStaticMeshCollider|FullyQualifiedName~Step_WithDynamicSphereAboveStaticMeshGround_ResolvesGroundContact" -v minimal
```

Expected: FAIL because static mesh colliders are still rejected and the BEPU world cannot register mesh statics.

- [ ] **Step 3: Implement BEPU mesh resource ownership and static registration**

Create `engine/helengine.bepu/BepuOwnedMeshShape3D.cs`:

```csharp
using BepuPhysics.Collidables;
using BepuUtilities.Memory;

namespace helengine {
    /// <summary>
    /// Owns one deserialized BEPU mesh shape and its pooled resources.
    /// </summary>
    public sealed class BepuOwnedMeshShape3D {
        /// <summary>
        /// Initializes one owned BEPU mesh shape.
        /// </summary>
        /// <param name="shape">Deserialized BEPU mesh shape.</param>
        public BepuOwnedMeshShape3D(Mesh shape) {
            Shape = shape;
        }

        /// <summary>
        /// Gets the owned BEPU mesh shape.
        /// </summary>
        public Mesh Shape { get; }

        /// <summary>
        /// Returns the owned mesh resources to the supplied BEPU buffer pool.
        /// </summary>
        /// <param name="bufferPool">Buffer pool that owns the mesh resources.</param>
        public void Dispose(BufferPool bufferPool) {
            Shape.Dispose(bufferPool ?? throw new ArgumentNullException(nameof(bufferPool)));
        }
    }
}
```

Extend `engine/helengine.bepu/BepuShapeFactory3D.cs`:

```csharp
using BepuUtilities.Memory;

public static BepuOwnedMeshShape3D CreateStaticMeshShape(StaticMeshCollisionRuntimeData3D runtimeData, BufferPool pool) {
    if (runtimeData == null) {
        throw new ArgumentNullException(nameof(runtimeData));
    } else if (!string.Equals(runtimeData.FormatId, BepuStaticMeshCollisionCookProcessor3D.FormatIdValue, StringComparison.Ordinal)) {
        throw new InvalidOperationException($"Unsupported BEPU static mesh payload format '{runtimeData.FormatId}'.");
    }

    return new BepuOwnedMeshShape3D(new Mesh(runtimeData.Data, pool ?? throw new ArgumentNullException(nameof(pool))));
}
```

Extend `engine/helengine.bepu/BepuBodyHandle3D.cs` with one static-mesh constructor and properties:

```csharp
public BepuBodyHandle3D(
    Entity entity,
    RigidBody3DComponent rigidBody,
    StaticMeshCollider3DComponent staticMeshCollider,
    TypedIndex shapeIndex,
    StaticHandle staticHandle,
    BepuOwnedMeshShape3D ownedMeshShape) {
    Entity = entity ?? throw new ArgumentNullException(nameof(entity));
    RigidBody = rigidBody ?? throw new ArgumentNullException(nameof(rigidBody));
    BoxCollider = null;
    SphereCollider = null;
    StaticMeshCollider = staticMeshCollider ?? throw new ArgumentNullException(nameof(staticMeshCollider));
    ShapeIndex = shapeIndex;
    BodyHandle = default;
    HasBodyHandle = false;
    StaticHandle = staticHandle;
    HasStaticHandle = true;
    OwnedMeshShape = ownedMeshShape ?? throw new ArgumentNullException(nameof(ownedMeshShape));
}

public StaticMeshCollider3DComponent StaticMeshCollider { get; }
public BepuOwnedMeshShape3D OwnedMeshShape { get; }
```

Modify `engine/helengine.bepu/BepuPhysicsFeatureGuard3D.cs`:

```csharp
public static void ValidateEntity(Entity entity) {
    if (entity == null) {
        throw new ArgumentNullException(nameof(entity));
    }

    List<Component> components = entity.Components;
    if (components == null) {
        return;
    }

    RigidBody3DComponent rigidBody = null;
    StaticMeshCollider3DComponent staticMeshCollider = null;
    for (int index = 0; index < components.Count; index++) {
        if (components[index] is RigidBody3DComponent body) {
            rigidBody = body;
        } else if (components[index] is StaticMeshCollider3DComponent meshCollider) {
            staticMeshCollider = meshCollider;
        } else if (components[index] is Collider3DComponent collider && components[index] is not BoxCollider3DComponent && components[index] is not SphereCollider3DComponent) {
            throw new NotSupportedException("Only box, sphere, and cooked static mesh colliders are supported by helengine.bepu in the current replacement pass.");
        }
    }

    if (staticMeshCollider == null) {
        return;
    } else if (rigidBody == null || rigidBody.BodyKind != BodyKind3D.Static) {
        throw new NotSupportedException("Static mesh colliders are supported only for static rigid bodies in helengine.bepu.");
    } else if (staticMeshCollider.CookedRuntimeData == null) {
        throw new NotSupportedException("Static mesh colliders require one cooked runtime payload for helengine.bepu.");
    } else if (!string.Equals(staticMeshCollider.CookedRuntimeData.FormatId, BepuStaticMeshCollisionCookProcessor3D.FormatIdValue, StringComparison.Ordinal)) {
        throw new NotSupportedException($"Static mesh collider payload format '{staticMeshCollider.CookedRuntimeData.FormatId}' is not supported by helengine.bepu.");
    }
}
```

Modify `engine/helengine.bepu/BepuPhysicsWorld3D.cs`:

```csharp
void RegisterEntityIfSupported(Entity entity) {
    if (entity == null) {
        throw new ArgumentNullException(nameof(entity));
    }

    BepuPhysicsFeatureGuard3D.ValidateEntity(entity);
    RigidBody3DComponent rigidBody = ResolveRigidBody(entity);
    if (rigidBody == null) {
        return;
    }

    BoxCollider3DComponent boxCollider = ResolveBoxCollider(entity);
    SphereCollider3DComponent sphereCollider = ResolveSphereCollider(entity);
    StaticMeshCollider3DComponent staticMeshCollider = ResolveStaticMeshCollider(entity);

    if (staticMeshCollider != null) {
        RegisterStaticMeshBody(entity, rigidBody, staticMeshCollider);
        return;
    }

    // existing box/sphere path...
}

void RegisterStaticMeshBody(Entity entity, RigidBody3DComponent rigidBody, StaticMeshCollider3DComponent staticMeshCollider) {
    if (rigidBody.BodyKind != BodyKind3D.Static) {
        throw new NotSupportedException("Static mesh colliders are supported only for static rigid bodies in helengine.bepu.");
    }

    BepuOwnedMeshShape3D ownedMeshShape = BepuShapeFactory3D.CreateStaticMeshShape(staticMeshCollider.CookedRuntimeData, BufferPoolValue);
    TypedIndex shapeIndex = SimulationValue.Shapes.Add(ownedMeshShape.Shape);
    StaticHandle staticHandle = SimulationValue.Statics.Add(new StaticDescription(BepuEntitySynchronization3D.CreatePose(entity), shapeIndex));
    CollidablePropertiesValue.Allocate(staticHandle) = CreateCollidableProperties(staticMeshCollider);
    BodyRegistryValue.Add(new BepuBodyHandle3D(entity, rigidBody, staticMeshCollider, shapeIndex, staticHandle, ownedMeshShape));
}

StaticMeshCollider3DComponent ResolveStaticMeshCollider(Entity entity) {
    List<Component> components = entity.Components;
    for (int index = 0; index < components.Count; index++) {
        if (components[index] is StaticMeshCollider3DComponent staticMeshCollider) {
            return staticMeshCollider;
        }
    }

    return null;
}

void ResetSimulation() {
    DisposeOwnedMeshShapes();
    if (SimulationValue != null) {
        SimulationValue.Dispose();
    }

    // existing simulation recreation...
}

void DisposeOwnedMeshShapes() {
    IReadOnlyList<BepuBodyHandle3D> handles = BodyRegistryValue.Handles;
    for (int index = 0; index < handles.Count; index++) {
        BepuOwnedMeshShape3D ownedMeshShape = handles[index].OwnedMeshShape;
        if (ownedMeshShape != null) {
            ownedMeshShape.Dispose(BufferPoolValue);
        }
    }
}
```

- [ ] **Step 4: Run the focused BEPU runtime tests again**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj --filter "FullyQualifiedName~ValidateSupportedCollider_WithStaticMeshCollider|FullyQualifiedName~Step_WithDynamicSphereAboveStaticMeshGround_ResolvesGroundContact" -v minimal
```

Expected: PASS

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.bepu/BepuOwnedMeshShape3D.cs engine/helengine.bepu/BepuShapeFactory3D.cs engine/helengine.bepu/BepuBodyHandle3D.cs engine/helengine.bepu/BepuPhysicsFeatureGuard3D.cs engine/helengine.bepu/BepuPhysicsWorld3D.cs engine/helengine.bepu.tests/BepuPhysicsFeatureGuard3DTests.cs engine/helengine.bepu.tests/BepuPhysicsWorld3DTests.cs
rtk git commit -m "Add BEPU static mesh runtime support"
```

## Task 5: Run Focused End-To-End Verification

**Files:**
- Modify: `engine/helengine.editor.tests/SceneComponentPackagingTransformServiceTests.cs`
- Modify: `engine/helengine.bepu.tests/BepuPhysicsWorld3DTests.cs`

- [ ] **Step 1: Add one regression test that packaging preserves generic data while adding the BEPU payload**

Add this assertion-focused test to `engine/helengine.editor.tests/SceneComponentPackagingTransformServiceTests.cs`:

```csharp
[Fact]
public void TryTransform_WhenBepuCookProcessorRuns_PreservesGenericCollisionDataAlongsideCookedPayload() {
    StaticMeshCollisionCookProcessorRegistry registry = new StaticMeshCollisionCookProcessorRegistry();
    registry.RegisterProcessor(new BepuStaticMeshCollisionCookProcessor3D());
    SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService(), registry);

    bool transformed = service.TryTransform(CreateStaticMeshColliderRecord(), BuildRootPath, out SceneComponentAssetRecord transformedRecord);

    Assert.True(transformed);
    StaticMeshCollider3DComponent restored = DeserializeAutomaticComponent<StaticMeshCollider3DComponent>(transformedRecord);
    Assert.Equal(3, restored.CollisionData.Vertices.Length);
    Assert.Equal(new[] { 0, 1, 2 }, restored.CollisionData.Indices);
    Assert.Equal(BepuStaticMeshCollisionCookProcessor3D.FormatIdValue, restored.CookedRuntimeData.FormatId);
    Assert.NotEmpty(restored.CookedRuntimeData.Data);
}
```

- [ ] **Step 2: Add one BEPU runtime failure test for corrupt payload formats**

Add this to `engine/helengine.bepu.tests/BepuPhysicsWorld3DTests.cs`:

```csharp
[Fact]
public void BindScene_WithStaticMeshColliderUsingWrongPayloadFormat_ThrowsNotSupportedException() {
    StaticMeshCollisionData3D collisionData = new StaticMeshCollisionData3D(
        [
            new float3(-1f, 0f, -1f),
            new float3(1f, 0f, -1f),
            new float3(-1f, 0f, 1f)
        ],
        [0, 1, 2]);
    Entity entity = new Entity();
    entity.InitComponents();
    entity.AddComponent(new RigidBody3DComponent {
        BodyKind = BodyKind3D.Static
    });
    entity.AddComponent(new StaticMeshCollider3DComponent {
        CollisionData = collisionData,
        CookedRuntimeData = new StaticMeshCollisionRuntimeData3D("wrong.format", [0x01])
    });

    BepuPhysicsWorld3D world = BepuPhysicsWorld3D.CreateDefault();

    Assert.Throws<NotSupportedException>(() => world.BindScene(new[] { entity }));
}
```

- [ ] **Step 3: Run the focused verification set**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~StaticMeshColliderGenericPersistenceTests|FullyQualifiedName~TryTransform_WhenStaticMeshColliderUsesRegisteredCookProcessor_WritesCookedRuntimePayload|FullyQualifiedName~TryTransform_WhenStaticMeshColliderUsesRealBepuCookProcessor_WritesBepuPayload|FullyQualifiedName~TryTransform_WhenBepuCookProcessorRuns_PreservesGenericCollisionDataAlongsideCookedPayload" -v minimal
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj --filter "FullyQualifiedName~BepuStaticMeshCollisionCookProcessorTests|FullyQualifiedName~ValidateSupportedCollider_WithStaticMeshCollider|FullyQualifiedName~Step_WithDynamicSphereAboveStaticMeshGround_ResolvesGroundContact|FullyQualifiedName~BindScene_WithStaticMeshColliderUsingWrongPayloadFormat_ThrowsNotSupportedException" -v minimal
```

Expected: PASS

- [ ] **Step 4: Commit**

```bash
rtk git add engine/helengine.editor.tests/SceneComponentPackagingTransformServiceTests.cs engine/helengine.bepu.tests/BepuPhysicsWorld3DTests.cs
rtk git commit -m "Verify BEPU static mesh cook and runtime flow"
```

## Spec Coverage Check

- Shared generic authored data plus opaque cooked runtime payload: covered by Task 1.
- Generic cook seam instead of runtime `BindScene` mesh building: covered by Task 2.
- BEPU-owned cook payload generation: covered by Task 3.
- Runtime static-only mesh registration and strict failure behavior: covered by Task 4.
- Focused persistence, packaging, and runtime verification: covered by Task 5.

## Placeholder Scan

- No `TODO`, `TBD`, or “implement later” placeholders remain.
- Every task names exact files, concrete tests, commands, and commit points.
- The only intentionally deferred behavior is dynamic/kinematic mesh support, and the plan encodes explicit failure tests for that scope boundary.

## Type Consistency Check

- Shared cooked payload type stays `StaticMeshCollisionRuntimeData3D` throughout the plan.
- Shared processor contract stays `IStaticMeshCollisionCookProcessor3D`.
- Editor registry stays `StaticMeshCollisionCookProcessorRegistry`.
- BEPU cook processor stays `BepuStaticMeshCollisionCookProcessor3D`.
- Runtime-owned BEPU mesh wrapper stays `BepuOwnedMeshShape3D`.
