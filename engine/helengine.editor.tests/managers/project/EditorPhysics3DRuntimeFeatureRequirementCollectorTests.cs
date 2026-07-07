using helengine.baseplatform.Manifest;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the 3D physics runtime feature collector converts scene analysis into generic runtime feature requirements.
/// </summary>
public sealed class EditorPhysics3DRuntimeFeatureRequirementCollectorTests : IDisposable {
    /// <summary>
    /// Temporary project root used by the collector tests.
    /// </summary>
    readonly string ProjectRootPath;

    /// <summary>
    /// Initializes one temporary project root with an `assets` folder.
    /// </summary>
    public EditorPhysics3DRuntimeFeatureRequirementCollectorTests() {
        ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-physics3d-runtime-feature-collector-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Scenes"));
    }

    /// <summary>
    /// Removes the temporary project root after each test.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    /// <summary>
    /// Verifies one scene with triggers and dynamic box contact emits generic scene-scoped runtime feature requirements.
    /// </summary>
    [Fact]
    public void Collect_withPhysicsScene_returnsGenericRuntimeFeatureRequirements() {
        WriteSceneAsset(
            "Scenes/PhysicsScene.helen",
            new SceneAsset {
                Id = "Scenes/PhysicsScene.helen",
                RootEntities = [
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Ground",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = [
                            CreateRigidBodyRecord(BodyKind3D.Static, false),
                            CreateBoxColliderRecord(new float3(8f, 1f, 8f), false)
                        ],
                        Children = []
                    },
                    new SceneEntityAsset {
                        Id = 2u,
                        Name = "DynamicBox",
                        LocalPosition = new float3(0f, 2f, 0f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = [
                            CreateRigidBodyRecord(BodyKind3D.Dynamic, true),
                            CreateBoxColliderRecord(new float3(1f, 1f, 1f), false)
                        ],
                        Children = []
                    },
                    new SceneEntityAsset {
                        Id = 3u,
                        Name = "Trigger",
                        LocalPosition = new float3(0f, 1f, 2f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = [
                            CreateRigidBodyRecord(BodyKind3D.Static, false),
                            CreateBoxColliderRecord(new float3(2f, 2f, 2f), true)
                        ],
                        Children = []
                    }
                ]
            });

        EditorPhysics3DRuntimeFeatureRequirementCollector collector = new(ProjectRootPath);

        PlatformBuildRequiredRuntimeFeature[] requiredFeatures = collector.Collect(
            new PlatformBuildManifest(
                1,
                "project",
                "1.0.0",
                "1.0.0-engine",
                "windows",
                "1.0.0",
                "PhysicsScene",
                [
                    new PlatformBuildScene(
                        "PhysicsScene",
                        "Physics Scene",
                        "Scenes/PhysicsScene.helen",
                        [],
                        [])
                ],
                Array.Empty<PlatformBuildAsset>(),
                Array.Empty<PlatformBuildArtifact>(),
                Array.Empty<PlatformBuildCodeModule>(),
                Array.Empty<PlatformArtifactPlacement>(),
                new PlatformContainerWritePlan(string.Empty, Array.Empty<PlatformContainerArtifact>())));

        Assert.Collection(
            requiredFeatures,
            requirement => {
                Assert.Equal("PhysicsScene", requirement.SourceId);
                Assert.Equal(RuntimeFeatureRequirementSourceKind.Scene, requirement.SourceKind);
                Assert.Equal(PhysicsSceneFeatureSymbolCatalog3D.TriggerEventsFeatureId, requirement.FeatureId);
            },
            requirement => {
                Assert.Equal("PhysicsScene", requirement.SourceId);
                Assert.Equal(RuntimeFeatureRequirementSourceKind.Scene, requirement.SourceKind);
                Assert.Equal(PhysicsSceneFeatureSymbolCatalog3D.BoxBoxContactFeatureId, requirement.FeatureId);
            });
    }

    /// <summary>
    /// Writes one serialized scene asset into the temporary source project.
    /// </summary>
    /// <param name="sceneId">Project-relative scene id.</param>
    /// <param name="sceneAsset">Scene asset to serialize.</param>
    void WriteSceneAsset(string sceneId, SceneAsset sceneAsset) {
        string fullScenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
        string directoryPath = Path.GetDirectoryName(fullScenePath);
        if (!string.IsNullOrWhiteSpace(directoryPath)) {
            Directory.CreateDirectory(directoryPath);
        }

        using FileStream stream = File.Create(fullScenePath);
        AssetSerializer.Serialize(stream, sceneAsset);
    }

    /// <summary>
    /// Creates one serialized rigid-body component record.
    /// </summary>
    /// <param name="bodyKind">Rigid-body participation mode to encode.</param>
    /// <param name="useGravity">True when gravity should be enabled.</param>
    /// <returns>Serialized rigid-body scene record.</returns>
    static SceneComponentAssetRecord CreateRigidBodyRecord(BodyKind3D bodyKind, bool useGravity) {
        AutomaticScriptComponentPersistenceDescriptor descriptor = new(new ScriptComponentReflectionSchemaBuilder());
        return descriptor.SerializeComponent(
            new RigidBody3DComponent {
                AngularVelocity = float3.Zero,
                BodyKind = bodyKind,
                GravityScale = 1d,
                LinearVelocity = float3.Zero,
                Mass = 1d,
                UseGravity = useGravity
            },
            0,
            new EntityComponentSaveState());
    }

    /// <summary>
    /// Creates one serialized box-collider component record.
    /// </summary>
    /// <param name="size">Full collider size to encode.</param>
    /// <param name="isTrigger">True when the collider should be encoded as a trigger.</param>
    /// <returns>Serialized box-collider scene record.</returns>
    static SceneComponentAssetRecord CreateBoxColliderRecord(float3 size, bool isTrigger) {
        AutomaticScriptComponentPersistenceDescriptor descriptor = new(new ScriptComponentReflectionSchemaBuilder());
        return descriptor.SerializeComponent(
            new BoxCollider3DComponent {
                CollisionLayer = 1,
                CollisionMask = ushort.MaxValue,
                DynamicFriction = 0.4d,
                IsTrigger = isTrigger,
                Restitution = 0d,
                Size = size,
                StaticFriction = 0.6d
            },
            1,
            new EntityComponentSaveState());
    }
}
