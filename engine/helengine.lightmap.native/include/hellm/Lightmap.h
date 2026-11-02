#pragma once
#include <cstdint>
#include <hellm/BakeOptions.h>
#include <hellm/Binding.h>
#include <hellm/ReflectionProbe.h>
#include <hellm/Scene.h>
#include <string>
#include <vector>

namespace hellm {
/// <summary>Linear HDR irradiance atlas; the renderer applies receiver albedo/pi exactly once.</summary>
struct Lightmap {
    /// <summary>Atlas row width in full-resolution texels.</summary>
    uint32_t Width = 0;
    /// <summary>Optional baked scene-radiance cube and complete GGX mip chain.</summary>
    ReflectionProbe Probe;
    /// <summary>Atlas row count in full-resolution texels.</summary>
    uint32_t Height = 0;
    /// <summary>Row-major RGBA32F irradiance; alpha is one for surfaces/gutters and zero for unused space.</summary>
    std::vector<float> Rgba;
    /// <summary>Per-drawable original-corner UV correspondence, independent of model sharing.</summary>
    std::vector<Binding> Bindings;
    /// <summary>Canonical source fingerprint including geometry, UV0, reflectance and sun.</summary>
    uint64_t SourceFingerprint = 0;
    /// <summary>Canonical algorithm/atlas/transport settings fingerprint, excluding worker scheduling.</summary>
    uint64_t SettingsFingerprint = 0;
};
/// <summary>Hash validated canonical source data, including sun, UV0 and diffuse textures.</summary>
uint64_t ComputeSceneFingerprint(const Scene &scene);
/// <summary>Hash validated radiometric and atlas controls, excluding scheduling and callbacks.</summary>
uint64_t ComputeSettingsFingerprint(const BakeOptions &options);
/// <summary>Save a validated scene in the versioned checksummed little-endian binary format.</summary>
void SaveScene(const Scene &scene, const std::string &path);
/// <summary>Load a scene, rejecting truncation, unsupported versions and corrupt payloads.</summary>
Scene LoadScene(const std::string &path);
/// <summary>Generate padded UV charts and trace deterministic direct and diffuse sun transport.</summary>
Lightmap Bake(const Scene &scene, const BakeOptions &options = BakeOptions{});
/// <summary>Save a validated irradiance atlas and its bindings in the checksummed asset format.</summary>
void SaveLightmap(const Lightmap &lightmap, const std::string &path);
/// <summary>Load and validate irradiance and per-corner correspondence without runtime dependencies.</summary>
Lightmap LoadLightmap(const std::string &path);

} // namespace hellm
