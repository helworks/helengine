namespace helengine {
    /// <summary>
    /// Resolves menu-definition providers from assembly-qualified type names persisted in scenes.
    /// </summary>
    public class MenuDefinitionProviderResolver {
        /// <summary>
        /// Optional script type resolver used for module-qualified provider type names.
        /// </summary>
        readonly IScriptTypeResolver ScriptTypeResolver;

        /// <summary>
        /// Initializes a new menu-definition provider resolver.
        /// </summary>
        /// <param name="scriptTypeResolver">Optional shared script type resolver used for loaded gameplay modules.</param>
        public MenuDefinitionProviderResolver(IScriptTypeResolver scriptTypeResolver = null) {
            ScriptTypeResolver = scriptTypeResolver;
        }

        /// <summary>
        /// Instantiates one menu-definition provider from an assembly-qualified type name.
        /// </summary>
        /// <param name="providerTypeName">Assembly-qualified type name of the provider.</param>
        /// <returns>Instantiated provider implementation.</returns>
        public IMenuDefinitionProvider Resolve(string providerTypeName) {
            if (string.IsNullOrWhiteSpace(providerTypeName)) {
                throw new ArgumentException("Provider type name must be provided.", nameof(providerTypeName));
            }

#if HELENGINE_CODEGEN_DISABLE_MENU_REFLECTION
            throw new InvalidOperationException("Menu definition provider reflection is not available in generated native builds.");
#else
            Type providerType = Type.GetType(providerTypeName, false);
            if (providerType == null && ScriptTypeResolver != null) {
                providerType = ScriptTypeResolver.Resolve(providerTypeName);
            }
            if (providerType == null) {
                throw new InvalidOperationException($"Menu provider type '{providerTypeName}' could not be resolved.");
            }
            if (!typeof(IMenuDefinitionProvider).IsAssignableFrom(providerType)) {
                throw new InvalidOperationException($"Menu provider type '{providerTypeName}' must implement {nameof(IMenuDefinitionProvider)}.");
            }

            var constructor = providerType.GetConstructor(Type.EmptyTypes);
            if (constructor == null || !constructor.IsPublic) {
                throw new InvalidOperationException($"Menu provider type '{providerTypeName}' must expose a public parameterless constructor.");
            }

            object instance = Activator.CreateInstance(providerType);
            IMenuDefinitionProvider provider = instance as IMenuDefinitionProvider;
            if (provider == null) {
                throw new InvalidOperationException($"Menu provider type '{providerTypeName}' could not be instantiated.");
            }

            return provider;
#endif
        }
    }
}
