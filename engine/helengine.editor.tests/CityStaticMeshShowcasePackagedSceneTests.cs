using helengine.editor.tests.testing;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the packaged city static-mesh showcase scene remains loadable by the shared runtime stack.
/// </summary>
public sealed class CityStaticMeshShowcasePackagedSceneTests {
    /// <summary>
    /// Stable serialized component id for 3D rigid bodies.
    /// </summary>
    const string RigidBody3DComponentTypeId = "helengine.RigidBody3DComponent";

    /// <summary>
    /// Stable serialized component id for static-mesh colliders.
    /// </summary>
    const string StaticMeshCollider3DComponentTypeId = "helengine.StaticMeshCollider3DComponent";

    /// <summary>
    /// Absolute packaged scene path for the GameCube static-mesh showcase scene.
    /// </summary>
    const string PackagedScenePath = @"C:\dev\helprojs\city\gamecube-build\disc\files\cooked\scenes\physics\test_scene_static_mesh_showcase.hasset";

    /// <summary>
    /// Absolute packaged content root used to resolve cooked runtime asset references.
    /// </summary>
    const string PackagedContentRootPath = @"C:\dev\helprojs\city\gamecube-build\disc\files";

    /// <summary>
    /// Ensures the packaged GameCube showcase scene exposes one BEPU static-mesh payload and binds successfully.
    /// </summary>
    [Fact]
    public void GameCube_packaged_static_mesh_showcase_scene_loads_and_binds() {
        using FileStream stream = File.OpenRead(PackagedScenePath);
        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
        Core core = new Core(new CoreInitializationOptions {
            ContentStreamSource = new HostFileSystemContentStreamSource(PackagedContentRootPath)
        });
        core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        BepuRuntimeComponentRegistration.Register(core);
        SceneEntityAsset colliderEntityAsset = FindEntityAssetWithComponent(sceneAsset.RootEntities, StaticMeshCollider3DComponentTypeId);
        SceneComponentAssetRecord rigidBodyRecord = FindComponentRecord(colliderEntityAsset.Components, RigidBody3DComponentTypeId);
        SceneComponentAssetRecord colliderRecord = FindComponentRecord(colliderEntityAsset.Components, StaticMeshCollider3DComponentTypeId);
        RigidBody3DComponent rigidBody = DeserializeAutomaticComponent<RigidBody3DComponent>(rigidBodyRecord);
        StaticMeshCollider3DComponent staticMeshCollider = DeserializeAutomaticComponent<StaticMeshCollider3DComponent>(colliderRecord);
        Entity colliderEntity = CreateRuntimeEntity(colliderEntityAsset, rigidBody, staticMeshCollider);
        BepuPhysicsWorld3D world = BepuPhysicsWorld3D.CreateDefault();

        Assert.NotNull(staticMeshCollider);
        Assert.NotNull(staticMeshCollider.CookedRuntimeData);
        Assert.Equal(BepuStaticMeshCollisionCookProcessor3D.FormatIdValue, staticMeshCollider.CookedRuntimeData.FormatId);
        using EngineBinaryReader reader = staticMeshCollider.CookedRuntimeData.CreatePayloadReader(
            BepuStaticMeshCollisionCookProcessor3D.FormatIdValue,
            BepuStaticMeshCollisionCookProcessor3D.BinaryFormatIdValue,
            BepuStaticMeshCollisionCookProcessor3D.BinaryFormatVersionValue);
        Assert.True(reader.GetStreamPosition() > 0L);

        world.BindScene(new[] { colliderEntity });
    }

    /// <summary>
    /// Creates one runtime entity from one packaged scene entity asset and supplied physics components.
    /// </summary>
    /// <param name="entityAsset">Packaged scene entity asset.</param>
    /// <param name="rigidBody">Rigid body that should be attached.</param>
    /// <param name="staticMeshCollider">Static mesh collider that should be attached.</param>
    /// <returns>Configured runtime entity.</returns>
    static Entity CreateRuntimeEntity(SceneEntityAsset entityAsset, RigidBody3DComponent rigidBody, StaticMeshCollider3DComponent staticMeshCollider) {
        if (entityAsset == null) {
            throw new ArgumentNullException(nameof(entityAsset));
        } else if (rigidBody == null) {
            throw new ArgumentNullException(nameof(rigidBody));
        } else if (staticMeshCollider == null) {
            throw new ArgumentNullException(nameof(staticMeshCollider));
        }

        Entity entity = new Entity {
            LocalPosition = entityAsset.LocalPosition,
            LocalScale = entityAsset.LocalScale,
            LocalOrientation = entityAsset.LocalOrientation
        };
        entity.InitComponents();
        entity.AddComponent(rigidBody);
        entity.AddComponent(staticMeshCollider);
        return entity;
    }

    /// <summary>
    /// Finds the first entity asset that owns the requested component type.
    /// </summary>
    /// <param name="entities">Entity assets to inspect.</param>
    /// <param name="componentTypeId">Component type id to search for.</param>
    /// <returns>The first entity asset owning the requested component.</returns>
    static SceneEntityAsset FindEntityAssetWithComponent(IReadOnlyList<SceneEntityAsset> entities, string componentTypeId) {
        if (entities == null) {
            throw new ArgumentNullException(nameof(entities));
        } else if (string.IsNullOrWhiteSpace(componentTypeId)) {
            throw new ArgumentException("A component type id must be provided.", nameof(componentTypeId));
        }

        for (int index = 0; index < entities.Count; index++) {
            SceneEntityAsset entity = entities[index];
            if (TryFindComponentRecord(entity.Components, componentTypeId, out _)) {
                return entity;
            }

            SceneEntityAsset childMatch = FindEntityAssetWithComponentOrNull(entity.Children, componentTypeId);
            if (childMatch != null) {
                return childMatch;
            }
        }

        throw new InvalidOperationException($"Expected one entity carrying component type '{componentTypeId}' in the packaged static-mesh showcase scene.");
    }

    /// <summary>
    /// Finds the first matching entity asset beneath the supplied subtree when present.
    /// </summary>
    /// <param name="entities">Entity assets to inspect.</param>
    /// <param name="componentTypeId">Component type id to search for.</param>
    /// <returns>The first matching entity asset when present; otherwise `null`.</returns>
    static SceneEntityAsset FindEntityAssetWithComponentOrNull(IReadOnlyList<SceneEntityAsset> entities, string componentTypeId) {
        if (entities == null) {
            throw new ArgumentNullException(nameof(entities));
        } else if (string.IsNullOrWhiteSpace(componentTypeId)) {
            throw new ArgumentException("A component type id must be provided.", nameof(componentTypeId));
        }

        for (int index = 0; index < entities.Count; index++) {
            SceneEntityAsset entity = entities[index];
            if (TryFindComponentRecord(entity.Components, componentTypeId, out _)) {
                return entity;
            }

            SceneEntityAsset childMatch = FindEntityAssetWithComponentOrNull(entity.Children, componentTypeId);
            if (childMatch != null) {
                return childMatch;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds one component record with the requested component type.
    /// </summary>
    /// <param name="components">Component records to inspect.</param>
    /// <param name="componentTypeId">Component type id to search for.</param>
    /// <returns>Matching component record.</returns>
    static SceneComponentAssetRecord FindComponentRecord(IReadOnlyList<SceneComponentAssetRecord> components, string componentTypeId) {
        if (!TryFindComponentRecord(components, componentTypeId, out SceneComponentAssetRecord record)) {
            throw new InvalidOperationException($"Expected one component record with type id '{componentTypeId}'.");
        }

        return record;
    }

    /// <summary>
    /// Finds one component record with the requested component type when present.
    /// </summary>
    /// <param name="components">Component records to inspect.</param>
    /// <param name="componentTypeId">Component type id to search for.</param>
    /// <param name="record">Receives the matching component record when found.</param>
    /// <returns>`True` when a matching component record is found.</returns>
    static bool TryFindComponentRecord(IReadOnlyList<SceneComponentAssetRecord> components, string componentTypeId, out SceneComponentAssetRecord record) {
        if (components == null) {
            throw new ArgumentNullException(nameof(components));
        } else if (string.IsNullOrWhiteSpace(componentTypeId)) {
            throw new ArgumentException("A component type id must be provided.", nameof(componentTypeId));
        }

        for (int index = 0; index < components.Count; index++) {
            SceneComponentAssetRecord candidate = components[index];
            if (string.Equals(candidate.ComponentTypeId, componentTypeId, StringComparison.Ordinal)) {
                record = candidate;
                return true;
            }
        }

        record = null;
        return false;
    }

    /// <summary>
    /// Deserializes one automatic reflected component record into a live component instance.
    /// </summary>
    /// <typeparam name="T">Expected component type.</typeparam>
    /// <param name="record">Component record to deserialize.</param>
    /// <returns>Deserialized component instance.</returns>
    static T DeserializeAutomaticComponent<T>(SceneComponentAssetRecord record) where T : Component {
        if (record == null) {
            throw new ArgumentNullException(nameof(record));
        }

        AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
        TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
        EntitySaveComponent saveComponent = new EntitySaveComponent();
        return Assert.IsType<T>(descriptor.DeserializeComponent(record, saveComponent, resolver));
    }
}

