namespace helengine;
public class ObjectManager {
    public List<IUpdateable>[] UpdateEntities { get; private set; }
    public byte TotalUpdateBuckets { get; private set; } = 4;

    public List<IDrawable2D> Drawables2D { get; private set; }
    public byte TotalBuckets2D { get; private set; } = 4;

    public List<IDrawable3D> Drawables3D { get; private set; }
    public byte TotalBuckets3D { get; private set; } = 3;
    public byte TotalVariants3D { get; private set; } = 4;

    public List<ICamera>[] Cameras { get; private set; }
    public byte TotalCameraBuckets { get; private set; } = 3;

    public ObjectManager() {
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
    }

    public virtual void RegisterForUpdate(IUpdateable entity) {
        int bucket = entity.UpdateOrder / TotalUpdateBuckets;
        UpdateEntities[bucket].Add(entity);
    }

    public virtual void RemoveFromUpdate(IUpdateable entity) {
        int bucket = entity.UpdateOrder / TotalUpdateBuckets;
        UpdateEntities[bucket].Remove(entity);
    }

    public void RegisterForRender2D(IDrawable2D drawable) {
        int index = Drawables2D.Count;
        Drawables2D.Add(drawable);

        // Add to cameras using index-based tracking
        for (int i = 0; i < TotalCameraBuckets; i++) {
            int camCount = Cameras[i].Count;
            for (int j = 0; j < camCount; j++) {
                ICamera cam = Cameras[i][j];
                if ((drawable.Parent.LayerMask & cam.Parent.LayerMask) != 0) {
                    cam.RenderIndices2D.Add(index);
                }
            }
        }
    }

    public void RemoveFromRender2D(IDrawable2D drawable) {
        int index = Drawables2D.IndexOf(drawable);
        if (index < 0) return;

        Drawables2D.RemoveAt(index);

        // Remove from cameras
        for (int i = 0; i < TotalCameraBuckets; i++) {
            int camCount = Cameras[i].Count;
            for (int j = 0; j < camCount; j++) {
                Cameras[i][j].RenderIndices2D.Remove(index);
            }
        }
    }

    public void RegisterForRender3D(IDrawable3D drawable, byte variant = 0) {
        int index = Drawables3D.Count;
        Drawables3D.Add(drawable);

        // Add to cameras using index-based tracking
        for (int i = 0; i < TotalCameraBuckets; i++) {
            int camCount = Cameras[i].Count;
            for (int j = 0; j < camCount; j++) {
                ICamera cam = Cameras[i][j];
                if ((drawable.Parent.LayerMask & cam.Parent.LayerMask) != 0) {
                    cam.RenderIndices3D.Add(index);
                }
            }
        }
    }

    public void RemoveFromRender3D(IDrawable3D drawable) {
        int index = Drawables3D.IndexOf(drawable);
        if (index < 0) return;

        Drawables3D.RemoveAt(index);

        // Remove from cameras
        for (int i = 0; i < TotalCameraBuckets; i++) {
            int camCount = Cameras[i].Count;
            for (int j = 0; j < camCount; j++) {
                Cameras[i][j].RenderIndices3D.Remove(index);
            }
        }
    }

    public void RegisterCamera(ICamera camera) {
        int bucket = camera.CameraDrawOrder / TotalBuckets3D;
        Cameras[bucket].Add(camera);
        camera.RenderIndices3D.Clear();

        for (int i = 0; i < Drawables3D.Count; i++) {
            if ((Drawables3D[i].Parent.LayerMask & camera.Parent.LayerMask) != 0) {
                camera.RenderIndices3D.Add(i);
            }
        }
    }

    public virtual void RemoveCamera(ICamera camera) {
        int bucket = camera.CameraDrawOrder / TotalCameraBuckets;
        Cameras[bucket].Remove(camera);

        camera.RenderIndices3D.Clear();
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
