using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nucleus.Animation {
    public class AnimationFrame {
        public AnimationClipAction Action { get; set; }
        public InterpolationType Interpolator { get; set; }

        public AnimationFrame(InterpolationType interpolator, 
            AnimationClipAction action) {
            Action = action;
            Interpolator = interpolator;
        }
    }
}
