using Assimp;

namespace helengine.editor.assimp {
    /// <summary>
    /// Imports model files through Assimp and converts them into the engine's dual-width indexed model asset format.
    /// </summary>
    public class HelengineAssimpImporter : IModelImporter {
        /// <summary>
        /// Assimp post-processing flags used to normalize imported geometry for the engine.
        /// </summary>
        const PostProcessSteps ImportPostProcessSteps =
            PostProcessSteps.Triangulate |
            PostProcessSteps.JoinIdenticalVertices |
            PostProcessSteps.GenerateSmoothNormals |
            PostProcessSteps.GenerateUVCoords |
            PostProcessSteps.ImproveCacheLocality;

        /// <summary>
        /// Converts imported scenes into engine model assets.
        /// </summary>
        readonly AssimpSceneModelAssetConverter SceneConverter = new AssimpSceneModelAssetConverter();

        /// <summary>
        /// Imports a model asset from the given data stream.
        /// </summary>
        /// <param name="stream">Stream containing model data.</param>
        /// <returns>Loaded model asset.</returns>
        public ModelAsset ImportModel(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            string formatHint = ResolveFormatHint(stream);
            using AssimpContext importer = new AssimpContext();
            Scene scene = importer.ImportFileFromStream(stream, ImportPostProcessSteps, formatHint);
            if (scene == null) {
                throw new InvalidOperationException("Assimp did not return a scene for the model stream.");
            }

            return SceneConverter.Convert(scene);
        }

        /// <summary>
        /// Resolves the Assimp stream format hint from a file stream name when available.
        /// </summary>
        /// <param name="stream">Source stream.</param>
        /// <returns>Lowercase format hint without a leading dot, or an empty string when unavailable.</returns>
        string ResolveFormatHint(Stream stream) {
            if (stream is FileStream fileStream) {
                string extension = Path.GetExtension(fileStream.Name);
                if (!string.IsNullOrWhiteSpace(extension)) {
                    return extension.TrimStart('.').ToLowerInvariant();
                }
            }

            return string.Empty;
        }
    }
}
