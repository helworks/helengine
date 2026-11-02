#pragma once
#include <cstdint>
namespace hellm::detail {
/// <summary>Explicit std430 layout shared with the Vulkan irradiance shader.</summary>
struct alignas(16) GpuTriangle {
    /// <summary>First world-space vertex.</summary>
    float A[4];
    /// <summary>Second world-space vertex.</summary>
    float B[4];
    /// <summary>Third world-space vertex.</summary>
    float C[4];
    /// <summary>First shading normal.</summary>
    float Na[4];
    /// <summary>Second shading normal.</summary>
    float Nb[4];
    /// <summary>Third shading normal.</summary>
    float Nc[4];
    /// <summary>UV0 coordinates for the first two vertices.</summary>
    float Uv01[4];
    /// <summary>UV0 coordinates for the third vertex.</summary>
    float Uv2[4];
    /// <summary>Material index followed by reserved words.</summary>
    uint32_t Metadata[4];
};
static_assert(sizeof(GpuTriangle) == 144, "Shader layout mismatch");
} // namespace hellm::detail
