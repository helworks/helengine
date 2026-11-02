#pragma once
#include <cstdint>
#include <string>
namespace helengine::vulkan {
/// <summary>Owns copied scene and asset identities for one measured submesh occurrence in a frame.</summary>
struct VulkanFrameTraceSubject final {
    /// <summary>Frame-local identity of the owning entity; never exposes a process address.</summary>
    int ObjectId;
    /// <summary>Authored scene entity ID, with zero representing unavailable metadata.</summary>
    uint32_t EntityId;
    /// <summary>Actual runtime model asset identifier.</summary>
    std::string ModelId;
    /// <summary>Actual runtime material asset identifier.</summary>
    std::string MaterialId;
    /// <summary>Native immutable mesh handle, distinct from the model asset identifier.</summary>
    uint64_t MeshId;
    /// <summary>Index of the authored submesh within the model.</summary>
    int SubmeshIndex;
    /// <summary>Actual triangle index count submitted for this submesh.</summary>
    uint32_t IndexCount;
};
}
