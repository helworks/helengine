#pragma once
#include <cstdint>
#include <hellm/Vec3.h>
#include <vector>

namespace hellm {
/// <summary>Linear RGB diffuse reflectance sampled with repeating bilinear UV0 filtering.</summary>
struct Texture {
    /// <summary>Texture width; zero denotes an untextured material.</summary>
    uint32_t Width = 0;
    /// <summary>Texture height, zero together with Width for an untextured material.</summary>
    uint32_t Height = 0;
    /// <summary>Top-to-bottom row-major linear RGB reflectance, with each channel in [0,1].</summary>
    std::vector<Vec3> Pixels;
};
} // namespace hellm
