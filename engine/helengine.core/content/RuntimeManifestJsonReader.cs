namespace helengine {
    /// <summary>
    /// Reads the small runtime manifest JSON shapes emitted by the editor build graph without depending on serializer codegen.
    /// </summary>
    internal static class RuntimeManifestJsonReader {
        /// <summary>
        /// Parses the runtime startup manifest shape and returns the corresponding runtime object.
        /// </summary>
        /// <param name="json">Runtime startup manifest JSON text.</param>
        /// <returns>The parsed runtime startup manifest.</returns>
        public static RuntimeStartupManifest ReadRuntimeStartupManifest(string json) {
            if (string.IsNullOrWhiteSpace(json)) {
                throw new ArgumentException("Runtime startup manifest JSON is required.", nameof(json));
            }

            string startupSceneId = ReadRequiredStringProperty(json, "StartupSceneId");
            string storageProfileJson = ReadRequiredObjectProperty(json, "StorageProfileId");
            string storageProfileValue = ReadRequiredStringProperty(storageProfileJson, "Value");

            return new RuntimeStartupManifest(startupSceneId, new RuntimeStorageProfileId(storageProfileValue));
        }

        /// <summary>
        /// Parses the runtime code-module manifest shape and returns the corresponding runtime object.
        /// </summary>
        /// <param name="json">Runtime code-module manifest JSON text.</param>
        /// <returns>The parsed runtime code-module manifest.</returns>
        public static RuntimeCodeModuleManifest ReadRuntimeCodeModuleManifest(string json) {
            if (string.IsNullOrWhiteSpace(json)) {
                throw new ArgumentException("Runtime code-module manifest JSON is required.", nameof(json));
            }

            RuntimeCodeModuleManifestEntry[] entries = ReadRuntimeCodeModuleEntries(json);
            return new RuntimeCodeModuleManifest(entries);
        }

        /// <summary>
        /// Parses the runtime scene catalog shape and returns the corresponding runtime object.
        /// </summary>
        /// <param name="json">Runtime scene catalog JSON text.</param>
        /// <returns>The parsed runtime scene catalog.</returns>
        public static RuntimeSceneCatalog ReadRuntimeSceneCatalog(string json) {
            if (string.IsNullOrWhiteSpace(json)) {
                throw new ArgumentException("Runtime scene catalog JSON is required.", nameof(json));
            }

            string entriesJson = ReadRequiredArrayProperty(json, "Entries");
            List<RuntimeSceneCatalogEntry> entries = new List<RuntimeSceneCatalogEntry>();
            int elementStart = 0;
            int elementLength = 0;
            int cursor = 1;
            while (TryReadNextArrayElement(entriesJson, ref cursor, out elementStart, out elementLength)) {
                string entryJson = entriesJson.Substring(elementStart, elementLength);
                string sceneId = ReadRequiredStringProperty(entryJson, "SceneId");
                string cookedRelativePath = ReadRequiredStringProperty(entryJson, "CookedRelativePath");
                entries.Add(new RuntimeSceneCatalogEntry(sceneId, cookedRelativePath));
            }

            return new RuntimeSceneCatalog(entries.ToArray());
        }

        /// <summary>
        /// Reads the runtime code-module entry array from one manifest JSON document.
        /// </summary>
        /// <param name="json">Runtime code-module manifest JSON text.</param>
        /// <returns>The parsed runtime code-module entries.</returns>
        static RuntimeCodeModuleManifestEntry[] ReadRuntimeCodeModuleEntries(string json) {
            string entriesJson = ReadRequiredArrayProperty(json, "Entries");
            List<RuntimeCodeModuleManifestEntry> entries = new List<RuntimeCodeModuleManifestEntry>();
            int elementStart = 0;
            int elementLength = 0;
            int cursor = 1;
            while (TryReadNextArrayElement(entriesJson, ref cursor, out elementStart, out elementLength)) {
                string entryJson = entriesJson.Substring(elementStart, elementLength);
                entries.Add(ReadRuntimeCodeModuleManifestEntry(entryJson));
            }

            return entries.ToArray();
        }

        /// <summary>
        /// Reads one runtime code-module entry object from its JSON representation.
        /// </summary>
        /// <param name="json">Runtime code-module entry JSON text.</param>
        /// <returns>The parsed runtime code-module entry.</returns>
        static RuntimeCodeModuleManifestEntry ReadRuntimeCodeModuleManifestEntry(string json) {
            string moduleId = ReadRequiredStringProperty(json, "ModuleId");
            string runtimeSpecializationId = ReadRequiredStringProperty(json, "RuntimeSpecializationId");
            RuntimeCodeModuleLoadState loadState = (RuntimeCodeModuleLoadState)ReadRequiredIntegerProperty(json, "LoadState");
            string[] dependencyModuleIds = ReadRequiredStringArrayProperty(json, "DependencyModuleIds");

            return new RuntimeCodeModuleManifestEntry(
                moduleId,
                runtimeSpecializationId,
                loadState,
                dependencyModuleIds);
        }

        /// <summary>
        /// Reads one required string property from a JSON object.
        /// </summary>
        /// <param name="json">JSON text containing the property.</param>
        /// <param name="propertyName">Property name to locate.</param>
        /// <returns>The decoded string value.</returns>
        static string ReadRequiredStringProperty(string json, string propertyName) {
            int valueStart = FindRequiredPropertyValueStart(json, propertyName);
            int valueLength = 0;
            string value = ReadJsonStringValue(json, valueStart, out valueLength);
            return value;
        }

        /// <summary>
        /// Reads one required integer property from a JSON object.
        /// </summary>
        /// <param name="json">JSON text containing the property.</param>
        /// <param name="propertyName">Property name to locate.</param>
        /// <returns>The decoded integer value.</returns>
        static int ReadRequiredIntegerProperty(string json, string propertyName) {
            int valueStart = FindRequiredPropertyValueStart(json, propertyName);
            int valueLength = 0;
            string valueText = ReadJsonPrimitiveValue(json, valueStart, out valueLength);
            int value = 0;
            if (!int.TryParse(valueText, out value)) {
                throw new InvalidOperationException($"Property '{propertyName}' did not contain a valid integer value.");
            }

            return value;
        }

        /// <summary>
        /// Reads one required string-array property from a JSON object.
        /// </summary>
        /// <param name="json">JSON text containing the property.</param>
        /// <param name="propertyName">Property name to locate.</param>
        /// <returns>The decoded string array.</returns>
        static string[] ReadRequiredStringArrayProperty(string json, string propertyName) {
            string arrayJson = ReadRequiredArrayProperty(json, propertyName);
            List<string> values = new List<string>();
            int elementStart = 0;
            int elementLength = 0;
            int cursor = 1;
            while (TryReadNextArrayElement(arrayJson, ref cursor, out elementStart, out elementLength)) {
                int consumedLength = 0;
                string value = ReadJsonStringValue(arrayJson, elementStart, out consumedLength);
                if (consumedLength != elementLength) {
                    throw new InvalidOperationException($"Property '{propertyName}' contained an invalid string array value.");
                }

                values.Add(value);
            }

            return values.ToArray();
        }

        /// <summary>
        /// Reads one required object property from a JSON object.
        /// </summary>
        /// <param name="json">JSON text containing the property.</param>
        /// <param name="propertyName">Property name to locate.</param>
        /// <returns>The object JSON fragment including its braces.</returns>
        static string ReadRequiredObjectProperty(string json, string propertyName) {
            int valueStart = FindRequiredPropertyValueStart(json, propertyName);
            int valueLength = ReadJsonValueLength(json, valueStart);
            if (valueLength <= 0 || json[valueStart] != '{') {
                throw new InvalidOperationException($"Property '{propertyName}' did not contain a JSON object.");
            }

            return json.Substring(valueStart, valueLength);
        }

        /// <summary>
        /// Reads one required array property from a JSON object.
        /// </summary>
        /// <param name="json">JSON text containing the property.</param>
        /// <param name="propertyName">Property name to locate.</param>
        /// <returns>The array JSON fragment including its brackets.</returns>
        static string ReadRequiredArrayProperty(string json, string propertyName) {
            int valueStart = FindRequiredPropertyValueStart(json, propertyName);
            int valueLength = ReadJsonValueLength(json, valueStart);
            if (valueLength <= 0 || json[valueStart] != '[') {
                throw new InvalidOperationException($"Property '{propertyName}' did not contain a JSON array.");
            }

            return json.Substring(valueStart, valueLength);
        }

        /// <summary>
        /// Finds the start index of one required property value inside a JSON object.
        /// </summary>
        /// <param name="json">JSON object text.</param>
        /// <param name="propertyName">Property name to locate.</param>
        /// <returns>The start index of the property value.</returns>
        static int FindRequiredPropertyValueStart(string json, string propertyName) {
            if (string.IsNullOrWhiteSpace(propertyName)) {
                throw new ArgumentException("Property name is required.", nameof(propertyName));
            }

            int propertyNameIndex = FindPropertyNameIndex(json, propertyName);
            if (propertyNameIndex < 0) {
                throw new InvalidOperationException($"Property '{propertyName}' was not found in the JSON object.");
            }

            int cursor = propertyNameIndex + propertyName.Length + 2;
            cursor = SkipWhitespace(json, cursor);
            if (cursor >= json.Length || json[cursor] != ':') {
                throw new InvalidOperationException($"Property '{propertyName}' was not followed by a JSON value.");
            }

            cursor = SkipWhitespace(json, cursor + 1);
            if (cursor >= json.Length) {
                throw new InvalidOperationException($"Property '{propertyName}' was not followed by a JSON value.");
            }

            return cursor;
        }

        /// <summary>
        /// Locates one quoted property name inside a JSON object without relying on higher-level string helpers.
        /// </summary>
        /// <param name="json">JSON text to scan.</param>
        /// <param name="propertyName">Property name to locate.</param>
        /// <returns>The start index of the quoted property name, or -1 when it is not present.</returns>
        static int FindPropertyNameIndex(string json, string propertyName) {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(propertyName)) {
                return -1;
            }

            int searchLength = propertyName.Length + 2;
            if (json.Length < searchLength) {
                return -1;
            }

            for (int index = 0; index <= json.Length - searchLength; index++) {
                if (json[index] != '"') {
                    continue;
                }

                bool matches = true;
                for (int offset = 0; offset < propertyName.Length; offset++) {
                    if (json[index + offset + 1] != propertyName[offset]) {
                        matches = false;
                        break;
                    }
                }

                if (matches && json[index + propertyName.Length + 1] == '"') {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// Reads one JSON value as a primitive string slice.
        /// </summary>
        /// <param name="json">JSON text containing the value.</param>
        /// <param name="valueStart">Index at which the value begins.</param>
        /// <param name="valueLength">Length of the value slice.</param>
        /// <returns>The trimmed primitive token text.</returns>
        static string ReadJsonPrimitiveValue(string json, int valueStart, out int valueLength) {
            int cursor = valueStart;
            while (cursor < json.Length && !IsJsonPrimitiveTerminator(json[cursor])) {
                cursor++;
            }

            valueLength = cursor - valueStart;
            return json.Substring(valueStart, valueLength);
        }

        /// <summary>
        /// Reads one JSON string value and decodes the supported escape sequences.
        /// </summary>
        /// <param name="json">JSON text containing the string value.</param>
        /// <param name="valueStart">Index at which the string value begins.</param>
        /// <param name="valueLength">Length of the encoded string value.</param>
        /// <returns>The decoded string value.</returns>
        static string ReadJsonStringValue(string json, int valueStart, out int valueLength) {
            if (valueStart < 0 || valueStart >= json.Length || json[valueStart] != '"') {
                throw new InvalidOperationException("JSON string value was expected.");
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            bool escaping = false;
            for (int index = valueStart + 1; index < json.Length; index++) {
                char current = json[index];
                if (escaping) {
                    if (current == '"') {
                        builder.Append('"');
                    }
                    else if (current == '\\') {
                        builder.Append('\\');
                    }
                    else if (current == '/') {
                        builder.Append('/');
                    }
                    else if (current == 'b') {
                        builder.Append('\b');
                    }
                    else if (current == 'f') {
                        builder.Append('\f');
                    }
                    else if (current == 'n') {
                        builder.Append('\n');
                    }
                    else if (current == 'r') {
                        builder.Append('\r');
                    }
                    else if (current == 't') {
                        builder.Append('\t');
                    }
                    else if (current == 'u') {
                        if (index + 4 >= json.Length) {
                            throw new InvalidOperationException("JSON unicode escape was truncated.");
                        }

                        int codePoint = 0;
                        for (int digitIndex = 1; digitIndex <= 4; digitIndex++) {
                            codePoint = (codePoint * 16) + ReadJsonHexDigit(json[index + digitIndex]);
                        }

                        builder.Append((char)codePoint);
                        index += 4;
                    }
                    else {
                        throw new InvalidOperationException($"Unsupported JSON escape sequence '\\{current}'.");
                    }

                    escaping = false;
                    continue;
                }

                if (current == '\\') {
                    escaping = true;
                    continue;
                }

                if (current == '"') {
                    valueLength = index - valueStart + 1;
                    return builder.ToString();
                }

                builder.Append(current);
            }

            throw new InvalidOperationException("JSON string value was not terminated.");
        }

        /// <summary>
        /// Converts one hexadecimal JSON escape digit into its numeric value.
        /// </summary>
        /// <param name="value">Hexadecimal character.</param>
        /// <returns>Numeric value for the supplied hex digit.</returns>
        static int ReadJsonHexDigit(char value) {
            if (value >= '0' && value <= '9') {
                return value - '0';
            }
            if (value >= 'a' && value <= 'f') {
                return (value - 'a') + 10;
            }
            if (value >= 'A' && value <= 'F') {
                return (value - 'A') + 10;
            }

            throw new InvalidOperationException("JSON unicode escape contained a non-hexadecimal digit.");
        }

        /// <summary>
        /// Returns the encoded length of one JSON value beginning at the specified index.
        /// </summary>
        /// <param name="json">JSON text containing the value.</param>
        /// <param name="valueStart">Index at which the value begins.</param>
        /// <returns>The value length measured in characters.</returns>
        static int ReadJsonValueLength(string json, int valueStart) {
            if (valueStart < 0 || valueStart >= json.Length) {
                throw new InvalidOperationException("JSON value start was out of range.");
            }

            char firstCharacter = json[valueStart];
            if (firstCharacter == '"') {
                int valueLength = 0;
                ReadJsonStringValue(json, valueStart, out valueLength);
                return valueLength;
            }

            if (firstCharacter == '{') {
                return FindMatchingJsonDelimiter(json, valueStart, '{', '}') - valueStart + 1;
            }

            if (firstCharacter == '[') {
                return FindMatchingJsonDelimiter(json, valueStart, '[', ']') - valueStart + 1;
            }

            int cursor = valueStart;
            while (cursor < json.Length) {
                char current = json[cursor];
                if (IsJsonWhitespace(current) || current == ',' || current == ']' || current == '}') {
                    break;
                }

                cursor++;
            }

            return cursor - valueStart;
        }

        /// <summary>
        /// Determines whether one character ends a JSON primitive token.
        /// </summary>
        /// <param name="value">Character to inspect.</param>
        /// <returns>True when the character terminates a primitive token.</returns>
        static bool IsJsonPrimitiveTerminator(char value) {
            if (value == ',' || value == ']' || value == '}' || value == ' ' || value == '\t' || value == '\r' || value == '\n') {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Finds the matching closing delimiter for one JSON array or object.
        /// </summary>
        /// <param name="json">JSON text to scan.</param>
        /// <param name="openIndex">Index of the opening delimiter.</param>
        /// <param name="openDelimiter">Opening delimiter character.</param>
        /// <param name="closeDelimiter">Closing delimiter character.</param>
        /// <returns>The index of the matching closing delimiter.</returns>
        static int FindMatchingJsonDelimiter(string json, int openIndex, char openDelimiter, char closeDelimiter) {
            if (json[openIndex] != openDelimiter) {
                throw new InvalidOperationException("JSON delimiter scan started on the wrong character.");
            }

            int depth = 0;
            bool insideString = false;
            bool escaping = false;
            for (int index = openIndex; index < json.Length; index++) {
                char current = json[index];
                if (insideString) {
                    if (escaping) {
                        escaping = false;
                    }
                    else if (current == '\\') {
                        escaping = true;
                    }
                    else if (current == '"') {
                        insideString = false;
                    }

                    continue;
                }

                if (current == '"') {
                    insideString = true;
                    continue;
                }

                if (current == openDelimiter) {
                    depth++;
                    continue;
                }

                if (current == closeDelimiter) {
                    depth--;
                    if (depth == 0) {
                        return index;
                    }
                }
            }

            throw new InvalidOperationException("JSON delimiter was not terminated.");
        }

        /// <summary>
        /// Skips whitespace from one character index.
        /// </summary>
        /// <param name="json">JSON text to scan.</param>
        /// <param name="startIndex">Index at which to begin skipping.</param>
        /// <returns>The first non-whitespace index or the string length.</returns>
        static int SkipWhitespace(string json, int startIndex) {
            int cursor = startIndex;
            while (cursor < json.Length && IsJsonWhitespace(json[cursor])) {
                cursor++;
            }

            return cursor;
        }

        /// <summary>
        /// Determines whether one character is treated as JSON whitespace by the runtime parser.
        /// </summary>
        /// <param name="value">Character to inspect.</param>
        /// <returns>True when the character is whitespace for this parser.</returns>
        static bool IsJsonWhitespace(char value) {
            if (value == ' ' || value == '\t' || value == '\r' || value == '\n') {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Reads the next JSON value from an array fragment.
        /// </summary>
        /// <param name="json">JSON array text.</param>
        /// <param name="cursor">Current scan position inside the array.</param>
        /// <param name="valueStart">Start index of the next value.</param>
        /// <param name="valueLength">Length of the next value.</param>
        /// <returns>True when another value was found; otherwise false when the array is exhausted.</returns>
        static bool TryReadNextArrayElement(string json, ref int cursor, out int valueStart, out int valueLength) {
            cursor = SkipWhitespace(json, cursor);
            while (cursor < json.Length && json[cursor] == ',') {
                cursor++;
                cursor = SkipWhitespace(json, cursor);
            }

            if (cursor >= json.Length || json[cursor] == ']') {
                valueStart = 0;
                valueLength = 0;
                return false;
            }

            valueStart = cursor;
            valueLength = ReadJsonValueLength(json, cursor);
            cursor += valueLength;
            return true;
        }
    }
}
