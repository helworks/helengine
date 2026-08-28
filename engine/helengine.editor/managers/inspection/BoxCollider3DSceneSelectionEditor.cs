namespace helengine.editor {
    /// <summary>
    /// Renders the selected entity's box collider as a wireframe line box that tracks the live transform and authored size.
    /// </summary>
    public sealed class BoxCollider3DSceneSelectionEditor : IComponentSceneSelectionEditor {
        /// <summary>
        /// Render order that keeps the collider wireframe above the selected entity's own meshes.
        /// </summary>
        const byte ColliderWireframeRenderOrder3D = 250;

        /// <summary>
        /// Returns whether the supplied component is one authored box collider.
        /// </summary>
        /// <param name="component">Component attached to the selected entity.</param>
        /// <returns>True for box colliders.</returns>
        public bool Supports(Component component) {
            return component is BoxCollider3DComponent;
        }

        /// <summary>
        /// Creates one unit wireframe box entity that is rescaled per frame to match the effective collider size.
        /// </summary>
        /// <param name="render3D">Renderer used to build the wireframe resources.</param>
        /// <param name="selectedEntity">Currently selected entity that owns the collider.</param>
        /// <param name="component">Box collider being visualized.</param>
        /// <returns>Owned internal wireframe entity.</returns>
        public EditorEntity CreateSelectionVisual(RenderManager3D render3D, EngineGeneratedMaterialCache generatedMaterialCache, Entity selectedEntity, Component component) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }
            if (generatedMaterialCache == null) {
                throw new ArgumentNullException(nameof(generatedMaterialCache));
            }

            EditorEntity visualEntity = ComponentSelectionWireframeFactory.CreateUnitLineBox(render3D, generatedMaterialCache, "Box Collider Selection Wireframe", ColliderWireframeRenderOrder3D);
            UpdateSelectionVisual(visualEntity, selectedEntity, component);
            return visualEntity;
        }

        /// <summary>
        /// Tracks the owning entity's world pose and applies the effective world-space collider size, matching the physics backends' size-times-scale convention.
        /// </summary>
        /// <param name="visualEntity">Wireframe entity created by <see cref="CreateSelectionVisual"/>.</param>
        /// <param name="selectedEntity">Currently selected entity that owns the collider.</param>
        /// <param name="component">Box collider being visualized.</param>
        public void UpdateSelectionVisual(EditorEntity visualEntity, Entity selectedEntity, Component component) {
            if (visualEntity == null) {
                throw new ArgumentNullException(nameof(visualEntity));
            } else if (selectedEntity == null) {
                throw new ArgumentNullException(nameof(selectedEntity));
            }

            if (component is not BoxCollider3DComponent boxCollider) {
                throw new ArgumentException("Box collider selection visuals require a BoxCollider3DComponent.", nameof(component));
            }

            float3 worldScale = selectedEntity.Scale;
            visualEntity.LocalPosition = selectedEntity.Position;
            visualEntity.LocalOrientation = selectedEntity.Orientation;
            visualEntity.LocalScale = new float3(
                Math.Abs(boxCollider.Size.X * worldScale.X),
                Math.Abs(boxCollider.Size.Y * worldScale.Y),
                Math.Abs(boxCollider.Size.Z * worldScale.Z));
        }
    }
}
