#include "Transport.h"
#include "Brdf.h"
#include <array>
#include <numeric>
namespace hellm::detail {
namespace {
/// <summary>Read the requested world coordinate for slab and split-axis calculations.</summary>
float Component(Vec3 v, int axis) {
    return axis == 0 ? v.X : (axis == 1 ? v.Y : v.Z);
}
/// <summary>Wrap signed bilinear texture coordinates into a valid repeating texel index.</summary>
uint32_t Wrap(int64_t i, uint32_t size) {
    const auto value = i % int64_t(size);
    return uint32_t(value < 0 ? value + size : value);
}
} // namespace
/// <summary>Sample original UV0 with repeating bilinear linear-RGB filtering and apply the material tint.</summary>
Vec3 Triangle::Reflectance(double u, double v) const {
    const auto &m = *Material;
    const auto &t = m.DiffuseTexture;
    if (t.Pixels.empty())
        return m.Albedo;
    const auto a = m.TexCoords[m.Indices[Corner]], b = m.TexCoords[m.Indices[Corner + 1]],
               c = m.TexCoords[m.Indices[Corner + 2]];
    const double ux = a.X * (1 - u - v) + b.X * u + c.X * v, uy = a.Y * (1 - u - v) + b.Y * u + c.Y * v;
    const double x = (ux - std::floor(ux)) * t.Width - 0.5, y = (uy - std::floor(uy)) * t.Height - 0.5;
    const auto ix = int64_t(std::floor(x)), iy = int64_t(std::floor(y));
    const double fx = x - ix, fy = y - iy;
    auto p00 = t.Pixels[Wrap(iy, t.Height) * t.Width + Wrap(ix, t.Width)],
         p10 = t.Pixels[Wrap(iy, t.Height) * t.Width + Wrap(ix + 1, t.Width)];
    auto p01 = t.Pixels[Wrap(iy + 1, t.Height) * t.Width + Wrap(ix, t.Width)],
         p11 = t.Pixels[Wrap(iy + 1, t.Height) * t.Width + Wrap(ix + 1, t.Width)];
    return Product(m.Albedo,
                   Add(Mul(Add(Mul(p00, 1 - fx), Mul(p10, fx)), 1 - fy), Mul(Add(Mul(p01, 1 - fx), Mul(p11, fx)), fy)));
}
/// <summary>Expand the world-space bounds to contain one triangle corner.</summary>
void Bounds::Include(Vec3 p) {
    Min = {std::min(Min.X, p.X), std::min(Min.Y, p.Y), std::min(Min.Z, p.Z)};
    Max = {std::max(Max.X, p.X), std::max(Max.Y, p.Y), std::max(Max.Z, p.Z)};
}
/// <summary>Intersect a ray with all three slabs, handling parallel rays without division by zero.</summary>
bool Bounds::Intersects(Vec3 o, Vec3 d, double limit) const {
    double near = 0, far = limit;
    for (int a = 0; a < 3; ++a) {
        double origin = Component(o, a), direction = Component(d, a), lo = Component(Min, a), hi = Component(Max, a);
        if (std::abs(direction) < 1e-30) {
            if (origin < lo || origin > hi)
                return false;
            continue;
        }
        double t0 = (lo - origin) / direction, t1 = (hi - origin) / direction;
        if (t0 > t1)
            std::swap(t0, t1);
        near = std::max(near, t0);
        far = std::min(far, t1);
        if (near > far)
            return false;
    }
    return true;
}
/// <summary>Initialize the triangle permutation and balanced BVH once before parallel tracing.</summary>
Bvh::Bvh(const std::vector<Triangle> &triangles, double epsilon) : Triangles(triangles), Epsilon(epsilon) {
    Order.resize(triangles.size());
    std::iota(Order.begin(), Order.end(), 0);
    Nodes.reserve(triangles.size() * 2);
    Build(0, uint32_t(Order.size()));
}
/// <summary>Construct a deterministic balanced median-split subtree over triangle centroids.</summary>
uint32_t Bvh::Build(uint32_t begin, uint32_t end) {
    uint32_t index = uint32_t(Nodes.size());
    Nodes.emplace_back();
    Bounds box, centers;
    for (uint32_t i = begin; i < end; ++i) {
        const auto &t = Triangles[Order[i]];
        box.Include(t.A);
        box.Include(t.B);
        box.Include(t.C);
        centers.Include(Mul(Add(t.A, Add(t.B, t.C)), 1.0 / 3));
    }
    Nodes[index].Box = box;
    if (end - begin <= 4) {
        Nodes[index].Begin = begin;
        Nodes[index].Count = end - begin;
        return index;
    }
    const auto extent = Sub(centers.Max, centers.Min);
    int axis = extent.Y > extent.X ? 1 : 0;
    if (Component(extent, 2) > Component(extent, axis))
        axis = 2;
    std::sort(Order.begin() + begin, Order.begin() + end, [this, axis](uint32_t a, uint32_t b) {
        const auto &x = Triangles[a];
        const auto &y = Triangles[b];
        double ca = Component(x.A, axis) + Component(x.B, axis) + Component(x.C, axis),
               cb = Component(y.A, axis) + Component(y.B, axis) + Component(y.C, axis);
        return ca == cb ? a < b : ca < cb;
    });
    const auto mid = begin + (end - begin) / 2;
    auto left = Build(begin, mid), right = Build(mid, end);
    Nodes[index].Left = left;
    Nodes[index].Right = right;
    return index;
}
/// <summary>Walk immutable BVH nodes, returning either the nearest intersection or the first blocker.</summary>
bool Bvh::Traverse(Vec3 o, Vec3 d, Hit &hit, bool anyHit) const {
    bool found = false;
    std::array<uint32_t, 128> stack;
    size_t size = 0;
    stack[size++] = 0;
    while (size) {
        const auto &node = Nodes[stack[--size]];
        if (!node.Box.Intersects(o, d, hit.Distance))
            continue;
        if (!node.Count) {
            if (size + 2 > stack.size())
                throw std::runtime_error("BVH traversal stack exhausted");
            stack[size++] = node.Left;
            stack[size++] = node.Right;
            continue;
        }
        for (uint32_t j = node.Begin; j < node.Begin + node.Count; ++j) {
            const auto ti = Order[j];
            const auto &t = Triangles[ti];
            auto e1 = Sub(t.B, t.A), e2 = Sub(t.C, t.A), p = Cross(d, e2);
            double determinant = Dot(e1, p);
            if (std::abs(determinant) < 1e-20)
                continue;
            double inv = 1 / determinant;
            auto s = Sub(o, t.A);
            double u = Dot(s, p) * inv;
            if (u < 0 || u > 1)
                continue;
            auto q = Cross(s, e1);
            double v = Dot(d, q) * inv;
            if (v < 0 || u + v > 1)
                continue;
            double distance = Dot(e2, q) * inv;
            if (distance > Epsilon * 0.1 && distance < hit.Distance) {
                hit = {ti, distance, u, v};
                if (anyHit)
                    return true;
                found = true;
            }
        }
    }
    return found;
}
/// <summary>Find the closest two-sided intersection beyond the origin exclusion distance.</summary>
bool Bvh::Intersect(Vec3 origin, Vec3 direction, Hit &hit) const {
    return Traverse(origin, direction, hit, false);
}
/// <summary>Stop at the first two-sided blocker along the directional sun ray.</summary>
bool Bvh::Occluded(Vec3 origin, Vec3 direction) const {
    Hit hit;
    return Traverse(origin, direction, hit, true);
}
/// <summary>Sample the shading hemisphere with density cos(theta)/pi.</summary>
Vec3 CosineDirection(Vec3 n, Random &r) {
    double radius = std::sqrt(r.Next()), angle = 2 * Pi * r.Next();
    Vec3 tangent = Unit(Cross(std::abs(n.Z) < 0.9 ? Vec3{0, 0, 1} : Vec3{0, 1, 0}, n)), bitangent = Cross(n, tangent);
    return Unit(Add(Add(Mul(tangent, radius * std::cos(angle)), Mul(bitangent, radius * std::sin(angle))),
                    Mul(n, std::sqrt(std::max(0.0, 1 - radius * radius)))));
}
/// <summary>Return one minus the spherical-cap half-angle cosine using a stable small-angle expression.</summary>
double SunCapSpan(float diameterDegrees) {
    const double sine = std::sin(double(diameterDegrees) * Pi / 720);
    return 2 * sine * sine;
}
/// <summary>Sample the cap uniformly in cosine and azimuth, preserving the legacy zero-angle random stream.</summary>
Vec3 SunDirection(Vec3 sun, double capSpan, Random &random) {
    if (capSpan == 0)
        return sun;
    const double offset = capSpan * random.Next(), angle = 2 * Pi * random.Next();
    const double sine = std::sqrt(offset * (2 - offset));
    const Vec3 tangent = Unit(Cross(std::abs(sun.Z) < 0.9 ? Vec3{0, 0, 1} : Vec3{0, 1, 0}, sun)), bitangent = Cross(sun, tangent);
    return Unit(Add(Add(Mul(tangent, sine * std::cos(angle)), Mul(bitangent, sine * std::sin(angle))),
                    Mul(sun, 1 - offset)));
}
/// <summary>Evaluate visible directional irradiance with shading and geometric hemisphere checks.</summary>
Vec3 Direct(const Bvh &bvh, Vec3 position, Vec3 normal, Vec3 geometric, Vec3 sun, Vec3 radiance, double epsilon, double capSpan, Random &random) {
    sun = SunDirection(sun, capSpan, random);
    double cosine = Dot(normal, sun);
    if (cosine <= 0 || Dot(geometric, sun) <= 0)
        return {};
    if (bvh.Occluded(Add(position, Mul(geometric, epsilon)), sun))
        return {};
    // E[cos(theta)] = (1 + cos(halfAngle))/2; preserve normal-incidence integrated irradiance.
    if (capSpan > 0)
        cosine /= 1 - capSpan * 0.5;
    return Mul(radiance, cosine);
}
/// <summary>Trace finite scene radiance with material BRDF importance sampling; a miss has no invented sky or direct sun disk.</summary>
Vec3 RadianceSample(const Bvh &bvh, const std::vector<Triangle> &triangles, Vec3 position, Vec3 direction,
                    const Scene &scene, const BakeOptions &o, double capSpan, uint32_t surfaces, Random &random) {
    Vec3 result{}, throughput{1,1,1};
    for (uint32_t bounce = 0; bounce < surfaces; ++bounce) {
        Hit hit;
        if (!bvh.Intersect(position, direction, hit)) break;
        const auto &t = triangles[hit.TriangleIndex];
        position = t.Position(hit.U, hit.V);
        Vec3 normal = t.Normal(hit.U, hit.V), geometric = t.Geometric;
        if (Dot(geometric, direction) > 0) geometric = Mul(geometric, -1);
        if (Dot(normal, geometric) < 0) normal = Mul(normal, -1);
        if (Dot(normal, direction) >= 0) normal = geometric;
        const Vec3 outgoing = Mul(direction, -1), base = t.Reflectance(hit.U, hit.V);
        const auto &material = *t.Material;
        const Vec3 sun = SunDirection(Unit(scene.Sun.Direction), capSpan, random);
        const double cosine = Dot(normal, sun);
        if (cosine > 0 && Dot(geometric, sun) > 0 && !bvh.Occluded(Add(position, Mul(geometric, o.RayEpsilon)), sun)) {
            const Vec3 reflected = Brdf(base, material.Metallic, material.Roughness, normal, outgoing, sun);
            result = Add(result, Product(throughput, Product(reflected, Mul(scene.Sun.Radiance, cosine / (1 - capSpan * 0.5)))));
        }
        if (bounce + 1 == surfaces) break;
        Vec3 weight;
        direction = SampleBrdf(base, material.Metallic, material.Roughness, normal, outgoing, random, weight);
        if (Dot(direction, geometric) <= 0 || std::max({weight.X,weight.Y,weight.Z}) <= 0) break;
        throughput = Product(throughput, weight);
        position = Add(position, Mul(geometric, o.RayEpsilon));
    }
    return result;
}
/// <summary>Estimate material-independent receiver irradiance with a cosine primary ray and GGX metallic secondary paths.</summary>
Vec3 IrradianceSample(const Bvh &bvh, const std::vector<Triangle> &triangles, uint32_t ti, double u, double v,
                      const Scene &scene, const BakeOptions &o, double capSpan, Random &random) {
    const auto &initial = triangles[ti];
    const auto position = initial.Position(u, v), normal = initial.Normal(u, v);
    auto geometric = initial.Geometric;
    if (Dot(geometric, normal) < 0) geometric = Mul(geometric, -1);
    Vec3 result = Direct(bvh, position, normal, geometric, Unit(scene.Sun.Direction), scene.Sun.Radiance, o.RayEpsilon, capSpan, random);
    if (o.MaxBounces == 0) return result;
    const Vec3 direction = CosineDirection(normal, random);
    if (Dot(direction, geometric) <= 0) return result;
    return Add(result, Mul(RadianceSample(bvh, triangles, Add(position, Mul(geometric,o.RayEpsilon)), direction,
                                         scene, o, capSpan, o.MaxBounces, random), Pi));
}
} // namespace hellm::detail
