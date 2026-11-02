#pragma once
#include "Transport.h"
namespace hellm::detail {
/// <summary>Evaluate the energy-conserving diffuse plus normalized GGX/Smith/Schlick metallic BRDF.</summary>
Vec3 Brdf(Vec3 baseColor, double metallic, double roughness, Vec3 normal, Vec3 outgoing, Vec3 incoming);
/// <summary>Sample the GGX-NDF/diffuse mixture, returning f*cos/pdf in weight; rejected below-surface samples have zero weight.</summary>
Vec3 SampleBrdf(Vec3 baseColor, double metallic, double roughness, Vec3 normal, Vec3 outgoing, Random &random, Vec3 &weight);
/// <summary>Sample a GGX normal-distribution half-vector with alpha equal to squared perceptual roughness.</summary>
Vec3 GgxHalfVector(Vec3 normal, double roughness, double first, double second);
} // namespace hellm::detail
