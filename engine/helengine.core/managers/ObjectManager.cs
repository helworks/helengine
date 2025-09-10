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

    public void RegisterForRender2D(IDrawable2D drawable) {
        int bucket = getBucket(drawable.RenderOrder2D, TotalBuckets2D);
        int index = Drawables2D.Count;
        Drawables2D.Add(drawable);

        // Add to cameras using index-based tracking
        for (int i = 0; i < TotalCameraBuckets; i++) {
            int camCount = Cameras[i].Count;
            for (int j = 0; j < camCount; j++) {
                ICamera cam = Cameras[i][j];
                if ((drawable.Parent.LayerMask & cam.LayerMask) != 0) {
                    cam.RenderIndices2D[bucket].Add(index);
                }
            }
        }
    }

    public void RemoveFromRender2D(IDrawable2D drawable) {
        int bucket = getBucket(drawable.RenderOrder2D, TotalBuckets2D);
        int index = Drawables2D.IndexOf(drawable);
        if (index < 0) return;

        Drawables2D.RemoveAt(index);

        // Remove from cameras
        for (int i = 0; i < TotalCameraBuckets; i++) {
            int camCount = Cameras[i].Count;
            for (int j = 0; j < camCount; j++) {
                Cameras[i][j].RenderIndices2D[bucket].Remove(index);
            }
        }
    }

    public void RegisterForRender3D(IDrawable3D drawable) {
        int bucket = drawable.RenderOrder3D / TotalBuckets3D;
        int index = Drawables3D.Count;
        Drawables3D.Add(drawable);

        // Add to cameras using index-based tracking
        for (int i = 0; i < TotalCameraBuckets; i++) {
            int camCount = Cameras[i].Count;
            for (int j = 0; j < camCount; j++) {
                ICamera cam = Cameras[i][j];
                if ((drawable.Parent.LayerMask & cam.LayerMask) != 0) {
                    cam.RenderIndices3D[drawable.Variant][bucket].Add(index);
                }
            }
        }
    }

    public void RemoveFromRender3D(IDrawable3D drawable) {
        int bucket = drawable.RenderOrder3D / TotalBuckets3D;
        int index = Drawables3D.IndexOf(drawable);
        if (index < 0) return;

        Drawables3D.RemoveAt(index);

        // Remove from cameras
        for (int i = 0; i < TotalCameraBuckets; i++) {
            int camCount = Cameras[i].Count;
            for (int j = 0; j < camCount; j++) {
                Cameras[i][j].RenderIndices3D[drawable.Variant][bucket].Remove(index);
            }
        }
    }

    public void RegisterCamera(ICamera camera) {
        // Use the correct camera bucket count for camera ordering
        int cameraBucket = camera.CameraDrawOrder / TotalCameraBuckets;
        Cameras[cameraBucket].Add(camera);

        // Backfill existing 3D drawables for this camera
        for (int i = 0; i < Drawables3D.Count; i++) {
            IDrawable3D drawable = Drawables3D[i];
            if ((drawable.Parent.LayerMask & camera.LayerMask) != 0) {
                int drawBucket3D = drawable.RenderOrder3D / TotalBuckets3D;
                camera.RenderIndices3D[drawable.Variant][drawBucket3D].Add(i);
            }
        }

        // Backfill existing 2D drawables for this camera (important when camera is added after UI)
        for (int i = 0; i < Drawables2D.Count; i++) {
            IDrawable2D drawable2D = Drawables2D[i];
            if ((drawable2D.Parent.LayerMask & camera.LayerMask) != 0) {
                int drawBucket2D = getBucket(drawable2D.RenderOrder2D, TotalBuckets2D);
                camera.RenderIndices2D[drawBucket2D].Add(i);
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
