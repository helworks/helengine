namespace helengine.editor {
    /// <summary>
    /// Marshals shader-build notifications from the background shader watcher onto the editor frame
    /// thread. The watcher publishes rebuilt packages from its own thread, but runtime renderer
    /// shader state may only be invalidated while the editor owns the frame, so notifications are
    /// queued on publication and applied when the session drains the queue during its update.
    /// </summary>
    public sealed class EditorShaderBuildNotificationQueue {
        /// <summary>
        /// Initial capacity reserved for pending shader-build notifications.
        /// </summary>
        const int PendingNotificationInitialCapacity = 8;

        /// <summary>
        /// Guards the pending-notification queue against concurrent watcher and frame-thread access.
        /// </summary>
        readonly object PendingNotificationLock = new object();

        /// <summary>
        /// Rebuilt shader packages waiting to refresh runtime renderer shader resources.
        /// </summary>
        readonly Queue<KeyValuePair<string, string>> PendingNotifications = new Queue<KeyValuePair<string, string>>(PendingNotificationInitialCapacity);

        /// <summary>
        /// Loads shader assets back out of rebuilt shader packages.
        /// </summary>
        EditorShaderPackageService ShaderPackageService { get; }

        /// <summary>
        /// Renderer whose shader resources are invalidated when a package is rebuilt.
        /// </summary>
        RenderManager3D Render3D { get; }

        /// <summary>
        /// Initializes one notification queue bound to the session's package service and renderer.
        /// </summary>
        /// <param name="shaderPackageService">Session-owned shader package resolver.</param>
        /// <param name="render3D">Renderer whose shader resources must be refreshed.</param>
        public EditorShaderBuildNotificationQueue(EditorShaderPackageService shaderPackageService, RenderManager3D render3D) {
            if (shaderPackageService == null) {
                throw new ArgumentNullException(nameof(shaderPackageService));
            }
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }

            ShaderPackageService = shaderPackageService;
            Render3D = render3D;
        }

        /// <summary>
        /// Queues one shader-build notification published by the background shader watcher.
        /// A notification without a package path carries nothing to reload and is discarded.
        /// </summary>
        /// <param name="shaderName">Shader name that was rebuilt.</param>
        /// <param name="packagePath">Package path containing the updated shader.</param>
        public void Enqueue(string shaderName, string packagePath) {
            if (string.IsNullOrWhiteSpace(packagePath)) {
                return;
            }

            string resolvedShaderName;
            if (shaderName == null) {
                resolvedShaderName = string.Empty;
            } else {
                resolvedShaderName = shaderName;
            }

            lock (PendingNotificationLock) {
                PendingNotifications.Enqueue(new KeyValuePair<string, string>(resolvedShaderName, packagePath));
            }
        }

        /// <summary>
        /// Applies every queued shader-build notification on the calling frame thread. One shader
        /// that fails to reload is reported and skipped so a single bad edit cannot stall the
        /// reload of the shaders queued behind it.
        /// </summary>
        public void ProcessPending() {
            while (true) {
                KeyValuePair<string, string> notification;
                lock (PendingNotificationLock) {
                    if (PendingNotifications.Count == 0) {
                        return;
                    }

                    notification = PendingNotifications.Dequeue();
                }

                try {
                    string shaderName = notification.Key;
                    string packagePath = notification.Value;
                    ShaderAsset shaderAsset = ShaderPackageService.LoadShaderAssetFromPackage(packagePath);
                    string shaderAssetId;
                    if (string.IsNullOrWhiteSpace(shaderAsset.Id)) {
                        shaderAssetId = shaderName;
                    } else {
                        shaderAssetId = shaderAsset.Id;
                    }

                    Render3D.InvalidateShaderResources(shaderAssetId, shaderAsset);
                } catch (Exception ex) {
                    Logger.WriteError($"Shader reload failed for '{notification.Key}': {ex.Message}");
                }
            }
        }
    }
}
