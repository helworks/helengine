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
    /// Verifies selected authored scenes resolve the expected compact stripping symbol set.
    /// </summary>
    [Fact]
    public void ResolveSymbols_WithSelectedScenes_ReturnsExpectedPhysicsFeatureSymbols() {
        WriteSceneAsset(
            "Scenes/PhysicsScene.helen",
            new SceneAsset {
                Id = "Scenes/PhysicsScene.helen",
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "ground",
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
                        Id = "dynamic-box",
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
                        Id = "trigger",
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

        IReadOnlyList<string> symbols = service.ResolveSymbols(["Scenes/PhysicsScene.helen"]);

        Assert.Collection(
            symbols,
            symbol => Assert.Equal(PhysicsSceneFeatureSymbolCatalog3D.SceneFeatureStrippingSymbol, symbol),
            symbol => Assert.Equal(PhysicsSceneFeatureSymbolCatalog3D.TriggerEventsSymbol, symbol),
            symbol => Assert.Equal(PhysicsSceneFeatureSymbolCatalog3D.BoxBoxContactSymbol, symbol));
    }

    /// <summary>
    /// Verifies project-relative scene ids cannot escape the source assets folder.
    /// </summary>
    [Fact]
    public void ResolveSymbols_WhenSceneEscapesAssetsRoot_Throws() {
        EditorPhysics3DCodegenFeatureSymbolService service = new EditorPhysics3DCodegenFeatureSymbolService(ProjectRootPath);

        Assert.Throws<InvalidOperationException>(() => service.ResolveSymbols(["..\\outside.helen"]));
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
        using MemoryStream stream = new MemoryStream();
        using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
        writer.WriteByte(1);
        writer.WriteByte((byte)bodyKind);
        writer.WriteByte(useGravity ? (byte)1 : (byte)0);
        writer.WriteSingle(1f);
        writer.WriteSingle(1f);
        writer.WriteFloat3(float3.Zero);

        return new SceneComponentAssetRecord {
            ComponentTypeId = "helengine.RigidBody3DComponent",
            ComponentIndex = 0,
            Payload = stream.ToArray()
        };
    }

    /// <summary>
    /// Creates one serialized box-collider component record.
    /// </summary>
    /// <param name="size">Full collider size to encode.</param>
    /// <param name="isTrigger">True when the collider should be encoded as a trigger.</param>
    /// <returns>Serialized box-collider scene record.</returns>
    static SceneComponentAssetRecord CreateBoxColliderRecord(float3 size, bool isTrigger) {
        using MemoryStream stream = new MemoryStream();
        using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
        writer.WriteByte(2);
        writer.WriteFloat3(size);
        writer.WriteUInt16(1);
        writer.WriteUInt16(ushort.MaxValue);
        writer.WriteByte(isTrigger ? (byte)1 : (byte)0);

        return new SceneComponentAssetRecord {
            ComponentTypeId = "helengine.BoxCollider3DComponent",
            ComponentIndex = 1,
            Payload = stream.ToArray()
        };
    }
}
