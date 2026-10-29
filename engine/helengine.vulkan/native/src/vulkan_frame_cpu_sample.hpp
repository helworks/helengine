#pragma once
#include <cstdint>
#include <string>

namespace helengine::vulkan {
/// <summary>Owns one completed provider interval until post-frame JSON export, independent of renderer scope nesting.</summary>
class VulkanFrameCpuSample final {
public:
    /// <summary>Copies a software stage or registered type name because callers need not retain temporary labels.</summary>
    std::string Name;
    /// <summary>Stores measured microsecond start converted relative to the exact native nanosecond frame origin.</summary>
    int64_t Start;
    /// <summary>Stores measured microsecond end converted relative to the exact native nanosecond frame origin.</summary>
    int64_t End;
    /// <summary>Retains the generated component type hash; zero identifies a stage.</summary>
    uint32_t TypeHash;
    /// <summary>Retains the stable component registration identifier; zero identifies a stage.</summary>
    uint64_t RegistrationId;
    /// <summary>Retains the recorded engine entity identifier without dereferencing component state.</summary>
    uint32_t EntityId;
    /// <summary>Retains the component index within the update loop; minus one identifies a stage.</summary>
    int32_t UpdateIndex;
};
}
