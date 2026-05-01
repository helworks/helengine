namespace helengine.editor {
    /// <summary>
    /// Packages selected editor scenes and their required runtime assets into one Windows player content root.
    /// </summary>
    public sealed class EditorPlatformBuildScenePackager {
        /// <summary>
        /// Relative packaged scene path used as the Windows startup scene.
        /// </summary>
        public const string StartupSceneRelativePath = "scenes/startup.helen";

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
        /// Runtime scene layer used by the current Windows player loader for materialized entities.
        /// </summary>
        const ushort RuntimeSceneLayerMask = 0b00000001;

        /// <summary>
        /// Stable generated-asset provider id used by engine-generated scene references.
        /// </summary>
        const string EngineGeneratedProviderId = "engine";

        /// <summary>
        /// Stable generated model asset id for the built-in cube primitive.
        /// </summary>
        const string CubeGeneratedAssetId = "engine:model:cube";

        /// <summary>
        /// Stable generated model asset id for the built-in plane primitive.
        /// </summary>
        const string PlaneGeneratedAssetId = "engine:model:plane";

        /// <summary>
        /// Stable generated material asset id for the built-in standard material.
        /// </summary>
        const string StandardGeneratedMaterialAssetId = "engine:material:standard";

        /// <summary>
        /// Shader source file used by the packaged generated standard material.
        /// </summary>
        const string StandardShaderFileName = "EditorDefaultMesh.hlsl";

        /// <summary>
        /// Vertex program name used by the packaged generated standard material.
        /// </summary>
        const string StandardVertexProgramName = "EditorDefaultMesh.vs";

        /// <summary>
        /// Pixel program name used by the packaged generated standard material.
        /// </summary>
        const string StandardPixelProgramName = "EditorDefaultMesh.ps";

        /// <summary>
        /// Shader variant name used by the packaged generated standard material.
        /// </summary>
        const string StandardShaderVariantName = "default";

        /// <summary>
        /// Relative packaged material path used by generated primitive scenes.
        /// </summary>
        const string StandardGeneratedMaterialRelativePath = "generated/engine/materials/standard.helmat";

        /// <summary>
        /// Relative packaged shader path used by generated primitive scenes.
        /// </summary>
        const string StandardGeneratedShaderRelativePath = "shaders/EditorDefaultMesh.dx11.shader.asset";

        /// <summary>
        /// Absolute project root that owns the source `assets` folder.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Absolute source assets root used to resolve project-relative scene ids and file-backed asset references.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Content manager used to load serialized scene and material assets from the project.
        /// </summary>
        readonly ContentManager ProjectContentManager;

        /// <summary>
        /// Asset import manager used to resolve file-backed source models into processed `ModelAsset` payloads.
        /// </summary>
        readonly AssetImportManager AssetImportManager;

        /// <summary>
        /// Resolver used to obtain processed `ModelAsset` payloads for file-backed source models.
        /// </summary>
        readonly EditorFileSystemModelResolver FileSystemModelResolver;

        /// <summary>
        /// Deduplicated shader asset ids referenced while packaging the current scene set.
        /// </summary>
        readonly List<string> ReferencedShaderAssetIds;

        /// <summary>
        /// Importer registrations supplied by the editor host for source-backed asset loading.
        /// </summary>
        readonly IReadOnlyList<IAssetImporterRegistration> Importers;

        /// <summary>
        /// Fast lookup used to deduplicate referenced shader asset ids while preserving discovery order.
        /// </summary>
        readonly HashSet<string> ReferencedShaderAssetIdsSet;

        /// <summary>
        /// Initializes one Windows scene packager for the supplied project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        public EditorPlatformBuildScenePackager(string projectRootPath)
            : this(projectRootPath, Array.Empty<IAssetImporterRegistration>()) {
        }

        /// <summary>
        /// Initializes one Windows scene packager for the supplied project root and importer registrations.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        /// <param name="importers">Importer registrations supplied by the editor host.</param>
        public EditorPlatformBuildScenePackager(string projectRootPath, IReadOnlyList<IAssetImporterRegistration> importers)
            : this(projectRootPath, importers, "windows") {
        }

        /// <summary>
        /// Initializes one scene packager for the supplied project root, importer registrations, and target platform id.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        /// <param name="importers">Importer registrations supplied by the editor host.</param>
        /// <param name="targetPlatformId">Platform id that should be reported to the asset-import pipeline.</param>
        public EditorPlatformBuildScenePackager(string projectRootPath, IReadOnlyList<IAssetImporterRegistration> importers, string targetPlatformId) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (importers == null) {
                throw new ArgumentNullException(nameof(importers));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
            ProjectContentManager = new ContentManager(AssetsRootPath);
            EditorContentManagerConfiguration.ConfigureSharedAssetContentManager(ProjectContentManager);

            ContentManager importContentManager = new ContentManager(AssetsRootPath);
            AssetImportManager = new AssetImportManager(ProjectRootPath, importContentManager);
            AssetImportManager.CurrentPlatformId = string.IsNullOrWhiteSpace(targetPlatformId) ? "windows" : targetPlatformId;
            Importers = importers;
            for (int index = 0; index < Importers.Count; index++) {
                IAssetImporterRegistration registration = Importers[index];
                if (registration == null) {
                    throw new InvalidOperationException("Importer registrations must not contain null entries.");
                }

                registration.Register(AssetImportManager);
            }
            FileSystemModelResolver = new EditorFileSystemModelResolver(AssetImportManager);
            ReferencedShaderAssetIds = new List<string>();
            ReferencedShaderAssetIdsSet = new HashSet<string>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Packages the selected scenes and the assets they require into the supplied build root.
        /// </summary>
        /// <param name="sceneIds">Project-relative scene ids selected for the build.</param>
        /// <param name="buildRootPath">Absolute build root path that will host the packaged content.</param>
        /// <returns>Scene-packaging result that carries the referenced shader ids.</returns>
        public EditorPlatformBuildScenePackagerResult Package(IReadOnlyList<string> sceneIds, string buildRootPath) {
            if (sceneIds == null) {
                throw new ArgumentNullException(nameof(sceneIds));
            }
            if (sceneIds.Count == 0) {
                throw new InvalidOperationException("At least one scene must be selected for the Windows build.");
            }
            if (string.IsNullOrWhiteSpace(buildRootPath)) {
                throw new ArgumentException("Build root path must be provided.", nameof(buildRootPath));
            }

            string fullBuildRootPath = Path.GetFullPath(buildRootPath);
            Directory.CreateDirectory(fullBuildRootPath);

            ReferencedShaderAssetIds.Clear();
            ReferencedShaderAssetIdsSet.Clear();
            EnsureGeneratedStandardMaterialAssets(fullBuildRootPath);

            for (int index = 0; index < sceneIds.Count; index++) {
                string sceneId = sceneIds[index];
                SceneAsset packagedSceneAsset = LoadSceneAsset(sceneId);
                RewriteSceneAsset(packagedSceneAsset, fullBuildRootPath);

                string packagedSceneRelativePath = BuildPackagedSceneRelativePath(sceneId);
                WriteAsset(Path.Combine(fullBuildRootPath, packagedSceneRelativePath), packagedSceneAsset);
                if (index == 0) {
                    WriteAsset(Path.Combine(fullBuildRootPath, StartupSceneRelativePath), packagedSceneAsset);
                }
            }

            return new EditorPlatformBuildScenePackagerResult(ReferencedShaderAssetIds);
        }

        /// <summary>
        /// Loads one selected scene asset from the source project.
        /// </summary>
        /// <param name="sceneId">Project-relative scene id to load.</param>
        /// <returns>Loaded serialized scene asset.</returns>
        SceneAsset LoadSceneAsset(string sceneId) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            string fullScenePath = ResolveProjectAssetPath(sceneId);
            using FileStream stream = File.OpenRead(fullScenePath);
            Asset asset = AssetSerializer.Deserialize(stream);
            if (asset is not SceneAsset sceneAsset) {
                throw new InvalidOperationException($"Scene '{sceneId}' did not deserialize into a SceneAsset.");
            }

            return sceneAsset;
        }

        /// <summary>
        /// Rewrites one serialized scene asset in place so it targets packaged runtime files instead of editor-only references.
        /// </summary>
        /// <param name="sceneAsset">Scene asset to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        void RewriteSceneAsset(SceneAsset sceneAsset, string buildRootPath) {
            if (sceneAsset == null) {
                throw new ArgumentNullException(nameof(sceneAsset));
            }
            if (string.IsNullOrWhiteSpace(buildRootPath)) {
                throw new ArgumentException("Build root path must be provided.", nameof(buildRootPath));
            }

            SceneEntityAsset[] rootEntityAssets = sceneAsset.RootEntities ?? Array.Empty<SceneEntityAsset>();
            for (int index = 0; index < rootEntityAssets.Length; index++) {
                RewriteEntityAsset(rootEntityAssets[index], buildRootPath);
            }
        }

        /// <summary>
        /// Rewrites one serialized scene entity recursively.
        /// </summary>
        /// <param name="entityAsset">Scene entity payload to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        void RewriteEntityAsset(SceneEntityAsset entityAsset, string buildRootPath) {
            if (entityAsset == null) {
                throw new ArgumentNullException(nameof(entityAsset));
            }

            SceneComponentAssetRecord[] componentRecords = entityAsset.Components ?? Array.Empty<SceneComponentAssetRecord>();
            for (int index = 0; index < componentRecords.Length; index++) {
                componentRecords[index] = RewriteComponentRecord(componentRecords[index], buildRootPath);
            }

            SceneEntityAsset[] childEntityAssets = entityAsset.Children ?? Array.Empty<SceneEntityAsset>();
            for (int index = 0; index < childEntityAssets.Length; index++) {
                RewriteEntityAsset(childEntityAssets[index], buildRootPath);
            }
        }

        /// <summary>
        /// Rewrites one serialized component record into its packaged runtime form.
        /// </summary>
        /// <param name="record">Component record to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Rewritten component record.</returns>
        SceneComponentAssetRecord RewriteComponentRecord(SceneComponentAssetRecord record, string buildRootPath) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            if (string.Equals(record.ComponentTypeId, MeshComponentTypeId, StringComparison.Ordinal)) {
                return RewriteMeshComponentRecord(record, buildRootPath);
            }

            if (string.Equals(record.ComponentTypeId, CameraComponentTypeId, StringComparison.Ordinal)) {
                return RewriteCameraComponentRecord(record);
            }

            throw new InvalidOperationException($"Windows player packaging does not support serialized component type '{record.ComponentTypeId}' yet.");
        }

        /// <summary>
        /// Rewrites one serialized mesh component record into its packaged runtime form.
        /// </summary>
        /// <param name="record">Serialized mesh component record to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Rewritten mesh component record.</returns>
        SceneComponentAssetRecord RewriteMeshComponentRecord(SceneComponentAssetRecord record, string buildRootPath) {
            using MemoryStream readStream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(readStream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != MeshComponentPayloadVersion) {
                throw new InvalidOperationException($"Unsupported mesh component payload version '{version}'.");
            }

            SceneAssetReference modelReference = RewriteModelReference(ReadOptionalReference(reader), buildRootPath);
            SceneAssetReference materialReference = RewriteMaterialReference(ReadOptionalReference(reader), buildRootPath);
            byte renderOrder3D = reader.ReadByte();

            using MemoryStream writeStream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(writeStream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(MeshComponentPayloadVersion);
            WriteOptionalReference(writer, modelReference);
            WriteOptionalReference(writer, materialReference);
            writer.WriteByte(renderOrder3D);

            return new SceneComponentAssetRecord {
                ComponentTypeId = record.ComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                Payload = writeStream.ToArray()
            };
        }

        /// <summary>
        /// Rewrites one serialized camera component record into the runtime layer space expected by the Windows player.
        /// </summary>
        /// <param name="record">Serialized camera component record to rewrite.</param>
        /// <returns>Rewritten camera component record.</returns>
        SceneComponentAssetRecord RewriteCameraComponentRecord(SceneComponentAssetRecord record) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            using MemoryStream readStream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(readStream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != CameraComponentPayloadVersion) {
                throw new InvalidOperationException($"Unsupported camera component payload version '{version}'.");
            }

            byte cameraDrawOrder = reader.ReadByte();
            ushort layerMask = reader.ReadUInt16();
            float4 viewport = ReadFloat4(reader);
            CameraClearSettings clearSettings = ReadClearSettings(reader);

            using MemoryStream writeStream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(writeStream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(CameraComponentPayloadVersion);
            writer.WriteByte(cameraDrawOrder);
            writer.WriteUInt16(NormalizePackagedCameraLayerMask(layerMask));
            WriteFloat4(writer, viewport);
            WriteClearSettings(writer, clearSettings);

            return new SceneComponentAssetRecord {
                ComponentTypeId = record.ComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                Payload = writeStream.ToArray()
            };
        }

        /// <summary>
        /// Rewrites one serialized model reference into a packaged file-backed scene reference.
        /// </summary>
        /// <param name="reference">Serialized model reference to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Packaged file-backed model reference, or null when the serialized reference was null.</returns>
        SceneAssetReference RewriteModelReference(SceneAssetReference reference, string buildRootPath) {
            if (reference == null) {
                return null;
            }

            if (reference.SourceKind == SceneAssetReferenceSourceKind.Generated) {
                return RewriteGeneratedModelReference(reference, buildRootPath);
            }

            if (reference.SourceKind == SceneAssetReferenceSourceKind.FileSystem) {
                return RewriteFileSystemModelReference(reference, buildRootPath);
            }

            throw new InvalidOperationException($"Unsupported model reference source kind '{reference.SourceKind}'.");
        }

        /// <summary>
        /// Rewrites one serialized material reference into a packaged file-backed scene reference.
        /// </summary>
        /// <param name="reference">Serialized material reference to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Packaged file-backed material reference, or null when the serialized reference was null.</returns>
        SceneAssetReference RewriteMaterialReference(SceneAssetReference reference, string buildRootPath) {
            if (reference == null) {
                return null;
            }

            if (reference.SourceKind == SceneAssetReferenceSourceKind.Generated) {
                return RewriteGeneratedMaterialReference(reference, buildRootPath);
            }

            if (reference.SourceKind == SceneAssetReferenceSourceKind.FileSystem) {
                return RewriteFileSystemMaterialReference(reference, buildRootPath);
            }

            throw new InvalidOperationException($"Unsupported material reference source kind '{reference.SourceKind}'.");
        }

        /// <summary>
        /// Rewrites one engine-generated model reference into a packaged file-backed model asset.
        /// </summary>
        /// <param name="reference">Generated model reference to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Packaged file-backed scene reference for the generated model.</returns>
        SceneAssetReference RewriteGeneratedModelReference(SceneAssetReference reference, string buildRootPath) {
            if (!string.Equals(reference.ProviderId, EngineGeneratedProviderId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Unsupported generated model provider '{reference.ProviderId}'.");
            }

            if (string.Equals(reference.AssetId, CubeGeneratedAssetId, StringComparison.Ordinal)) {
                string relativePath = "generated/engine/models/cube.model.asset";
                WriteAsset(Path.Combine(buildRootPath, relativePath), ModelUtils.GenerateCubeMesh(float3.Zero, float3.One));
                return CreateFileSystemReference(relativePath);
            }

            if (string.Equals(reference.AssetId, PlaneGeneratedAssetId, StringComparison.Ordinal)) {
                string relativePath = "generated/engine/models/plane.model.asset";
                WriteAsset(Path.Combine(buildRootPath, relativePath), ModelUtils.GeneratePlaneMesh(float3.Zero, float3.One));
                return CreateFileSystemReference(relativePath);
            }

            throw new InvalidOperationException($"Unsupported generated model asset id '{reference.AssetId}'.");
        }

        /// <summary>
        /// Rewrites one file-backed source-model reference into a packaged processed `ModelAsset`.
        /// </summary>
        /// <param name="reference">File-backed model reference to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Packaged file-backed scene reference for the processed model asset.</returns>
        SceneAssetReference RewriteFileSystemModelReference(SceneAssetReference reference, string buildRootPath) {
            string sourcePath = ResolveProjectAssetPath(reference.RelativePath);
            ModelAsset modelAsset = FileSystemModelResolver.ResolveModelAsset(sourcePath);
            string relativePath = BuildImportedModelRelativePath(reference.RelativePath);
            WriteAsset(Path.Combine(buildRootPath, relativePath), modelAsset);
            return CreateFileSystemReference(relativePath);
        }

        /// <summary>
        /// Rewrites one engine-generated standard material reference into a packaged file-backed material asset.
        /// </summary>
        /// <param name="reference">Generated material reference to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Packaged file-backed scene reference for the generated material.</returns>
        SceneAssetReference RewriteGeneratedMaterialReference(SceneAssetReference reference, string buildRootPath) {
            if (!string.Equals(reference.ProviderId, EngineGeneratedProviderId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Unsupported generated material provider '{reference.ProviderId}'.");
            }
            if (!string.Equals(reference.AssetId, StandardGeneratedMaterialAssetId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Unsupported generated material asset id '{reference.AssetId}'.");
            }

            EnsureGeneratedStandardMaterialAssets(buildRootPath);
            return CreateFileSystemReference(StandardGeneratedMaterialRelativePath);
        }

        /// <summary>
        /// Rewrites one file-backed material reference by copying the serialized material asset and its required shader package.
        /// </summary>
        /// <param name="reference">File-backed material reference to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Packaged file-backed scene reference for the copied material asset.</returns>
        SceneAssetReference RewriteFileSystemMaterialReference(SceneAssetReference reference, string buildRootPath) {
            string fullPath = ResolveProjectAssetPath(reference.RelativePath);
            MaterialAsset materialAsset = ProjectContentManager.Load<MaterialAsset>(fullPath, EditorContentProcessorIds.MaterialAsset);
            RememberReferencedShaderAssetId(materialAsset.ShaderAssetId);

            string relativePath = NormalizeRelativePath(reference.RelativePath);
            CopyFile(fullPath, Path.Combine(buildRootPath, relativePath));
            return CreateFileSystemReference(relativePath);
        }

        /// <summary>
        /// Ensures the packaged generated standard material and its shader asset exist under the build root.
        /// </summary>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        void EnsureGeneratedStandardMaterialAssets(string buildRootPath) {
            ShaderAsset shaderAsset = EditorBuiltInShaderAssetLibrary.LoadShaderAsset(ShaderCompileTarget.DirectX11, StandardShaderFileName);
            WriteAsset(Path.Combine(buildRootPath, StandardGeneratedShaderRelativePath), shaderAsset);

            MaterialAsset materialAsset = new MaterialAsset {
                Id = "Engine.Materials.Standard.material",
                ShaderAssetId = shaderAsset.Id,
                VertexProgram = StandardVertexProgramName,
                PixelProgram = StandardPixelProgramName,
                Variant = StandardShaderVariantName
            };
            WriteAsset(Path.Combine(buildRootPath, StandardGeneratedMaterialRelativePath), materialAsset);
        }

        /// <summary>
        /// Tracks one referenced shader asset id if it has not already been recorded.
        /// </summary>
        /// <param name="shaderAssetId">Referenced shader asset identifier.</param>
        void RememberReferencedShaderAssetId(string shaderAssetId) {
            if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                throw new InvalidOperationException("Material assets used by packaged scenes must include a shader asset id.");
            }

            if (ReferencedShaderAssetIdsSet.Add(shaderAssetId)) {
                ReferencedShaderAssetIds.Add(shaderAssetId);
            }
        }

        /// <summary>
        /// Creates one packaged file-backed scene reference for the supplied relative path.
        /// </summary>
        /// <param name="relativePath">Packaged asset path relative to the build root.</param>
        /// <returns>File-backed packaged scene reference.</returns>
        SceneAssetReference CreateFileSystemReference(string relativePath) {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = NormalizeRelativePath(relativePath),
                ProviderId = string.Empty,
                AssetId = string.Empty
            };
        }

        /// <summary>
        /// Resolves one project-relative asset path beneath the source `assets` folder.
        /// </summary>
        /// <param name="relativePath">Project-relative asset path.</param>
        /// <returns>Absolute source asset path.</returns>
        string ResolveProjectAssetPath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            string normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(AssetsRootPath, normalizedRelativePath));
            string assetsRootPrefix = EnsureTrailingDirectorySeparator(AssetsRootPath);
            if (!fullPath.StartsWith(assetsRootPrefix, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("Project asset paths must stay inside the source assets folder.");
            }

            return fullPath;
        }

        /// <summary>
        /// Builds one packaged scene relative path for an authored scene id.
        /// </summary>
        /// <param name="sceneId">Project-relative scene id.</param>
        /// <returns>Packaged scene relative path beneath the `scenes` folder.</returns>
        string BuildPackagedSceneRelativePath(string sceneId) {
            string normalizedSceneId = NormalizeRelativePath(sceneId);
            return NormalizeRelativePath(Path.Combine("scenes", normalizedSceneId));
        }

        /// <summary>
        /// Builds one packaged processed-model relative path for an authored source-model reference.
        /// </summary>
        /// <param name="relativePath">Original project-relative source-model path.</param>
        /// <returns>Packaged processed-model relative path.</returns>
        string BuildImportedModelRelativePath(string relativePath) {
            string normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string changedExtensionPath = Path.ChangeExtension(normalizedRelativePath, ".model.asset");
            return NormalizeRelativePath(Path.Combine("generated", "imported", changedExtensionPath));
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
        /// Writes one optional scene asset reference to the current payload position.
        /// </summary>
        /// <param name="writer">Writer receiving the optional-reference payload.</param>
        /// <param name="reference">Optional scene asset reference to encode.</param>
        void WriteOptionalReference(EngineBinaryWriter writer, SceneAssetReference reference) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteByte(reference == null ? (byte)0 : (byte)1);
            if (reference == null) {
                return;
            }

            writer.WriteInt32((int)reference.SourceKind);
            writer.WriteString(reference.RelativePath);
            writer.WriteString(reference.ProviderId);
            writer.WriteString(reference.AssetId);
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
        /// Writes one <see cref="float4"/> value into the current payload.
        /// </summary>
        /// <param name="writer">Writer receiving the vector payload.</param>
        /// <param name="value">Vector value to encode.</param>
        void WriteFloat4(EngineBinaryWriter writer, float4 value) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteSingle(value.X);
            writer.WriteSingle(value.Y);
            writer.WriteSingle(value.Z);
            writer.WriteSingle(value.W);
        }

        /// <summary>
        /// Reads one camera clear-settings payload from the current reader position.
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

        /// <summary>
        /// Writes one camera clear-settings payload into the current writer position.
        /// </summary>
        /// <param name="writer">Writer receiving the clear-settings payload.</param>
        /// <param name="settings">Camera clear settings to encode.</param>
        void WriteClearSettings(EngineBinaryWriter writer, CameraClearSettings settings) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteByte(settings.ClearColorEnabled ? (byte)1 : (byte)0);
            WriteFloat4(writer, settings.ClearColor);
            writer.WriteByte(settings.ClearDepthEnabled ? (byte)1 : (byte)0);
            writer.WriteSingle(settings.ClearDepth);
            writer.WriteByte(settings.ClearStencilEnabled ? (byte)1 : (byte)0);
            writer.WriteByte(settings.ClearStencil);
        }

        /// <summary>
        /// Normalizes one packaged scene-camera layer mask into the runtime scene layer used by the current Windows player loader.
        /// </summary>
        /// <param name="layerMask">Serialized authored camera layer mask.</param>
        /// <returns>Runtime layer mask used by packaged Windows players.</returns>
        ushort NormalizePackagedCameraLayerMask(ushort layerMask) {
            return RuntimeSceneLayerMask;
        }

        /// <summary>
        /// Writes one serialized asset payload to disk.
        /// </summary>
        /// <param name="fullPath">Absolute output path.</param>
        /// <param name="asset">Serialized asset to write.</param>
        void WriteAsset(string fullPath, Asset asset) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Output path must be provided.", nameof(fullPath));
            }
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            string directoryPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Output directory could not be resolved.");
            }

            Directory.CreateDirectory(directoryPath);
            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, asset);
        }

        /// <summary>
        /// Copies one file into the packaged build root, creating parent folders when required.
        /// </summary>
        /// <param name="sourcePath">Absolute source file path.</param>
        /// <param name="targetPath">Absolute packaged output path.</param>
        void CopyFile(string sourcePath, string targetPath) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }
            if (string.IsNullOrWhiteSpace(targetPath)) {
                throw new ArgumentException("Target path must be provided.", nameof(targetPath));
            }

            string directoryPath = Path.GetDirectoryName(targetPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Copy target directory could not be resolved.");
            }

            Directory.CreateDirectory(directoryPath);
            File.Copy(sourcePath, targetPath, true);
        }

        /// <summary>
        /// Normalizes one relative path to use forward slashes for persisted scene references.
        /// </summary>
        /// <param name="relativePath">Relative path to normalize.</param>
        /// <returns>Normalized forward-slash relative path.</returns>
        string NormalizeRelativePath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            return relativePath.Replace('\\', '/');
        }

        /// <summary>
        /// Ensures one directory path ends with a trailing separator before prefix comparisons occur.
        /// </summary>
        /// <param name="path">Directory path that should end with a separator.</param>
        /// <returns>Directory path with a trailing separator.</returns>
        string EnsureTrailingDirectorySeparator(string path) {
            if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)) {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }
}
