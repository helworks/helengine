namespace helengine {
    public class Entity {
        bool isEnabled;
        bool isStatic;
        float3 position;
        float3 scale;
        float4 orientation;

        public virtual float3 Position {
            get {
                float3 pos = this.position;

                if (Parent != null) {
                    pos += Parent.Position;
                }

                return pos;
            }
            set { position = value; }
        }
        
        public float3 Scale {
            get {
                float3 sca = this.scale;

                if (Parent != null) {
                    sca += Parent.Scale;
                }

                return sca;
            }
            set { scale = value; }
        }

        public float4 Orientation {
            get {
                float4 ori = this.orientation;

                if (Parent != null) {
                    ori *= Parent.Orientation;
                }

                return ori;
            }
            set { orientation = value; }
        }

        public Entity Parent { get; private set; }

        public ushort LayerMask { get; set; }

        /// <summary>
        /// TODO seal/rework this list, adding directly breaks the system
        /// </summary>
        public List<Component>? Components { get; internal set; }

        /// <summary>
        /// TODO seal/rework this list, adding directly breaks the system
        /// </summary>
        public List<Entity>? Children { get; internal set; }

        public bool Enabled {
            get { return isEnabled; }
            set {
                if (isEnabled != value) {
                    ParentEnabledChange(value);
                }
                isEnabled = value;
            }
        }

        public bool Static {
            get { return isStatic; }
            set {
                if (isStatic != value) {
                    ParentStaticChange(value);
                }
                isStatic = value;
            }
        }

        public Entity() {
            isEnabled = true;
            Orientation = float4.Identity;
            Scale = float3.One;
            LayerMask = 0b00000001;

            Core.Instance.ObjectManager.RegisterEntity(this);
        }

        public void InitChildren() {
            Children = new List<Entity>();
        }

        public void AddChild(Entity entity) {
            if (entity.Parent != null) {
                throw new Exception("Parent is not empty");
            }

            entity.Parent = this;
            Children.Add(entity);
        }

        public void InitComponents() {
            Components = new List<Component>();
        }

        public void AddComponent(Component comp) {
            Components.Add(comp);
            comp.ComponentAdded(this);
        }

        protected virtual void ParentEnabledChange(bool newEnabled) {
            if (Components != null) {
                for (int i = 0; i < Components.Count; i++) {
                    Components[i].ParentStaticChange(newEnabled);
                }
            }

            if (Children != null) {
                for (int i = 0; i < Children.Count; i++) {
                    Children[i].ParentEnabledChange(newEnabled);
                }
            }
        }

        protected virtual void ParentStaticChange(bool newEnabled) {
            if (Components != null) {
                for (int i = 0; i < Components.Count; i++) {
                    Components[i].ParentEnabledChange(newEnabled);
                }
            }

            if (Children != null) {
                for (int i = 0; i < Children.Count; i++) {
                    Children[i].ParentStaticChange(newEnabled);
                }
            }
        }
    }
}
