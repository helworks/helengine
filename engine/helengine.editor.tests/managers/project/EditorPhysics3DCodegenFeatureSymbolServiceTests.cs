namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies source-scene 3D physics feature analysis resolves deterministic codegen stripping symbols.
/// </summary>
public sealed class EditorPhysics3DCodegenFeatureSymbolServiceTests : IDisposable {
    /// <summary>
    /// Temporary project root used by the service tests.
    /// </summary>
    readonly string ProjectRootPath;

    /// <summary>
    /// Initializes one temporary project root with an `assets` folder.
    /// </summary>
    public EditorPhysics3DCodegenFeatureSymbolServiceTests() {
        ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-physics3d-codegen-feature-symbol-tests", Guid.NewGuid().ToString("N"));
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
    /// Verifies selected stable scene ids resolve the expected compact stripping symbol set.
    /// </summary>
    [Fact]
    public void ResolveSymbols_WithSelectedScenes_ReturnsExpectedPhysicsFeatureSymbols() {
        WriteSceneAsset(
            "Scenes/PhysicsScene.helen",
            new SceneAsset {
                Id = "Scenes/PhysicsScene.helen",
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Ground",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateRigidBodyRecord(BodyKind3D.Static, false),
                            CreateBoxColliderRecord(new float3(8f, 1f, 8f), false)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 2u,
                        Name = "DynamicBox",
                        LocalPosition = new float3(0f, 2f, 0f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateRigidBodyRecord(BodyKind3D.Dynamic, true),
                            CreateBoxColliderRecord(new float3(1f, 1f, 1f), false)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 3u,
                        Name = "Trigger",
                        LocalPosition = new float3(0f, 1f, 2f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateRigidBodyRecord(BodyKind3D.Static, false),
                            CreateBoxColliderRecord(new float3(2f, 2f, 2f), true)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            });

        EditorPhysics3DCodegenFeatureSymbolService service = new EditorPhysics3DCodegenFeatureSymbolService(ProjectRootPath);

        IReadOnlyList<string> symbols = service.ResolveSymbols(["PhysicsScene"]);

        Assert.Collection(
            symbols,
            symbol => Assert.Equal(PhysicsSceneFeatureSymbolCatalog3D.SceneFeatureStrippingSymbol, symbol),
            symbol => Assert.Equal(PhysicsSceneFeatureSymbolCatalog3D.TriggerEventsSymbol, symbol),
            symbol => Assert.Equal(PhysicsSceneFeatureSymbolCatalog3D.BoxBoxContactSymbol, symbol));
    }

    /// <summary>
    /// Verifies missing scene ids fail fast instead of being treated as relative asset paths.
    /// </summary>
    [Fact]
    public void ResolveSymbols_WhenSceneIdDoesNotExist_Throws() {
        EditorPhysics3DCodegenFeatureSymbolService service = new EditorPhysics3DCodegenFeatureSymbolService(ProjectRootPath);

        Assert.Throws<InvalidOperationException>(() => service.ResolveSymbols(["MissingScene"]));
    }

    /// <summary>
    /// Verifies automatic reflected rigid-body and box-collider payloads remain analyzable after built-in persistence moved away from manual descriptors.
    /// </summary>
    [Fact]
    public void ResolveSymbols_WithAutomaticPhysicsPayloads_ReturnsExpectedPhysicsFeatureSymbols() {
        WriteSceneAsset(
            "Scenes/AutomaticPhysicsScene.helen",
            new SceneAsset {
                Id = "Scenes/AutomaticPhysicsScene.helen",
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Ground",
                        LocalPosition = new float3(0f, -1f, 0f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateAutomaticRigidBodyRecord(BodyKind3D.Static, false),
                            CreateAutomaticBoxColliderRecord(new float3(8f, 1f, 8f), false)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 2u,
                        Name = "DynamicBox",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateAutomaticRigidBodyRecord(BodyKind3D.Dynamic, true),
                            CreateAutomaticBoxColliderRecord(new float3(1f, 1f, 1f), false)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 3u,
                        Name = "Trigger",
                        LocalPosition = new float3(0f, 2f, 0f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateAutomaticRigidBodyRecord(BodyKind3D.Static, false),
                            CreateAutomaticBoxColliderRecord(new float3(2f, 2f, 2f), true)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            });

        EditorPhysics3DCodegenFeatureSymbolService service = new EditorPhysics3DCodegenFeatureSymbolService(ProjectRootPath);

        IReadOnlyList<string> symbols = service.ResolveSymbols(["AutomaticPhysicsScene"]);

        Assert.Collection(
            symbols,
            symbol => Assert.Equal(PhysicsSceneFeatureSymbolCatalog3D.SceneFeatureStrippingSymbol, symbol),
            symbol => Assert.Equal(PhysicsSceneFeatureSymbolCatalog3D.TriggerEventsSymbol, symbol),
            symbol => Assert.Equal(PhysicsSceneFeatureSymbolCatalog3D.BoxBoxContactSymbol, symbol));
    }

    /// <summary>
    /// Verifies selected stable scene ids resolve the expected raw 3D physics feature flags for downstream generic runtime feature collectors.
    /// </summary>
    [Fact]
    public void ResolveFeatureFlags_WithSelectedScenes_ReturnsExpectedPhysicsFeatureFlags() {
        WriteSceneAsset(
            "Scenes/PhysicsFlagsScene.helen",
            new SceneAsset {
                Id = "Scenes/PhysicsFlagsScene.helen",
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Ground",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateRigidBodyRecord(BodyKind3D.Static, false),
                            CreateBoxColliderRecord(new float3(8f, 1f, 8f), false)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 2u,
                        Name = "DynamicBox",
                        LocalPosition = new float3(0f, 2f, 0f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateRigidBodyRecord(BodyKind3D.Dynamic, true),
                            CreateBoxColliderRecord(new float3(1f, 1f, 1f), false)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 3u,
                        Name = "Trigger",
                        LocalPosition = new float3(0f, 1f, 2f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateRigidBodyRecord(BodyKind3D.Static, false),
                            CreateBoxColliderRecord(new float3(2f, 2f, 2f), true)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            });

        EditorPhysics3DCodegenFeatureSymbolService service = new EditorPhysics3DCodegenFeatureSymbolService(ProjectRootPath);

        PhysicsSceneFeatureFlags3D featureFlags = service.ResolveFeatureFlags(["PhysicsFlagsScene"]);

        Assert.Equal(
            PhysicsSceneFeatureFlags3D.TriggerEvents | PhysicsSceneFeatureFlags3D.BoxBoxContact,
            featureFlags);
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
        AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
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
    /// Creates one serialized box-collider component record from the editor automatic persistence path.
    /// </summary>
    /// <param name="size">Full collider size to encode.</param>
    /// <param name="isTrigger">True when the collider should be encoded as a trigger.</param>
    /// <returns>Serialized box-collider scene record.</returns>
    static SceneComponentAssetRecord CreateBoxColliderRecord(float3 size, bool isTrigger) {
        AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
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

    /// <summary>
    /// Creates one reflected automatic rigid-body component record using the current ordered member payload contract.
    /// </summary>
    /// <param name="bodyKind">Rigid-body participation mode to encode.</param>
    /// <param name="useGravity">True when gravity should be enabled.</param>
    /// <returns>Serialized rigid-body scene record that matches automatic built-in persistence.</returns>
    static SceneComponentAssetRecord CreateAutomaticRigidBodyRecord(BodyKind3D bodyKind, bool useGravity) {
        AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
        SceneComponentAssetRecord record = descriptor.SerializeComponent(
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

        AutomaticScriptComponentPersistenceDescriptor automaticDescriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
        return new SceneComponentAssetRecord {
            ComponentTypeId = record.ComponentTypeId,
            ComponentIndex = record.ComponentIndex,
            Payload = automaticDescriptor.DeserializeComponent(record, null, null) is RigidBody3DComponent component
                ? SerializeAutomaticRigidBodyRuntimePayload(component)
                : throw new InvalidOperationException("Serialized rigid-body test record could not be restored.")
        };
    }

    /// <summary>
    /// Creates one reflected automatic box-collider component record using the current ordered member payload contract.
    /// </summary>
    /// <param name="size">Full collider size to encode.</param>
    /// <param name="isTrigger">True when the collider should be encoded as a trigger.</param>
    /// <returns>Serialized box-collider scene record that matches automatic built-in persistence.</returns>
    static SceneComponentAssetRecord CreateAutomaticBoxColliderRecord(float3 size, bool isTrigger) {
        AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
        SceneComponentAssetRecord record = descriptor.SerializeComponent(
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

        AutomaticScriptComponentPersistenceDescriptor automaticDescriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
        return new SceneComponentAssetRecord {
            ComponentTypeId = record.ComponentTypeId,
            ComponentIndex = record.ComponentIndex,
            Payload = automaticDescriptor.DeserializeComponent(record, null, null) is BoxCollider3DComponent component
                ? SerializeAutomaticBoxColliderRuntimePayload(component)
                : throw new InvalidOperationException("Serialized box-collider test record could not be restored.")
        };
    }

    /// <summary>
    /// Serializes one rigid-body component into the ordinal runtime payload used by packaged automatic components.
    /// </summary>
    /// <param name="component">Rigid-body component to serialize.</param>
    /// <returns>Ordinal runtime payload for the component.</returns>
    static byte[] SerializeAutomaticRigidBodyRuntimePayload(RigidBody3DComponent component) {
        if (component == null) {
            throw new ArgumentNullException(nameof(component));
        }

        using MemoryStream stream = new MemoryStream();
        using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
        writer.WriteByte(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion);
        writer.WriteInt32(6);
        writer.WriteFloat3(component.AngularVelocity);
        writer.WriteInt32((int)component.BodyKind);
        writer.WriteDouble(component.GravityScale);
        writer.WriteFloat3(component.LinearVelocity);
        writer.WriteDouble(component.Mass);
        writer.WriteByte(component.UseGravity ? (byte)1 : (byte)0);
        return stream.ToArray();
    }

    /// <summary>
    /// Serializes one box-collider component into the ordinal runtime payload used by packaged automatic components.
    /// </summary>
    /// <param name="component">Box-collider component to serialize.</param>
    /// <returns>Ordinal runtime payload for the component.</returns>
    static byte[] SerializeAutomaticBoxColliderRuntimePayload(BoxCollider3DComponent component) {
        if (component == null) {
            throw new ArgumentNullException(nameof(component));
        }

        using MemoryStream stream = new MemoryStream();
        using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
        writer.WriteByte(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion);
        writer.WriteInt32(7);
        writer.WriteUInt16(component.CollisionLayer);
        writer.WriteUInt16(component.CollisionMask);
        writer.WriteDouble(component.DynamicFriction);
        writer.WriteByte(component.IsTrigger ? (byte)1 : (byte)0);
        writer.WriteDouble(component.Restitution);
        writer.WriteFloat3(component.Size);
        writer.WriteDouble(component.StaticFriction);
        return stream.ToArray();
    }
}
