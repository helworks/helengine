using helengine.files;

namespace helengine.video {
    /// <summary>
    /// Reads and writes <c>.hgraphic</c> files through the editor asset format, validating the definition on both sides so
    /// no tool persists or expands a template the compiler would reject. This is the C# authoring entry point: construct a
    /// <see cref="GraphicTemplateAsset"/> in code and call <see cref="Save"/>.
    /// </summary>
    public static class GraphicTemplateFile {
        /// <summary>
        /// Loads and validates one graphic template file.
        /// </summary>
        /// <param name="path">Path of the <c>.hgraphic</c> file.</param>
        /// <returns>Validated template definition.</returns>
        public static GraphicTemplateAsset Load(string path) {
            using FileStream stream = File.OpenRead(path);
            if (EditorAssetBinarySerializer.Deserialize(stream) is not GraphicTemplateAsset template) {
                throw new InvalidDataException($"'{path}' is not a graphic template asset.");
            }
            GraphicTemplateValidator.Validate(template);
            return template;
        }

        /// <summary>
        /// Validates a template definition and writes it as a <c>.hgraphic</c> file, replacing any existing file.
        /// </summary>
        /// <param name="path">Destination path; must end in <see cref="GraphicTemplateAsset.FileExtension"/>.</param>
        /// <param name="template">Template definition to persist.</param>
        public static void Save(string path, GraphicTemplateAsset template) {
            if (!string.Equals(Path.GetExtension(path), GraphicTemplateAsset.FileExtension, StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentException($"Graphic template files must use the {GraphicTemplateAsset.FileExtension} extension.", nameof(path));
            }
            GraphicTemplateValidator.Validate(template);
            using MemoryStream buffer = new MemoryStream();
            EditorAssetBinarySerializer.Serialize(buffer, template);
            File.WriteAllBytes(path, buffer.ToArray());
        }
    }
}
