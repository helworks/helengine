using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace helengine {
    public interface IDrawable3D {
        public byte RenderOrder3D { get; set; }
    }
}
