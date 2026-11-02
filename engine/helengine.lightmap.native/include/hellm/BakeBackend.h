#pragma once
namespace hellm {
/// <summary>Selects the explicitly requested transport implementation; unavailable GPU support is an error.</summary>
enum class BakeBackend {
    /// <summary>Portable multithreaded CPU reference transport.</summary>
    Cpu,
    /// <summary>Vulkan hardware ray queries with bounded compute dispatches.</summary>
    Vulkan
};
} // namespace hellm
