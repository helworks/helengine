using System.Reflection;

namespace helengine.editor {
    /// <summary>
    /// Builds metadata-driven descriptors for the default reflected component inspector.
    /// </summary>
    public class ReflectedComponentPropertyDescriptorBuilder {
        /// <summary>
        /// Registered provider-backed custom property editors.
        /// </summary>
        readonly List<IComponentPropertyEditorProvider> Providers;

        /// <summary>
        /// Initializes the reflected descriptor builder with the currently supported custom editor providers.
        /// </summary>
        public ReflectedComponentPropertyDescriptorBuilder() {
            Providers = new List<IComponentPropertyEditorProvider> {
                new CameraClearSettingsPropertyEditorProvider(),
                new SceneMapPropertyEditorProvider()
            };
        }

        /// <summary>
        /// Builds reflected descriptors for the supplied component type.
        /// </summary>
        /// <param name="componentType">Component type being inspected.</param>
        /// <returns>Ordered descriptors eligible for the default inspector.</returns>
        public List<ReflectedComponentPropertyDescriptor> Build(Type componentType) {
            if (componentType == null) {
                throw new ArgumentNullException(nameof(componentType));
            }

            PropertyInfo[] properties = componentType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            List<ReflectedComponentPropertyDescriptor> descriptors = new List<ReflectedComponentPropertyDescriptor>(properties.Length);
            for (int index = 0; index < properties.Length; index++) {
                PropertyInfo property = properties[index];
                if (!ShouldInclude(property)) {
                    continue;
                }

                if (TryBuildCustomEditorDescriptor(property, out ReflectedComponentPropertyDescriptor customDescriptor)) {
                    descriptors.Add(customDescriptor);
                    continue;
                }

                if (!TryMapRowKind(property, out ComponentPropertyRowKind rowKind)) {
                    continue;
                }

                descriptors.Add(new ReflectedComponentPropertyDescriptor(
                    property,
                    ResolveDisplayName(property),
                    rowKind,
                    ResolveOrder(property)));
            }

            descriptors.Sort(CompareDescriptors);
            return descriptors;
        }

        /// <summary>
        /// Determines whether one reflected property should participate in the default inspector.
        /// </summary>
        /// <param name="property">Property metadata under consideration.</param>
        /// <returns>True when the property is eligible for reflected inspection.</returns>
        bool ShouldInclude(PropertyInfo property) {
            if (property == null) {
                return false;
            }
            if (property.GetIndexParameters().Length > 0) {
                return false;
            }
            if (!property.CanRead) {
                return false;
            }
            if (property.IsDefined(typeof(EditorPropertyHiddenAttribute), true)) {
                return false;
            }
            if (string.Equals(property.Name, "Parent", StringComparison.Ordinal)) {
                return false;
            }
            if (string.Equals(property.Name, nameof(Component.IsEditorUpdateExecutionSuppressionMarker), StringComparison.Ordinal)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Tries to map one property to a supported row kind.
        /// </summary>
        /// <param name="property">Property metadata being inspected.</param>
        /// <param name="rowKind">Resolved row kind when supported.</param>
        /// <returns>True when the property can be rendered by the default inspector.</returns>
        bool TryMapRowKind(PropertyInfo property, out ComponentPropertyRowKind rowKind) {
            if (property == null) {
                throw new ArgumentNullException(nameof(property));
            }

            Type propertyType = property.PropertyType;
            bool isEditable = property.CanWrite;
            if (propertyType == typeof(float3)) {
                rowKind = isEditable ? ComponentPropertyRowKind.Vector3 : ComponentPropertyRowKind.ReadOnly;
                return true;
            }
            if (propertyType == typeof(bool)) {
                rowKind = isEditable ? ComponentPropertyRowKind.Boolean : ComponentPropertyRowKind.ReadOnly;
                return true;
            }
            if (propertyType == typeof(RuntimeMaterial)) {
                rowKind = isEditable ? ComponentPropertyRowKind.Material : ComponentPropertyRowKind.ReadOnly;
                return true;
            }
            if (propertyType == typeof(RuntimeMaterial[])
                && string.Equals(property.Name, nameof(MeshComponent.Materials), StringComparison.Ordinal)) {
                rowKind = isEditable ? ComponentPropertyRowKind.Material : ComponentPropertyRowKind.ReadOnly;
                return true;
            }
            if (propertyType == typeof(FontAsset)) {
                rowKind = isEditable ? ComponentPropertyRowKind.Font : ComponentPropertyRowKind.ReadOnly;
                return true;
            }
            if (propertyType == typeof(RuntimeModel)) {
                rowKind = isEditable ? ComponentPropertyRowKind.Model : ComponentPropertyRowKind.ReadOnly;
                return true;
            }
            if (IsEditableScalar(propertyType)) {
                rowKind = isEditable ? ComponentPropertyRowKind.Scalar : ComponentPropertyRowKind.ReadOnly;
                return true;
            }

            rowKind = default;
            return false;
        }

        /// <summary>
        /// Determines whether one type should use a scalar field row.
        /// </summary>
        /// <param name="propertyType">Property type to evaluate.</param>
        /// <returns>True when the type is scalar-editable in the default inspector.</returns>
        bool IsEditableScalar(Type propertyType) {
            if (propertyType == null) {
                throw new ArgumentNullException(nameof(propertyType));
            }
            if (propertyType == typeof(string)) {
                return true;
            }
            if (propertyType == typeof(int)
                || propertyType == typeof(float)
                || propertyType == typeof(double)
                || propertyType == typeof(byte)
                || propertyType == typeof(short)
                || propertyType == typeof(long)
                || propertyType == typeof(uint)
                || propertyType == typeof(ulong)
                || propertyType == typeof(ushort)
                || propertyType == typeof(sbyte)) {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tries to build a provider-backed custom editor descriptor for one reflected property.
        /// </summary>
        /// <param name="property">Property metadata being inspected.</param>
        /// <param name="descriptor">Resolved provider-backed descriptor when supported.</param>
        /// <returns>True when one provider claims the property.</returns>
        bool TryBuildCustomEditorDescriptor(PropertyInfo property, out ReflectedComponentPropertyDescriptor descriptor) {
            if (property == null) {
                throw new ArgumentNullException(nameof(property));
            }

            for (int index = 0; index < Providers.Count; index++) {
                IComponentPropertyEditorProvider provider = Providers[index];
                if (!provider.TryCreateDescriptor(property, out ComponentPropertyEditorDescriptor customEditor)) {
                    continue;
                }

                descriptor = new ReflectedComponentPropertyDescriptor(
                    property,
                    customEditor.DisplayName,
                    customEditor,
                    customEditor.Order);
                return true;
            }

            descriptor = null;
            return false;
        }

        /// <summary>
        /// Resolves the display label for one property.
        /// </summary>
        /// <param name="property">Property metadata being inspected.</param>
        /// <returns>Display label used by the inspector.</returns>
        string ResolveDisplayName(PropertyInfo property) {
            if (property == null) {
                throw new ArgumentNullException(nameof(property));
            }

            EditorPropertyDisplayNameAttribute attribute = property.GetCustomAttribute<EditorPropertyDisplayNameAttribute>(true);
            if (attribute != null) {
                return attribute.DisplayName;
            }

            return property.Name;
        }

        /// <summary>
        /// Resolves the display order for one property.
        /// </summary>
        /// <param name="property">Property metadata being inspected.</param>
        /// <returns>Display order used during sorting.</returns>
        int ResolveOrder(PropertyInfo property) {
            if (property == null) {
                throw new ArgumentNullException(nameof(property));
            }

            EditorPropertyOrderAttribute attribute = property.GetCustomAttribute<EditorPropertyOrderAttribute>(true);
            if (attribute != null) {
                return attribute.Order;
            }

            return int.MaxValue;
        }

        /// <summary>
        /// Compares two reflected descriptors for stable inspector ordering.
        /// </summary>
        /// <param name="left">Left descriptor.</param>
        /// <param name="right">Right descriptor.</param>
        /// <returns>Comparison result for sorting.</returns>
        int CompareDescriptors(ReflectedComponentPropertyDescriptor left, ReflectedComponentPropertyDescriptor right) {
            if (left == null) {
                throw new ArgumentNullException(nameof(left));
            }
            if (right == null) {
                throw new ArgumentNullException(nameof(right));
            }

            int orderComparison = left.Order.CompareTo(right.Order);
            if (orderComparison != 0) {
                return orderComparison;
            }

            return string.Compare(left.Property.Name, right.Property.Name, StringComparison.Ordinal);
        }
    }
}
