namespace helengine.editor {
    /// <summary>
    /// Converts builder-owned synthetic component member definitions into default-inspector row descriptors.
    /// </summary>
    public sealed class PlatformComponentMemberDescriptorResolver {
        /// <summary>
        /// Shared definition resolver used to discover builder-owned synthetic component members for the active platform.
        /// </summary>
        readonly PlatformComponentMemberDefinitionResolver DefinitionResolver;

        /// <summary>
        /// Initializes a new synthetic platform component-member descriptor resolver.
        /// </summary>
        public PlatformComponentMemberDescriptorResolver() {
            DefinitionResolver = new PlatformComponentMemberDefinitionResolver();
        }

        /// <summary>
        /// Stores the supported platform definitions that should drive synthetic component-member resolution.
        /// </summary>
        /// <param name="platformDefinitionsById">Platform definitions keyed by stable platform identifier.</param>
        public void SetPlatformDefinitions(IReadOnlyDictionary<string, helengine.baseplatform.Definitions.PlatformDefinition> platformDefinitionsById) {
            DefinitionResolver.SetPlatformDefinitions(platformDefinitionsById);
        }

        /// <summary>
        /// Resolves the synthetic platform component-member descriptors exposed for one component type on one active platform.
        /// </summary>
        /// <param name="platformId">Active platform identifier.</param>
        /// <param name="componentType">Component type being inspected.</param>
        /// <returns>Ordered inspector descriptors exposed by the platform.</returns>
        public IReadOnlyList<PlatformComponentMemberDescriptor> Resolve(string platformId, Type componentType) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }
            if (componentType == null) {
                throw new ArgumentNullException(nameof(componentType));
            }

            IReadOnlyList<helengine.baseplatform.Definitions.PlatformComponentMemberDefinition> definitions = DefinitionResolver.Resolve(platformId, componentType);
            if (definitions.Count < 1) {
                return Array.Empty<PlatformComponentMemberDescriptor>();
            }

            PlatformComponentMemberDescriptor[] descriptors = new PlatformComponentMemberDescriptor[definitions.Count];
            for (int index = 0; index < definitions.Count; index++) {
                descriptors[index] = new PlatformComponentMemberDescriptor(
                    definitions[index],
                    PlatformComponentMemberValueUtility.ResolveRowKind(definitions[index]),
                    PlatformComponentMemberValueUtility.ResolveValueType(definitions[index]));
            }

            return descriptors;
        }
    }
}
