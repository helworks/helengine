using Assimp;

namespace helengine.editor.assimp {
    /// <summary>
    /// Produces stable, unique material names for imported Assimp scenes.
    /// </summary>
    public static class AssimpMaterialNameCatalog {
        /// <summary>
        /// Resolves one unique material name for each material in an imported scene.
        /// </summary>
        /// <param name="scene">Imported scene whose materials should be named.</param>
        /// <returns>Array of unique material names keyed by material index.</returns>
        public static string[] ResolveMaterialNames(Scene scene) {
            if (scene == null) {
                throw new ArgumentNullException(nameof(scene));
            }

            string[] materialNames = new string[scene.MaterialCount];
            HashSet<string> usedNames = new HashSet<string>(StringComparer.Ordinal);
            for (int materialIndex = 0; materialIndex < scene.MaterialCount; materialIndex++) {
                Material material = scene.Materials[materialIndex];
                if (material == null) {
                    throw new InvalidOperationException("Imported scene contains a null material.");
                }

                materialNames[materialIndex] = ResolveMaterialName(material, materialIndex, usedNames);
            }

            return materialNames;
        }

        /// <summary>
        /// Resolves one stable unique material name from an Assimp material definition.
        /// </summary>
        /// <param name="material">Imported material definition.</param>
        /// <param name="materialIndex">Zero-based material index used for deterministic fallback naming.</param>
        /// <param name="usedNames">Set of names already assigned within the scene.</param>
        /// <returns>Unique material name for the imported scene.</returns>
        static string ResolveMaterialName(Material material, int materialIndex, HashSet<string> usedNames) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            } else if (materialIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(materialIndex), "Material index must be non-negative.");
            } else if (usedNames == null) {
                throw new ArgumentNullException(nameof(usedNames));
            }

            string baseName;
            if (!string.IsNullOrWhiteSpace(material.Name)) {
                baseName = material.Name;
            } else {
                baseName = string.Concat("Material", materialIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            string uniqueName = baseName;
            if (usedNames.Contains(uniqueName)) {
                uniqueName = string.Concat(
                    baseName,
                    "_",
                    materialIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
                int uniqueSuffix = 2;
                while (usedNames.Contains(uniqueName)) {
                    uniqueName = string.Concat(
                        baseName,
                        "_",
                        materialIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        "_",
                        uniqueSuffix.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    uniqueSuffix++;
                }
            }

            usedNames.Add(uniqueName);
            return uniqueName;
        }
    }
}
