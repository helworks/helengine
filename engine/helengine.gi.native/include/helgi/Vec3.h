#pragma once

namespace helgi {
/// <summary>Fixed 32-bit floating-point vector used at the portable GPU boundary.</summary>
struct Vec3 {
    /// <summary>World-space X or red component.</summary>
    float X;
    /// <summary>World-space Y or green component.</summary>
    float Y;
    /// <summary>World-space Z or blue component.</summary>
    float Z;
};
}
