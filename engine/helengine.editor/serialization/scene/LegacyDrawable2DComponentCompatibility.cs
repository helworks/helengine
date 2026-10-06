namespace helengine.editor {
    /// <summary>
    /// Identifies the built-in drawable component schemas that may safely discard the removed 2D draw-order field.
    /// </summary>
    internal static class LegacyDrawable2DComponentCompatibility {
        /// <summary>
        /// Former built-in field name retained for loading older scene payloads.
        /// </summary>
        internal const string RemovedRenderOrder2DFieldName = "RenderOrder2D";

        /// <summary>
        /// Returns whether one resolved component type is a former built-in drawable whose old order field is obsolete.
        /// </summary>
        /// <param name="componentType">Resolved runtime component type.</param>
        /// <returns>True when the type itself is one of the former built-in drawable types.</returns>
        internal static bool IsFormerBuiltInDrawableType(Type componentType) {
            if (componentType == null) {
                throw new ArgumentNullException(nameof(componentType));
            }

            return componentType == typeof(SpriteComponent)
                || componentType == typeof(TextComponent)
                || componentType == typeof(RoundedRectComponent);
        }

        /// <summary>
        /// Returns whether one persisted component id identifies a former built-in drawable type.
        /// </summary>
        /// <param name="componentTypeId">Stable persisted component id.</param>
        /// <returns>True when the id identifies a former built-in drawable type.</returns>
        internal static bool IsFormerBuiltInDrawableTypeId(string componentTypeId) {
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                return false;
            }

            return string.Equals(
                    componentTypeId,
                    AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(SpriteComponent)),
                    StringComparison.Ordinal)
                || string.Equals(
                    componentTypeId,
                    AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(TextComponent)),
                    StringComparison.Ordinal)
                || string.Equals(
                    componentTypeId,
                    AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(RoundedRectComponent)),
                    StringComparison.Ordinal);
        }
    }
}
