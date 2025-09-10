namespace helengine {
    public class RoundedRectComponent : Component, IRoundedRectDrawable2D {
        byte renderOrder2D;

        public byte RenderOrder2D {
            get { return renderOrder2D; }
            set {
                if (renderOrder2D != value) {
                    if (Parent != null && Parent.Enabled) {
                        Core.Instance.ObjectManager.RemoveFromRender2D(this);
                        renderOrder2D = value;
                        Core.Instance.ObjectManager.RegisterForRender2D(this);
                    } else {
                        renderOrder2D = value;
                    }
                }
            }
        }

        public byte LayerMask { get; set; }

        public float Rotation { get; set; }
        public byte4 Color { get; set; }
        public float4 SourceRect { get; set; }
        public int2 Size { get; set; }
        public float Radius { get; set; }
        public float BorderThickness { get; set; }
        public byte4 FillColor { get; set; }
        public byte4 BorderColor { get; set; }

        public RoundedRectComponent() {
            Size = new int2(64, 32);
            SourceRect = new float4(0, 0, 1, 1);
            Color = new byte4(255, 255, 255, 255);
            Radius = 8f;
            BorderThickness = 0f;
            FillColor = new byte4(255, 255, 255, 255);
            BorderColor = new byte4(0, 0, 0, 255);
        }

        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (entity.Enabled) {
                Core.Instance.ObjectManager.RegisterForRender2D(this);
            }
        }

        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (newEnabled) {
                Core.Instance.ObjectManager.RegisterForRender2D(this);
            } else {
                Core.Instance.ObjectManager.RemoveFromRender2D(this);
            }
        }

        public virtual void Draw() {
            Core.Instance.RenderManager2D.DrawRoundedRect(this);
        }
    }
}
