namespace helengine;

/// <summary>
/// Tracks engine objects and organizes update and render lists for efficient processing.
/// </summary>
public class ObjectManager {
    /// <summary>
    /// Initializes a new object manager and allocates buckets for updates, rendering, and cameras.
    /// </summary>
    public ObjectManager() : this(new CoreInitializationOptions()) { }

    /// <summary>
    /// Initializes a new object manager using the provided initialization options.
    /// </summary>
    /// <param name="options">Initialization options that control bucket sizing.</param>
    public ObjectManager(CoreInitializationOptions options) {
        CoreInitializationOptions settings = options ?? new CoreInitializationOptions();
        settings.Normalize();

        TotalUpdateBuckets = settings.TotalUpdateBuckets;
        TotalBuckets2D = settings.TotalBuckets2D;
        TotalBuckets3D = settings.TotalBuckets3D;
        TotalVariants3D = settings.TotalVariants3D;
        TotalCameraBuckets = settings.TotalCameraBuckets;

        Entities = new List<Entity>();

        UpdateEntities = new UpdateBucket[TotalUpdateBuckets];
        for (int i = 0; i < TotalUpdateBuckets; i++) {
            UpdateEntities[i] = new UpdateBucket(settings.UpdateBucketInitialCapacity[i]);
        }

        Drawables2D = new List<IDrawable2D>();

        Drawables3D = new List<IDrawable3D>();

        Cameras = new List<ICamera>[TotalCameraBuckets];
        for (int i = 0; i < TotalCameraBuckets; i++) {
            Cameras[i] = new List<ICamera>();
        }

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
    /// Gets grouped cameras by draw order.
    /// </summary>
    public List<ICamera>[] Cameras { get; private set; }

    /// <summary>
    /// Gets the number of camera buckets.
    /// </summary>
    public byte TotalCameraBuckets { get; private set; } = 3;

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
        RemoveFromUpdate(entity);

        int bucket = RenderBucketUtils.GetBucketIndex(entity.UpdateOrder, TotalUpdateBuckets);
        UpdateBucket updateBucket = UpdateEntities[bucket];
        updateBucket.Add(entity);
    }

    /// <summary>
    /// Removes an object from the update buckets.
    /// </summary>
    /// <param name="entity">Updateable to remove.</param>
    public virtual void RemoveFromUpdate(IUpdateable entity) {
        if (entity == null) {
            return;
        }

        for (int i = 0; i < TotalUpdateBuckets; i++) {
            UpdateBucket updateBucket = UpdateEntities[i];
            if (updateBucket.Remove(entity)) {
                return;
            }
        }
    }

    /// <summary>
    /// Registers a 2D drawable with all matching cameras.
    /// </summary>
    /// <param name="drawable">Drawable to register.</param>
    public void RegisterForRender2D(IDrawable2D drawable) {
        Drawables2D.Add(drawable);

        for (int i = 0; i < TotalCameraBuckets; i++) {
            int camCount = Cameras[i].Count;
            for (int j = 0; j < camCount; j++) {
                var cam = Cameras[i][j] as CameraComponent;
                if (cam == null) {
                    continue;
                }
                if ((drawable.Parent.LayerMask & cam.LayerMask) == 0) {
                    continue;
                }

                var reg = cam.Get2DRegistry();
                RenderBucket2D list = reg.Bucket;
                int pos = list.InsertSorted(drawable);
                reg.Map[drawable] = new Index2D(0, pos);
                Update2DIndicesAfterInsert(reg, pos + 1);
            }
        }
    }

    /// <summary>
    /// Removes a 2D drawable from all camera lists.
    /// </summary>
    /// <param name="drawable">Drawable to remove.</param>
    public void RemoveFromRender2D(IDrawable2D drawable) {
        Drawables2D.Remove(drawable);

        for (int i = 0; i < TotalCameraBuckets; i++) {
            int camCount = Cameras[i].Count;
            for (int j = 0; j < camCount; j++) {
                var cam = Cameras[i][j] as CameraComponent;
                if (cam == null) {
                    continue;
                }

                var reg = cam.Get2DRegistry();
                if (reg.Map.TryGetValue(drawable, out var idx)) {
                    reg.Bucket.RemoveAt(idx.Pos);
                    reg.Map.Remove(drawable);
                    Update2DIndicesAfterRemoval(reg, idx.Pos);
                }
            }
        }
    }

    /// <summary>
    /// Registers a 3D drawable with all matching cameras.
    /// </summary>
    /// <param name="drawable">Drawable to register.</param>
    public void RegisterForRender3D(IDrawable3D drawable) {
        Drawables3D.Add(drawable);

        for (int i = 0; i < TotalCameraBuckets; i++) {
            int camCount = Cameras[i].Count;
            for (int j = 0; j < camCount; j++) {
                var cam = Cameras[i][j] as CameraComponent;
                if (cam == null) {
                    continue;
                }
                if ((drawable.Parent.LayerMask & cam.LayerMask) == 0) {
                    continue;
                }

                var reg = cam.Get3DRegistry();
                RenderBucket3D list = reg.Bucket;
                int pos = list.InsertSorted(drawable);
                reg.Map[drawable] = new Index3D(0, 0, 0, pos);
                Update3DIndicesAfterInsert(reg, pos + 1);
            }
        }
    }

    /// <summary>
    /// Removes a 3D drawable from all camera lists.
    /// </summary>
    /// <param name="drawable">Drawable to remove.</param>
    public void RemoveFromRender3D(IDrawable3D drawable) {
        Drawables3D.Remove(drawable);

        for (int i = 0; i < TotalCameraBuckets; i++) {
            int camCount = Cameras[i].Count;
            for (int j = 0; j < camCount; j++) {
                var cam = Cameras[i][j] as CameraComponent;
                if (cam == null) {
                    continue;
                }

                var reg = cam.Get3DRegistry();
                if (reg.Map.TryGetValue(drawable, out var idx)) {
                    reg.Bucket.RemoveAt(idx.Pos);
                    reg.Map.Remove(drawable);
                    Update3DIndicesAfterRemoval(reg, idx.Pos);
                }
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
    /// Ensures the 2D render list can fit additional items for matching cameras.
    /// </summary>
    /// <param name="renderOrder">Render order used for ordering.</param>
    /// <param name="additional">Number of items expected to be added.</param>
    /// <param name="layerMask">Layer mask used to match cameras.</param>
    public void ReserveRender2DCapacity(byte renderOrder, int additional, ushort layerMask) {
        if (additional < 1) {
            return;
        }

        for (int i = 0; i < TotalCameraBuckets; i++) {
            int camCount = Cameras[i].Count;
            for (int j = 0; j < camCount; j++) {
                var cam = Cameras[i][j] as CameraComponent;
                if (cam == null) {
                    continue;
                }
                if ((layerMask & cam.LayerMask) == 0) {
                    continue;
                }

                RenderBucket2D list = cam.Get2DRegistry().Bucket;
                list.EnsureCapacity(list.Count + additional);
            }
        }
    }

    /// <summary>
    /// Ensures the 3D render list can fit additional items for matching cameras.
    /// </summary>
    /// <param name="renderOrder">Render order used for ordering.</param>
    /// <param name="variant">Render pipeline variant placeholder.</param>
    /// <param name="model">Model placeholder for compatibility.</param>
    /// <param name="additional">Number of items expected to be added.</param>
    /// <param name="layerMask">Layer mask used to match cameras.</param>
    public void ReserveRender3DCapacity(byte renderOrder, byte variant, RuntimeModel model, int additional, ushort layerMask) {
        if (additional < 1) {
            return;
        }

        for (int i = 0; i < TotalCameraBuckets; i++) {
            int camCount = Cameras[i].Count;
            for (int j = 0; j < camCount; j++) {
                var cam = Cameras[i][j] as CameraComponent;
                if (cam == null) {
                    continue;
                }
                if ((layerMask & cam.LayerMask) == 0) {
                    continue;
                }

                RenderBucket3D list = cam.Get3DRegistry().Bucket;
                list.EnsureCapacity(list.Count + additional);
            }
        }
    }

    /// <summary>
    /// Registers a camera for rendering and backfills existing drawables into its registries.
    /// </summary>
    /// <param name="camera">Camera to register.</param>
    public void RegisterCamera(ICamera camera) {
        int cameraBucket = RenderBucketUtils.GetBucketIndex(camera.CameraDrawOrder, TotalCameraBuckets);
        Cameras[cameraBucket].Add(camera);

        var camComp = camera as CameraComponent;
        if (camComp != null) {
            var reg3 = camComp.Get3DRegistry();
            for (int i = 0; i < Drawables3D.Count; i++) {
                IDrawable3D drawable = Drawables3D[i];
                if ((drawable.Parent.LayerMask & camera.LayerMask) != 0) {
                    int pos3 = reg3.Bucket.InsertSorted(drawable);
                    reg3.Map[drawable] = new Index3D(0, 0, 0, pos3);
                    Update3DIndicesAfterInsert(reg3, pos3 + 1);
                }
            }
        }

        var camComp2 = camera as CameraComponent;
        if (camComp2 != null) {
            var reg = camComp2.Get2DRegistry();
            for (int i = 0; i < Drawables2D.Count; i++) {
                IDrawable2D drawable2D = Drawables2D[i];
                if ((drawable2D.Parent.LayerMask & camera.LayerMask) != 0) {
                    int pos = reg.Bucket.InsertSorted(drawable2D);
                    reg.Map[drawable2D] = new Index2D(0, pos);
                    Update2DIndicesAfterInsert(reg, pos + 1);
                }
            }
        }
    }

    /// <summary>
    /// Removes a camera and its buckets.
    /// </summary>
    /// <param name="camera">Camera to remove.</param>
    public virtual void RemoveCamera(ICamera camera) {
        int cameraBucket = RenderBucketUtils.GetBucketIndex(camera.CameraDrawOrder, TotalCameraBuckets);
        Cameras[cameraBucket].Remove(camera);
    }

    /// <summary>
    /// Updates all registered updateables in bucket order.
    /// </summary>
    public virtual void Update() {
        for (int i = 0; i < TotalUpdateBuckets; i++) {
            UpdateBucket bucket = UpdateEntities[i];
            int j = 0;
            while (j < bucket.Count) {
                IUpdateable item = bucket.Items[j];
                item.Update();

                if (j < bucket.Count && ReferenceEquals(bucket.Items[j], item)) {
                    j++;
                }
            }
        }
    }

    /// <summary>
    /// Updates cached indices after inserting into a 2D list.
    /// </summary>
    /// <param name="reg">2D registry.</param>
    /// <param name="startIndex">First index to update.</param>
    void Update2DIndicesAfterInsert(Camera2DRegistry reg, int startIndex) {
        RenderBucket2D list = reg.Bucket;
        IDrawable2D[] items = list.Items;
        int count = list.Count;

        for (int i = startIndex; i < count; i++) {
            IDrawable2D item = items[i];
            if (item != null) {
                reg.Map[item] = new Index2D(0, i);
            }
        }
    }

    /// <summary>
    /// Updates cached indices after removing from a 2D list.
    /// </summary>
    /// <param name="reg">2D registry.</param>
    /// <param name="startIndex">First index to update.</param>
    void Update2DIndicesAfterRemoval(Camera2DRegistry reg, int startIndex) {
        RenderBucket2D list = reg.Bucket;
        IDrawable2D[] items = list.Items;
        int count = list.Count;

        for (int i = startIndex; i < count; i++) {
            IDrawable2D item = items[i];
            if (item != null) {
                reg.Map[item] = new Index2D(0, i);
            }
        }
    }

    /// <summary>
    /// Updates cached indices after inserting into a 3D list.
    /// </summary>
    /// <param name="reg">3D registry.</param>
    /// <param name="startIndex">First index to update.</param>
    void Update3DIndicesAfterInsert(Camera3DRegistry reg, int startIndex) {
        RenderBucket3D list = reg.Bucket;
        IDrawable3D[] items = list.Items;
        int count = list.Count;

        for (int i = startIndex; i < count; i++) {
            IDrawable3D item = items[i];
            if (item != null) {
                reg.Map[item] = new Index3D(0, 0, 0, i);
            }
        }
    }

    /// <summary>
    /// Updates cached indices after removing from a 3D list.
    /// </summary>
    /// <param name="reg">3D registry.</param>
    /// <param name="startIndex">First index to update.</param>
    void Update3DIndicesAfterRemoval(Camera3DRegistry reg, int startIndex) {
        RenderBucket3D list = reg.Bucket;
        IDrawable3D[] items = list.Items;
        int count = list.Count;

        for (int i = startIndex; i < count; i++) {
            IDrawable3D item = items[i];
            if (item != null) {
                reg.Map[item] = new Index3D(0, 0, 0, i);
            }
        }
    }
}
