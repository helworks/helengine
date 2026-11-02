#pragma once
#include "Bvh.h"
#include "Random.h"
namespace hellm::detail {
/// <summary>Sample the shading hemisphere with density cos(theta)/pi.</summary>
Vec3 CosineDirection(Vec3 normal, Random &random);
/// <summary>Return one minus the spherical-cap half-angle cosine using a stable small-angle expression.</summary>
double SunCapSpan(float diameterDegrees);
/// <summary>Sample a uniform solid-angle spherical cap, consuming no random numbers when its span is zero.</summary>
Vec3 SunDirection(Vec3 sun, double capSpan, Random &random);
/// <summary>Evaluate visible directional irradiance with shading and geometric hemisphere checks.</summary>
Vec3 Direct(const Bvh &bvh, Vec3 position, Vec3 normal, Vec3 geometric, Vec3 sun, Vec3 radiance, double epsilon, double capSpan, Random &random);
/// <summary>Trace material-reflected scene radiance for a bounded number of hit surfaces; misses return black.</summary>
Vec3 RadianceSample(const Bvh &bvh, const std::vector<Triangle> &triangles, Vec3 position, Vec3 direction,
                    const Scene &scene, const BakeOptions &options, double capSpan, uint32_t surfaces, Random &random);
/// <summary>Estimate direct irradiance plus finite diffuse sun paths, applying albedo only at secondary hits.</summary>
Vec3 IrradianceSample(const Bvh &bvh, const std::vector<Triangle> &triangles, uint32_t triangle, double u, double v,
                      const Scene &scene, const BakeOptions &options, double capSpan, Random &random);
} // namespace hellm::detail
