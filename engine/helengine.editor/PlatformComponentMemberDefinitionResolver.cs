using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Resolves builder-registered synthetic component member definitions for one supported platform and component type.
    /// </summary>
    public sealed class PlatformComponentMemberDefinitionResolver {
        /// <summary>
        /// Platform definitions keyed by stable platform identifier.
        /// </summary>
        IReadOnlyDictionary<string, PlatformDefinition> PlatformDefinitionsById;

        /// <summary>
        /// Initializes a new synthetic component-member definition resolver with no supported platform definitions.
        /// </summary>
        public PlatformComponentMemberDefinitionResolver() {
            PlatformDefinitionsById = new Dictionary<string, PlatformDefinition>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Stores the supported platform definitions that should drive synthetic component-member resolution.
        /// </summary>
        /// <param name="platformDefinitionsById">Platform definitions keyed by stable platform identifier.</param>
        public void SetPlatformDefinitions(IReadOnlyDictionary<string, PlatformDefinition> platformDefinitionsById) {
            PlatformDefinitionsById = platformDefinitionsById
                ?? new Dictionary<string, PlatformDefinition>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolves the synthetic component-member definitions exposed for one component type on one active platform.
        /// </summary>
        /// <param name="platformId">Active platform identifier.</param>
        /// <param name="componentType">Component type being inspected or packaged.</param>
        /// <returns>Ordered synthetic component-member definitions exposed by the platform.</returns>
        public IReadOnlyList<PlatformComponentMemberDefinition> Resolve(string platformId, Type componentType) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }
            if (componentType == null) {
                throw new ArgumentNullException(nameof(componentType));
            }
            if (!PlatformDefinitionsById.TryGetValue(platformId, out PlatformDefinition platformDefinition) || platformDefinition == null) {
                return Array.Empty<PlatformComponentMemberDefinition>();
            }

            string componentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(componentType);
            return platformDefinition.ComponentMemberDefinitions
                .Where(definition => string.Equals(definition.ComponentTypeId, componentTypeId, StringComparison.Ordinal))
                .OrderBy(definition => definition.Order)
                .ThenBy(definition => definition.MemberName, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
