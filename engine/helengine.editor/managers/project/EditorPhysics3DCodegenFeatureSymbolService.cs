namespace helengine.editor {
    /// <summary>
    /// Resolves the unioned 3D physics scene feature mask and preprocessor symbols required by one build's selected scenes.
    /// </summary>
    public sealed class EditorPhysics3DCodegenFeatureSymbolService {
        /// <summary>
        /// Stable serialized component id for 3D rigid bodies.
        /// </summary>
        const string RigidBody3DComponentTypeId = "helengine.RigidBody3DComponent";

        /// <summary>
        /// Stable serialized component id for 3D box colliders.
        /// </summary>
        const string BoxCollider3DComponentTypeId = "helengine.BoxCollider3DComponent";

        /// <summary>
        /// Stable serialized component id for 3D sphere colliders.
        /// </summary>
        const string SphereCollider3DComponentTypeId = "helengine.SphereCollider3DComponent";

        /// <summary>
        /// Stable serialized component id for 3D capsule colliders.
        /// </summary>
        const string CapsuleCollider3DComponentTypeId = "helengine.CapsuleCollider3DComponent";

        /// <summary>
        /// Stable serialized component id for 3D static mesh colliders.
        /// </summary>
        const string StaticMeshCollider3DComponentTypeId = "helengine.StaticMeshCollider3DComponent";

        /// <summary>
        /// Stable serialized component id for 3D character controllers.
        /// </summary>
        const string CharacterController3DComponentTypeId = "helengine.CharacterController3DComponent";

        /// <summary>
        /// Stable serialized component id for 3D kinematic motion paths.
        /// </summary>
        const string KinematicMotion3DComponentTypeId = "helengine.KinematicMotion3DComponent";

        /// <summary>
        /// Absolute project root that owns the source `assets` folder.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Absolute source assets root used to resolve project-relative scene ids.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Reflected schema builder used to convert tagged editor physics payloads into runtime payloads for feature analysis.
        /// </summary>
        readonly ScriptComponentReflectionSchemaBuilder ScriptComponentSchemaBuilder;

        /// <summary>
        /// Automatic component descriptor used to read tagged editor physics payloads without creating scene entities.
        /// </summary>
        readonly AutomaticScriptComponentPersistenceDescriptor AutomaticScriptComponentDescriptor;

        /// <summary>
        /// Project scene catalog used to resolve stable scene ids into source asset paths.
        /// </summary>
        readonly EditorProjectSceneCatalogService SceneCatalogService;

        /// <summary>
        /// Initializes one 3D physics codegen feature-symbol resolver for the supplied project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        public EditorPhysics3DCodegenFeatureSymbolService(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
            ScriptComponentSchemaBuilder = new ScriptComponentReflectionSchemaBuilder();
            AutomaticScriptComponentDescriptor = new AutomaticScriptComponentPersistenceDescriptor(ScriptComponentSchemaBuilder);
            SceneCatalogService = new EditorProjectSceneCatalogService(ProjectRootPath);
        }

        /// <summary>
        /// Resolves the unioned 3D physics scene feature mask required by the supplied stable scene ids.
        /// </summary>
        /// <param name="sceneIds">Stable scene ids selected for the build.</param>
        /// <returns>Unioned 3D physics scene feature mask.</returns>
        public PhysicsSceneFeatureFlags3D ResolveFeatureFlags(IReadOnlyList<string> sceneIds) {
            if (sceneIds == null) {
                throw new ArgumentNullException(nameof(sceneIds));
            }

            PhysicsSceneFeatureFlags3D featureFlags = PhysicsSceneFeatureFlags3D.None;
            for (int index = 0; index < sceneIds.Count; index++) {
                SceneAsset sceneAsset = LoadSceneAsset(sceneIds[index]);
                featureFlags |= AnalyzeSourceScene(sceneAsset);
            }

            return featureFlags;
        }

        /// <summary>
        /// Resolves the ordered 3D physics preprocessor symbols required by the supplied stable scene ids.
        /// </summary>
        /// <param name="sceneIds">Stable scene ids selected for the build.</param>
        /// <returns>Ordered preprocessor symbols for generated-core stripping.</returns>
        public IReadOnlyList<string> ResolveSymbols(IReadOnlyList<string> sceneIds) {
            PhysicsSceneFeatureFlags3D featureFlags = ResolveFeatureFlags(sceneIds);
            return PhysicsSceneFeatureSymbolCatalog3D.BuildSymbols(featureFlags);
        }

        /// <summary>
        /// Loads one authored scene asset from the project source assets folder.
        /// </summary>
        /// <param name="sceneId">Stable scene id to load.</param>
        /// <returns>Loaded scene asset.</returns>
        SceneAsset LoadSceneAsset(string sceneId) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            string relativeScenePath = SceneCatalogService.ResolveScenePath(sceneId);
            string fullScenePath = ResolveProjectAssetPath(relativeScenePath);
            string previousAssetPath = EngineBinaryReadContext.CurrentAssetPath;
            try {
                EngineBinaryReadContext.CurrentAssetPath = fullScenePath;
                using FileStream stream = File.OpenRead(fullScenePath);
                Asset asset = AssetSerializer.Deserialize(stream);
                if (asset is not SceneAsset sceneAsset) {
                    throw new InvalidOperationException($"Scene '{sceneId}' did not deserialize into a SceneAsset.");
                }

                return sceneAsset;
            } catch (Exception ex) when (ex is not InvalidOperationException || !ex.Message.Contains(sceneId, StringComparison.Ordinal)) {
                throw new InvalidOperationException(
                    $"Scene '{sceneId}' at '{relativeScenePath}' could not be read for physics feature discovery.",
                    ex);
            } finally {
                EngineBinaryReadContext.CurrentAssetPath = previousAssetPath;
            }
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

        /// <summary>
        /// Analyzes one source scene by materializing physics components through the editor persistence registry before feature discovery.
        /// </summary>
        /// <param name="sceneAsset">Source scene asset loaded from the project.</param>
        /// <returns>Required 3D physics feature flags for the scene.</returns>
        PhysicsSceneFeatureFlags3D AnalyzeSourceScene(SceneAsset sceneAsset) {
            if (sceneAsset == null) {
                throw new ArgumentNullException(nameof(sceneAsset));
            }

            SceneEntityAsset[] rootEntityAssets = sceneAsset.RootEntities ?? Array.Empty<SceneEntityAsset>();
            for (int index = 0; index < rootEntityAssets.Length; index++) {
                RewriteTaggedPhysicsPayloads(rootEntityAssets[index]);
            }

            return PhysicsSceneFeatureAnalyzer3D.Analyze(sceneAsset);
        }

        /// <summary>
        /// Rewrites tagged editor physics payloads on one source entity subtree into the strict runtime payloads consumed by the feature analyzer.
        /// </summary>
        /// <param name="entityAsset">Serialized source entity whose physics component records should be normalized.</param>
        void RewriteTaggedPhysicsPayloads(SceneEntityAsset entityAsset) {
            if (entityAsset == null) {
                throw new ArgumentNullException(nameof(entityAsset));
            }

            SceneComponentAssetRecord[] componentRecords = entityAsset.Components ?? Array.Empty<SceneComponentAssetRecord>();
            for (int index = 0; index < componentRecords.Length; index++) {
                SceneComponentAssetRecord componentRecord = componentRecords[index];
                if (componentRecord != null && IsPhysicsComponentTypeId(componentRecord.ComponentTypeId) && IsTaggedPhysicsPayload(componentRecord)) {
                    componentRecords[index] = BuildRuntimePhysicsComponentRecord(componentRecord);
                }
            }

            SceneEntityAsset[] childEntityAssets = entityAsset.Children ?? Array.Empty<SceneEntityAsset>();
            for (int index = 0; index < childEntityAssets.Length; index++) {
                RewriteTaggedPhysicsPayloads(childEntityAssets[index]);
            }
        }

        /// <summary>
        /// Builds one runtime-ready automatic component record from a tagged editor physics component record.
        /// </summary>
        /// <param name="record">Tagged editor component record to convert.</param>
        /// <returns>Runtime-ready component record with an ordinal automatic payload.</returns>
        SceneComponentAssetRecord BuildRuntimePhysicsComponentRecord(SceneComponentAssetRecord record) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            Component component = AutomaticScriptComponentDescriptor.DeserializeComponent(record, new EntitySaveComponent(), null);
            ScriptComponentReflectionSchema schema = ScriptComponentSchemaBuilder.Build(component.GetType());
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion);
            writer.WriteInt32(schema.Members.Count);
            for (int index = 0; index < schema.Members.Count; index++) {
                ScriptComponentReflectionMember member = schema.Members[index];
                AutomaticScriptComponentPersistenceDescriptor.WriteSupportedMemberValue(writer, member, component, null);
            }

            return new SceneComponentAssetRecord {
                ComponentKey = record.ComponentKey,
                ComponentTypeId = record.ComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Returns whether one physics component record uses the named-field editor payload format rather than a packaged runtime payload.
        /// </summary>
        /// <param name="record">Physics component record to inspect.</param>
        /// <returns>True when the payload is a tagged editor payload; otherwise false.</returns>
        static bool IsTaggedPhysicsPayload(SceneComponentAssetRecord record) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            try {
                EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(record.Payload ?? Array.Empty<byte>());
                return TaggedPhysicsPayloadHasExpectedField(record.ComponentTypeId, reader);
            } catch (Exception ex) when (ex is EndOfStreamException || ex is InvalidOperationException) {
                return false;
            }
        }

        /// <summary>
        /// Returns whether a parsed tagged payload exposes a field that belongs to the supplied physics component type.
        /// </summary>
        /// <param name="componentTypeId">Serialized physics component type id.</param>
        /// <param name="reader">Parsed tagged payload reader.</param>
        /// <returns>True when the tagged payload matches the expected physics component schema.</returns>
        static bool TaggedPhysicsPayloadHasExpectedField(string componentTypeId, EditorTaggedSceneComponentFieldReader reader) {
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                throw new ArgumentException("Component type id must be provided.", nameof(componentTypeId));
            }
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            if (string.Equals(componentTypeId, RigidBody3DComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                return TaggedPayloadContainsField(reader, "BodyKind");
            }
            if (string.Equals(componentTypeId, BoxCollider3DComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, SphereCollider3DComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, CapsuleCollider3DComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, StaticMeshCollider3DComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                return TaggedPayloadContainsField(reader, "IsTrigger");
            }
            if (string.Equals(componentTypeId, CharacterController3DComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                return TaggedPayloadContainsField(reader, "DesiredMoveDirection");
            }
            if (string.Equals(componentTypeId, KinematicMotion3DComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                return TaggedPayloadContainsField(reader, "StartLocalPosition");
            }

            return false;
        }

        /// <summary>
        /// Returns whether a tagged payload contains the supplied field name and disposes the temporary field reader immediately.
        /// </summary>
        /// <param name="reader">Tagged payload reader being queried.</param>
        /// <param name="fieldName">Stable tagged field name to find.</param>
        /// <returns>True when the tagged field exists; otherwise false.</returns>
        static bool TaggedPayloadContainsField(EditorTaggedSceneComponentFieldReader reader, string fieldName) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }
            if (string.IsNullOrWhiteSpace(fieldName)) {
                throw new ArgumentException("Field name must be provided.", nameof(fieldName));
            }

            bool hasField = reader.TryGetFieldReader(fieldName, out EngineBinaryReader fieldReader);
            if (fieldReader != null) {
                fieldReader.Dispose();
            }

            return hasField;
        }

        /// <summary>
        /// Returns whether one serialized component id belongs to the 3D physics feature set.
        /// </summary>
        /// <param name="componentTypeId">Serialized component id to inspect.</param>
        /// <returns>True when the component participates in physics feature detection; otherwise false.</returns>
        static bool IsPhysicsComponentTypeId(string componentTypeId) {
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                return false;
            }

            return string.Equals(componentTypeId, RigidBody3DComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, BoxCollider3DComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, SphereCollider3DComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, CapsuleCollider3DComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, StaticMeshCollider3DComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, CharacterController3DComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, KinematicMotion3DComponentTypeId, StringComparison.OrdinalIgnoreCase);
        }
    }
}
