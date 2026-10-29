#pragma once
#include <cstdint>
#include <string_view>

namespace helengine::vulkan {
/// <summary>Restricts mesh batching to the built-in opaque shader contracts understood by the native renderer.</summary>
class VulkanMeshBatchEligibility final {
public:
    /// <summary>Checks authored shader identity, exact programs, supported variants and opaque blend state before geometry can be merged.</summary>
    static bool Supports(std::string_view shaderId,std::string_view vertexProgram,std::string_view pixelProgram,std::string_view variant,uint32_t blendMode) {
        return shaderId=="ForwardStandardShader" && vertexProgram=="ForwardStandardShader.vs" && pixelProgram=="ForwardStandardShader.ps" && (variant=="default" || variant=="ForwardStandard" || variant=="ForwardStandardShadowed") && blendMode==0;
    }
};
}
