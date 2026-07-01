using helengine.editor;

namespace helengine.editor.tests;

/// <summary>
/// Verifies build-time 3D physics feature analysis can read reflected static-mesh collider payloads emitted by the editor persistence path.
/// </summary>
public sealed class PhysicsSceneFeatureAnalyzerStaticMeshPayloadTests {
    /// <summary>
    /// Ensures one reflected static-mesh collider payload does not throw during feature analysis and still reports sphere-versus-static-mesh contact support.
    /// </summary>
    [Fact]
    public void AnalyzeSceneAsset_WhenStaticMeshColliderUsesReflectedPayload_ReportsSphereStaticMeshContact() {
        SceneAsset sceneAsset = CreateStaticMeshFeatureAnalysisSceneAsset(false);

        PhysicsSceneFeatureFlags3D features = PhysicsSceneFeatureAnalyzer3D.Analyze(sceneAsset);

        Assert.True((features & PhysicsSceneFeatureFlags3D.SphereStaticMeshContact) != 0);
    }

    /// <summary>
    /// Ensures one reflected static-mesh collider payload that already carries one cooked runtime payload still reports sphere-versus-static-mesh contact support.
    /// </summary>
    [Fact]
    public void AnalyzeSceneAsset_WhenStaticMeshColliderUsesReflectedPayloadWithCookedRuntimeData_ReportsSphereStaticMeshContact() {
        SceneAsset sceneAsset = CreateStaticMeshFeatureAnalysisSceneAsset(true);

        PhysicsSceneFeatureFlags3D features = PhysicsSceneFeatureAnalyzer3D.Analyze(sceneAsset);

        Assert.True((features & PhysicsSceneFeatureFlags3D.SphereStaticMeshContact) != 0);
    }

    /// <summary>
    /// Creates one serialized scene asset that pairs one dynamic sphere with one static-mesh collider for feature-analysis validation.
    /// </summary>
    /// <param name="includeCookedRuntimeData">True when the static-mesh collider should also carry one cooked backend payload.</param>
    /// <returns>Serialized scene asset that exercises static-mesh feature analysis.</returns>
    static SceneAsset CreateStaticMeshFeatureAnalysisSceneAsset(bool includeCookedRuntimeData) {
        return new SceneAsset {
            Id = "scenes/test_static_mesh_feature_analysis.helen",
            RootEntities = new[] {
                new SceneEntityAsset {
                    Id = 1u,
                    Name = "StaticMeshGround",
                    LocalPosition = float3.Zero,
                    LocalScale = float3.One,
                    LocalOrientation = float4.Identity,
                    Components = new[] {
                        CreateRigidBodyRecord(BodyKind3D.Static, false, 0),
                        CreateStaticMeshColliderRecord(includeCookedRuntimeData, 1)
                    },
                    Children = Array.Empty<SceneEntityAsset>()
                },
                new SceneEntityAsset {
                    Id = 2u,
                    Name = "DynamicSphere",
                    LocalPosition = new float3(0f, 2f, 0f),
                    LocalScale = float3.One,
                    LocalOrientation = float4.Identity,
                    Components = new[] {
                        CreateRigidBodyRecord(BodyKind3D.Dynamic, true, 0),
                        CreateSphereColliderRecord(0.5f, 1)
                    },
                    Children = Array.Empty<SceneEntityAsset>()
                }
            }
        };
    }

    /// <summary>
    /// Creates one serialized rigid-body component record using the automatic reflected persistence path.
    /// </summary>
    /// <param name="bodyKind">Rigid-body participation mode stored in the payload.</param>
    /// <param name="useGravity">True when gravity should remain enabled.</param>
    /// <param name="componentIndex">Entity-local component order index.</param>
    /// <returns>Serialized reflected rigid-body record.</returns>
    static SceneComponentAssetRecord CreateRigidBodyRecord(BodyKind3D bodyKind, bool useGravity, int componentIndex) {
        RigidBody3DComponent component = new RigidBody3DComponent {
            BodyKind = bodyKind,
            UseGravity = useGravity,
            Mass = 1d,
            GravityScale = 1d,
            LinearVelocity = float3.Zero,
            AngularVelocity = float3.Zero
        };
        return CreateRuntimeAutomaticComponentRecord(component, componentIndex);
    }

    /// <summary>
    /// Creates one serialized reflected static-mesh collider component record for build-time feature analysis.
    /// </summary>
    /// <param name="includeCookedRuntimeData">True when the reflected payload should also serialize one cooked backend payload.</param>
    /// <param name="componentIndex">Entity-local component order index.</param>
    /// <returns>Serialized reflected static-mesh collider record.</returns>
    static SceneComponentAssetRecord CreateStaticMeshColliderRecord(bool includeCookedRuntimeData, int componentIndex) {
        StaticMeshCollider3DComponent component = new StaticMeshCollider3DComponent {
            CollisionData = new StaticMeshCollisionData3D(
                [
                    new float3(-1f, 0f, -1f),
                    new float3(1f, 0f, -1f),
                    new float3(1f, 0f, 1f),
                    new float3(-1f, 0f, 1f)
                ],
                [0, 2, 1, 0, 3, 2]),
            CollisionLayer = 1,
            CollisionMask = ushort.MaxValue,
            DynamicFriction = 0.4d,
            IsTrigger = false,
            Restitution = 0d,
            StaticFriction = 0.6d
        };
        if (includeCookedRuntimeData) {
            component.CookedRuntimeData = StaticMeshCollisionRuntimeData3D.Create(
                "helengine.bepu.static-mesh",
                0x7302,
                1,
                EngineBinaryEndianness.LittleEndian,
                writer => writer.WriteInt32(1));
        }

        return CreateRuntimeAutomaticComponentRecord(component, componentIndex);
    }

    /// <summary>
    /// Creates one serialized reflected sphere-collider component record for build-time feature analysis.
    /// </summary>
    /// <param name="radius">Sphere radius stored in the payload.</param>
    /// <param name="componentIndex">Entity-local component order index.</param>
    /// <returns>Serialized reflected sphere-collider record.</returns>
    static SceneComponentAssetRecord CreateSphereColliderRecord(float radius, int componentIndex) {
        SphereCollider3DComponent component = new SphereCollider3DComponent {
            CollisionLayer = 1,
            CollisionMask = ushort.MaxValue,
            DynamicFriction = 0.4d,
            IsTrigger = false,
            Radius = radius,
            Restitution = 0d,
            StaticFriction = 0.6d
        };
        return CreateRuntimeAutomaticComponentRecord(component, componentIndex);
    }

    /// <summary>
    /// Serializes one component into the automatic runtime payload layout consumed by build-time feature analysis.
    /// </summary>
    /// <param name="component">Component instance to serialize.</param>
    /// <param name="componentIndex">Entity-local component order index.</param>
    /// <returns>Serialized runtime component record.</returns>
    static SceneComponentAssetRecord CreateRuntimeAutomaticComponentRecord(Component component, int componentIndex) {
        ScriptComponentReflectionSchemaBuilder schemaBuilder = new ScriptComponentReflectionSchemaBuilder();
        ScriptComponentReflectionSchema schema = schemaBuilder.Build(component.GetType());
        using MemoryStream stream = new MemoryStream();
        using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
        writer.WriteByte(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion);
        writer.WriteInt32(schema.Members.Count);
        for (int index = 0; index < schema.Members.Count; index++) {
            ScriptComponentReflectionMember member = schema.Members[index];
            AutomaticScriptComponentPersistenceDescriptor.WriteSupportedMemberValue(writer, member, component, null);
        }

        return new SceneComponentAssetRecord {
            ComponentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(component.GetType()),
            ComponentIndex = componentIndex,
            Payload = stream.ToArray()
        };
    }
}
