namespace helengine {
    public class ObjectManager {
        public List<Entity>[] UpdateEntities { get; private set; }
        public int UpdateBuckets { get; private set; } = 8;

        public List<IDrawable2D>[] RenderEntities2D { get; private set; }
        public int RenderBuckets2D { get; private set; } = 8;

        public List<IDrawable3D>[] RenderEntities3D { get; private set; }
        public int RenderBuckets3D { get; private set; } = 8;


        public ObjectManager() {
            UpdateEntities = new List<Entity>[UpdateBuckets];
            for (int i = 0; i < UpdateBuckets; i++) {
                UpdateEntities[i] = new List<Entity>();
            }

            RenderEntities2D = new List<IDrawable2D>[RenderBuckets2D];
            for (int i = 0; i < RenderBuckets2D; i++) {
                RenderEntities2D[i] = new List<IDrawable2D>();
            }

            RenderEntities3D = new List<IDrawable3D>[RenderBuckets3D];
            for (int i = 0; i < RenderBuckets3D; i++) {
                RenderEntities3D[i] = new List<IDrawable3D>();
            }
        }

        public virtual void RegisterForUpdate(Entity entity) {
            int bucket = entity.UpdateOrder / UpdateBuckets;
            UpdateEntities[bucket].Add(entity);
        }

        public virtual void RemoveFromUpdate(Entity entity) {
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

        public virtual void Update() {
            for (int i = 0; i < UpdateBuckets; i++) {
                List<Entity> entities = UpdateEntities[i];

                for (int j = 0; j < entities.Count; j++) {
                    entities[j].Update();
                }
            }
        }

        public virtual void Draw2D() {
            //for (int i = 0; i < RenderBuckets2D; i++) {
            //    List<IDrawable2D> entities = RenderEntities2D[i];

            //    for (int j = 0; j < entities.Count; j++) {
            //        entities[j].Draw();
            //    }
            //}
        }

        public virtual void Draw3D() {
            //for (int i = 0; i < RenderBuckets3D; i++) {
            //    List<IDrawable3D> entities = RenderEntities3D[i];

            //    for (int j = 0; j < entities.Count; j++) {
            //        entities[j].Update();
            //    }
            //}
        }
    }
}
