#pragma once
#include <cstdint>
namespace hellm::detail {
/// <summary>Explicit std430 layout shared with the Vulkan irradiance shader.</summary>
struct alignas(16) GpuCoverage {
    /// <summary>Triangle index, polygon vertex count and reserved words.</summary>
    uint32_t Metadata[4];
    /// <summary>Coverage area, total polygon fan area and reserved components.</summary>
    float Measures[4];
    /// <summary>Barycentric UV vertices and fan-triangle areas at each fan endpoint.</summary>
    float Points[12][4];
};
static_assert(sizeof(GpuCoverage) == 224, "Shader layout mismatch");
} // namespace hellm::detail
