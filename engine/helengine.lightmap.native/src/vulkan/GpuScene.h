#pragma once
#include "GpuTriangle.h"
#include "GpuMaterial.h"
#include <array>
#include <vector>
namespace hellm::detail {
/// <summary>Static scene data uploaded once for all irradiance batches.</summary>
struct GpuScene {
    /// <summary>World-space triangles in original primitive order.</summary>
    std::vector<GpuTriangle> Triangles;
    /// <summary>Materials referenced by triangle metadata.</summary>
    std::vector<GpuMaterial> Materials;
    /// <summary>Concatenated linear RGBA material texture pixels.</summary>
    std::vector<std::array<float, 4>> Pixels;
};
} // namespace hellm::detail
