using System.Reflection;
using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Reflects addable component types from a loaded scripting assembly into editor descriptors.
    /// </summary>
    public static class EditorScriptComponentCatalog {
        /// <summary>
        /// Builds add descriptors for all valid component types found in one assembly.
        /// </summary>
        /// <param name="assembly">Loaded scripting assembly to inspect.</param>
        /// <returns>Descriptors for the discovered component types.</returns>
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
        /// Builds one add descriptor for a single reflected component type.
        /// </summary>
        /// <param name="componentType">Component type reflected from the scripting assembly.</param>
        /// <returns>Descriptor for the type, or null when the type cannot be added.</returns>
        public static EditorComponentAddDescriptor BuildDescriptor(Type componentType) {
            if (!IsAddableComponentType(componentType)) {
                return null;
            }

            return CreateDescriptor(componentType);
        }

        /// <summary>
        /// Returns whether one reflected type is an addable component type.
        /// </summary>
        /// <param name="type">Type to inspect.</param>
        /// <returns>True when the type can be shown in the add-component dialog.</returns>
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

            ConstructorInfo ctor = type.GetConstructor(Type.EmptyTypes);
            return ctor != null;
        }

        /// <summary>
        /// Creates one editor descriptor for a reflected component type.
        /// </summary>
        /// <param name="componentType">Component type reflected from the scripting assembly.</param>
        /// <returns>Editor descriptor that can instantiate the component.</returns>
        static EditorComponentAddDescriptor CreateDescriptor(Type componentType) {
            string displayName = FormatDisplayName(componentType.Name);
            return new EditorComponentAddDescriptor(displayName, componentType, false, entity => AddComponent(entity, componentType));
        }

        /// <summary>
        /// Adds one reflected component to the supplied entity.
        /// </summary>
        /// <param name="entity">Entity receiving the reflected component.</param>
        /// <param name="componentType">Concrete component type to instantiate.</param>
        static void AddComponent(Entity entity, Type componentType) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (componentType == null) {
                throw new ArgumentNullException(nameof(componentType));
            }

            if (entity is not EditorEntity editorEntity) {
                throw new InvalidOperationException("Reflected script components can only be attached to editor entities.");
            }

            Component component = (Component)Activator.CreateInstance(componentType);
            editorEntity.AddComponent(component);
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
