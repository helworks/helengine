using helengine.files;

namespace helengine.vfx {
    /// <summary>
    /// Reads and writes <c>.heffect</c> files through the editor asset format, validating the definition on both sides so
    /// no tool persists or executes an effect the runtime would reject. This is the C# builder entry point: construct an
    /// <see cref="EffectAsset"/> in code and call <see cref="Save"/>.
    /// </summary>
    public static class VfxEffectFile {
        /// <summary>
        /// Loads and validates one effect file.
        /// </summary>
        /// <param name="path">Path of the <c>.heffect</c> file.</param>
        /// <returns>Validated effect definition.</returns>
        public static EffectAsset Load(string path) {
            using FileStream stream = File.OpenRead(path);
            if (EditorAssetBinarySerializer.Deserialize(stream) is not EffectAsset effect) {
                throw new InvalidDataException($"'{path}' is not an effect asset.");
            }
            VfxEffectValidator.Validate(effect);
            return effect;
        }

        /// <summary>
        /// Validates an effect definition and writes it as a <c>.heffect</c> file, replacing any existing file.
        /// </summary>
        /// <param name="path">Destination path; must end in <see cref="EffectAsset.FileExtension"/>.</param>
        /// <param name="effect">Effect definition to persist.</param>
        public static void Save(string path, EffectAsset effect) {
            if (!string.Equals(Path.GetExtension(path), EffectAsset.FileExtension, StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentException($"Effect files must use the {EffectAsset.FileExtension} extension.", nameof(path));
            }
            VfxEffectValidator.Validate(effect);
            using MemoryStream buffer = new MemoryStream();
            EditorAssetBinarySerializer.Serialize(buffer, effect);
            File.WriteAllBytes(path, buffer.ToArray());
        }
    }
}
