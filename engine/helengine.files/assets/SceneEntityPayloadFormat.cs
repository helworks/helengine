using helengine;

namespace helengine.files {
    /// <summary>
    /// Owns the scene-entity half of the editor asset format: the entity tree, its platform overrides, the deterministic ordering those overrides are written in, scene settings, and the asset-reference and component-record tables that both the scene and the blueprint payloads store.
    /// </summary>
    public static class SceneEntityPayloadFormat {
        /// <summary>
        /// Version marker written into current scene entity payloads.
        /// </summary>
        public const byte SceneEntityPayloadVersion = 8;

        /// <summary>Validates one scene entity and all nested entities.</summary>
        public static void ValidateDeterministicSceneEntityOverrides(SceneEntityAsset entity) {
            if (entity == null) {
                return;
            }

            EnsureUniquePlatformOverrideScopes(entity.PlatformExistenceOverrides, item => item?.PlatformId, item => item?.EnvironmentId);
            EnsureUniquePlatformOverrideScopes(entity.PlatformTransformOverrides, item => item?.PlatformId, item => item?.EnvironmentId);
            EnsureUniquePlatformOverrideScopes(entity.PlatformComponentOverrides, item => item?.PlatformId, item => item?.EnvironmentId);
            for (int index = 0; index < (entity.Children?.Length ?? 0); index++) {
                ValidateDeterministicSceneEntityOverrides(entity.Children[index]);
            }
        }

        /// <summary>
        /// Writes scene-level settings persisted by the editor scene asset format.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="sceneSettings">Scene settings to serialize.</param>
        public static void WriteSceneSettingsAsset(EngineBinaryWriter writer, SceneSettingsAsset sceneSettings) {
            WriteSceneCanvasProfile(writer, sceneSettings.CanvasProfile);
            writer.WriteByte(sceneSettings.DontUnload ? (byte)1 : (byte)0);
        }

        /// <summary>
        /// Reads scene-level settings persisted by the editor scene asset format.
        /// </summary>
        /// <param name="reader">Source reader positioned at the scene settings payload.</param>
        /// <returns>Deserialized scene settings.</returns>
        public static SceneSettingsAsset ReadSceneSettingsAsset(EngineBinaryReader reader) {
            SceneSettingsAsset sceneSettings = new SceneSettingsAsset {
                CanvasProfile = ReadSceneCanvasProfile(reader)
            };
            sceneSettings.DontUnload = ReadBooleanByte(reader, "scene settings");
            return sceneSettings;
        }

        /// <summary>
        /// Writes one authored scene canvas profile.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="canvasProfile">Canvas profile to serialize.</param>
        static void WriteSceneCanvasProfile(EngineBinaryWriter writer, SceneCanvasProfile canvasProfile) {
            writer.WriteInt32(canvasProfile.Width);
            writer.WriteInt32(canvasProfile.Height);
        }

        /// <summary>
        /// Reads one authored scene canvas profile.
        /// </summary>
        /// <param name="reader">Source reader positioned at the canvas profile payload.</param>
        /// <returns>Deserialized scene canvas profile.</returns>
        static SceneCanvasProfile ReadSceneCanvasProfile(EngineBinaryReader reader) {
            return new SceneCanvasProfile {
                Width = reader.ReadInt32(),
                Height = reader.ReadInt32()
            };
        }

        /// <summary>
        /// Reads a boolean encoded as one byte where zero means false and one means true.
        /// </summary>
        /// <param name="reader">Reader positioned at the encoded boolean value.</param>
        /// <param name="context">Description of the payload being decoded.</param>
        /// <returns>Decoded boolean value.</returns>
        static bool ReadBooleanByte(EngineBinaryReader reader, string context) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }
            if (string.IsNullOrWhiteSpace(context)) {
                throw new ArgumentException("Boolean read context is required.", nameof(context));
            }

            byte value = reader.ReadByte();
            if (value == 0) {
                return false;
            }
            if (value == 1) {
                return true;
            }

            throw new InvalidOperationException($"Unsupported {context} boolean value '{value}'.");
        }

        /// <summary>
        /// Writes one serialized scene entity payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Scene entity asset to serialize.</param>
        public static void WriteSceneEntityAsset(EngineBinaryWriter writer, SceneEntityAsset asset) {
            writer.WriteByte(SceneEntityPayloadVersion);
            writer.WriteUInt32(asset.Id);
            writer.WriteString(asset.Name);
            writer.WriteByte(asset.IsStatic ? (byte)1 : (byte)0);
            writer.WriteByte(asset.Enabled ? (byte)1 : (byte)0);
            writer.WriteUInt16(asset.LayerMask);
            writer.WriteFloat3(asset.LocalPosition);
            writer.WriteFloat3(asset.LocalScale);
            writer.WriteFloat4(asset.LocalOrientation);
            writer.WriteArray(asset.Components, WriteSceneComponentAssetRecordValue);
            writer.WriteArray(SortSceneEntityPlatformExistenceOverrides(asset.PlatformExistenceOverrides), WriteSceneEntityPlatformExistenceOverrideAsset);
            writer.WriteArray(SortSceneEntityPlatformTransformOverrides(asset.PlatformTransformOverrides), WriteSceneEntityPlatformTransformOverrideAsset);
            writer.WriteArray(SortSceneEntityPlatformComponentOverrides(asset.PlatformComponentOverrides), WriteSceneEntityPlatformComponentOverrideAsset);
            writer.WriteArray(asset.Children, WriteSceneEntityAsset);
        }

        /// <summary>
        /// Reads one serialized scene entity payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene entity asset.</returns>
        public static SceneEntityAsset ReadSceneEntityAsset(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            byte payloadVersion = reader.ReadByte();
            if (payloadVersion != SceneEntityPayloadVersion) {
                throw new InvalidOperationException($"Unsupported scene entity payload version '{payloadVersion}'.");
            }

            uint id = reader.ReadUInt32();
            string name = reader.ReadString();
            bool isStatic = reader.ReadByte() != 0;
            bool enabled = reader.ReadByte() != 0;
            ushort layerMask = reader.ReadUInt16();
            float3 localPosition = reader.ReadFloat3();
            float3 localScale = reader.ReadFloat3();
            float4 localOrientation = reader.ReadFloat4();
            SceneComponentAssetRecord[] components = ReadSceneComponentAssetRecordArray(reader) ?? Array.Empty<SceneComponentAssetRecord>();
            SceneEntityPlatformExistenceOverrideAsset[] platformExistenceOverrides = reader.ReadArray(ReadSceneEntityPlatformExistenceOverrideAsset) ?? Array.Empty<SceneEntityPlatformExistenceOverrideAsset>();
            SceneEntityPlatformTransformOverrideAsset[] platformTransformOverrides = reader.ReadArray(ReadSceneEntityPlatformTransformOverrideAsset) ?? Array.Empty<SceneEntityPlatformTransformOverrideAsset>();
            SceneEntityPlatformComponentOverrideAsset[] platformComponentOverrides = reader.ReadArray(ReadSceneEntityPlatformComponentOverrideAsset) ?? Array.Empty<SceneEntityPlatformComponentOverrideAsset>();

            return new SceneEntityAsset {
                Id = id,
                Name = name,
                IsStatic = isStatic,
                Enabled = enabled,
                LayerMask = layerMask,
                LocalPosition = localPosition,
                LocalScale = localScale,
                LocalOrientation = localOrientation,
                Components = components,
                PlatformExistenceOverrides = platformExistenceOverrides,
                PlatformTransformOverrides = platformTransformOverrides,
                PlatformComponentOverrides = platformComponentOverrides,
                Children = ReadSceneEntityAssetArray(reader) ?? Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Orders entity existence overrides by scope and then by their serialized value.
        /// </summary>
        static SceneEntityPlatformExistenceOverrideAsset[] SortSceneEntityPlatformExistenceOverrides(SceneEntityPlatformExistenceOverrideAsset[] overrides) {
            EnsureUniquePlatformOverrideScopes(overrides, item => item?.PlatformId, item => item?.EnvironmentId);
            return overrides?.OrderBy(item => NormalizeOverrideScopeIdentifier(item?.PlatformId), StringComparer.Ordinal)
                .ThenBy(item => NormalizeOverrideScopeIdentifier(item?.EnvironmentId), StringComparer.Ordinal)
                .ThenBy(item => item?.Exists == true ? 1 : 0)
                .ToArray();
        }

        /// <summary>
        /// Orders entity transform overrides by scope and then by their serialized value.
        /// </summary>
        static SceneEntityPlatformTransformOverrideAsset[] SortSceneEntityPlatformTransformOverrides(SceneEntityPlatformTransformOverrideAsset[] overrides) {
            EnsureUniquePlatformOverrideScopes(overrides, item => item?.PlatformId, item => item?.EnvironmentId);
            return overrides?.OrderBy(item => NormalizeOverrideScopeIdentifier(item?.PlatformId), StringComparer.Ordinal)
                .ThenBy(item => NormalizeOverrideScopeIdentifier(item?.EnvironmentId), StringComparer.Ordinal)
                .ThenBy(item => item?.HasLocalPositionOverride == true ? 1 : 0)
                .ThenBy(item => item?.LocalPosition.X ?? 0f)
                .ThenBy(item => item?.LocalPosition.Y ?? 0f)
                .ThenBy(item => item?.LocalPosition.Z ?? 0f)
                .ThenBy(item => item?.HasLocalScaleOverride == true ? 1 : 0)
                .ThenBy(item => item?.LocalScale.X ?? 0f)
                .ThenBy(item => item?.LocalScale.Y ?? 0f)
                .ThenBy(item => item?.LocalScale.Z ?? 0f)
                .ThenBy(item => item?.HasLocalOrientationOverride == true ? 1 : 0)
                .ThenBy(item => item?.LocalOrientation.X ?? 0f)
                .ThenBy(item => item?.LocalOrientation.Y ?? 0f)
                .ThenBy(item => item?.LocalOrientation.Z ?? 0f)
                .ThenBy(item => item?.LocalOrientation.W ?? 0f)
                .ToArray();
        }

        /// <summary>
        /// Orders entity component overrides by scope and every serialized nested field.
        /// </summary>
        static SceneEntityPlatformComponentOverrideAsset[] SortSceneEntityPlatformComponentOverrides(SceneEntityPlatformComponentOverrideAsset[] overrides) {
            EnsureUniquePlatformOverrideScopes(overrides, item => item?.PlatformId, item => item?.EnvironmentId);
            return overrides?.OrderBy(item => NormalizeOverrideScopeIdentifier(item?.PlatformId), StringComparer.Ordinal)
                .ThenBy(item => NormalizeOverrideScopeIdentifier(item?.EnvironmentId), StringComparer.Ordinal)
                .ThenBy(item => string.Join("\u001f", (item?.RemovedComponentKeys ?? Array.Empty<string>())
                    .OrderBy(key => key ?? string.Empty, StringComparer.Ordinal)), StringComparer.Ordinal)
                .ThenBy(item => string.Join("\u001f", SortSceneEntityPlatformAddedComponents(item?.AddedComponents ?? Array.Empty<SceneEntityPlatformAddedComponentAsset>())
                    .Select(SerializeSceneComponentSortKey)), StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Rejects multiple override records for one exact platform/environment scope before bytes are emitted.
        /// </summary>
        static void EnsureUniquePlatformOverrideScopes<T>(
            T[] overrides,
            Func<T, string> platformSelector,
            Func<T, string> environmentSelector) {
            if (overrides == null) {
                return;
            }

            Dictionary<string, HashSet<string>> environmentsByPlatform = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < overrides.Length; index++) {
                string platformId = NormalizeOverrideScopeIdentifier(platformSelector(overrides[index]));
                string environmentId = NormalizeOverrideScopeIdentifier(environmentSelector(overrides[index]));
                if (!environmentsByPlatform.TryGetValue(platformId, out HashSet<string> environments)) {
                    environments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    environmentsByPlatform.Add(platformId, environments);
                }

                if (!environments.Add(environmentId)) {
                    throw new InvalidOperationException($"Duplicate platform override scope '{platformId}/{environmentId}'.");
                }
            }
        }

        /// <summary>
        /// Normalizes one serialized override scope identifier using the public scope identity rules.
        /// </summary>
        static string NormalizeOverrideScopeIdentifier(string value) {
            return (value ?? string.Empty).Trim();
        }

        /// <summary>
        /// Orders platform-added component records by stable key and complete payload identity.
        /// </summary>
        static SceneEntityPlatformAddedComponentAsset[] SortSceneEntityPlatformAddedComponents(SceneEntityPlatformAddedComponentAsset[] components) {
            return components?.OrderBy(item => item?.Component?.ComponentKey ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(item => item?.Component?.ComponentTypeId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(item => item?.Component?.ComponentIndex ?? 0)
                .ThenBy(item => SerializeSceneComponentSortKey(item), StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Creates a total ordinal sort key for a platform-added component payload.
        /// </summary>
        static string SerializeSceneComponentSortKey(SceneEntityPlatformAddedComponentAsset item) {
            SceneComponentAssetRecord component = item?.Component;
            if (component == null) {
                return string.Empty;
            }

            return (component.ComponentKey ?? string.Empty) + "\u001f"
                + (component.ComponentTypeId ?? string.Empty) + "\u001f"
                + component.ComponentIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\u001f"
                + Convert.ToBase64String(component.Payload ?? Array.Empty<byte>());
        }

        /// <summary>
        /// Writes one serialized scene entity existence override payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Scene entity existence override to serialize.</param>
        static void WriteSceneEntityPlatformExistenceOverrideAsset(EngineBinaryWriter writer, SceneEntityPlatformExistenceOverrideAsset asset) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            writer.WriteString(asset.PlatformId);
            writer.WriteString(asset.EnvironmentId ?? string.Empty);
            writer.WriteByte(asset.Exists ? (byte)1 : (byte)0);
        }

        /// <summary>
        /// Reads one serialized scene entity existence override payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene entity existence override.</returns>
        static SceneEntityPlatformExistenceOverrideAsset ReadSceneEntityPlatformExistenceOverrideAsset(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new SceneEntityPlatformExistenceOverrideAsset {
                PlatformId = reader.ReadString(),
                EnvironmentId = reader.ReadString(),
                Exists = reader.ReadByte() != 0
            };
        }

        /// <summary>
        /// Writes one serialized scene entity transform override payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Scene entity transform override to serialize.</param>
        static void WriteSceneEntityPlatformTransformOverrideAsset(EngineBinaryWriter writer, SceneEntityPlatformTransformOverrideAsset asset) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            writer.WriteString(asset.PlatformId);
            writer.WriteString(asset.EnvironmentId ?? string.Empty);
            writer.WriteByte(asset.HasLocalPositionOverride ? (byte)1 : (byte)0);
            writer.WriteFloat3(asset.LocalPosition);
            writer.WriteByte(asset.HasLocalScaleOverride ? (byte)1 : (byte)0);
            writer.WriteFloat3(asset.LocalScale);
            writer.WriteByte(asset.HasLocalOrientationOverride ? (byte)1 : (byte)0);
            writer.WriteFloat4(asset.LocalOrientation);
        }

        /// <summary>
        /// Reads one serialized scene entity transform override payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene entity transform override.</returns>
        static SceneEntityPlatformTransformOverrideAsset ReadSceneEntityPlatformTransformOverrideAsset(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new SceneEntityPlatformTransformOverrideAsset {
                PlatformId = reader.ReadString(),
                EnvironmentId = reader.ReadString(),
                HasLocalPositionOverride = reader.ReadByte() != 0,
                LocalPosition = reader.ReadFloat3(),
                HasLocalScaleOverride = reader.ReadByte() != 0,
                LocalScale = reader.ReadFloat3(),
                HasLocalOrientationOverride = reader.ReadByte() != 0,
                LocalOrientation = reader.ReadFloat4()
            };
        }

        /// <summary>
        /// Writes one serialized scene entity component existence override payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Scene entity component existence override to serialize.</param>
        static void WriteSceneEntityPlatformComponentOverrideAsset(EngineBinaryWriter writer, SceneEntityPlatformComponentOverrideAsset asset) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            writer.WriteString(asset.PlatformId);
            writer.WriteString(asset.EnvironmentId ?? string.Empty);
            writer.WriteArray(asset.RemovedComponentKeys?.OrderBy(key => key ?? string.Empty, StringComparer.Ordinal).ToArray(), EditorAssetPayloadPrimitives.WriteStringValue);
            writer.WriteArray(SortSceneEntityPlatformAddedComponents(asset.AddedComponents), WriteSceneEntityPlatformAddedComponentAsset);
        }

        /// <summary>
        /// Writes one serialized platform-only component payload attached to a scene entity.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Platform-only component payload to serialize.</param>
        static void WriteSceneEntityPlatformAddedComponentAsset(EngineBinaryWriter writer, SceneEntityPlatformAddedComponentAsset asset) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            } else if (asset.Component == null) {
                throw new InvalidOperationException("Platform-added component assets must define a serialized component record.");
            }

            WriteSceneComponentAssetRecord(writer, asset.Component);
        }

        /// <summary>
        /// Reads one serialized scene entity component existence override payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene entity component existence override.</returns>
        static SceneEntityPlatformComponentOverrideAsset ReadSceneEntityPlatformComponentOverrideAsset(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new SceneEntityPlatformComponentOverrideAsset {
                PlatformId = reader.ReadString(),
                EnvironmentId = reader.ReadString(),
                RemovedComponentKeys = reader.ReadArray(EditorAssetPayloadPrimitives.ReadStringValue) ?? Array.Empty<string>(),
                AddedComponents = reader.ReadArray(ReadSceneEntityPlatformAddedComponentAsset) ?? Array.Empty<SceneEntityPlatformAddedComponentAsset>()
            };
        }

        /// <summary>
        /// Reads one serialized platform-only component payload attached to a scene entity.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized platform-only component payload.</returns>
        static SceneEntityPlatformAddedComponentAsset ReadSceneEntityPlatformAddedComponentAsset(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new SceneEntityPlatformAddedComponentAsset {
                Component = ReadSceneComponentAssetRecord(reader)
            };
        }

        /// <summary>
        /// Writes one serialized scene asset reference payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="reference">Scene asset reference to serialize.</param>
        public static void WriteSceneAssetReference(EngineBinaryWriter writer, SceneAssetReference reference) {
            writer.WriteInt32((int)reference.SourceKind);
            writer.WriteString(reference.RelativePath);
            writer.WriteString(reference.ProviderId);
            writer.WriteString(reference.AssetId);
            writer.WriteString(reference.ContentHash);
        }

        /// <summary>
        /// Reads one serialized scene asset reference payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene asset reference.</returns>
        static SceneAssetReference ReadSceneAssetReference(EngineBinaryReader reader) {
            return SceneAssetReferenceFactory.ReadRequiredCurrentReference(reader);
        }

        /// <summary>
        /// Reads an array of scene asset references from the payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene asset references.</returns>
        public static SceneAssetReference[] ReadSceneAssetReferenceArray(EngineBinaryReader reader) {
            return reader.ReadArray(SceneAssetReferenceFactory.ReadRequiredCurrentReference);
        }

        /// <summary>
        /// Orders scene and blueprint references by every serialized identity field.
        /// </summary>
        /// <param name="references">References to order.</param>
        /// <returns>Ordinally ordered references.</returns>
        public static SceneAssetReference[] SortSceneAssetReferences(SceneAssetReference[] references) {
            return references?.OrderBy(reference => (int)(reference?.SourceKind ?? default(SceneAssetReferenceSourceKind)))
                .ThenBy(reference => reference?.RelativePath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(reference => reference?.ProviderId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(reference => reference?.AssetId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(reference => reference?.ContentHash ?? string.Empty, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Writes one serialized scene component record.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="record">Scene component record to serialize.</param>
        static void WriteSceneComponentAssetRecord(EngineBinaryWriter writer, SceneComponentAssetRecord record) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            writer.WriteString(record.ComponentKey);
            writer.WriteString(record.ComponentTypeId);
            writer.WriteInt32(record.ComponentIndex);
            writer.WriteByteArray(record.Payload);
        }

        /// <summary>
        /// Reads one serialized scene component record.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene component record.</returns>
        static SceneComponentAssetRecord ReadSceneComponentAssetRecord(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new SceneComponentAssetRecord {
                ComponentKey = reader.ReadString(),
                ComponentTypeId = reader.ReadString(),
                ComponentIndex = reader.ReadInt32(),
                Payload = reader.ReadByteArray() ?? Array.Empty<byte>()
            };
        }

        /// <summary>
        /// Reads one array of serialized scene component records.
        /// </summary>
        /// <param name="reader">Source reader positioned at the component array payload.</param>
        /// <returns>Decoded component records or null when the source payload was null.</returns>
        static SceneComponentAssetRecord[] ReadSceneComponentAssetRecordArray(EngineBinaryReader reader) {
            int length = reader.ReadInt32();
            if (length == -1) {
                return null;
            } else if (length < -1) {
                throw new InvalidOperationException("Array length cannot be negative.");
            } else if (length == 0) {
                return Array.Empty<SceneComponentAssetRecord>();
            }

            SceneComponentAssetRecord[] values = new SceneComponentAssetRecord[length];
            for (int index = 0; index < values.Length; index++) {
                values[index] = ReadSceneComponentAssetRecord(reader);
            }

            return values;
        }

        /// <summary>
        /// Reads a scene entity array using the current scene-entity layout.
        /// </summary>
        /// <param name="reader">Source reader positioned at the array payload.</param>
        /// <returns>Decoded scene entity array or null when the source payload was null.</returns>
        public static SceneEntityAsset[] ReadSceneEntityAssetArray(EngineBinaryReader reader) {
            int length = reader.ReadInt32();
            if (length == -1) {
                return null;
            } else if (length < -1) {
                throw new InvalidOperationException("Array length cannot be negative.");
            } else if (length == 0) {
                return Array.Empty<SceneEntityAsset>();
            }

            SceneEntityAsset[] values = new SceneEntityAsset[length];
            for (int index = 0; index < values.Length; index++) {
                values[index] = ReadSceneEntityAsset(reader);
            }

            return values;
        }

        /// <summary>
        /// Writes one scene-component record into a scene entity using the current scene payload version.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Scene component record to serialize.</param>
        static void WriteSceneComponentAssetRecordValue(EngineBinaryWriter writer, SceneComponentAssetRecord asset) {
            WriteSceneComponentAssetRecord(writer, asset);
        }
    }
}
