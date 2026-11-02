#pragma once
#include <hellm/Vec2.h>
namespace hellm::detail {
/// <summary>Double-precision atlas geometry used to retain exact shared edges during clipping.</summary>
struct Point2 {
    /// <summary>Horizontal coordinate in full-resolution atlas texels.</summary>
    double X;
    /// <summary>Vertical coordinate in full-resolution atlas texels.</summary>
    double Y;
    /// <summary>Construct a clipping point without reducing intersection precision.</summary>
    Point2(double x = 0, double y = 0) : X(x), Y(y) {
    }
    /// <summary>Import an original float UV exactly before any clipping arithmetic.</summary>
    Point2(Vec2 uv) : X(uv.X), Y(uv.Y) {
    }
};
} // namespace hellm::detail
