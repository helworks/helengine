using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nucleus.Animation {
    public class AnimationClip {
        public Dictionary<int, AnimationFrame> Frames { get; set; }

        public AnimationClip() {
            Frames = new Dictionary<int, AnimationFrame>();
        }

        public void AddFrame(int frame,
            InterpolationType interpolator,
            AnimationClipAction actionDelegate) {
            Frames.Add(frame, new AnimationFrame(interpolator, actionDelegate));
        }
    }
}
