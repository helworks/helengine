#pragma once
#include <cstdint>
#include <functional>
#include <string>
namespace helengine::vulkan {
/// <summary>Identifies one object/material/submesh occurrence for frame-local profiling without exporting pointers.</summary>
struct VulkanFrameTraceSubjectKey final {
    /// <summary>Borrows entity identity only for comparison during the current measured frame.</summary>
    const void* Object;
    /// <summary>Owns the authored runtime material identifier used by this occurrence.</summary>
    std::string MaterialId;
    /// <summary>Distinguishes immutable native geometry handles.</summary>
    uint64_t MeshId;
    /// <summary>Distinguishes authored submesh ranges in the same geometry.</summary>
    int SubmeshIndex;
    /// <summary>Uses the standard total pointer ordering before comparing value identities.</summary>
    bool operator<(const VulkanFrameTraceSubjectKey& other) const {
        if(Object!=other.Object) return std::less<const void*>{}(Object,other.Object);
        if(MaterialId!=other.MaterialId) return MaterialId<other.MaterialId;
        if(MeshId!=other.MeshId) return MeshId<other.MeshId;
        return SubmeshIndex<other.SubmeshIndex;
    }
};
}
