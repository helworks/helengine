namespace helengine;

/// <summary>
/// Tracks engine objects and organizes update and render lists for processing.
/// </summary>
public class ObjectManager {
    /// <summary>
    /// Indicates whether the update loop is actively iterating updateables.
    /// </summary>
    bool updateLoopActive;

    /// <summary>
    /// Tracks the number of update passes executed so early-frame crash diagnostics can be correlated with one specific pass.
    /// </summary>
    int diagnosticUpdatePassCount;

    /// <summary>
    /// Holds update list changes requested during the active update loop.
    /// </summary>
    readonly List<PendingUpdateOperation> pendingUpdateOperations;

    /// <summary>
    /// Initializes a new object manager using the provided initialization options.
    /// </summary>
    /// <param name="settings">Initialization settings that control ordering and list sizing.</param>
    public ObjectManager(CoreInitializationOptions settings) {
        if (settings == null) {
            throw new ArgumentNullException(nameof(settings));
        }

        settings.Normalize();

        UpdateOrderLayers = settings.UpdateOrderLayers;
        RenderOrderLayers3D = settings.RenderOrderLayers3D;

        Entities = new List<Entity>();
        Updateables = new List<IUpdateable>(settings.UpdateListInitialCapacity);
        pendingUpdateOperations = new List<PendingUpdateOperation>();

        Drawables2D = new List<IDrawable2D>(settings.RenderList2DInitialCapacity);
        Drawables3D = new List<IDrawable3D>(settings.RenderList3DInitialCapacity);

        Cameras = new List<ICamera>();
        DirectionalLights = new List<DirectionalLightComponent>();
        AmbientLights = new List<AmbientLightComponent>();
        PointLights = new List<PointLightComponent>();
        SpotLights = new List<SpotLightComponent>();
        Interactables = new List<IInteractable2D>();
    }

    /// <summary>
    /// Gets the registered entities.
    /// </summary>
    public List<Entity> Entities { get; private set; }

    /// <summary>
    /// Gets the current entity-list capacity reserved by the manager.
    /// </summary>
    public int EntityCapacity => Entities.Capacity;

    /// <summary>
    /// Gets the updateables ordered by update order.
    /// </summary>
    public List<IUpdateable> Updateables { get; private set; }

    /// <summary>
    /// Gets the current update-list capacity reserved by the manager.
    /// </summary>
    public int UpdateableCapacity => Updateables.Capacity;

    /// <summary>
    /// Gets the update-pass index associated with the most recent updateable execution breadcrumb.
    /// </summary>
    public int LastUpdateableDiagnosticPass { get; private set; }

    /// <summary>
    /// Gets the update-list index associated with the most recent updateable execution breadcrumb.
    /// </summary>
    public int LastUpdateableDiagnosticIndex { get; private set; } = -1;

    /// <summary>
    /// Gets one stable hash of the updateable type name associated with the most recent updateable execution breadcrumb.
    /// </summary>
    public uint LastUpdateableDiagnosticTypeHash { get; private set; }

    /// <summary>
    /// Gets the authored scene entity id of the owner associated with the most recent updateable execution breadcrumb.
    /// </summary>
    public uint LastUpdateableDiagnosticOwnerSceneEntityId { get; private set; }

    /// <summary>
    /// Gets whether the object manager is currently iterating the update list.
    /// </summary>
    public bool IsUpdateLoopActive => updateLoopActive;

    /// <summary>
    /// Gets the number of update order layers available for helper methods.
    /// </summary>
    public byte UpdateOrderLayers { get; private set; } = 4;

    /// <summary>
    /// Gets registered 2D drawables for diagnostics.
    /// </summary>
    public List<IDrawable2D> Drawables2D { get; private set; }

    /// <summary>
    /// Gets the current 2D-drawable list capacity reserved by the manager.
    /// </summary>
    public int Drawable2DCapacity => Drawables2D.Capacity;

    /// <summary>
    /// Gets registered 3D drawables for diagnostics.
    /// </summary>
    public List<IDrawable3D> Drawables3D { get; private set; }

    /// <summary>
    /// Gets the current 3D-drawable list capacity reserved by the manager.
    /// </summary>
    public int Drawable3DCapacity => Drawables3D.Capacity;

    /// <summary>
    /// Gets the number of 3D render order layers available for helper methods.
    /// </summary>
    public byte RenderOrderLayers3D { get; private set; } = 4;

    /// <summary>
    /// Gets cameras sorted by draw order.
    /// </summary>
    public List<ICamera> Cameras { get; private set; }

    /// <summary>
    /// Gets registered directional lights in scene-registration order.
    /// </summary>
    public List<DirectionalLightComponent> DirectionalLights { get; private set; }

    /// <summary>
    /// Gets registered ambient lights in scene-registration order.
    /// </summary>
    public List<AmbientLightComponent> AmbientLights { get; private set; }

    /// <summary>
    /// Gets registered point lights in scene-registration order.
    /// </summary>
    public List<PointLightComponent> PointLights { get; private set; }

    /// <summary>
    /// Gets registered spot lights in scene-registration order.
    /// </summary>
    public List<SpotLightComponent> SpotLights { get; private set; }

    /// <summary>
    /// Gets the current camera-list capacity reserved by the manager.
    /// </summary>
    public int CameraCapacity => Cameras.Capacity;

    /// <summary>
    /// Gets registered 2D interactables.
    /// </summary>
    public List<IInteractable2D> Interactables { get; private set; }

    /// <summary>
    /// Gets the current interactable-list capacity reserved by the manager.
    /// </summary>
    public int InteractableCapacity => Interactables.Capacity;

    /// <summary>
    /// Registers a directional light for backend light selection.
    /// </summary>
    /// <param name="light">Directional light to register.</param>
    public void RegisterDirectionalLight(DirectionalLightComponent light) {
        if (ContainsReference(DirectionalLights, light)) {
            return;
        }

        DirectionalLights.Add(light);
    }

    /// <summary>
    /// Removes a directional light from backend light selection.
    /// </summary>
    /// <param name="light">Directional light to remove.</param>
    public void RemoveDirectionalLight(DirectionalLightComponent light) {
        RemoveByReference(DirectionalLights, light);
    }

    /// <summary>
    /// Registers an ambient light for backend light selection.
    /// </summary>
    /// <param name="light">Ambient light to register.</param>
    public void RegisterAmbientLight(AmbientLightComponent light) {
        if (ContainsReference(AmbientLights, light)) {
            return;
        }

        AmbientLights.Add(light);
    }

    /// <summary>
    /// Removes an ambient light from backend light selection.
    /// </summary>
    /// <param name="light">Ambient light to remove.</param>
    public void RemoveAmbientLight(AmbientLightComponent light) {
        RemoveByReference(AmbientLights, light);
    }

    /// <summary>
    /// Registers a point light for backend light selection.
    /// </summary>
    /// <param name="light">Point light to register.</param>
    public void RegisterPointLight(PointLightComponent light) {
        if (ContainsReference(PointLights, light)) {
            return;
        }

        PointLights.Add(light);
    }

    /// <summary>
    /// Removes a point light from backend light selection.
    /// </summary>
    /// <param name="light">Point light to remove.</param>
    public void RemovePointLight(PointLightComponent light) {
        RemoveByReference(PointLights, light);
    }

    /// <summary>
    /// Registers a spot light for backend light selection.
    /// </summary>
    /// <param name="light">Spot light to register.</param>
    public void RegisterSpotLight(SpotLightComponent light) {
        if (ContainsReference(SpotLights, light)) {
            return;
        }

        SpotLights.Add(light);
    }

    /// <summary>
    /// Removes a spot light from backend light selection.
    /// </summary>
    /// <param name="light">Spot light to remove.</param>
    public void RemoveSpotLight(SpotLightComponent light) {
        RemoveByReference(SpotLights, light);
    }

    /// <summary>
    /// Gets the number of pending update operations waiting for the active update loop to finish.
    /// </summary>
    public int PendingUpdateOperationCount => pendingUpdateOperations.Count;

    /// <summary>
    /// Gets the current pending-update-operation list capacity reserved by the manager.
    /// </summary>
    public int PendingUpdateOperationCapacity => pendingUpdateOperations.Capacity;

    /// <summary>
    /// Computes a render order value that maps to a desired 3D layer.
    /// </summary>
    /// <param name="layerIndex">Desired layer index.</param>
    /// <returns>Render order value aligned to the requested layer.</returns>
    public byte GetRenderOrderForLayer3D(int layerIndex) {
        return GetOrderForLayer(layerIndex, RenderOrderLayers3D);
    }

    /// <summary>
    /// Computes an update order value that maps to a desired update layer.
    /// </summary>
    /// <param name="layerIndex">Desired layer index.</param>
    /// <returns>Update order value aligned to the requested layer.</returns>
    public byte GetUpdateOrderForLayer(int layerIndex) {
        return GetOrderForLayer(layerIndex, UpdateOrderLayers);
    }

    /// <summary>
    /// Registers an interactable element for hit testing.
    /// </summary>
    /// <param name="entity">Interactable to register.</param>
    public virtual void RegisterInteractable(IInteractable2D entity) {
        if (ContainsReference(Interactables, entity)) {
            return;
        }

        Interactables.Add(entity);
    }

    /// <summary>
    /// Removes an interactable element.
    /// </summary>
    /// <param name="entity">Interactable to remove.</param>
    public virtual void RemoveInteractable(IInteractable2D entity) {
        RemoveByReference(Interactables, entity);
    }

    /// <summary>
    /// Registers an entity with the manager.
    /// </summary>
    /// <param name="entity">Entity to register.</param>
    public virtual void RegisterEntity(Entity entity) {
        if (ContainsReference(Entities, entity)) {
            return;
        }

        Entities.Add(entity);
    }

    /// <summary>
    /// Removes an entity from the manager.
    /// </summary>
    /// <param name="entity">Entity to remove.</param>
    public virtual void RemoveEntity(Entity entity) {
        RemoveByReference(Entities, entity);
    }

    /// <summary>
    /// Registers an object to be updated each frame based on its update order.
    /// </summary>
    /// <param name="entity">Updateable instance.</param>
    public virtual void RegisterForUpdate(IUpdateable entity) {
        if (entity == null) {
            return;
        }

        if (updateLoopActive) {
            QueueUpdateOperation(entity, true);
            return;
        }

        AddUpdateableToList(entity);
    }

    /// <summary>
    /// Removes an object from the update list.
    /// </summary>
    /// <param name="entity">Updateable to remove.</param>
    /// <param name="lastUpdateOrder">Update order value captured before removal.</param>
    public virtual void RemoveFromUpdate(IUpdateable entity, byte lastUpdateOrder) {
        if (entity == null) {
            return;
        }

        if (updateLoopActive) {
            QueueUpdateOperation(entity, false);
            return;
        }

        RemoveUpdateableFromList(entity);
    }

    /// <summary>
    /// Registers a 2D drawable with all matching cameras.
    /// </summary>
    /// <param name="drawable">Drawable to register.</param>
    public void RegisterForRender2D(IDrawable2D drawable) {
        if (ContainsReference(Drawables2D, drawable)) {
            return;
        }

        Drawables2D.Add(drawable);

        for (int i = 0; i < Cameras.Count; i++) {
            ICamera camera = Cameras[i];
            if (!ShouldRegisterDrawableWithCamera(drawable.Parent, camera)) {
                continue;
            }

            IRenderQueue2D list = camera.RenderQueue2D;
            list.Add(drawable);
        }
    }

    /// <summary>
    /// Removes a 2D drawable from all camera lists.
    /// </summary>
    /// <param name="drawable">Drawable to remove.</param>
    public void RemoveFromRender2D(IDrawable2D drawable) {
        RemoveByReference(Drawables2D, drawable);

        for (int i = 0; i < Cameras.Count; i++) {
            ICamera camera = Cameras[i];
            IRenderQueue2D list = camera.RenderQueue2D;
            list.Remove(drawable);
        }
    }

    /// <summary>
    /// Registers a 3D drawable with all matching cameras.
    /// </summary>
    /// <param name="drawable">Drawable to register.</param>
    public void RegisterForRender3D(IDrawable3D drawable) {
        if (ContainsReference(Drawables3D, drawable)) {
            return;
        }

        Drawables3D.Add(drawable);

        for (int i = 0; i < Cameras.Count; i++) {
            ICamera camera = Cameras[i];
            if (!ShouldRegisterDrawableWithCamera(drawable.Parent, camera)) {
                continue;
            }

            IRenderQueue3D list = camera.RenderQueue3D;
            list.Add(drawable);
        }
    }

    /// <summary>
    /// Removes a 3D drawable from all camera lists.
    /// </summary>
    /// <param name="drawable">Drawable to remove.</param>
    public void RemoveFromRender3D(IDrawable3D drawable) {
        RemoveByReference(Drawables3D, drawable);

        for (int i = 0; i < Cameras.Count; i++) {
            ICamera camera = Cameras[i];
            IRenderQueue3D list = camera.RenderQueue3D;
            list.Remove(drawable);
        }
    }

    /// <summary>
    /// Ensures the update list can fit additional items.
    /// </summary>
    /// <param name="updateOrder">Update order used to place the upcoming items.</param>
    /// <param name="additional">Number of items expected to be added.</param>
    public void ReserveUpdateCapacity(byte updateOrder, int additional) {
        if (additional < 1) {
            return;
        }

        int desired = Updateables.Count + additional;
        if (Updateables.Capacity < desired) {
            Updateables.Capacity = desired;
        }
    }

    /// <summary>
    /// Ensures 2D render lists can fit additional items for matching cameras.
    /// </summary>
    /// <param name="renderOrder">Render order used to place the upcoming items.</param>
    /// <param name="additional">Number of items expected to be added.</param>
    /// <param name="layerMask">Layer mask used to match cameras.</param>
    public void ReserveRender2DCapacity(byte renderOrder, int additional, ushort layerMask) {
        if (additional < 1) {
            return;
        }

        for (int i = 0; i < Cameras.Count; i++) {
            ICamera camera = Cameras[i];
            if ((layerMask & camera.LayerMask) == 0) {
                continue;
            }

            IRenderQueue2D list = camera.RenderQueue2D;
            list.EnsureCapacity(list.Count + additional);
        }
    }

    /// <summary>
    /// Ensures 3D render lists can fit additional items for matching cameras.
    /// </summary>
    /// <param name="renderOrder">Render order used to place the upcoming items.</param>
    /// <param name="additional">Number of items expected to be added.</param>
    /// <param name="layerMask">Layer mask used to match cameras.</param>
    public void ReserveRender3DCapacity(byte renderOrder, int additional, ushort layerMask) {
        if (additional < 1) {
            return;
        }

        for (int i = 0; i < Cameras.Count; i++) {
            ICamera camera = Cameras[i];
            if ((layerMask & camera.LayerMask) == 0) {
                continue;
            }

            IRenderQueue3D list = camera.RenderQueue3D;
            list.EnsureCapacity(list.Count + additional);
        }
    }

    /// <summary>
    /// Registers a camera for rendering and backfills existing drawables into its lists.
    /// </summary>
    /// <param name="camera">Camera to register.</param>
    public void RegisterCamera(ICamera camera) {
        if (ContainsReference(Cameras, camera)) {
            return;
        }

        InsertCameraByDrawOrder(camera);

        IRenderQueue3D list3D = camera.RenderQueue3D;
        for (int i = 0; i < Drawables3D.Count; i++) {
            IDrawable3D drawable = Drawables3D[i];
            if (ShouldRegisterDrawableWithCamera(drawable.Parent, camera)) {
                list3D.Add(drawable);
            }
        }

        IRenderQueue2D list2D = camera.RenderQueue2D;
        for (int i = 0; i < Drawables2D.Count; i++) {
            IDrawable2D drawable2D = Drawables2D[i];
            if (ShouldRegisterDrawableWithCamera(drawable2D.Parent, camera)) {
                list2D.Add(drawable2D);
            }
        }
    }

    /// <summary>
    /// Removes a camera from the draw-order list.
    /// </summary>
    /// <param name="camera">Camera to remove.</param>
    public virtual void RemoveCamera(ICamera camera) {
        if (camera == null) {
            return;
        }

        RemoveByReference(Cameras, camera);
        camera.RenderQueue3D.Clear();
        camera.RenderQueue2D.Clear();
    }

    /// <summary>
    /// Inserts a camera into the draw-order list while preserving order.
    /// </summary>
    /// <param name="camera">Camera to insert.</param>
    void InsertCameraByDrawOrder(ICamera camera) {
        int insertIndex = Cameras.Count;
        byte order = camera.CameraDrawOrder;
        for (int i = 0; i < Cameras.Count; i++) {
            if (order < Cameras[i].CameraDrawOrder) {
                insertIndex = i;
                break;
            }
        }

        Cameras.Insert(insertIndex, camera);
    }

    /// <summary>
    /// Determines whether one drawable owner should be registered with one camera after applying layer-mask and viewport-binding rules.
    /// </summary>
    /// <param name="drawableOwner">Entity that owns the drawable being considered.</param>
    /// <param name="camera">Camera that may receive the drawable.</param>
    /// <returns>True when the drawable should appear in the camera queue; otherwise false.</returns>
    bool ShouldRegisterDrawableWithCamera(Entity drawableOwner, ICamera camera) {
        if (drawableOwner == null || camera == null) {
            return false;
        }

        if ((drawableOwner.LayerMask & camera.LayerMask) == 0) {
            return false;
        }

        ICameraBoundViewportOwner viewportComponent = ResolveNearestViewportComponent(drawableOwner);
        if (viewportComponent == null) {
            return true;
        }

        CameraComponent boundCamera = viewportComponent.GetBoundCameraComponent();
        if (viewportComponent.BindingMode == ViewportComponent.ExplicitCameraBindingMode) {
            return ReferenceEquals(boundCamera, camera);
        }

        if (viewportComponent.BindingMode == ViewportComponent.AncestorCameraBindingMode && boundCamera != null) {
            return ReferenceEquals(boundCamera, camera);
        }

        return true;
    }

    /// <summary>
    /// Resolves the nearest viewport component that governs one drawable owner's subtree, when present.
    /// </summary>
    /// <param name="entity">Drawable owner whose ancestor chain should be inspected.</param>
    /// <returns>Nearest viewport component for the subtree, or null when no viewport owns the drawable.</returns>
    ICameraBoundViewportOwner ResolveNearestViewportComponent(Entity entity) {
        Entity current = entity;
        while (current != null) {
            if (current.Components != null) {
                for (int componentIndex = 0; componentIndex < current.Components.Count; componentIndex++) {
                    if (current.Components[componentIndex] is ICameraBoundViewportOwner viewportComponent) {
                        return viewportComponent;
                    }
                }
            }

            current = current.Parent;
        }

        return null;
    }

    /// <summary>
    /// Updates all registered updateables in order.
    /// </summary>
    public virtual void Update() {
        diagnosticUpdatePassCount++;
        LastUpdateableDiagnosticPass = diagnosticUpdatePassCount;
        LastUpdateableDiagnosticIndex = -1;
        LastUpdateableDiagnosticTypeHash = 0u;
        LastUpdateableDiagnosticOwnerSceneEntityId = 0u;

        try {
            updateLoopActive = true;

            for (int i = 0; i < Updateables.Count; i++) {
                IUpdateable item = Updateables[i];
                LastUpdateableDiagnosticPass = diagnosticUpdatePassCount;
                LastUpdateableDiagnosticIndex = i;
                LastUpdateableDiagnosticTypeHash = ComputeStableTypeNameHash(item);
                LastUpdateableDiagnosticOwnerSceneEntityId = ResolveUpdateableOwnerSceneEntityId(item);
                item.Update();
            }
        } finally {
            updateLoopActive = false;
        }

        ApplyPendingUpdateOperations();
    }

    /// <summary>
    /// Adds an updateable to the ordered update list.
    /// </summary>
    /// <param name="entity">Updateable to register.</param>
    void AddUpdateableToList(IUpdateable entity) {
        int insertIndex = FindUpdateInsertIndex(entity.UpdateOrder);
        Updateables.Insert(insertIndex, entity);
    }

    /// <summary>
    /// Removes an updateable from the ordered update list.
    /// </summary>
    /// <param name="entity">Updateable to remove.</param>
    void RemoveUpdateableFromList(IUpdateable entity) {
        RemoveByReference(Updateables, entity);
    }

    /// <summary>
    /// Queues a pending update operation when modifications occur during the update loop.
    /// </summary>
    /// <param name="entity">Updateable to modify.</param>
    /// <param name="isAdd">True for registration, false for removal.</param>
    void QueueUpdateOperation(IUpdateable entity, bool isAdd) {
        pendingUpdateOperations.Add(new PendingUpdateOperation(entity, isAdd));
    }

    /// <summary>
    /// Applies queued update operations after the update loop completes.
    /// </summary>
    void ApplyPendingUpdateOperations() {
        if (pendingUpdateOperations.Count == 0) {
            return;
        }

        for (int i = 0; i < pendingUpdateOperations.Count; i++) {
            PendingUpdateOperation op = pendingUpdateOperations[i];
            if (op.IsAdd) {
                AddUpdateableToList(op.Entity);
            } else {
                RemoveUpdateableFromList(op.Entity);
            }
        }

        pendingUpdateOperations.Clear();
    }

    /// <summary>
    /// Finds the insertion index for a given update order.
    /// </summary>
    /// <param name="updateOrder">Update order to insert.</param>
    /// <returns>Insertion index.</returns>
    int FindUpdateInsertIndex(byte updateOrder) {
        for (int i = 0; i < Updateables.Count; i++) {
            if (updateOrder < Updateables[i].UpdateOrder) {
                return i;
            }
        }

        return Updateables.Count;
    }

    /// <summary>
    /// Determines whether a list already contains the given reference instance.
    /// </summary>
    /// <typeparam name="T">Reference type stored in the list.</typeparam>
    /// <param name="list">List to search.</param>
    /// <param name="item">Item reference to locate.</param>
    /// <returns>True when the exact instance is already registered; otherwise false.</returns>
    static bool ContainsReference<T>(List<T> list, T item) where T : class {
        for (int index = 0; index < list.Count; index++) {
            if (ReferenceEquals(list[index], item)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Removes every matching item from a list using reference equality so duplicated registrations cannot survive one removal request.
    /// </summary>
    /// <typeparam name="T">Reference type stored in the list.</typeparam>
    /// <param name="list">List to search.</param>
    /// <param name="item">Item to remove.</param>
    /// <returns>True when at least one item was removed.</returns>
    static bool RemoveByReference<T>(List<T> list, T item) where T : class {
        bool removed = false;
        for (int index = list.Count - 1; index >= 0; index--) {
            if (!ReferenceEquals(list[index], item)) {
                continue;
            }

            list.RemoveAt(index);
            removed = true;
        }

        return removed;
    }

    /// <summary>
    /// Resolves one stable numeric hash for the current updateable type name so hard-crash diagnostics can identify the active managed object.
    /// </summary>
    /// <param name="item">Updateable about to execute.</param>
    /// <returns>Stable non-cryptographic hash of the type name.</returns>
    static uint ComputeStableTypeNameHash(IUpdateable item) {
        string typeName = ResolveUpdateableTypeName(item);
        uint hash = 2166136261u;
        for (int index = 0; index < typeName.Length; index++) {
            hash ^= typeName[index];
            hash *= 16777619u;
        }

        return hash;
    }

    /// <summary>
    /// Resolves the authored scene entity id of the current updateable owner when the updateable is one component.
    /// </summary>
    /// <param name="item">Updateable about to execute.</param>
    /// <returns>Owner scene entity id, or <c>0</c> when unavailable.</returns>
    static uint ResolveUpdateableOwnerSceneEntityId(IUpdateable item) {
        if (item is Component component) {
            return ResolveSceneEntityRuntimeIdOrZero(component.Parent);
        }

        return 0u;
    }

    /// <summary>
    /// Resolves the concrete updateable type name when the runtime object is one component and falls back to the interface token name otherwise.
    /// </summary>
    /// <param name="item">Updateable about to execute.</param>
    /// <returns>Concrete runtime type name when available.</returns>
    static string ResolveUpdateableTypeName(IUpdateable item) {
        if (item is Component component) {
            return component.GetType().Name;
        }

        return item == null ? string.Empty : item.GetType().Name;
    }

    /// <summary>
    /// Resolves the authored scene entity id attached to one runtime entity when that metadata component is present.
    /// </summary>
    /// <param name="entity">Entity to inspect.</param>
    /// <returns>Authored scene entity id, or <c>0</c> when unavailable.</returns>
    static uint ResolveSceneEntityRuntimeIdOrZero(Entity entity) {
        if (entity == null || entity.Components == null) {
            return 0u;
        }

        for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
            if (entity.Components[componentIndex] is SceneEntityRuntimeIdComponent runtimeIdComponent) {
                return runtimeIdComponent.SceneEntityId;
            }
        }

        return 0u;
    }

    /// <summary>
    /// Computes an order value aligned to a layer count.
    /// </summary>
    /// <param name="layerIndex">Desired layer index.</param>
    /// <param name="layerCount">Total number of layers.</param>
    /// <returns>Order value between 0 and 255.</returns>
    static byte GetOrderForLayer(int layerIndex, int layerCount) {
        if (layerCount < 1) {
            throw new ArgumentOutOfRangeException(nameof(layerCount));
        }

        int clamped = layerIndex;
        if (clamped < 0) {
            clamped = 0;
        } else if (clamped >= layerCount) {
            clamped = layerCount - 1;
        }

        if (layerCount == 1) {
            return 0;
        }

        double step = 255.0 / (layerCount - 1);
        int value = (int)Math.Round(clamped * step, MidpointRounding.AwayFromZero);
        if (value < 0) {
            return 0;
        }
        if (value > 255) {
            return 255;
        }

        return (byte)value;
    }
}
