#pragma once
#include <array>
#include "helgi/Vec3.h"
namespace helgi {
/// <summary>Opaque voxel with six groups of similarly oriented reflecting surfaces.</summary>
struct Cell {
    /// <summary>Surface groups: weighted normal XYZ, weighted albedo RGB, area, outgoing reflected radiance RGB.</summary>
    std::array<std::array<float, 10>, 6> Surfaces{};
    /// <summary>Number of triangle intersections, allowing overlaps without ownership loss.</summary>
    unsigned int Count = 0;
    /// <summary>Unit normal of the first actual triangle plane intersecting this cell.</summary>
    Vec3 PlaneNormal{};
    /// <summary>Signed first-plane distance from the world origin.</summary>
    float PlaneOffset = 0;
    /// <summary>True only when every intersecting triangle lies in the same geometric plane, including reversed winding.</summary>
    bool Coplanar = true;
};
}

