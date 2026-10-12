namespace helengine {
    /// <summary>Resolves authored UI window scope from hierarchy-owned cameras and viewport providers.</summary>
    public static class CameraWindowLayoutUtils {
        /// <summary>Finds the closest camera in the entity chain, including the supplied entity.</summary>
        /// <param name="entity">Layout owner whose hierarchy defines its camera scope.</param>
        /// <returns>A borrowed ancestor camera, or null for explicitly unbound primary-window UI.</returns>
        [NativeBorrowedReturn]
        public static CameraComponent FindAncestorCamera(Entity entity) {
            Entity current = entity;
            while (current != null) {
                if (current.Components != null) {
                    for (int index = 0; index < current.Components.Count; index++) {
                        if (current.Components[index] is CameraComponent camera) {
                            return camera;
                        }
                    }
                }
                current = current.Parent;
            }
            return null;
        }

        /// <summary>Finds the closest authored viewport whose dimensions govern a fitted subtree.</summary>
        /// <param name="entity">Layout owner whose hierarchy is searched.</param>
        /// <returns>A borrowed viewport, or null when the camera directly defines the layout scope.</returns>
        [NativeBorrowedReturn]
        public static ViewportComponent FindAncestorViewport(Entity entity) {
            Entity current = entity;
            while (current != null) {
                if (current.Components != null) {
                    for (int index = 0; index < current.Components.Count; index++) {
                        if (current.Components[index] is ViewportComponent viewport) {
                            return viewport;
                        }
                    }
                }
                current = current.Parent;
            }
            return null;
        }

        /// <summary>Resolves a layout's host tag, returning empty when fixed or render-target bounds are independent of host windows.</summary>
        /// <param name="viewport">Optional authored viewport defining explicit layout policy.</param>
        /// <param name="camera">Optional nearest camera when no viewport policy is present.</param>
        /// <returns>The governing host tag, main for unbound/shared UI, or empty for host-independent bounds.</returns>
        public static string ResolveWindowTag(ViewportComponent viewport, CameraComponent camera) {
            if (viewport != null) {
                if (viewport.BindingMode == ViewportComponent.FixedBindingMode) {
                    return string.Empty;
                } else if (viewport.BindingMode == ViewportComponent.ScreenBindingMode) {
                    return "main";
                }
                camera = viewport.GetBoundCameraComponent();
                if (camera == null) {
                    return string.Empty;
                }
            }
            if (camera == null) {
                return "main";
            } else if (camera.RenderTarget != null) {
                return string.Empty;
            }
            return camera.OutputWindowTag.Length == 0 ? "main" : camera.OutputWindowTag;
        }

        /// <summary>Resolves camera layout pixels, allowing render-target cameras to use their own surface dimensions.</summary>
        /// <param name="renderer">Initialized renderer owning the host window registry.</param>
        /// <param name="camera">Optional camera; null explicitly uses the primary layout reference.</param>
        /// <returns>The camera surface size or the primary layout size for unbound UI.</returns>
        public static int2 ResolveCameraSize(RenderManager3D renderer, CameraComponent camera) {
            if (renderer == null) {
                throw new ArgumentNullException(nameof(renderer));
            }
            if (camera == null) {
                return renderer.MainWindowSize;
            } else if (camera.RenderTarget != null && camera.RenderTarget.Width > 0 && camera.RenderTarget.Height > 0) {
                return new int2(camera.RenderTarget.Width, camera.RenderTarget.Height);
            }
            return renderer.GetCameraLayoutSize(camera);
        }
    }
}
