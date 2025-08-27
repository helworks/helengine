using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nucleus.Animation.Interpolators {
    public static class InterpolatorService {
        public static double Interpolate(double delta, InterpolationType interpolationType) {
            switch (interpolationType) {
                case InterpolationType.Linear:
                    return delta;
                case InterpolationType.Sin:
                    return SinInterpolator.Interpolate(delta);
            }

            return delta;
        }
    }
}
