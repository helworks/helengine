using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nucleus.Animation.Interpolators {
    public static class SinInterpolator {
        public static readonly double DEG90 = Math.PI / 2.0;

        public static double Interpolate(double delta) {
            // convert to degrees
            delta *= DEG90;
            return Math.Sin(delta);
        }
    }
}
