#pragma once
#include "Internal.h"

namespace hellm::detail {
/// <summary>Axis-aligned bounding box with robust parallel-ray slab intersection.</summary>
struct Bounds {
    /// <summary>Lower world-space slab limits; initially empty.</summary>
    Vec3 Min{INFINITY, INFINITY, INFINITY};
    /// <summary>Upper world-space slab limits; initially empty.</summary>
    Vec3 Max{-INFINITY, -INFINITY, -INFINITY};
    /// <summary>Expand the world-space bounds to contain one triangle corner.</summary>
    void Include(Vec3 p);
    /// <summary>Intersect a ray with all three slabs, handling parallel rays without division by zero.</summary>
    bool Intersects(Vec3 origin, Vec3 direction, double limit) const;
};
} // namespace hellm::detail
