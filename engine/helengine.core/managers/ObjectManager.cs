namespace helengine;

public class ObjectManager {
    public List<Entity> Entities { get; private set; }

    public List<IUpdateable>[] UpdateEntities { get; private set; }
    public byte TotalUpdateBuckets { get; private set; } = 4;

    public List<IDrawable2D> Drawables2D { get; private set; }
    public byte TotalBuckets2D { get; private set; } = 4;

    public List<IDrawable3D> Drawables3D { get; private set; }
    public byte TotalBuckets3D { get; private set; } = 3;
    public byte TotalVariants3D { get; private set; } = 4;

    public List<ICamera>[] Cameras { get; private set; }
    public byte TotalCameraBuckets { get; private set; } = 3;

    public List<IInteractable2D> Interactables { get; private set; }

    public ObjectManager() {
        Entities = new List<Entity>();

        UpdateEntities = new List<IUpdateable>[TotalUpdateBuckets];
        for (int i = 0; i < TotalUpdateBuckets; i++) {
            UpdateEntities[i] = new List<IUpdateable>();
        }

        Drawables2D = new List<IDrawable2D>();

        Drawables3D = new List<IDrawable3D>();

        Cameras = new List<ICamera>[TotalCameraBuckets];
        for (int i = 0; i < TotalCameraBuckets; i++) {
            Cameras[i] = new List<ICamera>();
        }

        Interactables = new List<IInteractable2D>();
    }

    public virtual void RegisterInteractable(IInteractable2D entity) {
        Interactables.Add(entity);
    }

    public virtual void RemoveInteractable(IInteractable2D entity) {
        Interactables.Remove(entity);
    }

    public virtual void RegisterEntity(Entity entity) {
        Entities.Add(entity);
    }

    public virtual void RemoveEntity(Entity entity) {
        Entities.Remove(entity);
    }

    public virtual void RegisterForUpdate(IUpdateable entity) {
        int bucket = entity.UpdateOrder / TotalUpdateBuckets;
        UpdateEntities[bucket].Add(entity);
    }

    public virtual void RemoveFromUpdate(IUpdateable entity) {
        int bucket = entity.UpdateOrder / TotalUpdateBuckets;
        UpdateEntities[bucket].Remove(entity);
    }

    private int getBucket(byte renderOrder, byte totalBuckets) {
        int gaps = 255 / totalBuckets;
        return renderOrder / gaps;
    }

    private static int getStateBin3D(IDrawable3D drawable, int bins) {
        var model = drawable.Model;
        if (model == null || bins <= 1) return 0;
        int h = model.Id != null ? model.Id.GetHashCode() : 0;
        uint uh = unchecked((uint)h);
        return (int)(uh % (uint)bins);
    }

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
                    var swapped = reg.Buckets[idx.Bucket].RemoveSwapAt(idx.Pos);
                    if (swapped != null) {
                        reg.Map[(IDrawable2D)swapped] = new Index2D(idx.Bucket, idx.Pos);
                    }
                    reg.Map.Remove(drawable);
                }
            }
        }
    }

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
                int bin = getStateBin3D(drawable, reg.BinsPerBucket);
                reg.Buckets[variant][bucket][bin].Add(drawable, out int pos);
                reg.Map[drawable] = new Index3D(variant, bucket, bin, pos);
            }
        }
    }

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

    public void RegisterCamera(ICamera camera) {
        // Use the correct camera bucket count for camera ordering
        int cameraBucket = camera.CameraDrawOrder / TotalCameraBuckets;
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
                    int bin = getStateBin3D(drawable, reg3.BinsPerBucket);
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

    public virtual void RemoveCamera(ICamera camera) {
        int cameraBucket = camera.CameraDrawOrder / TotalCameraBuckets;
        Cameras[cameraBucket].Remove(camera);
    }

    public virtual void Update() {
        for (int i = 0; i < TotalUpdateBuckets; i++) {
            List<IUpdateable> entities = UpdateEntities[i];

            for (int j = 0; j < entities.Count; j++) {
                entities[j].Update();
            }
        }
    }
}
