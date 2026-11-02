#pragma once
#include <array>
namespace helengine::vulkan {
/// <summary>Defines the fixed GPU vertex ABI in float32, independent of the engine's configurable scalar policy.</summary>
struct QuadVertex {
    /// <summary>Stores a target-pixel position, translated to clip space by the vertex shader.</summary>
    std::array<float, 2> Position;
    /// <summary>Stores normalized texture coordinates authored by the generated 2D command stream.</summary>
    std::array<float, 2> Uv;
    /// <summary>Stores straight-alpha linear tint in the GPU shader's explicit float32 layout.</summary>
    std::array<float, 4> Color;
};
static_assert(sizeof(QuadVertex) == 32);
}
