#include "UvGeometry.h"
namespace hellm::detail {
/// <summary>Evaluate an oriented 2D edge function for clipping, overlap and barycentric coordinates.</summary>
double Edge(Point2 a, Point2 b, Point2 p) {
    return (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
}
/// <summary>Clip a convex polygon against one directed half-plane with explicit capacity checks.</summary>
Polygon Clip(const Polygon &input, Point2 a, Point2 b) {
    Polygon output;
    if (!input.Count)
        return output;
    auto previous = input.Points[input.Count - 1];
    double pd = Edge(a, b, previous);
    for (uint32_t i = 0; i < input.Count; ++i) {
        auto current = input.Points[i];
        double cd = Edge(a, b, current);
        bool pi = pd >= 0, ci = cd >= 0;
        if (pi != ci) {
            const double k = pd / (pd - cd);
            if (output.Count >= output.Points.size())
                throw std::runtime_error("Polygon clipping overflow");
            output.Points[output.Count++] = {previous.X + (current.X - previous.X) * k,
                                             previous.Y + (current.Y - previous.Y) * k};
        }
        if (ci) {
            if (output.Count >= output.Points.size())
                throw std::runtime_error("Polygon clipping overflow");
            output.Points[output.Count++] = current;
        }
        previous = current;
        pd = cd;
    }
    return output;
}
/// <summary>Orient the original UV triangle counterclockwise for exact clipping.</summary>
Polygon TrianglePolygon(const Triangle &t) {
    Polygon p;
    p.Count = 3;
    p.Points[0] = t.Uv[0];
    if (Edge(t.Uv[0], t.Uv[1], t.Uv[2]) > 0) {
        p.Points[1] = t.Uv[1];
        p.Points[2] = t.Uv[2];
    } else {
        p.Points[1] = t.Uv[2];
        p.Points[2] = t.Uv[1];
    }
    return p;
}
/// <summary>Intersect a UV triangle with one full-resolution texel square.</summary>
Polygon PixelPolygon(const Triangle &t, uint32_t x, uint32_t y) {
    auto p = TrianglePolygon(t);
    float a = float(x), b = float(y);
    p = Clip(p, {a, b}, {a + 1, b});
    p = Clip(p, {a + 1, b}, {a + 1, b + 1});
    p = Clip(p, {a + 1, b + 1}, {a, b + 1});
    return Clip(p, {a, b + 1}, {a, b});
}
/// <summary>Compute convex polygon area from a consistently oriented triangle fan.</summary>
double Area(const Polygon &p) {
    double area = 0;
    if (p.Count < 3)
        return 0;
    for (uint32_t i = 1; i + 1 < p.Count; ++i)
        area += std::abs(Edge(p.Points[0], p.Points[i], p.Points[i + 1])) * 0.5;
    return area;
}
/// <summary>Measure positive UV triangle overlap, excluding shared zero-area edges.</summary>
double Overlap(const Triangle &a, const Triangle &b) {
    auto p = TrianglePolygon(a), q = TrianglePolygon(b);
    for (uint32_t i = 0; i < 3; ++i)
        p = Clip(p, q.Points[i], q.Points[(i + 1) % 3]);
    return Area(p);
}
/// <summary>Sample clipped triangle coverage uniformly by fan area, including arbitrarily thin coverage.</summary>
Point2 SamplePolygon(const Polygon &p, Random &r) {
    double total = Area(p), choice = r.Next() * total;
    uint32_t selected = p.Count - 2;
    for (uint32_t i = 1; i + 1 < p.Count; ++i) {
        double a = std::abs(Edge(p.Points[0], p.Points[i], p.Points[i + 1])) * 0.5;
        if (choice <= a) {
            selected = i;
            break;
        }
        choice -= a;
    }
    double u = std::sqrt(r.Next()), v = r.Next();
    const auto a = p.Points[0], b = p.Points[selected], c = p.Points[selected + 1];
    return {a.X * (1 - u) + b.X * u * (1 - v) + c.X * u * v, a.Y * (1 - u) + b.Y * u * (1 - v) + c.Y * u * v};
}
/// <summary>Recover source-triangle weights while removing floating-point boundary overshoot.</summary>
Vec2 Barycentric(const Triangle &t, Point2 uv) {
    double divisor = Edge(t.Uv[0], t.Uv[1], t.Uv[2]);
    double u = Edge(t.Uv[0], uv, t.Uv[2]) / divisor, v = Edge(t.Uv[0], t.Uv[1], uv) / divisor;
    // Clipping arithmetic can land several ULP outside an edge; normalize these boundary coordinates.
    u = std::max(0.0, std::min(1.0, u));
    v = std::max(0.0, std::min(1.0 - u, v));
    return {float(u), float(v)};
}
} // namespace hellm::detail
