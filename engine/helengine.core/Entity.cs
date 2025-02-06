namespace helengine {
    public class Entity {
        bool shouldUpdate;
        bool shouldRender;
        byte updateOrder;
        byte renderOrder2D;
        byte renderOrder3D;

        public float3 Position { get; set; }
        public float3 Scale { get; set; }
        public float4 Orientation { get; set; }

        public List<ComponentLineRenderer>? Components { get; set; }

        public byte UpdateOrder {
            get { return updateOrder; } 
            set {
                if (updateOrder != value) {
                    if (shouldUpdate) {
                        Core.Instance.ObjectManager.RemoveFromUpdate(this);
                        updateOrder = value;
                        Core.Instance.ObjectManager.RegisterForUpdate(this);
                    } else {
                        updateOrder = value;
                    }
                }
            }
        }

        public byte RenderOrder2D {
            get { return renderOrder2D; }
            set {
                if (renderOrder2D != value) {
                    if (shouldRender) {
                        //Core.Instance.ObjectManager.RemoveFromRender2D(this);
                        //updateOrder = value;
                        //Core.Instance.ObjectManager.RegisterForRender2D(this);
                    } else {
                        updateOrder = value;
                    }
                }
            }
        }

        public byte RenderOrder3D {
            get { return renderOrder3D; }
            set {
                if (renderOrder3D != value) {
                    if (shouldRender) {
                        //Core.Instance.ObjectManager.RemoveFromRender2D(this);
                        //updateOrder = value;
                        //Core.Instance.ObjectManager.RegisterForRender2D(this);
                    } else {
                        updateOrder = value;
                    }
                }
            }
        }

        public bool ShouldUpdate {
            get { return shouldUpdate; }
            set { 
                shouldUpdate = value; 
                if (value) {
                    Core.Instance.ObjectManager.RegisterForUpdate(this);
                } else {
                    Core.Instance.ObjectManager.RemoveFromUpdate(this);
                }
            }
        }

        public bool ShouldRender {
            get { return shouldRender; }
            set {
                shouldRender = value;
                if (value) {
                    //Core.Instance.ObjectManager.RegisterForRender2D(this);
                } else {
                    //Core.Instance.ObjectManager.RemoveFromRender2D(this);
                }
            }
        }

        public virtual void Update() {
            if (Components == null) {
                return;
            }

            for (int i = 0; i < Components.Count; i++) {
                Components[i].Update();
            }
        }
    }
}
