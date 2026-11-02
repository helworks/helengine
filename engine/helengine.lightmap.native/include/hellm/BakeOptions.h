#pragma once
#include <cstdint>
#include <hellm/BakeBackend.h>
#include <functional>
#include <string>

namespace hellm {
/// <summary>Deterministic bake controls; worker count does not affect the result.</summary>
struct BakeOptions {
    /// <summary>Explicit transport backend; CPU remains available without Vulkan build dependencies.</summary>
    BakeBackend Backend = BakeBackend::Cpu;
    /// <summary>Maximum occupied texels per GPU submission, further bounded by sample/bounce cost; does not affect sampling.</summary>
    uint32_t GpuBatchTexels = 4096;
    /// <summary>Square atlas width; the first-stage runtime samples only mip level zero.</summary>
    uint32_t Width = 4096;
    /// <summary>Power-of-two reflection capture face edge in [1,1024]; used only by enabled probes.</summary>
    uint32_t ProbeFaceSize = 128;
    /// <summary>Independent sun and material path samples per reflection capture pixel.</summary>
    uint32_t ProbeSamples = 128;
    /// <summary>GGX importance samples per filtered reflection mip pixel.</summary>
    uint32_t ProbeFilterSamples = 128;
    /// <summary>Square atlas height, required to equal Width.</summary>
    uint32_t Height = 4096;
    /// <summary>Independent area and path samples for every triangle/texel intersection.</summary>
    uint32_t SamplesPerTexel = 128;
    /// <summary>Maximum diffuse reflecting surfaces beyond the receiver; zero selects direct only.</summary>
    uint32_t MaxBounces = 4;
    /// <summary>Chart-owned gutter width in full-resolution texels, at least two.</summary>
    uint32_t Padding = 4;
    /// <summary>Worker count; zero uses available logical processors, limited to 64.</summary>
    uint32_t WorkerCount = 0;
    /// <summary>Deterministic seed combined with atlas texel identity.</summary>
    uint64_t Seed = 1;
    /// <summary>World-space geometric-normal ray-origin offset; must match scene scale.</summary>
    float RayEpsilon = 0.0001f;
    /// <summary>Full apparent sun diameter in degrees, finite in [0,20]; zero preserves legacy directional sampling.</summary>
    float SunAngularDiameterDegrees = 0;
    /// <summary>Caller-thread progress callback receiving stage, completed count and total count.</summary>
    std::function<void(const std::string &, uint32_t, uint32_t)> Progress;
};
} // namespace hellm
