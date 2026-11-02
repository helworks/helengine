#pragma once
#include "Internal.h"
#include "Point2.h"
#include <array>
namespace hellm::detail {
/// <summary>Small convex polygon used for exact triangle/texel coverage and area sampling.</summary>
struct Polygon {
    /// <summary>Convex clipped boundary, in counterclockwise atlas texel coordinates.</summary>
    std::array<Point2, 12> Points;
    /// <summary>Number of initialized boundary vertices.</summary>
    uint32_t Count = 0;
};
} // namespace hellm::detail
