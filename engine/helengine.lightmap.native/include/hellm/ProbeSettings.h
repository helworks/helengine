#pragma once
#include <hellm/Vec3.h>
namespace hellm {
/// <summary>Explicit world-space capture and box-projection volume for one shared reflection probe.</summary>
struct ProbeSettings {
    /// <summary>Enable scene-radiance capture; disabled scenes allocate no probe data.</summary>
    bool Enabled = false;
    /// <summary>World-space capture origin strictly inside the box volume.</summary>
    Vec3 Position{};
    /// <summary>Minimum world-space corner of the box-projection volume.</summary>
    Vec3 BoundsMin{};
    /// <summary>Maximum world-space corner of the box-projection volume.</summary>
    Vec3 BoundsMax{};
};
} // namespace hellm
