using System.Drawing;

namespace Nucleus {
    public static class PointUtils {
        public static Point Lerp(Point a, Point b, float lerp) {
            return new Point(
                MathUtil.Lerp(a.X, b.X, lerp),
                MathUtil.Lerp(a.Y, b.Y, lerp));
        }

        public static Point Lerp(Point a, Point b, double lerp) {
            return new Point(
                MathUtil.Lerp(a.X, b.X, lerp),
                MathUtil.Lerp(a.Y, b.Y, lerp));
        }
    }
}
