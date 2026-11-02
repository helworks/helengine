#pragma once

namespace hellm {
/// <summary>Three-component world-space vector or linear RGB value.</summary>
struct Vec3 {
    /// <summary>World X coordinate or linear red component.</summary>
    float X = 0;
    /// <summary>World Y coordinate or linear green component.</summary>
    float Y = 0;
    /// <summary>World Z coordinate or linear blue component.</summary>
    float Z = 0;
};
} // namespace hellm
