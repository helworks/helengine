namespace helengine {
    /// <summary>
    /// Materializes packaged scene assets into live runtime entities for player builds.
    /// </summary>
    public sealed class RuntimeSceneLoadService {
        /// <summary>
        /// Current payload version for serialized mesh component scene records.
        /// </summary>
        const byte MeshComponentPayloadVersion = 1;

        /// <summary>
        /// Current payload version for serialized camera component scene records.
        /// </summary>
        const byte CameraComponentPayloadVersion = 1;

        /// <summary>
        /// Stable serialized component id for mesh components.
        /// </summary>
        const string MeshComponentTypeId = "helengine.MeshComponent";

        /// <summary>
        /// Stable serialized component id for camera components.
        /// </summary>
        const string CameraComponentTypeId = "helengine.CameraComponent";

        /// <summary>
        /// Resolver used to rebuild runtime assets referenced by packaged scene records.
        /// </summary>
        readonly RuntimeSceneAssetReferenceResolver ReferenceResolver;

        /// <summary>
        /// Initializes a new runtime scene-load service.
        /// </summary>
        /// <param name="referenceResolver">Resolver used to rebuild packaged runtime assets.</param>
        public RuntimeSceneLoadService(RuntimeSceneAssetReferenceResolver referenceResolver) {
            ReferenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
        }

        /// <summary>
        /// Loads root runtime entities from one packaged scene asset.
        /// </summary>
        /// <param name="sceneAsset">Packaged scene asset payload to materialize.</param>
        /// <returns>Loaded root runtime entities.</returns>
        public IReadOnlyList<Entity> Load(SceneAsset sceneAsset) {
            if (sceneAsset == null) {
                throw new ArgumentNullException(nameof(sceneAsset));
            }

            Logger.WriteLine("Loading packaged scene assets.");
            System.Diagnostics.Stopwatch loadStopwatch = System.Diagnostics.Stopwatch.StartNew();
            SceneEntityAsset[] rootEntityAssets = sceneAsset.RootEntities ?? Array.Empty<SceneEntityAsset>();
            List<Entity> rootEntities = new List<Entity>(rootEntityAssets.Length);
            for (int index = 0; index < rootEntityAssets.Length; index++) {
                rootEntities.Add(LoadEntity(rootEntityAssets[index]));
            }

            loadStopwatch.Stop();
            Logger.WriteLine($"Loaded packaged scene assets ({rootEntities.Count} root entities).");

            return rootEntities;
        }

        /// <summary>
        /// Loads one serialized runtime entity recursively.
        /// </summary>
        /// <param name="entityAsset">Serialized runtime entity payload to materialize.</param>
        /// <returns>Loaded runtime entity.</returns>
        Entity LoadEntity(SceneEntityAsset entityAsset) {
            if (entityAsset == null) {
                throw new ArgumentNullException(nameof(entityAsset));
            }

            Entity entity = new Entity {
                LocalPosition = entityAsset.LocalPosition,
                LocalScale = entityAsset.LocalScale,
                LocalOrientation = entityAsset.LocalOrientation
            };
            entity.InitComponents();
            entity.InitChildren();

            SceneComponentAssetRecord[] componentRecords = entityAsset.Components ?? Array.Empty<SceneComponentAssetRecord>();
            for (int index = 0; index < componentRecords.Length; index++) {
                entity.AddComponent(LoadComponent(componentRecords[index]));
            }

            SceneEntityAsset[] childEntityAssets = entityAsset.Children ?? Array.Empty<SceneEntityAsset>();
            for (int index = 0; index < childEntityAssets.Length; index++) {
                entity.AddChild(LoadEntity(childEntityAssets[index]));
            }

            return entity;
        }

        /// <summary>
        /// Loads one serialized runtime component from its scene record.
        /// </summary>
        /// <param name="record">Serialized component record to materialize.</param>
        /// <returns>Loaded runtime component.</returns>
        Component LoadComponent(SceneComponentAssetRecord record) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            if (string.Equals(record.ComponentTypeId, MeshComponentTypeId, StringComparison.Ordinal)) {
                return LoadMeshComponent(record);
            }

            if (string.Equals(record.ComponentTypeId, CameraComponentTypeId, StringComparison.Ordinal)) {
                return LoadCameraComponent(record);
            }

            throw new InvalidOperationException($"Player builds do not support serialized component type '{record.ComponentTypeId}' yet.");
        }

        /// <summary>
        /// Loads one serialized mesh component from its scene record.
        /// </summary>
        /// <param name="record">Serialized mesh component scene record.</param>
        /// <returns>Loaded runtime mesh component.</returns>
        MeshComponent LoadMeshComponent(SceneComponentAssetRecord record) {
            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != MeshComponentPayloadVersion) {
                throw new InvalidOperationException($"Unsupported mesh component payload version '{version}'.");
            }

            SceneAssetReference modelReference = ReadOptionalReference(reader);
            SceneAssetReference materialReference = ReadOptionalReference(reader);
            byte renderOrder3D = reader.ReadByte();

            MeshComponent meshComponent = new MeshComponent {
                RenderOrder3D = renderOrder3D
            };

            if (modelReference != null) {
                meshComponent.Model = ReferenceResolver.ResolveModel(modelReference);
            }

            if (materialReference != null) {
                meshComponent.Material = ReferenceResolver.ResolveMaterial(materialReference);
            }

            return meshComponent;
        }

        /// <summary>
        /// Loads one serialized camera component from its scene record.
        /// </summary>
        /// <param name="record">Serialized camera component scene record.</param>
        /// <returns>Loaded runtime camera component.</returns>
        CameraComponent LoadCameraComponent(SceneComponentAssetRecord record) {
            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != CameraComponentPayloadVersion) {
                throw new InvalidOperationException($"Unsupported camera component payload version '{version}'.");
            }

            return new CameraComponent {
                CameraDrawOrder = reader.ReadByte(),
                LayerMask = reader.ReadUInt16(),
                Viewport = ReadFloat4(reader),
                ClearSettings = ReadClearSettings(reader)
            };
        }

        /// <summary>
        /// Reads one optional scene asset reference from the current payload position.
        /// </summary>
        /// <param name="reader">Reader positioned at the optional-reference payload.</param>
        /// <returns>Decoded scene asset reference when present; otherwise null.</returns>
        SceneAssetReference ReadOptionalReference(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            if (reader.ReadByte() == 0) {
                return null;
            }

            return new SceneAssetReference {
                SourceKind = (SceneAssetReferenceSourceKind)reader.ReadInt32(),
                RelativePath = reader.ReadString(),
                ProviderId = reader.ReadString(),
                AssetId = reader.ReadString()
            };
        }

        /// <summary>
        /// Reads one <see cref="float4"/> value from the current payload position.
        /// </summary>
        /// <param name="reader">Reader positioned at the vector payload.</param>
        /// <returns>Decoded <see cref="float4"/> value.</returns>
        float4 ReadFloat4(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new float4(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());
        }

        /// <summary>
        /// Reads one camera clear-settings payload from the current payload position.
        /// </summary>
        /// <param name="reader">Reader positioned at the clear-settings payload.</param>
        /// <returns>Decoded camera clear settings.</returns>
        CameraClearSettings ReadClearSettings(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new CameraClearSettings(
                reader.ReadByte() != 0,
                ReadFloat4(reader),
                reader.ReadByte() != 0,
                reader.ReadSingle(),
                reader.ReadByte() != 0,
                reader.ReadByte());
        }
    }
}
