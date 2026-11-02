#pragma once
#include "Internal.h"
#include <array>
namespace hellm::detail {
/// <summary>One exact triangle/texel intersection; multiple faces are area-averaged at seams.</summary>
struct Coverage {
    /// <summary>Original triangle whose area intersects this texel.</summary>
    uint32_t TriangleIndex;
    /// <summary>Next same-texel coverage entry, or minus one at the end.</summary>
    int32_t Next;
    /// <summary>Exact clipped area used to average faces meeting inside a texel.</summary>
    float Area;
};
} // namespace hellm::detail
