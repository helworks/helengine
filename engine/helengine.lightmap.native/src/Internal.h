#pragma once
#include <algorithm>
#include <cmath>
#include <hellm/Lightmap.h>
#include <stdexcept>
namespace hellm::detail {
/// <summary>Circle constant used by the normalized Lambertian cosine sampler.</summary>
constexpr double Pi = 3.14159265358979323846;
/// <summary>Add world-space or RGB components without changing their units.</summary>
inline Vec3 Add(Vec3 a, Vec3 b) {
    return {a.X + b.X, a.Y + b.Y, a.Z + b.Z};
}
/// <summary>Subtract world-space components to form an edge or displacement.</summary>
inline Vec3 Sub(Vec3 a, Vec3 b) {
    return {a.X - b.X, a.Y - b.Y, a.Z - b.Z};
}
/// <summary>Scale a vector using double intermediates and float storage.</summary>
inline Vec3 Mul(Vec3 a, double s) {
    return {float(a.X * s), float(a.Y * s), float(a.Z * s)};
}
/// <summary>Multiply linear RGB reflectances or path throughput component by component.</summary>
inline Vec3 Product(Vec3 a, Vec3 b) {
    return {a.X * b.X, a.Y * b.Y, a.Z * b.Z};
}
/// <summary>Compute the scalar product with double accumulation.</summary>
inline double Dot(Vec3 a, Vec3 b) {
    return double(a.X) * b.X + double(a.Y) * b.Y + double(a.Z) * b.Z;
}
/// <summary>Compute an oriented world-space area vector with double intermediates.</summary>
inline Vec3 Cross(Vec3 a, Vec3 b) {
    return {float(double(a.Y) * b.Z - double(a.Z) * b.Y), float(double(a.Z) * b.X - double(a.X) * b.Z),
            float(double(a.X) * b.Y - double(a.Y) * b.X)};
}
/// <summary>Normalize a required nonzero vector, rejecting invalid initialization.</summary>
inline Vec3 Unit(Vec3 a) {
    const double l = std::sqrt(Dot(a, a));
    if (!(l > 1e-20))
        throw std::runtime_error("Zero-length surface normal or sun direction");
    return Mul(a, 1 / l);
}
/// <summary>Check that all stored components are finite before publishing results.</summary>
inline bool Finite(Vec3 a) {
    return std::isfinite(a.X) && std::isfinite(a.Y) && std::isfinite(a.Z);
}
/// <summary>Evaluate a world-space point or shading normal with original-order barycentric weights.</summary>
inline Vec3 Interpolate(Vec3 a, Vec3 b, Vec3 c, double u, double v) {
    return Add(Mul(a, 1 - u - v), Add(Mul(b, u), Mul(c, v)));
}
/// <summary>Reject missing geometry, invalid materials, duplicate keys and degenerate world triangles.</summary>
void ValidateScene(const Scene &scene);
/// <summary>Enforce supported atlas, padding, sample count and transport limits.</summary>
void ValidateOptions(const BakeOptions &options);
/// <summary>Reject malformed atlas dimensions, HDR values and original-corner bindings.</summary>
void ValidateLightmap(const Lightmap &map);
} // namespace hellm::detail
