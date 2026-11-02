#pragma once
#include "Internal.h"

namespace hellm::detail {
/// <summary>Nearest two-sided ray intersection with original triangle correspondence.</summary>
struct Hit {
    /// <summary>Index into the immutable original-order triangle collection.</summary>
    uint32_t TriangleIndex = 0;
    /// <summary>Closest accepted distance along the unit ray.</summary>
    double Distance = INFINITY;
    /// <summary>Barycentric weight of the second corner.</summary>
    double U = 0;
    /// <summary>Barycentric weight of the third corner.</summary>
    double V = 0;
};
} // namespace hellm::detail
