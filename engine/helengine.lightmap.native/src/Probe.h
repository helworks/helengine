#pragma once
#include "Transport.h"
namespace hellm::detail {
/// <summary>Map a top-left-origin D3D cube face coordinate in [-1,1] to its unit world direction.</summary>
Vec3 CubeDirection(uint32_t face, double horizontal, double vertical);
/// <summary>Sample a seamless bilinear six-face RGBA32F cube in the documented D3D orientation.</summary>
Vec3 SampleCube(const std::vector<float> &data, uint32_t size, Vec3 direction);
/// <summary>Build the full GGX perceptual-roughness mip chain from an unfiltered HDR base capture.</summary>
void FilterProbe(ReflectionProbe &probe, const BakeOptions &options);
/// <summary>Capture optional scene radiance on the selected CPU or Vulkan backend and prefilter offline.</summary>
void BakeProbe(const Scene &scene, const BakeOptions &options, const std::vector<Triangle> &triangles, Lightmap &result);
} // namespace hellm::detail
