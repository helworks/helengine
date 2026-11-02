#pragma once
#include "GpuScene.h"
#include "GpuTexel.h"
#include "GpuCoverage.h"
#include "GpuParameters.h"
#include <memory>
#include <string>
namespace hellm::detail {
/// <summary>Own a capability-checked Vulkan ray-query device and static scene acceleration structures.</summary>
class VulkanTransport {
    /// <summary>Hide Vulkan handles and loader types from CPU-only translation units.</summary>
    class Implementation;
    /// <summary>Exclusive ownership guarantees teardown after partial initialization failures.</summary>
    std::unique_ptr<Implementation> State;
public:
    /// <summary>Load Vulkan and upload the scene, rejecting unavailable capabilities explicitly.</summary>
    explicit VulkanTransport(const GpuScene &scene);
    /// <summary>Wait for submitted work and release all device and loader resources.</summary>
    ~VulkanTransport();
    /// <summary>Disallow copying device ownership and live Vulkan handles.</summary>
    VulkanTransport(const VulkanTransport &) = delete;
    /// <summary>Disallow assigning another device into a live transport.</summary>
    VulkanTransport &operator=(const VulkanTransport &) = delete;
    /// <summary>Return the actual selected Vulkan device name.</summary>
    std::string DeviceName() const;
    /// <summary>Evaluate one bounded coverage batch and read back finite linear RGBA irradiance.</summary>
    void Trace(const std::vector<GpuTexel> &texels, const std::vector<GpuCoverage> &coverages,
        const GpuParameters &parameters, std::vector<float> &rgba);
};
} // namespace hellm::detail
