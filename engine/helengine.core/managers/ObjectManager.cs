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
        Interactables.Add(entity);
    }

    /// <summary>
    /// Removes an interactable element.
    /// </summary>
    /// <param name="entity">Interactable to remove.</param>
    public virtual void RemoveInteractable(IInteractable2D entity) {
        Interactables.Remove(entity);
    }

    /// <summary>
    /// Registers an entity with the manager.
    /// </summary>
    /// <param name="entity">Entity to register.</param>
    public virtual void RegisterEntity(Entity entity) {
        Entities.Add(entity);
    }

    /// <summary>
    /// Removes an entity from the manager.
    /// </summary>
    /// <param name="entity">Entity to remove.</param>
    public virtual void RemoveEntity(Entity entity) {
        Entities.Remove(entity);
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
        Drawables2D.Add(drawable);

        for (int i = 0; i < Cameras.Count; i++) {
            ICamera camera = Cameras[i];
            if ((drawable.Parent.LayerMask & camera.LayerMask) == 0) {
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
        Drawables3D.Add(drawable);

        for (int i = 0; i < Cameras.Count; i++) {
            ICamera camera = Cameras[i];
            if ((drawable.Parent.LayerMask & camera.LayerMask) == 0) {
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
        InsertCameraByDrawOrder(camera);

        IRenderQueue3D list3D = camera.RenderQueue3D;
        for (int i = 0; i < Drawables3D.Count; i++) {
            IDrawable3D drawable = Drawables3D[i];
            if ((drawable.Parent.LayerMask & camera.LayerMask) != 0) {
                list3D.Add(drawable);
            }
        }

        IRenderQueue2D list2D = camera.RenderQueue2D;
        for (int i = 0; i < Drawables2D.Count; i++) {
            IDrawable2D drawable2D = Drawables2D[i];
            if ((drawable2D.Parent.LayerMask & camera.LayerMask) != 0) {
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

        Cameras.Remove(camera);
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
    /// Updates all registered updateables in order.
    /// </summary>
    public virtual void Update() {
        try {
            updateLoopActive = true;

            for (int i = 0; i < Updateables.Count; i++) {
                IUpdateable item = Updateables[i];
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
    /// Removes the first matching item from a list using reference equality.
    /// </summary>
    /// <typeparam name="T">Reference type stored in the list.</typeparam>
    /// <param name="list">List to search.</param>
    /// <param name="item">Item to remove.</param>
    /// <returns>True when an item was removed.</returns>
    static bool RemoveByReference<T>(List<T> list, T item) where T : class {
        for (int i = 0; i < list.Count; i++) {
            if (ReferenceEquals(list[i], item)) {
                list.RemoveAt(i);
                return true;
            }
        }

        return false;
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
