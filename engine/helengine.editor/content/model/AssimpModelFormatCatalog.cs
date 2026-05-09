namespace helengine.editor {
    /// <summary>
    /// Centralizes the model file extensions reported by Assimp for use throughout the editor.
    /// </summary>
    public static class AssimpModelFormatCatalog {
        /// <summary>
        /// De-duplicated and normalized model extensions supported by the active Assimp runtime.
        /// </summary>
        static readonly string[] AllModelExtensionsInternal = BuildAllModelExtensions();

        /// <summary>
        /// Gets the normalized model extensions supported by Assimp.
        /// </summary>
        public static IReadOnlyList<string> AllModelExtensions => AllModelExtensionsInternal;

        /// <summary>
        /// Builds the normalized and sorted model extension catalog from Assimp.
        /// </summary>
        /// <returns>Ordered collection of supported model extensions.</returns>
        static string[] BuildAllModelExtensions() {
            string[] assimpExtensions = LoadAssimpExtensions();
            if (assimpExtensions == null) {
                throw new InvalidOperationException("Assimp returned no model extensions.");
            }

            HashSet<string> uniqueExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < assimpExtensions.Length; index++) {
                string normalizedExtension = NormalizeExtension(assimpExtensions[index]);
                if (!string.IsNullOrEmpty(normalizedExtension)) {
                    uniqueExtensions.Add(normalizedExtension);
                }
            }

            List<string> extensions = new List<string>(uniqueExtensions);
            extensions.Sort(StringComparer.OrdinalIgnoreCase);
            return extensions.ToArray();
        }

        /// <summary>
        /// Loads the Assimp model extension list from the runtime assembly that ships with the importer plugin.
        /// </summary>
        /// <returns>Extensions reported by Assimp.</returns>
        static string[] LoadAssimpExtensions() {
            string[] extensions = LoadExtensionsFromImporterDescriptions();
            if (extensions != null) {
                return extensions;
            }

            extensions = LoadExtensionsFromSupportedImportFormats();
            if (extensions != null) {
                return extensions;
            }

            extensions = LoadExtensionsFromExtensionList();
            if (extensions != null) {
                return extensions;
            }

            throw new InvalidOperationException("AssimpNetter is not available or does not expose import formats.");
        }

        /// <summary>
        /// Loads extensions from Assimp importer descriptions.
        /// </summary>
        /// <returns>Normalized extensions, or null when the runtime is unavailable.</returns>
        static string[] LoadExtensionsFromImporterDescriptions() {
            Type contextType = Type.GetType("Assimp.AssimpContext, AssimpNetter");
            if (contextType == null) {
                return null;
            }

            object context = Activator.CreateInstance(contextType);
            if (context == null) {
                return null;
            }

            System.Reflection.MethodInfo descriptionsMethod = contextType.GetMethod(
                "GetImporterDescriptions",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (descriptionsMethod == null) {
                return null;
            }

            object result = descriptionsMethod.Invoke(context, Array.Empty<object>());
            return ExtractExtensions(result);
        }

        /// <summary>
        /// Loads extensions from Assimp's supported-import-format catalog.
        /// </summary>
        /// <returns>Normalized extensions, or null when the runtime is unavailable.</returns>
        static string[] LoadExtensionsFromSupportedImportFormats() {
            Type contextType = Type.GetType("Assimp.AssimpContext, AssimpNetter");
            if (contextType == null) {
                return null;
            }

            object context = Activator.CreateInstance(contextType);
            if (context == null) {
                return null;
            }

            System.Reflection.MethodInfo formatsMethod = contextType.GetMethod(
                "GetSupportedImportFormats",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (formatsMethod == null) {
                return null;
            }

            object result = formatsMethod.Invoke(context, Array.Empty<object>());
            return ExtractExtensions(result);
        }

        /// <summary>
        /// Loads extensions from the unmanaged importer catalog helper.
        /// </summary>
        /// <returns>Normalized extensions, or null when the runtime is unavailable.</returns>
        static string[] LoadExtensionsFromExtensionList() {
            Type assimpLibraryType = Type.GetType("Assimp.Unmanaged.AssimpLibrary, AssimpNetter");
            if (assimpLibraryType == null) {
                return null;
            }

            System.Reflection.MethodInfo extensionListMethod = assimpLibraryType.GetMethod(
                "GetExtensionList",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (extensionListMethod == null) {
                return null;
            }

            object result = extensionListMethod.Invoke(null, Array.Empty<object>());
            return ExtractExtensions(result);
        }

        /// <summary>
        /// Extracts extension strings from whichever Assimp API shape the runtime exposes.
        /// </summary>
        /// <param name="result">Reflected method result.</param>
        /// <returns>Array of extension strings, or null when the result shape is unsupported.</returns>
        static string[] ExtractExtensions(object result) {
            if (result == null) {
                return null;
            }

            string[] stringArray = result as string[];
            if (stringArray != null) {
                return stringArray;
            }

            System.Collections.Generic.List<string> extensions = new System.Collections.Generic.List<string>();
            System.Collections.IEnumerable enumerable = result as System.Collections.IEnumerable;
            if (enumerable == null) {
                return null;
            }

            foreach (object item in enumerable) {
                if (item == null) {
                    continue;
                }

                if (item is string itemString) {
                    AddExtensionStrings(extensions, itemString);
                    continue;
                }

                System.Reflection.PropertyInfo fileExtensionsProperty = item.GetType().GetProperty(
                    "FileExtensions",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (fileExtensionsProperty == null) {
                    fileExtensionsProperty = item.GetType().GetProperty(
                        "Extension",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                }

                if (fileExtensionsProperty == null) {
                    continue;
                }

                object propertyValue = fileExtensionsProperty.GetValue(item);
                AddExtensionValue(extensions, propertyValue);
            }

            if (extensions.Count == 0) {
                return null;
            }

            return extensions.ToArray();
        }

        /// <summary>
        /// Adds one reflected property value to the extension list.
        /// </summary>
        /// <param name="extensions">Target extension list.</param>
        /// <param name="value">Reflected property value.</param>
        static void AddExtensionValue(System.Collections.Generic.List<string> extensions, object value) {
            if (extensions == null) {
                throw new ArgumentNullException(nameof(extensions));
            }

            if (value == null) {
                return;
            }

            if (value is string valueString) {
                AddExtensionStrings(extensions, valueString);
                return;
            }

            System.Collections.IEnumerable enumerable = value as System.Collections.IEnumerable;
            if (enumerable == null) {
                return;
            }

            foreach (object item in enumerable) {
                if (item is string itemString) {
                    AddExtensionStrings(extensions, itemString);
                }
            }
        }

        /// <summary>
        /// Splits a candidate extension string and appends the normalized parts.
        /// </summary>
        /// <param name="extensions">Target extension list.</param>
        /// <param name="value">Candidate extension string.</param>
        static void AddExtensionStrings(System.Collections.Generic.List<string> extensions, string value) {
            if (extensions == null) {
                throw new ArgumentNullException(nameof(extensions));
            }

            if (string.IsNullOrWhiteSpace(value)) {
                return;
            }

            string[] candidates = value.Split(new[] { ';', ',', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (candidates.Length == 0) {
                candidates = new[] { value };
            }

            for (int index = 0; index < candidates.Length; index++) {
                string normalizedExtension = NormalizeExtension(candidates[index]);
                if (!string.IsNullOrEmpty(normalizedExtension)) {
                    extensions.Add(normalizedExtension);
                }
            }
        }

        /// <summary>
        /// Normalizes one extension so it uses a leading dot and lower-case characters.
        /// </summary>
        /// <param name="extension">Extension reported by Assimp.</param>
        /// <returns>Normalized extension, or an empty string when the input is blank.</returns>
        static string NormalizeExtension(string extension) {
            if (string.IsNullOrWhiteSpace(extension)) {
                return string.Empty;
            }

            string trimmedExtension = extension.Trim();
            if (!trimmedExtension.StartsWith(".")) {
                trimmedExtension = "." + trimmedExtension;
            }

            return trimmedExtension.ToLowerInvariant();
        }
    }
}
