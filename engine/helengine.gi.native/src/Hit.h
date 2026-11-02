#pragma once
#include "helgi/Vec3.h"
#include <cstddef>
namespace helgi {
/// <summary>Conservative surface contribution retained independently from other contributors.</summary>
struct Hit {
    /// <summary>Linear index of the intersected voxel.</summary>
    std::size_t Cell;
    /// <summary>Winding-derived unit normal of the triangle.</summary>
    Vec3 Normal;
    /// <summary>Actual triangle plane distance dot(Normal, A), retained for grazing solar visibility.</summary>
    float PlaneOffset;
    /// <summary>Linear diffuse reflectance of the triangle.</summary>
    Vec3 Albedo;
    /// <summary>Triangle area weight for surface aggregation.</summary>
    float Weight;
};
}

