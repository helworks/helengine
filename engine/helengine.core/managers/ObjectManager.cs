namespace helengine {
    public class ObjectManager {
        public List<IUpdateable>[] UpdateEntities { get; private set; }
        public byte TotalUpdateBuckets { get; private set; } = 4;

        public List<IDrawable2D>[] Drawables2D { get; private set; }
        public byte TotalBuckets2D { get; private set; } = 4;

        public List<IDrawable3D>[][] Drawables3D { get; private set; }
        public byte TotalVariants3D { get; private set; } = 4;
        public byte TotalBuckets3D { get; private set; } = 3;

        public List<ICamera>[] Cameras { get; private set; }
        public byte TotalCameraBuckets { get; private set; } = 3;

        public ObjectManager() {
            UpdateEntities = new List<IUpdateable>[TotalUpdateBuckets];
            for (int i = 0; i < TotalUpdateBuckets; i++) {
                UpdateEntities[i] = new List<IUpdateable>();
            }

            Drawables2D = new List<IDrawable2D>[TotalBuckets2D];
            for (int i = 0; i < TotalBuckets2D; i++) {
                Drawables2D[i] = new List<IDrawable2D>();
            }

            Drawables3D = new List<IDrawable3D>[TotalVariants3D][];
            for (int i = 0; i < TotalVariants3D; i++) {
                Drawables3D[i] = new List<IDrawable3D>[TotalBuckets3D];
                for (int j = 0; j < TotalBuckets3D; j++) {
                    Drawables3D[i][j] = new List<IDrawable3D>();
                }
            }

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

        public virtual void RegisterForRender2D(IDrawable2D drawable) {
            int bucket = drawable.RenderOrder2D / TotalBuckets2D;
            Drawables2D[bucket].Add(drawable);
        }

        public virtual void RemoveFromRender2D(IDrawable2D drawable) {
            int bucket = drawable.RenderOrder2D / TotalBuckets2D;
            Drawables2D[bucket].Remove(drawable);
        }

        public virtual void RegisterForRender3D(IDrawable3D drawable, byte variant = 0) {
            int bucket = drawable.RenderOrder3D / TotalBuckets3D;
            Drawables3D[variant][bucket].Add(drawable);
        }

        public virtual void RemoveFromRender3D(IDrawable3D drawable, byte variant = 0) {
            int bucket = drawable.RenderOrder3D / TotalBuckets3D;
            Drawables3D[variant][bucket].Remove(drawable);
        }

        public virtual void RegisterCamera(ICamera camera) {
            int bucket = camera.CameraDrawOrder / TotalCameraBuckets;
            Cameras[bucket].Add(camera);
        }

        public virtual void RemoveCamera(ICamera camera) {
            int bucket = camera.CameraDrawOrder / TotalCameraBuckets;
            Cameras[bucket].Remove(camera);
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
}
