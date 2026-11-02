#pragma once
#include "Coverage.h"
#include "Triangle.h"
namespace hellm::detail {
/// <summary>Preserve exact CPU coverage while evaluating only occupied texels through bounded Vulkan batches.</summary>
void TraceVulkan(const Scene &scene, const BakeOptions &options, const std::vector<Triangle> &triangles,
    const std::vector<int32_t> &heads, const std::vector<Coverage> &samples, Lightmap &result);
/// <summary>Trace reflection-probe primary rays on the selected Vulkan ray-query device without CPU fallback.</summary>
void TraceProbeVulkan(const Scene &scene, const BakeOptions &options, const std::vector<Triangle> &triangles,
    std::vector<float> &rgba);
} // namespace hellm::detail
