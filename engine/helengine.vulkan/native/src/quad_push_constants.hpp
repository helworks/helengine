#pragma once
#include <array>
#include <cstdint>

namespace helengine::vulkan {
/// <summary>Defines the explicit float32/uint32 Vulkan push-constant ABI shared by textured quads and signed-distance UI shapes.</summary>
struct QuadPushConstants {
    /// <summary>Stores the physical framebuffer width and height for clip-space conversion.</summary>
    std::array<float,2> TargetSize {};
    /// <summary>Stores shape dimensions in pixels for the rounded-rectangle distance field.</summary>
    std::array<float,2> ShapeSize {};
    /// <summary>Stores the clamped corner radius in physical pixels.</summary>
    float Radius = 0;
    /// <summary>Stores the inward border width in physical pixels.</summary>
    float BorderThickness = 0;
    /// <summary>Stores engine corner bits: top-left1, top-right2, bottom-left4, bottom-right8.</summary>
    uint32_t Corners = 0;
    /// <summary>Selects textured sampling (0) or a signed-distance rounded rectangle (1).</summary>
    uint32_t Mode = 0;
    /// <summary>Stores a straight-alpha linear border color independent of engine scalar storage.</summary>
    std::array<float,4> BorderColor {};
};
static_assert(sizeof(QuadPushConstants) == 48);
}
