using System.Reflection;
using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Discovers and caches the components that can be added from the properties panel.
    /// </summary>
    public static class EditorComponentAddCatalog {
        /// <summary>
        /// Synchronizes initialization of the cached component descriptors.
        /// </summary>
        readonly static object SyncRoot = new object();

        /// <summary>
        /// Cached addable component descriptors discovered from the engine assembly.
        /// </summary>
        static IReadOnlyList<EditorComponentAddDescriptor> CachedComponents;

        /// <summary>
        /// Tracks whether the cached component descriptors have been initialized.
        /// </summary>
        static bool IsInitialized;

        /// <summary>
        /// Initializes the cached descriptor set from the engine assembly.
        /// </summary>
        public static void Initialize() {
            lock (SyncRoot) {
                if (IsInitialized) {
                    return;
                }

                CachedComponents = BuildDescriptors(typeof(Component).Assembly);
                IsInitialized = true;
            }
        }

        /// <summary>
        /// Returns the component options that can be added to the supplied entity.
        /// </summary>
        /// <param name="entity">Entity that will receive the new component.</param>
        /// <returns>Filtered list of component descriptors.</returns>
        public static IReadOnlyList<EditorComponentAddDescriptor> GetAvailableComponents(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            EnsureInitialized();

            List<EditorComponentAddDescriptor> results = new List<EditorComponentAddDescriptor>(CachedComponents.Count);
            for (int i = 0; i < CachedComponents.Count; i++) {
                EditorComponentAddDescriptor descriptor = CachedComponents[i];
                if (descriptor.SingleInstance && HasExactComponent(entity, descriptor.ComponentType)) {
                    continue;
                }

                results.Add(descriptor);
            }

            return results;
        }

        /// <summary>
        /// Builds addable component descriptors from one assembly.
        /// </summary>
        /// <param name="assembly">Assembly to scan for addable component types.</param>
        /// <returns>Descriptors for the addable component types found in the assembly.</returns>
        public static IReadOnlyList<EditorComponentAddDescriptor> BuildDescriptors(Assembly assembly) {
            if (assembly == null) {
                throw new ArgumentNullException(nameof(assembly));
            }

            Type[] types;
            try {
                types = assembly.GetExportedTypes();
            } catch (ReflectionTypeLoadException ex) {
                types = ex.Types ?? Array.Empty<Type>();
            }

            List<EditorComponentAddDescriptor> descriptors = new List<EditorComponentAddDescriptor>(types.Length);
            for (int i = 0; i < types.Length; i++) {
                Type componentType = types[i];
                EditorComponentAddDescriptor descriptor = BuildDescriptor(componentType);
                if (descriptor != null) {
                    descriptors.Add(descriptor);
                }
            }

            descriptors.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal));
            return descriptors;
        }

        /// <summary>
        /// Builds one add descriptor for a single component type.
        /// </summary>
        /// <param name="componentType">Component type to inspect.</param>
        /// <returns>Descriptor for the type, or null when the type should not be shown.</returns>
        static EditorComponentAddDescriptor BuildDescriptor(Type componentType) {
            if (!IsAddableComponentType(componentType)) {
                return null;
            }

            return CreateDescriptor(componentType);
        }

        /// <summary>
        /// Creates one editor descriptor for a reflected component type.
        /// </summary>
        /// <param name="componentType">Component type reflected from the engine assembly.</param>
        /// <returns>Editor descriptor that can instantiate the component.</returns>
        static EditorComponentAddDescriptor CreateDescriptor(Type componentType) {
            string displayName = FormatDisplayName(componentType.Name);
            bool singleInstance = componentType == typeof(FPSComponent);
            return new EditorComponentAddDescriptor(displayName, componentType, singleInstance, entity => AddComponent(entity, componentType));
        }

        /// <summary>
        /// Adds one reflected component to the supplied entity.
        /// </summary>
        /// <param name="entity">Entity receiving the reflected component.</param>
        /// <param name="componentType">Concrete component type to instantiate.</param>
        static void AddComponent(Entity entity, Type componentType) {
            if (componentType == null) {
                throw new ArgumentNullException(nameof(componentType));
            }

            EditorEntity editorEntity = RequireEditorEntity(entity);
            Component component = (Component)Activator.CreateInstance(componentType);
            editorEntity.AddComponent(component);
        }

        /// <summary>
        /// Resolves the editor entity used by the add actions.
        /// </summary>
        /// <param name="entity">Entity targeted by the add action.</param>
        /// <returns>The supplied entity as an editor entity.</returns>
        static EditorEntity RequireEditorEntity(Entity entity) {
            if (entity is not EditorEntity editorEntity) {
                throw new InvalidOperationException("Addable components can only be attached to editor entities.");
            }

            return editorEntity;
        }

        /// <summary>
        /// Returns whether one reflected type can appear in the add-component dialog.
        /// </summary>
        /// <param name="type">Type to inspect.</param>
        /// <returns>True when the type can be shown in the picker.</returns>
        static bool IsAddableComponentType(Type type) {
            if (type == null) {
                return false;
            }
            if (type == typeof(Component)) {
                return false;
            }
            if (!type.IsClass || type.IsAbstract || type.ContainsGenericParameters) {
                return false;
            }
            if (!typeof(Component).IsAssignableFrom(type)) {
                return false;
            }
            if (!(type.IsPublic || type.IsNestedPublic)) {
                return false;
            }
            if (type.GetConstructor(Type.EmptyTypes) == null) {
                return false;
            }
            if (typeof(ICamera).IsAssignableFrom(type)) {
                return false;
            }
            if (type == typeof(UpdateComponent)) {
                return false;
            }
            if (type == typeof(InteractableComponent)) {
                return false;
            }
            if (typeof(IEditorHiddenComponent).IsAssignableFrom(type)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Ensures the cached descriptors are available before the catalog is queried.
        /// </summary>
        static void EnsureInitialized() {
            if (IsInitialized) {
                return;
            }

            Initialize();
        }

        /// <summary>
        /// Determines whether one entity already owns a component of the exact requested type.
        /// </summary>
        /// <param name="entity">Entity whose component list should be searched.</param>
        /// <param name="componentType">Concrete component type to locate.</param>
        /// <returns>True when the entity already owns the exact component type.</returns>
        static bool HasExactComponent(Entity entity, Type componentType) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (componentType == null) {
                throw new ArgumentNullException(nameof(componentType));
            }
            if (entity.Components == null) {
                return false;
            }

            for (int i = 0; i < entity.Components.Count; i++) {
                Component component = entity.Components[i];
                if (component != null && component.GetType() == componentType) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Formats one component type name into a human-friendly label.
        /// </summary>
        /// <param name="componentTypeName">Raw type name from reflection.</param>
        /// <returns>Readable component label.</returns>
        static string FormatDisplayName(string componentTypeName) {
            if (string.IsNullOrWhiteSpace(componentTypeName)) {
                return string.Empty;
            }

            const string ComponentSuffix = "Component";
            if (componentTypeName.EndsWith(ComponentSuffix, StringComparison.Ordinal)) {
                componentTypeName = componentTypeName.Substring(0, componentTypeName.Length - ComponentSuffix.Length);
            }

            StringBuilder builder = new StringBuilder(componentTypeName.Length + 8);
            for (int i = 0; i < componentTypeName.Length; i++) {
                char current = componentTypeName[i];
                if (i > 0 && char.IsUpper(current) && !char.IsUpper(componentTypeName[i - 1])) {
                    builder.Append(' ');
                }

                builder.Append(current);
            }

            return builder.ToString();
        }
    }
}
