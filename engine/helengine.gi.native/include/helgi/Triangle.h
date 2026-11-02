#pragma once
#include "Vec3.h"
namespace helgi {
/// <summary>Single-sided Lambertian reflector; winding defines its reflecting normal.</summary>
struct Triangle {
    /// <summary>First world-space vertex.</summary>
    Vec3 A;
    /// <summary>Second world-space vertex.</summary>
    Vec3 B;
    /// <summary>Third world-space vertex.</summary>
    Vec3 C;
    /// <summary>Linear diffuse reflectance, each channel in [0, 1].</summary>
    Vec3 Albedo;
};
}
