#pragma once
#include <cstdint>
#include <hellm/Texture.h>
#include <hellm/Vec2.h>
#include <hellm/Vec3.h>
#include <string>
#include <vector>

namespace hellm {
/// <summary>One independently bound drawable submesh; original triangle order is immutable.</summary>
struct Mesh {
    /// <summary>Stable unique drawable/submesh identity shared by snapshot and renderer.</summary>
    std::string Key;
    /// <summary>Initialized world-space vertex positions.</summary>
    std::vector<Vec3> Positions;
    /// <summary>World-space shading normals, one per position; the baker preserves them.</summary>
    std::vector<Vec3> Normals;
    /// <summary>Original material UV0 coordinates, one per position and never modified.</summary>
    std::vector<Vec2> TexCoords;
    /// <summary>Triangle indices in the renderer's original element order.</summary>
    std::vector<uint32_t> Indices;
    /// <summary>Linear base color: diffuse reflectance for dielectrics and conductor Fresnel F0; excluded from receiver irradiance.</summary>
    Vec3 Albedo{1, 1, 1};
    /// <summary>Metallic material fraction in [0,1]; conductors use base color as Fresnel F0 and have no diffuse lobe.</summary>
    float Metallic = 0;
    /// <summary>Perceptual GGX roughness in [0,1], squared into microfacet alpha; legacy fallback is 0.86.</summary>
    float Roughness = 0.86f;
    /// <summary>Optional linear base-color texture multiplied by Albedo at material hits.</summary>
    Texture DiffuseTexture;
};
} // namespace hellm
