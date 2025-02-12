namespace helengine {
    public class SpriteComponent : Component, ISpriteDrawable2D {
        byte renderOrder2D;

        public byte RenderOrder2D {
            get { return renderOrder2D; }
            set {
                if (renderOrder2D != value) {
                    if (Parent.Enabled) {
                        Core.Instance.ObjectManager.RemoveFromRender2D(this);
                        renderOrder2D = value;
                        Core.Instance.ObjectManager.RegisterForRender2D(this);
                    } else {
                        renderOrder2D = value;
                    }
                }
            }
        }

        public RuntimeTexture? Texture { get; set; }

        public float Rotation { get; set; }

        public byte LayerMask { get; set; }

        public float4 SourceRect { get; set; }

        public int2 Size { get; set; }

        public byte4 Color { get; set; }

        public SpriteComponent() {
            SourceRect = new float4(0, 0, 1, 1);
            Color = new byte4(255, 255, 255, 255);
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
            Core.Instance.RenderManager.DrawSprite(this);
        }
    }
}
