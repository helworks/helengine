using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Migrates legacy string-valued material asset fields to typed stable references.
    /// </summary>
    public sealed class MaterialAssetReferenceMigrationService {
        readonly string ProjectRootPath;
        readonly EditorAssetReferenceResolver Resolver;

        /// <summary>Initializes migration for one project root.</summary>
        /// <param name="projectRootPath">Project root owning authored assets.</param>
        public MaterialAssetReferenceMigrationService(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            ProjectRootPath = Path.GetFullPath(projectRootPath);
            Resolver = new EditorAssetReferenceResolver(ProjectRootPath);
        }

        /// <summary>Migrates fields declared as asset references.</summary>
        /// <param name="settings">Material settings to update.</param>
        /// <param name="fields">Builder field declarations.</param>
        /// <returns>Number of migrated fields.</returns>
        public int Migrate(MaterialAssetProcessorSettings settings, IReadOnlyList<PlatformMaterialFieldDefinition> fields) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }
            if (fields == null) {
                throw new ArgumentNullException(nameof(fields));
            }
            int migrated = 0;
            for (int index = 0; index < fields.Count; index++) {
                PlatformMaterialFieldDefinition field = fields[index];
                if (field.FieldKind != PlatformMaterialFieldKind.AssetReference || !settings.FieldValues.TryGetValue(field.FieldId, out string value) || string.IsNullOrWhiteSpace(value)) {
                    continue;
                }
                string candidate = Path.IsPathRooted(value) ? value : Path.Combine(ProjectRootPath, "assets", value.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(candidate)) {
                    continue;
                }
                AssetEntryKind kind = new EditorAssetPathClassifier().Classify(candidate);
                settings.AssetReferenceValues[field.FieldId] = Resolver.CreateFileReference(candidate, kind);
                settings.FieldValues.Remove(field.FieldId);
                migrated++;
            }
            return migrated;
        }
    }
}
