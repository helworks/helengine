#pragma once
#include "Internal.h"

namespace hellm::detail {
/// <summary>Original world-space triangle, retaining its mesh and original element offset.</summary>
struct Triangle {
    /// <summary>Original mesh supplying immutable normals, UV0 and diffuse material.</summary>
    const Mesh *Material;
    /// <summary>First original element index of this triangle.</summary>
    uint32_t Corner;
    /// <summary>Global xatlas chart identity used to constrain gutter growth.</summary>
    uint32_t Chart = 0;
    /// <summary>First world-space corner.</summary>
    Vec3 A;
    /// <summary>Second world-space corner.</summary>
    Vec3 B;
    /// <summary>Third world-space corner.</summary>
    Vec3 C;
    /// <summary>Unit winding normal oriented toward the reflecting side during transport.</summary>
    Vec3 Geometric;
    /// <summary>Original-order corners in full-resolution atlas texel coordinates.</summary>
    Vec2 Uv[3];
    /// <summary>Interpolate the immutable world triangle at the supplied barycentric coordinates.</summary>
    Vec3 Position(double u, double v) const {
        return Interpolate(A, B, C, u, v);
    }
    /// <summary>Interpolate and normalize imported world-space shading normals.</summary>
    Vec3 Normal(double u, double v) const {
        const auto &m = *Material;
        return Unit(Interpolate(m.Normals[m.Indices[Corner]], m.Normals[m.Indices[Corner + 1]],
                                m.Normals[m.Indices[Corner + 2]], u, v));
    }
    /// <summary>Sample original UV0 with repeating bilinear linear-RGB filtering and apply the material tint.</summary>
    Vec3 Reflectance(double u, double v) const;
};
} // namespace hellm::detail
