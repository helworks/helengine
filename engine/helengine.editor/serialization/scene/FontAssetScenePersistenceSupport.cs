namespace helengine.editor {
    /// <summary>
    /// Shared helper for scene persistence of components that own font asset references.
    /// </summary>
    static class FontAssetScenePersistenceSupport {
        /// <summary>
        /// Generated provider id reserved for the editor's built-in font asset.
        /// </summary>
        const string EditorGeneratedProviderId = "editor";

        /// <summary>
        /// Stable asset id used for the editor's built-in font asset.
        /// </summary>
        const string EditorFontAssetId = "ui-font";

        /// <summary>
        /// Stable asset id used for the generated Nintendo DS debug font.
        /// </summary>

        /// <summary>
        /// Stable relative path used for the editor's built-in font asset.
        /// </summary>
        const string EditorFontRelativePath = "generated/editor/fonts/ui.hefont";

        /// <summary>
        /// Stable relative path used for the generated Nintendo DS debug font asset.
        /// </summary>
        /// <summary>
        /// Stable reference slot name used for font references.
        /// </summary>
        internal const string FontReferenceName = "Font";

        /// <summary>
        /// Resolves the stable font reference for one component font value.
        /// </summary>
        /// <param name="componentName">Friendly component name used in error messages.</param>
        /// <param name="font">Runtime font value currently assigned to the component.</param>
        /// <param name="saveState">Editor-time save metadata associated with the component.</param>
        /// <returns>Stable scene asset reference for the font.</returns>
        internal static SceneAssetReference ResolveFontReference(string componentName, FontAsset font, EntityComponentSaveState saveState) {
            if (string.IsNullOrWhiteSpace(componentName)) {
                throw new ArgumentException("Component name must be provided.", nameof(componentName));
            }
            if (font == null) {
                throw new InvalidOperationException($"{componentName} requires a font asset before it can be serialized.");
            }
            if (saveState != null && saveState.TryGetAssetReference(FontReferenceName, out SceneAssetReference storedReference)) {
                return storedReference;
            }
            if (TryResolveEditorCoreFont(font, out SceneAssetReference editorFontReference)) {
                if (saveState != null) {
                    saveState.SetAssetReference(FontReferenceName, editorFontReference);
                }

                return editorFontReference;
            }

            throw new InvalidOperationException($"{componentName} Font is assigned but does not have a stored scene asset reference.");
        }

        /// <summary>
        /// Attempts to map one runtime font back to the generated editor UI-font reference.
        /// </summary>
        /// <param name="font">Runtime font currently assigned to a component.</param>
        /// <param name="reference">Resolved generated editor-font reference when the font belongs to the active editor core.</param>
        /// <returns>True when the font belongs to the active editor core.</returns>
        internal static bool TryResolveEditorCoreFont(FontAsset font, out SceneAssetReference reference) {
            reference = null;
            if (font == null) {
                return false;
            }
            if (Core.Instance is not EditorCore editorCore) {
                return false;
            }
            if (editorCore.DefaultFontAssetForEditor == null) {
                return false;
            }
            if (!ReferenceEquals(font, editorCore.DefaultFontAssetForEditor)) {
                return false;
            }

            reference = BuildEditorFontReference();
            return true;
        }

        /// <summary>
        /// Resolves one serialized font reference into a runtime font asset.
        /// </summary>
        /// <param name="referenceResolver">Resolver used to rebuild runtime asset references.</param>
        /// <param name="fontReference">Stored font asset reference.</param>
        /// <returns>Runtime font asset.</returns>
        internal static FontAsset ResolveFont(ISceneAssetReferenceResolver referenceResolver, SceneAssetReference fontReference) {
            if (referenceResolver == null) {
                throw new ArgumentNullException(nameof(referenceResolver));
            }
            if (fontReference == null) {
                throw new ArgumentNullException(nameof(fontReference));
            }

            return referenceResolver.ResolveFont(fontReference);
        }

        /// <summary>
        /// Builds the stable scene asset reference for the editor's built-in font.
        /// </summary>
        /// <returns>Stable generated editor-font reference.</returns>
        internal static SceneAssetReference BuildEditorFontReference() {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = EditorFontRelativePath,
                ProviderId = EditorGeneratedProviderId,
                AssetId = EditorFontAssetId
            };
        }

        /// <summary>
        /// Resolves one serialized optional scene asset reference.
        /// </summary>
        /// <param name="reader">Reader positioned at the reference payload.</param>
        /// <returns>Stable scene asset reference when present; otherwise null.</returns>
        internal static SceneAssetReference ReadOptionalReference(EngineBinaryReader reader) {
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
        /// Writes one optional scene asset reference to the payload.
        /// </summary>
        /// <param name="writer">Writer receiving the serialized reference.</param>
        /// <param name="reference">Reference to write.</param>
        internal static void WriteOptionalReference(EngineBinaryWriter writer, SceneAssetReference reference) {
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
        /// Writes one four-channel byte color value.
        /// </summary>
        /// <param name="writer">Writer receiving the color.</param>
        /// <param name="value">Color value to write.</param>
        internal static void WriteByte4(EngineBinaryWriter writer, byte4 value) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteByte(value.X);
            writer.WriteByte(value.Y);
            writer.WriteByte(value.Z);
            writer.WriteByte(value.W);
        }

        /// <summary>
        /// Reads one four-channel byte color value.
        /// </summary>
        /// <param name="reader">Reader positioned at the color payload.</param>
        /// <returns>Decoded color value.</returns>
        internal static byte4 ReadByte4(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new byte4(
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte());
        }
    }
}
