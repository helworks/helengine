namespace helengine;

/// <summary>
/// Tracks engine objects and organizes update and render buckets for efficient processing.
/// </summary>
public class ObjectManager {
    /// <summary>
    /// Initializes a new object manager and allocates buckets for updates, rendering, and cameras.
    /// </summary>
    public ObjectManager() {
        Entities = new List<Entity>();

        UpdateEntities = new UpdateBucket[TotalUpdateBuckets];
        for (int i = 0; i < TotalUpdateBuckets; i++) {
            UpdateEntities[i] = new UpdateBucket(64);
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
        if (entity.UpdateBucketIndex >= 0 && entity.UpdateBucket >= 0) {
            RemoveFromUpdate(entity);
        }

        int bucket = getBucket(entity.UpdateOrder, TotalUpdateBuckets);
        UpdateBucket updateBucket = UpdateEntities[bucket];
        updateBucket.Add(entity, out int pos);
        entity.UpdateBucket = bucket;
        entity.UpdateBucketIndex = pos;
    }

    /// <summary>
    /// Removes an object from the update buckets.
    /// </summary>
    /// <param name="entity">Updateable to remove.</param>
    public virtual void RemoveFromUpdate(IUpdateable entity) {
        int bucket = entity.UpdateBucket;
        int pos = entity.UpdateBucketIndex;
        if (bucket < 0 || pos < 0) {
            return;
        }

        UpdateBucket updateBucket = UpdateEntities[bucket];
        IUpdateable swapped = updateBucket.RemoveSwapAt(pos);
        if (swapped != null) {
            swapped.UpdateBucket = bucket;
            swapped.UpdateBucketIndex = pos;
        }

        entity.UpdateBucket = -1;
        entity.UpdateBucketIndex = -1;
    }

    /// <summary>
    /// Registers a 2D drawable with all matching cameras.
    /// </summary>
    /// <param name="drawable">Drawable to register.</param>
    public void RegisterForRender2D(IDrawable2D drawable) {
        // Maintain a side list for diagnostics, but membership uses per-camera dense buckets of references
        Drawables2D.Add(drawable);

        int bucket = getBucket(drawable.RenderOrder2D, TotalBuckets2D);
        for (int i = 0; i < TotalCameraBuckets; i++) {
            int camCount = Cameras[i].Count;
            for (int j = 0; j < camCount; j++) {
                var cam = Cameras[i][j] as CameraComponent;
                if (cam == null) continue;
                if ((drawable.Parent.LayerMask & cam.LayerMask) == 0) continue;

                var reg = cam.Get2DRegistry();
                reg.Buckets[bucket].Add(drawable, out int pos);
                reg.Map[drawable] = new Index2D(bucket, pos);
            }
        }
    }

    /// <summary>
    /// Removes a 2D drawable from all camera buckets.
    /// </summary>
    /// <param name="drawable">Drawable to remove.</param>
    public void RemoveFromRender2D(IDrawable2D drawable) {
        // Keep Drawables2D as a diagnostic list but remove reference if present
        Drawables2D.Remove(drawable);

        for (int i = 0; i < TotalCameraBuckets; i++) {
            int camCount = Cameras[i].Count;
            for (int j = 0; j < camCount; j++) {
                var cam = Cameras[i][j] as CameraComponent;
                if (cam == null) continue;
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
    }

    /// <summary>
    /// Registers a 3D drawable with all matching cameras.
    /// </summary>
    /// <param name="drawable">Drawable to register.</param>
    public void RegisterForRender3D(IDrawable3D drawable) {
        Drawables3D.Add(drawable);

        int bucket = getBucket(drawable.RenderOrder3D, TotalBuckets3D);
        int variant = drawable.Variant;

        for (int i = 0; i < TotalCameraBuckets; i++) {
            int camCount = Cameras[i].Count;
            for (int j = 0; j < camCount; j++) {
                var cam = Cameras[i][j] as CameraComponent;
                if (cam == null) continue;
                if ((drawable.Parent.LayerMask & cam.LayerMask) == 0) continue;

                var reg = cam.Get3DRegistry();
                int bin = getStateBin3D(drawable.Model, reg.BinsPerBucket);
                reg.Buckets[variant][bucket][bin].Add(drawable, out int pos);
                reg.Map[drawable] = new Index3D(variant, bucket, bin, pos);
            }
        }
    }

    /// <summary>
    /// Removes a 3D drawable from all camera buckets.
    /// </summary>
    /// <param name="drawable">Drawable to remove.</param>
    public void RemoveFromRender3D(IDrawable3D drawable) {
        Drawables3D.Remove(drawable);

        for (int i = 0; i < TotalCameraBuckets; i++) {
            int camCount = Cameras[i].Count;
            for (int j = 0; j < camCount; j++) {
                var cam = Cameras[i][j] as CameraComponent;
                if (cam == null) continue;
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

        int bucket = getBucket(updateOrder, TotalUpdateBuckets);
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

        int bucket = getBucket(renderOrder, TotalBuckets2D);
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

                var reg = cam.Get2DRegistry();
                RenderBucket2D renderBucket = reg.Buckets[bucket];
                renderBucket.EnsureCapacity(renderBucket.Count + additional);
            }
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

        int bucket = getBucket(renderOrder, TotalBuckets3D);
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
    }

    /// <summary>
    /// Registers a camera for rendering and backfills existing drawables into its registries.
    /// </summary>
    /// <param name="camera">Camera to register.</param>
    public void RegisterCamera(ICamera camera) {
        // Use the correct camera bucket count for camera ordering
        int cameraBucket = getBucket(camera.CameraDrawOrder, TotalCameraBuckets);
        Cameras[cameraBucket].Add(camera);

        // Backfill existing 3D drawables for this camera (by reference)
        var camComp = camera as CameraComponent;
        if (camComp != null) {
            var reg3 = camComp.Get3DRegistry();
            for (int i = 0; i < Drawables3D.Count; i++) {
                IDrawable3D drawable = Drawables3D[i];
                if ((drawable.Parent.LayerMask & camera.LayerMask) != 0) {
                    int drawBucket3D = getBucket(drawable.RenderOrder3D, TotalBuckets3D);
                    int variant = drawable.Variant;
                    int bin = getStateBin3D(drawable.Model, reg3.BinsPerBucket);
                    reg3.Buckets[variant][drawBucket3D][bin].Add(drawable, out int pos3);
                    reg3.Map[drawable] = new Index3D(variant, drawBucket3D, bin, pos3);
                }
            }
        }

        // Backfill existing 2D drawables for this camera with references
        var camComp2 = camera as CameraComponent;
        if (camComp2 != null) {
            var reg = camComp2.Get2DRegistry();
            for (int i = 0; i < Drawables2D.Count; i++) {
                IDrawable2D drawable2D = Drawables2D[i];
                if ((drawable2D.Parent.LayerMask & camera.LayerMask) != 0) {
                    int drawBucket2D = getBucket(drawable2D.RenderOrder2D, TotalBuckets2D);
                    reg.Buckets[drawBucket2D].Add(drawable2D, out int pos);
                    reg.Map[drawable2D] = new Index2D(drawBucket2D, pos);
                }
            }
        }
    }

    /// <summary>
    /// Removes a camera and its buckets.
    /// </summary>
    /// <param name="camera">Camera to remove.</param>
    public virtual void RemoveCamera(ICamera camera) {
        int cameraBucket = getBucket(camera.CameraDrawOrder, TotalCameraBuckets);
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
    /// Computes the bucket index based on a render order value.
    /// </summary>
    /// <param name="renderOrder">Render order to bucketize.</param>
    /// <param name="totalBuckets">Total buckets available.</param>
    /// <returns>Calculated bucket index.</returns>
    private int getBucket(byte renderOrder, byte totalBuckets) {
        return (renderOrder * totalBuckets) / 256;
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
