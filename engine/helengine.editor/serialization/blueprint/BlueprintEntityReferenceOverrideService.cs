using System.Reflection;

namespace helengine.editor {
    /// <summary>
    /// Builds scene-owned overrides for writable scene-entity reference properties exposed by a blueprint.
    /// </summary>
    public sealed class BlueprintEntityReferenceOverrideService {
        /// <summary>
        /// Persistence registry used to materialize blueprint component properties and preserve their serialized keys.
        /// </summary>
        readonly ComponentPersistenceRegistry PersistenceRegistry;

        /// <summary>
        /// Initializes one blueprint entity-reference override service.
        /// </summary>
        /// <param name="persistenceRegistry">Registry that knows how blueprint components are persisted.</param>
        public BlueprintEntityReferenceOverrideService(ComponentPersistenceRegistry persistenceRegistry) {
            PersistenceRegistry = persistenceRegistry ?? throw new ArgumentNullException(nameof(persistenceRegistry));
        }

        /// <summary>
        /// Assigns one scene entity to every writable <see cref="SceneEntityReference"/> property in a blueprint instance.
        /// </summary>
        /// <param name="instanceComponent">Scene-owned blueprint instance receiving the overrides.</param>
        /// <param name="blueprintAsset">Blueprint whose component references should be exposed.</param>
        /// <param name="targetEntityId">Authored scene entity id assigned to each reference property.</param>
        public void BindAllEntityReferences(BlueprintInstanceComponent instanceComponent, BlueprintAsset blueprintAsset, uint targetEntityId) {
            if (instanceComponent == null) {
                throw new ArgumentNullException(nameof(instanceComponent));
            }

            instanceComponent.EntityReferenceOverrides = CreateAllEntityReferenceOverrides(blueprintAsset, targetEntityId);
        }

        /// <summary>
        /// Creates serialized scene-owned overrides for every writable scene-entity reference in a blueprint hierarchy.
        /// </summary>
        /// <param name="blueprintAsset">Blueprint whose component references should be exposed.</param>
        /// <param name="targetEntityId">Authored scene entity id assigned to each reference property.</param>
        /// <returns>Stable overrides that the package expander can apply before cloning entity ids.</returns>
        public BlueprintEntityReferenceOverrideAsset[] CreateAllEntityReferenceOverrides(BlueprintAsset blueprintAsset, uint targetEntityId) {
            if (blueprintAsset == null) {
                throw new ArgumentNullException(nameof(blueprintAsset));
            } else if (blueprintAsset.RootEntity == null) {
                throw new InvalidOperationException("Blueprint entity-reference overrides require a blueprint root entity.");
            } else if (targetEntityId == 0u) {
                throw new ArgumentOutOfRangeException(nameof(targetEntityId), "Blueprint entity-reference overrides require a non-zero target scene entity id.");
            }

            List<BlueprintEntityReferenceOverrideAsset> overrides = new List<BlueprintEntityReferenceOverrideAsset>();
            CollectEntityReferenceOverrides(blueprintAsset.RootEntity, targetEntityId, overrides);
            return overrides.ToArray();
        }

        /// <summary>
        /// Recursively discovers writable scene-entity reference properties in one blueprint entity subtree.
        /// </summary>
        /// <param name="entityAsset">Current blueprint entity being inspected.</param>
        /// <param name="targetEntityId">Authored scene entity id assigned to each discovered reference.</param>
        /// <param name="overrides">Collection receiving discovered overrides.</param>
        void CollectEntityReferenceOverrides(SceneEntityAsset entityAsset, uint targetEntityId, List<BlueprintEntityReferenceOverrideAsset> overrides) {
            if (entityAsset == null) {
                return;
            }

            SceneComponentAssetRecord[] components = entityAsset.Components ?? Array.Empty<SceneComponentAssetRecord>();
            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++) {
                SceneComponentAssetRecord componentRecord = components[componentIndex];
                if (componentRecord == null || string.IsNullOrWhiteSpace(componentRecord.ComponentKey)) {
                    continue;
                }

                IComponentPersistenceDescriptor descriptor = PersistenceRegistry.GetDescriptor(componentRecord.ComponentTypeId);
                Type componentType = descriptor.ComponentType;
                if (componentType == typeof(Component)) {
                    Component component = descriptor.DeserializeComponent(componentRecord, null, null);
                    componentType = component.GetType();
                }

                PropertyInfo[] properties = componentType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
                for (int propertyIndex = 0; propertyIndex < properties.Length; propertyIndex++) {
                    PropertyInfo property = properties[propertyIndex];
                    if (property.PropertyType != typeof(SceneEntityReference) || !property.CanWrite) {
                        continue;
                    }

                    overrides.Add(new BlueprintEntityReferenceOverrideAsset {
                        SourceEntityId = entityAsset.Id,
                        ComponentKey = componentRecord.ComponentKey,
                        PropertyName = property.Name,
                        TargetEntityId = targetEntityId
                    });
                }
            }

            SceneEntityAsset[] children = entityAsset.Children ?? Array.Empty<SceneEntityAsset>();
            for (int childIndex = 0; childIndex < children.Length; childIndex++) {
                CollectEntityReferenceOverrides(children[childIndex], targetEntityId, overrides);
            }
        }
    }
}
