#pragma once
#include <hellm/Mesh.h>
#include <hellm/ProbeSettings.h>
#include <hellm/Sun.h>
#include <vector>

namespace hellm {
/// <summary>Fully initialized immutable world snapshot independent of runtime objects.</summary>
struct Scene {
    /// <summary>Independently charted drawable submeshes with unique stable keys.</summary>
    std::vector<Mesh> Meshes;
    /// <summary>The one directional source whose direct and bounced irradiance is baked.</summary>
    hellm::Sun Sun;
    /// <summary>Optional explicit scene-radiance capture and box projection settings.</summary>
    ProbeSettings Probe;
};
} // namespace hellm
