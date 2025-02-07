namespace helengine {
    public class Entity {
        bool enabled;

        public float3 Position { get; set; }
        public float3 Scale { get; set; }
        public float4 Orientation { get; set; }

        /// <summary>
        /// TODO seal/rework this list, adding directly breaks the system
        /// </summary>
        public List<Component>? Components { get; internal set; }
        public List<Entity>? Children { get; internal set; }
        public Entity Parent { get; private set; }

        public bool Enabled {
            get { return enabled; }
            set {
                if (enabled != value) {
                    if (Components != null) {
                        for (int i = 0; i < Components.Count; i++) {
                            Components[i].ParentEnabledChange(value);
                        }
                    }
                }
                enabled = value;
            }
        }

        public void InitChildren() {
            Children = new List<Entity>();
        }

        public void AddChild(Entity entity) {
            if (entity.Parent != null) {
                throw new Exception("Parent is not empty");
            }

            Children.Add(entity);

        }

        public void InitComponents() {
            Components = new List<Component>();
        }

        public void AddComponent(Component comp) {
            Components.Add(comp);
            comp.ComponentAdded(this);
        }
    }
}
