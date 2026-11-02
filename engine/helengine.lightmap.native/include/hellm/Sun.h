#pragma once
#include <hellm/Vec3.h>

namespace hellm {
/// <summary>Directional sun source; no environment or artificial ambient is supplied.</summary>
struct Sun {
    /// <summary>World-space vector toward the source; normalized internally.</summary>
    Vec3 Direction{0, 1, 0};
    /// <summary>Linear HDR incident irradiance at normal incidence, using the directional delta-light
    /// convention.</summary>
    Vec3 Radiance{1, 1, 1};
};
} // namespace hellm
