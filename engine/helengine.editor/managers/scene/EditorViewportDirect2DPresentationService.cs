namespace helengine.editor {
    /// <summary>
    /// Resolves the world-presented 2D size for one scene viewport where one viewport pixel maps to one world unit.
    /// </summary>
    public static class EditorViewportDirect2DPresentationService {
        /// <summary>
        /// Resolves the direct 2D world-presentation size from the supplied viewport component.
        /// </summary>
        /// <param name="sceneViewportComponent">Viewport component that owns the authoritative scene viewport rectangle.</param>
        /// <returns>World-presented 2D size in world units.</returns>
        public static int2 ResolvePresentedWorldSize(ViewportComponent sceneViewportComponent) {
            if (sceneViewportComponent == null) {
                throw new ArgumentNullException(nameof(sceneViewportComponent));
            }

            return sceneViewportComponent.ResolvedViewportSize;
        }

        /// <summary>
        /// Resolves the top-most selectable 2D scene entity at one viewport pointer position before any 3D fallback selection is considered.
        /// </summary>
        /// <param name="sceneCamera">Scene camera that renders the direct 2D viewport content.</param>
        /// <param name="viewport">Viewport rectangle in window coordinates.</param>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <returns>Selectable 2D scene entity when one is hit; otherwise null.</returns>
        public static Entity ResolveSelectableEntityAtPointer(CameraComponent sceneCamera, float4 viewport, int2 pointer) {
            if (sceneCamera == null) {
                throw new ArgumentNullException(nameof(sceneCamera));
            }

            if (!IsPointerInsideViewport(pointer, viewport)) {
                return null;
            }

            IInteractable2D interactable = PointerInteractableHitResolver.ResolveTopInteractableAt(
                Core.Instance.ObjectManager.Interactables,
                Core.Instance.ObjectManager.Drawables2D,
                sceneCamera,
                pointer.X,
                pointer.Y);
            if (interactable == null) {
                return null;
            }

            return EditorViewportSceneSelectionFilter.ResolveSelectableEntity(interactable.Parent);
        }

        /// <summary>
        /// Determines whether one pointer lies inside the supplied viewport rectangle.
        /// </summary>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <param name="viewport">Viewport rectangle in window coordinates.</param>
        /// <returns>True when the pointer lies within the viewport bounds.</returns>
        static bool IsPointerInsideViewport(int2 pointer, float4 viewport) {
            return pointer.X >= viewport.X &&
                   pointer.Y >= viewport.Y &&
                   pointer.X < viewport.X + viewport.Z &&
                   pointer.Y < viewport.Y + viewport.W;
        }
    }
}
