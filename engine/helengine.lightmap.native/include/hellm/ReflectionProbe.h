#pragma once
#include <hellm/Vec3.h>
#include <cstdint>
#include <vector>
namespace hellm {
/// <summary>Linear HDR scene radiance prefiltered for GGX reflection, without analytic direct-sun emission.</summary>
struct ReflectionProbe {
    /// <summary>Power-of-two edge length of the base cube; zero identifies a disabled probe.</summary>
    uint32_t FaceSize = 0;
    /// <summary>World-space origin used by all six capture faces.</summary>
    Vec3 Position{};
    /// <summary>Minimum world-space box-projection corner.</summary>
    Vec3 BoundsMin{};
    /// <summary>Maximum world-space box-projection corner.</summary>
    Vec3 BoundsMax{};
    /// <summary>Full chain of six-face row-major RGBA32F arrays in D3D +X,-X,+Y,-Y,+Z,-Z order. Row zero is the top; mip roughness is level/(count-1).</summary>
    std::vector<std::vector<float>> Mips;
};
} // namespace hellm
