#pragma once
#include <cstdint>
#include <string_view>
namespace helengine::vulkan {
/// <summary>Orders compiler-owned source locations without copying their static strings per scope.</summary>
struct VulkanFrameTraceSourceKey final {
    /// <summary>Identifies the compiler-reported translation unit path.</summary>
    std::string_view File;
    /// <summary>Identifies the actual containing function independently of the scope label.</summary>
    std::string_view Function;
    /// <summary>Identifies the source line at which the instrumentation scope was constructed.</summary>
    uint32_t Line;
    /// <summary>Provides stable lexical ordering for frame-local source interning.</summary>
    bool operator<(const VulkanFrameTraceSourceKey& other) const {
        if(File!=other.File) return File<other.File;
        if(Function!=other.Function) return Function<other.Function;
        return Line<other.Line;
    }
};
}
