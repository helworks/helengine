using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace helengine {
    public class ComponentMesh : Component, IDrawable3D {
        byte renderOrder3D;

        public ModelRenderData? RenderData { get; set; }

        public byte RenderOrder3D {
            get { return renderOrder3D; }
            set {
                if (renderOrder3D != value) {
                    if (Parent.Enabled) {
                        Core.Instance.ObjectManager.RemoveFromRender3D(this);
                        renderOrder3D = value;
                        Core.Instance.ObjectManager.RegisterForRender3D(this);
                    } else {
                        renderOrder3D = value;
                    }
                }
            }
        }

        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (entity.Enabled) {
                Core.Instance.ObjectManager.RegisterForRender3D(this);
            }
        }

        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (newEnabled) {
                Core.Instance.ObjectManager.RegisterForRender3D(this);
            } else {
                Core.Instance.ObjectManager.RemoveFromRender3D(this);
            }
        }
    }
}
