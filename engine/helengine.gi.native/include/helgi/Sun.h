#pragma once
#include "Vec3.h"
namespace helgi {
/// <summary>Single directional sun used only to seed reflected radiance inside the GI solver.</summary>
struct Sun {
    /// <summary>Nonzero world-space direction from a surface toward the sun; normalized on submission.</summary>
    Vec3 DirectionToSun;
    /// <summary>Nonnegative linear incident irradiance at normal incidence.</summary>
    Vec3 Irradiance;
};
}
