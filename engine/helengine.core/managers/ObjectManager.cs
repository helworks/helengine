namespace helengine;

/// <summary>
/// Tracks engine objects and organizes update and render buckets for efficient processing.
/// </summary>
public class ObjectManager {
    /// <summary>
    /// Indicates whether the update loop is actively iterating buckets.
    /// </summary>
    bool updateLoopActive;

    /// <summary>
    /// Holds update bucket changes requested during the active update loop.
    /// </summary>
    List<PendingUpdateOperation> pendingUpdateOperations;

    /// <summary>
    /// Initializes a new object manager using the provided initialization options.
    /// </summary>
    /// <param name="settings">Initialization settings that control bucket sizing.</param>
    public ObjectManager(CoreInitializationOptions settings) {
        if (settings == null) {
            throw new ArgumentNullException(nameof(settings));
        }

        settings.Normalize();

        TotalUpdateBuckets = settings.TotalUpdateBuckets;
        TotalBuckets2D = settings.TotalBuckets2D;
        TotalBuckets3D = settings.TotalBuckets3D;
        TotalVariants3D = settings.TotalVariants3D;
        Entities = new List<Entity>();
        pendingUpdateOperations = new List<PendingUpdateOperation>();

        UpdateEntities = new UpdateBucket[TotalUpdateBuckets];
        for (int i = 0; i < TotalUpdateBuckets; i++) {
            UpdateEntities[i] = new UpdateBucket(settings.UpdateBucketInitialCapacity[i]);
        }

        Drawables2D = new List<IDrawable2D>();

        Drawables3D = new List<IDrawable3D>();

        Cameras = new List<ICamera>();

        Interactables = new List<IInteractable2D>();
    }

    /// <summary>
    /// Gets the registered entities.
    /// </summary>
    public List<Entity> Entities { get; private set; }

    /// <summary>
    /// Gets update buckets used to group updateables by order.
    /// </summary>
    public UpdateBucket[] UpdateEntities { get; private set; }

    /// <summary>
    /// Gets the number of update buckets.
    /// </summary>
    public byte TotalUpdateBuckets { get; private set; } = 4;

    /// <summary>
    /// Gets registered 2D drawables for diagnostics.
    /// </summary>
    public List<IDrawable2D> Drawables2D { get; private set; }

    /// <summary>
    /// Gets the number of 2D render buckets.
    /// </summary>
    public byte TotalBuckets2D { get; private set; } = 4;

    /// <summary>
    /// Gets registered 3D drawables for diagnostics.
    /// </summary>
    public List<IDrawable3D> Drawables3D { get; private set; }

    /// <summary>
    /// Gets the number of 3D render buckets.
    /// </summary>
    public byte TotalBuckets3D { get; private set; } = 4;

    /// <summary>
    /// Gets the number of 3D variants (bins) supported.
    /// </summary>
    public byte TotalVariants3D { get; private set; } = 4;

    /// <summary>
    /// Gets cameras sorted by draw order.
    /// </summary>
    public List<ICamera> Cameras { get; private set; }

    /// <summary>
    /// Gets registered 2D interactables.
    /// </summary>
    public List<IInteractable2D> Interactables { get; private set; }

    /// <summary>
    /// Computes a render order value that maps to a desired 2D bucket.
    /// </summary>
    /// <param name="bucketIndex">Desired bucket index.</param>
    /// <returns>Render order value that maps into the requested bucket.</returns>
    public byte GetRenderOrderForBucket2D(int bucketIndex) {
        return RenderBucketUtils.GetRenderOrderForBucket(bucketIndex, TotalBuckets2D);
    }

    /// <summary>
    /// Computes a render order value that maps to a desired 3D bucket.
    /// </summary>
    /// <param name="bucketIndex">Desired bucket index.</param>
    /// <returns>Render order value that maps into the requested bucket.</returns>
    public byte GetRenderOrderForBucket3D(int bucketIndex) {
        return RenderBucketUtils.GetRenderOrderForBucket(bucketIndex, TotalBuckets3D);
    }

    /// <summary>
    /// Computes an update order value that maps to a desired update bucket.
    /// </summary>
    /// <param name="bucketIndex">Desired bucket index.</param>
    /// <returns>Update order value that maps into the requested bucket.</returns>
    public byte GetUpdateOrderForBucket(int bucketIndex) {
        return RenderBucketUtils.GetRenderOrderForBucket(bucketIndex, TotalUpdateBuckets);
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
            QueueUpdateOperation(entity, true, entity.UpdateOrder);
            return;
        }

        AddUpdateableToBucket(entity, entity.UpdateOrder);
    }

    /// <summary>
    /// Removes an object from the update buckets.
    /// </summary>
    /// <param name="entity">Updateable to remove.</param>
    /// <param name="lastUpdateOrder">Update order value captured before removal.</param>
    public virtual void RemoveFromUpdate(IUpdateable entity, byte lastUpdateOrder) {
        if (entity == null) {
            return;
        }

        if (updateLoopActive) {
            QueueUpdateOperation(entity, false, lastUpdateOrder);
            return;
        }

        RemoveUpdateableFromBucket(entity, lastUpdateOrder);
    }

    /// <summary>
    /// Registers a 2D drawable with all matching cameras.
    /// </summary>
    /// <param name="drawable">Drawable to register.</param>
    public void RegisterForRender2D(IDrawable2D drawable) {
        // Maintain a side list for diagnostics, but membership uses per-camera dense buckets of references
        Drawables2D.Add(drawable);

        int bucket = RenderBucketUtils.GetBucketIndex(drawable.RenderOrder2D, TotalBuckets2D);
        for (int i = 0; i < Cameras.Count; i++) {
            ICamera camera = Cameras[i];
            if (camera is not CameraComponent cam) {
                continue;
            }
            if ((drawable.Parent.LayerMask & cam.LayerMask) == 0) {
                continue;
            }

            var reg = cam.Get2DRegistry();
            reg.Buckets[bucket].Add(drawable, out int pos);
            reg.Map[drawable] = new Index2D(bucket, pos);
        }
    }

    /// <summary>
    /// Removes a 2D drawable from all camera buckets.
    /// </summary>
    /// <param name="drawable">Drawable to remove.</param>
    public void RemoveFromRender2D(IDrawable2D drawable) {
        // Keep Drawables2D as a diagnostic list but remove reference if present
        Drawables2D.Remove(drawable);

        for (int i = 0; i < Cameras.Count; i++) {
            ICamera camera = Cameras[i];
            if (camera is not CameraComponent cam) {
                continue;
            }
            var reg = cam.Get2DRegistry();
            if (reg.Map.TryGetValue(drawable, out var idx)) {
                var bucket = reg.Buckets[idx.Bucket];
                IDrawable2D swapped = bucket.RemoveSwapAt(idx.Pos);
                if (swapped != null) {
                    reg.Map[swapped] = new Index2D(idx.Bucket, idx.Pos);
                }
                reg.Map.Remove(drawable);
            }
        }
    }

    /// <summary>
    /// Registers a 3D drawable with all matching cameras.
    /// </summary>
    /// <param name="drawable">Drawable to register.</param>
    public void RegisterForRender3D(IDrawable3D drawable) {
        Drawables3D.Add(drawable);

        int bucket = RenderBucketUtils.GetBucketIndex(drawable.RenderOrder3D, TotalBuckets3D);
        int variant = drawable.Variant;

        for (int i = 0; i < Cameras.Count; i++) {
            ICamera camera = Cameras[i];
            if (camera is not CameraComponent cam) {
                continue;
            }
            if ((drawable.Parent.LayerMask & cam.LayerMask) == 0) {
                continue;
            }

            var reg = cam.Get3DRegistry();
            int bin = getStateBin3D(drawable.Model, reg.BinsPerBucket);
            reg.Buckets[variant][bucket][bin].Add(drawable, out int pos);
            reg.Map[drawable] = new Index3D(variant, bucket, bin, pos);
        }
    }

    /// <summary>
    /// Removes a 3D drawable from all camera buckets.
    /// </summary>
    /// <param name="drawable">Drawable to remove.</param>
    public void RemoveFromRender3D(IDrawable3D drawable) {
        Drawables3D.Remove(drawable);

        for (int i = 0; i < Cameras.Count; i++) {
            ICamera camera = Cameras[i];
            if (camera is not CameraComponent cam) {
                continue;
            }
            var reg = cam.Get3DRegistry();
            if (reg.Map.TryGetValue(drawable, out var idx)) {
                var swapped = reg.Buckets[idx.Variant][idx.Bucket][idx.Bin].RemoveSwapAt(idx.Pos);
                if (swapped != null) {
                    reg.Map[(IDrawable3D)swapped] = new Index3D(idx.Variant, idx.Bucket, idx.Bin, idx.Pos);
                }
                reg.Map.Remove(drawable);
            }
        }
    }

    /// <summary>
    /// Ensures the update bucket for the given order can fit additional items.
    /// </summary>
    /// <param name="updateOrder">Update order used to select the bucket.</param>
    /// <param name="additional">Number of items expected to be added.</param>
    public void ReserveUpdateCapacity(byte updateOrder, int additional) {
        if (additional < 1) {
            return;
        }

        int bucket = RenderBucketUtils.GetBucketIndex(updateOrder, TotalUpdateBuckets);
        UpdateBucket updateBucket = UpdateEntities[bucket];
        updateBucket.EnsureCapacity(updateBucket.Count + additional);
    }

    /// <summary>
    /// Ensures 2D render buckets can fit additional items for matching cameras.
    /// </summary>
    /// <param name="renderOrder">Render order used to select the bucket.</param>
    /// <param name="additional">Number of items expected to be added.</param>
    /// <param name="layerMask">Layer mask used to match cameras.</param>
    public void ReserveRender2DCapacity(byte renderOrder, int additional, ushort layerMask) {
        if (additional < 1) {
            return;
        }

        int bucket = RenderBucketUtils.GetBucketIndex(renderOrder, TotalBuckets2D);
        for (int i = 0; i < Cameras.Count; i++) {
            ICamera camera = Cameras[i];
            if (camera is not CameraComponent cam) {
                continue;
            }
            if ((layerMask & cam.LayerMask) == 0) {
                continue;
            }

            var reg = cam.Get2DRegistry();
            RenderBucket2D renderBucket = reg.Buckets[bucket];
            renderBucket.EnsureCapacity(renderBucket.Count + additional);
        }
    }

    /// <summary>
    /// Ensures 3D render buckets can fit additional items for matching cameras.
    /// </summary>
    /// <param name="renderOrder">Render order used to select the bucket.</param>
    /// <param name="variant">Render pipeline variant to target.</param>
    /// <param name="model">Model used to determine the state bin.</param>
    /// <param name="additional">Number of items expected to be added.</param>
    /// <param name="layerMask">Layer mask used to match cameras.</param>
    public void ReserveRender3DCapacity(byte renderOrder, byte variant, RuntimeModel model, int additional, ushort layerMask) {
        if (additional < 1) {
            return;
        }

        int bucket = RenderBucketUtils.GetBucketIndex(renderOrder, TotalBuckets3D);
        for (int i = 0; i < Cameras.Count; i++) {
            ICamera camera = Cameras[i];
            if (camera is not CameraComponent cam) {
                continue;
            }
            if ((layerMask & cam.LayerMask) == 0) {
                continue;
            }

            var reg = cam.Get3DRegistry();
            if (reg.BinsPerBucket < 1) {
                continue;
            }

            int variantIndex = variant;
            if (variantIndex < 0 || variantIndex >= reg.Buckets.Length) {
                continue;
            }

            int bin = getStateBin3D(model, reg.BinsPerBucket);
            RenderBucket3D renderBucket = reg.Buckets[variantIndex][bucket][bin];
            renderBucket.EnsureCapacity(renderBucket.Count + additional);
        }
    }

    /// <summary>
    /// Registers a camera for rendering and backfills existing drawables into its registries.
    /// </summary>
    /// <param name="camera">Camera to register.</param>
    public void RegisterCamera(ICamera camera) {
        InsertCameraByDrawOrder(camera);

        if (camera is not CameraComponent camComp) {
            return;
        }

        // Backfill existing 3D drawables for this camera (by reference)
        var reg3 = camComp.Get3DRegistry();
        for (int i = 0; i < Drawables3D.Count; i++) {
            IDrawable3D drawable = Drawables3D[i];
            if ((drawable.Parent.LayerMask & camera.LayerMask) != 0) {
                int drawBucket3D = RenderBucketUtils.GetBucketIndex(drawable.RenderOrder3D, TotalBuckets3D);
                int variant = drawable.Variant;
                int bin = getStateBin3D(drawable.Model, reg3.BinsPerBucket);
                reg3.Buckets[variant][drawBucket3D][bin].Add(drawable, out int pos3);
                reg3.Map[drawable] = new Index3D(variant, drawBucket3D, bin, pos3);
            }
        }

        // Backfill existing 2D drawables for this camera with references
        var reg = camComp.Get2DRegistry();
        for (int i = 0; i < Drawables2D.Count; i++) {
            IDrawable2D drawable2D = Drawables2D[i];
            if ((drawable2D.Parent.LayerMask & camera.LayerMask) != 0) {
                int drawBucket2D = RenderBucketUtils.GetBucketIndex(drawable2D.RenderOrder2D, TotalBuckets2D);
                reg.Buckets[drawBucket2D].Add(drawable2D, out int pos);
                reg.Map[drawable2D] = new Index2D(drawBucket2D, pos);
            }
        }
    }

    /// <summary>
    /// Removes a camera and its buckets.
    /// </summary>
    /// <param name="camera">Camera to remove.</param>
    public virtual void RemoveCamera(ICamera camera) {
        Cameras.Remove(camera);
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
    /// Updates all registered updateables in bucket order.
    /// </summary>
    public virtual void Update() {
        try {
            updateLoopActive = true;

            for (int i = 0; i < TotalUpdateBuckets; i++) {
                UpdateBucket bucket = UpdateEntities[i];
                for (int j = 0; j < bucket.Count; j++) {
                    IUpdateable item = bucket.Items[j];
                    item.Update();
                }
            }
        } finally {
            updateLoopActive = false;
        }

        ApplyPendingUpdateOperations();
    }

    /// <summary>
    /// Adds an updateable to its bucket using the supplied update order.
    /// </summary>
    /// <param name="entity">Updateable to register.</param>
    /// <param name="updateOrder">Order value to bucket against.</param>
    void AddUpdateableToBucket(IUpdateable entity, byte updateOrder) {
        int bucket = RenderBucketUtils.GetBucketIndex(updateOrder, TotalUpdateBuckets);
        UpdateBucket updateBucket = UpdateEntities[bucket];
        updateBucket.Add(entity);
    }

    /// <summary>
    /// Removes an updateable from its bucket using the supplied update order.
    /// </summary>
    /// <param name="entity">Updateable to remove.</param>
    /// <param name="updateOrder">Order value to bucket against.</param>
    void RemoveUpdateableFromBucket(IUpdateable entity, byte updateOrder) {
        int bucket = RenderBucketUtils.GetBucketIndex(updateOrder, TotalUpdateBuckets);
        UpdateBucket updateBucket = UpdateEntities[bucket];
        updateBucket.Remove(entity);
    }

    /// <summary>
    /// Queues a pending update operation when modifications occur during the update loop.
    /// </summary>
    /// <param name="entity">Updateable to modify.</param>
    /// <param name="isAdd">True for registration, false for removal.</param>
    /// <param name="updateOrder">Update order captured at the time of the request.</param>
    void QueueUpdateOperation(IUpdateable entity, bool isAdd, byte updateOrder) {
        pendingUpdateOperations.Add(new PendingUpdateOperation(entity, isAdd, updateOrder));
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
                AddUpdateableToBucket(op.Entity, op.UpdateOrder);
            } else {
                RemoveUpdateableFromBucket(op.Entity, op.UpdateOrder);
            }
        }

        pendingUpdateOperations.Clear();
    }

    /// <summary>
    /// Calculates a stable bin for a model based on its identifier.
    /// </summary>
    /// <param name="model">Model to hash.</param>
    /// <param name="bins">Number of bins available.</param>
    /// <returns>Bin index.</returns>
    private static int getStateBin3D(RuntimeModel model, int bins) {
        if (model == null || bins <= 1) {
            return 0;
        }

        int h = model.Id != null ? model.Id.GetHashCode() : 0;
        uint uh = unchecked((uint)h);
        return (int)(uh % (uint)bins);
    }
}
