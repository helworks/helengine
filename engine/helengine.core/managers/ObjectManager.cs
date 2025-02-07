namespace helengine {
    public class ObjectManager {
        public List<IUpdateable>[] UpdateEntities { get; private set; }
        public int UpdateBuckets { get; private set; } = 8;

        public List<IDrawable2D>[] RenderEntities2D { get; private set; }
        public int RenderBuckets2D { get; private set; } = 8;

        public List<IDrawable3D>[] RenderEntities3D { get; private set; }
        public int RenderBuckets3D { get; private set; } = 8;

        public List<ICamera>[] Cameras { get; private set; }
        public int CameraBuckets { get; private set; } = 8;

        public ObjectManager() {
            UpdateEntities = new List<IUpdateable>[UpdateBuckets];
            for (int i = 0; i < UpdateBuckets; i++) {
                UpdateEntities[i] = new List<IUpdateable>();
            }

            RenderEntities2D = new List<IDrawable2D>[RenderBuckets2D];
            for (int i = 0; i < RenderBuckets2D; i++) {
                RenderEntities2D[i] = new List<IDrawable2D>();
            }

            RenderEntities3D = new List<IDrawable3D>[RenderBuckets3D];
            for (int i = 0; i < RenderBuckets3D; i++) {
                RenderEntities3D[i] = new List<IDrawable3D>();
            }

            Cameras = new List<ICamera>[CameraBuckets];
            for (int i = 0; i < RenderBuckets3D; i++) {
                Cameras[i] = new List<ICamera>();
            }
        }

        public virtual void RegisterForUpdate(IUpdateable entity) {
            int bucket = entity.UpdateOrder / UpdateBuckets;
            UpdateEntities[bucket].Add(entity);
        }

        public virtual void RemoveFromUpdate(IUpdateable entity) {
            int bucket = entity.UpdateOrder / UpdateBuckets;
            UpdateEntities[bucket].Remove(entity);
        }

        public virtual void RegisterForRender2D(IDrawable2D drawable) {
            int bucket = drawable.RenderOrder2D / RenderBuckets2D;
            RenderEntities2D[bucket].Add(drawable);
        }

        public virtual void RemoveFromRender2D(IDrawable2D drawable) {
            int bucket = drawable.RenderOrder2D / RenderBuckets2D;
            RenderEntities2D[bucket].Remove(drawable);
        }

        public virtual void RegisterForRender3D(IDrawable3D drawable) {
            int bucket = drawable.RenderOrder3D / RenderBuckets3D;
            RenderEntities3D[bucket].Add(drawable);
        }

        public virtual void RemoveFromRender3D(IDrawable3D drawable) {
            int bucket = drawable.RenderOrder3D / RenderBuckets3D;
            RenderEntities3D[bucket].Remove(drawable);
        }

        public virtual void RegisterCamera(ICamera camera) {
            int bucket = camera.CameraDrawOrder / CameraBuckets;
            Cameras[bucket].Add(camera);
        }

        public virtual void RemoveCamera(ICamera camera) {
            int bucket = camera.CameraDrawOrder / CameraBuckets;
            Cameras[bucket].Remove(camera);
        }

        public virtual void Update() {
            for (int i = 0; i < UpdateBuckets; i++) {
                List<IUpdateable> entities = UpdateEntities[i];

                for (int j = 0; j < entities.Count; j++) {
                    entities[j].Update();
                }
            }
        }
    }
}
