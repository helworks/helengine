#include "Bytes.h"
#include "Internal.h"
#include <cstring>
#include <fstream>
#include <limits>
#include <unordered_set>

namespace hellm::detail {
/// <summary>Reject missing geometry, invalid materials, duplicate keys and degenerate world triangles.</summary>
void ValidateScene(const Scene &scene) {
    if (scene.Meshes.empty())
        throw std::runtime_error("Scene has no meshes");
    if (!Finite(scene.Sun.Direction) || Dot(scene.Sun.Direction, scene.Sun.Direction) < 1e-20 ||
        !Finite(scene.Sun.Radiance) || scene.Sun.Radiance.X < 0 || scene.Sun.Radiance.Y < 0 || scene.Sun.Radiance.Z < 0)
        throw std::runtime_error("Invalid directional sun");
    if (scene.Probe.Enabled) {
        const auto &p = scene.Probe;
        if (!Finite(p.Position) || !Finite(p.BoundsMin) || !Finite(p.BoundsMax) ||
            !(p.BoundsMin.X < p.Position.X && p.Position.X < p.BoundsMax.X) ||
            !(p.BoundsMin.Y < p.Position.Y && p.Position.Y < p.BoundsMax.Y) ||
            !(p.BoundsMin.Z < p.Position.Z && p.Position.Z < p.BoundsMax.Z))
            throw std::runtime_error("Probe position must lie strictly inside finite ordered bounds");
    }
    std::unordered_set<std::string> keys;
    for (const auto &m : scene.Meshes) {
        if (m.Key.empty() || !keys.insert(m.Key).second)
            throw std::runtime_error("Mesh binding keys must be nonempty and unique");
        if (m.Positions.empty() || m.Positions.size() != m.Normals.size() || m.Positions.size() != m.TexCoords.size() ||
            m.Indices.empty() || m.Indices.size() % 3)
            throw std::runtime_error("Mesh requires matching positions, world normals, UV0 and triangle indices: " +
                                     m.Key);
        if (!Finite(m.Albedo) || m.Albedo.X < 0 || m.Albedo.Y < 0 || m.Albedo.Z < 0 || m.Albedo.X > 1 ||
            m.Albedo.Y > 1 || m.Albedo.Z > 1)
            throw std::runtime_error("Diffuse albedo must be finite and in [0,1]: " + m.Key);
        if (!std::isfinite(m.Metallic) || m.Metallic < 0 || m.Metallic > 1 ||
            !std::isfinite(m.Roughness) || m.Roughness < 0 || m.Roughness > 1)
            throw std::runtime_error("Metallic and roughness must be finite and in [0,1]: " + m.Key);
        for (size_t i = 0; i < m.Positions.size(); ++i)
            if (!Finite(m.Positions[i]) || !Finite(m.Normals[i]) || Dot(m.Normals[i], m.Normals[i]) < 1e-20 ||
                !std::isfinite(m.TexCoords[i].X) || !std::isfinite(m.TexCoords[i].Y))
                throw std::runtime_error("Invalid vertex: " + m.Key);
        for (auto i : m.Indices)
            if (i >= m.Positions.size())
                throw std::runtime_error("Triangle index out of range: " + m.Key);
        for (size_t i = 0; i < m.Indices.size(); i += 3) {
            const auto e = Cross(Sub(m.Positions[m.Indices[i + 1]], m.Positions[m.Indices[i]]),
                                 Sub(m.Positions[m.Indices[i + 2]], m.Positions[m.Indices[i]]));
            if (!Finite(e) || Dot(e, e) <= 1e-24)
                throw std::runtime_error("Degenerate world triangle: " + m.Key);
        }
        const auto &t = m.DiffuseTexture;
        if (uint64_t(t.Width) * t.Height != t.Pixels.size() || ((t.Width == 0) != (t.Height == 0)))
            throw std::runtime_error("Invalid diffuse texture dimensions: " + m.Key);
        for (auto p : t.Pixels)
            if (!Finite(p) || p.X < 0 || p.Y < 0 || p.Z < 0 || p.X > 1 || p.Y > 1 || p.Z > 1)
                throw std::runtime_error("Diffuse texture reflectance must be in [0,1]: " + m.Key);
    }
}
/// <summary>Enforce supported atlas, padding, sample count and transport limits.</summary>
void ValidateOptions(const BakeOptions &o) {
    if (o.Backend != BakeBackend::Cpu && o.Backend != BakeBackend::Vulkan)
        throw std::runtime_error("Unknown lightmap transport backend");
    if (o.GpuBatchTexels == 0 || o.GpuBatchTexels > 65536)
        throw std::runtime_error("GPU batch texel limit must be in [1,65536]");
    if (!std::isfinite(o.SunAngularDiameterDegrees) || o.SunAngularDiameterDegrees < 0 || o.SunAngularDiameterDegrees > 20)
        throw std::runtime_error("Sun angular diameter must be finite and in [0,20] degrees");
    if (!o.ProbeFaceSize || o.ProbeFaceSize > 1024 || (o.ProbeFaceSize & (o.ProbeFaceSize - 1)) ||
        !o.ProbeSamples || o.ProbeSamples > 65536 || !o.ProbeFilterSamples || o.ProbeFilterSamples > 65536)
        throw std::runtime_error("Probe requires power-of-two face size [1,1024] and sample counts [1,65536]");
    if (o.Width != o.Height || o.Width < 16 || o.Width > 8192 || !o.SamplesPerTexel || o.SamplesPerTexel > 65536 ||
        o.MaxBounces > 16 || o.Padding < 2 || o.Padding > 64 || o.Padding * 4 >= o.Width || !(o.RayEpsilon > 0) ||
        !std::isfinite(o.RayEpsilon))
        throw std::runtime_error("Invalid bake options: square atlas [16,8192], spp [1,65536], bounces [0,16], "
                                 "padding >=2, positive epsilon required");
}
/// <summary>Reject malformed atlas dimensions, HDR values and original-corner bindings.</summary>
void ValidateLightmap(const Lightmap &m) {
    if (!m.Width || !m.Height || m.Width > 8192 || m.Height > 8192 ||
        m.Rgba.size() != uint64_t(m.Width) * m.Height * 4 || m.Bindings.empty())
        throw std::runtime_error("Invalid lightmap dimensions or missing bindings");
    const auto &p = m.Probe;
    if (p.FaceSize == 0) {
        if (!p.Mips.empty()) throw std::runtime_error("Disabled reflection probe contains mip data");
    } else {
        if (p.FaceSize > 1024 || (p.FaceSize & (p.FaceSize - 1)) || !Finite(p.Position) ||
            !Finite(p.BoundsMin) || !Finite(p.BoundsMax) ||
            !(p.BoundsMin.X < p.Position.X && p.Position.X < p.BoundsMax.X) ||
            !(p.BoundsMin.Y < p.Position.Y && p.Position.Y < p.BoundsMax.Y) ||
            !(p.BoundsMin.Z < p.Position.Z && p.Position.Z < p.BoundsMax.Z))
            throw std::runtime_error("Invalid reflection probe size or bounds");
        uint32_t levels = 1;
        for (uint32_t size = p.FaceSize; size > 1; size >>= 1) ++levels;
        if (p.Mips.size() != levels) throw std::runtime_error("Reflection probe must contain its full mip chain");
        for (uint32_t level = 0; level < levels; ++level) {
            const uint32_t size = std::max(1u, p.FaceSize >> level);
            const auto &data = p.Mips[level];
            if (data.size() != uint64_t(size) * size * 6 * 4)
                throw std::runtime_error("Reflection probe mip dimensions do not match face size");
            for (size_t i = 0; i < data.size(); ++i)
                if (!std::isfinite(data[i]) || data[i] < 0 || (i % 4 == 3 && data[i] != 1))
                    throw std::runtime_error("Invalid reflection probe HDR data or alpha");
        }
    }
    const float *values = m.Rgba.data();
    const size_t valueCount = m.Rgba.size();
    for (size_t i = 0; i < valueCount; ++i)
        if (!std::isfinite(values[i]) || values[i] < 0)
            throw std::runtime_error("Lightmap contains negative or nonfinite HDR data");
    std::unordered_set<std::string> keys;
    for (const auto &b : m.Bindings) {
        if (b.Key.empty() || !keys.insert(b.Key).second || b.CornerUvs.empty() || b.CornerUvs.size() % 3)
            throw std::runtime_error("Invalid lightmap binding");
        for (auto uv : b.CornerUvs)
            if (!std::isfinite(uv.X) || !std::isfinite(uv.Y) || uv.X < 0 || uv.X > 1 || uv.Y < 0 || uv.Y > 1)
                throw std::runtime_error("Lightmap UV outside atlas");
    }
}
} // namespace hellm::detail
namespace hellm {
namespace {
using detail::Bytes;

/// <summary>Compute the documented FNV-1a fingerprint over canonical payload bytes.</summary>
uint64_t Hash(const std::vector<uint8_t> &data) {
    uint64_t h = 14695981039346656037ull;
    const uint8_t *bytes = data.data();
    const size_t count = data.size();
    for (size_t i = 0; i < count; ++i) {
        h ^= bytes[i];
        h *= 1099511628211ull;
    }
    return h;
}
/// <summary>Validate and encode every source field in deterministic mesh and triangle order.</summary>
Bytes SceneBytes(const Scene &s) {
    /// <summary>Reject missing geometry, invalid materials, duplicate keys and degenerate world triangles.</summary>
    detail::ValidateScene(s);
    Bytes b;
    b.U32(uint32_t(s.Meshes.size()));
    b.V3(s.Sun.Direction);
    b.V3(s.Sun.Radiance);
    b.U32(s.Probe.Enabled ? 1u : 0u);
    b.V3(s.Probe.Position);
    b.V3(s.Probe.BoundsMin);
    b.V3(s.Probe.BoundsMax);
    for (const auto &m : s.Meshes) {
        b.String(m.Key);
        b.V3(m.Albedo);
        b.F32(m.Metallic);
        b.F32(m.Roughness);
        b.U32(uint32_t(m.Positions.size()));
        if (!std::isfinite(m.Metallic) || m.Metallic < 0 || m.Metallic > 1 ||
            !std::isfinite(m.Roughness) || m.Roughness < 0 || m.Roughness > 1)
            throw std::runtime_error("Metallic and roughness must be finite and in [0,1]: " + m.Key);
        for (size_t i = 0; i < m.Positions.size(); ++i) {
            b.V3(m.Positions[i]);
            b.V3(m.Normals[i]);
            b.V2(m.TexCoords[i]);
        }
        b.U32(uint32_t(m.Indices.size()));
        for (auto i : m.Indices)
            b.U32(i);
        b.U32(m.DiffuseTexture.Width);
        b.U32(m.DiffuseTexture.Height);
        b.U32(uint32_t(m.DiffuseTexture.Pixels.size()));
        for (auto p : m.DiffuseTexture.Pixels)
            b.V3(p);
    }
    return b;
}
/// <summary>Write the versioned asset header, payload length and integrity checksum.</summary>
void Save(const Bytes &payload, const char *magic, const std::string &path) {
    Bytes h;
    for (int i = 0; i < 8; ++i)
        h.Data.push_back(magic[i]);
    h.U32(2);
    h.U64(payload.Data.size());
    h.U64(Hash(payload.Data));
    std::ofstream f(path, std::ios::binary | std::ios::trunc);
    if (!f)
        throw std::runtime_error("Cannot create asset: " + path);
    f.write(reinterpret_cast<const char *>(h.Data.data()), std::streamsize(h.Data.size()));
    f.write(reinterpret_cast<const char *>(payload.Data.data()), std::streamsize(payload.Data.size()));
    f.flush();
    if (!f)
        throw std::runtime_error("Failed writing asset: " + path);
}
/// <summary>Validate the complete file header, length and checksum before decoding collections.</summary>
Bytes Load(const char *magic, const std::string &path, uint32_t &version) {
    std::ifstream f(path, std::ios::binary | std::ios::ate);
    if (!f)
        throw std::runtime_error("Cannot open asset: " + path);
    const auto n = f.tellg();
    if (n < 28 || n > std::streamoff(2147483648ull))
        throw std::runtime_error("Invalid asset size: " + path);
    Bytes h;
    h.Data.resize(size_t(n));
    f.seekg(0);
    f.read(reinterpret_cast<char *>(h.Data.data()), n);
    if (!f)
        throw std::runtime_error("Failed reading asset: " + path);
    for (int i = 0; i < 8; ++i)
        if (h.Data[i] != uint8_t(magic[i]))
            throw std::runtime_error("Wrong asset kind: " + path);
    h.Position = 8;
    version = h.Read32();
    if (version != 1 && version != 2)
        throw std::runtime_error("Unsupported asset version");
    auto size = h.Read64();
    auto checksum = h.Read64();
    if (size != h.Data.size() - 28)
        throw std::runtime_error("Truncated or trailing asset payload");
    Bytes p;
    p.Data.assign(h.Data.begin() + 28, h.Data.end());
    if (Hash(p.Data) != checksum)
        throw std::runtime_error("Asset checksum mismatch: " + path);
    return p;
}
} // namespace
uint64_t ComputeSceneFingerprint(const Scene &s) {
    return Hash(SceneBytes(s).Data);
}
uint64_t ComputeSettingsFingerprint(const BakeOptions &o) {
    /// <summary>Enforce supported atlas, padding, sample count and transport limits.</summary>
    detail::ValidateOptions(o);
    Bytes b;
    b.U32(2);
    b.U32(o.ProbeFaceSize);
    b.U32(o.ProbeSamples);
    b.U32(o.ProbeFilterSamples);
    b.U32(o.Width);
    b.U32(o.Height);
    b.U32(o.SamplesPerTexel);
    b.U32(o.MaxBounces);
    b.U32(o.Padding);
    b.U64(o.Seed);
    b.F32(o.RayEpsilon);
    // Preserve existing CPU fingerprints while identifying the GPU transport algorithm independently.
    if (o.Backend == BakeBackend::Vulkan)
        b.U32(0x564b0001u);
    // Zero retains both legacy backend fingerprints. Positive diameters identify finite-sun transport version 1.
    if (o.SunAngularDiameterDegrees > 0) {
        b.U32(0x53554e01u);
        b.F32(o.SunAngularDiameterDegrees);
    }
    return Hash(b.Data);
}
void SaveScene(const Scene &s, const std::string &path) {
    Save(SceneBytes(s), "HELLMSC1", path);
}
Scene LoadScene(const std::string &path) {
    uint32_t version = 0;
    auto b = Load("HELLMSC1", path, version);
    Scene s;
    auto n = b.Count(1);
    s.Sun.Direction = b.Read3();
    s.Sun.Radiance = b.Read3();
    if (version >= 2) {
        const auto enabled = b.Read32();
        if (enabled > 1) throw std::runtime_error("Invalid probe enabled flag");
        s.Probe.Enabled = enabled != 0;
        s.Probe.Position = b.Read3();
        s.Probe.BoundsMin = b.Read3();
        s.Probe.BoundsMax = b.Read3();
    }
    s.Meshes.reserve(n);
    for (uint32_t i = 0; i < n; ++i) {
        Mesh m;
        m.Key = b.ReadString();
        m.Albedo = b.Read3();
        if (version >= 2) {
            m.Metallic = b.ReadFloat();
            m.Roughness = b.ReadFloat();
        }
        auto vc = b.Count(32);
        m.Positions.reserve(vc);
        m.Normals.reserve(vc);
        m.TexCoords.reserve(vc);
        for (uint32_t j = 0; j < vc; ++j) {
            m.Positions.push_back(b.Read3());
            m.Normals.push_back(b.Read3());
            m.TexCoords.push_back(b.Read2());
        }
        auto ic = b.Count(4);
        m.Indices.reserve(ic);
        for (uint32_t j = 0; j < ic; ++j)
            m.Indices.push_back(b.Read32());
        m.DiffuseTexture.Width = b.Read32();
        m.DiffuseTexture.Height = b.Read32();
        auto tc = b.Count(12);
        m.DiffuseTexture.Pixels.reserve(tc);
        for (uint32_t j = 0; j < tc; ++j)
            m.DiffuseTexture.Pixels.push_back(b.Read3());
        s.Meshes.push_back(std::move(m));
    }
    b.End();
    /// <summary>Reject missing geometry, invalid materials, duplicate keys and degenerate world triangles.</summary>
    detail::ValidateScene(s);
    return s;
}
void SaveLightmap(const Lightmap &m, const std::string &path) {
    /// <summary>Reject malformed atlas dimensions, HDR values and original-corner bindings.</summary>
    detail::ValidateLightmap(m);
    Bytes b;
    b.U32(m.Width);
    b.U32(m.Height);
    b.U64(m.SourceFingerprint);
    b.U64(m.SettingsFingerprint);
    b.U32(uint32_t(m.Rgba.size()));
    for (auto v : m.Rgba)
        b.F32(v);
    b.U32(uint32_t(m.Bindings.size()));
    for (const auto &k : m.Bindings) {
        b.String(k.Key);
        b.U32(uint32_t(k.CornerUvs.size()));
        for (auto uv : k.CornerUvs)
            b.V2(uv);
    }
    b.U32(m.Probe.FaceSize);
    b.V3(m.Probe.Position);
    b.V3(m.Probe.BoundsMin);
    b.V3(m.Probe.BoundsMax);
    b.U32(uint32_t(m.Probe.Mips.size()));
    for (const auto &mip : m.Probe.Mips) {
        b.U32(uint32_t(mip.size()));
        for (auto value : mip) b.F32(value);
    }
    Save(b, "HELLMLM1", path);
}
Lightmap LoadLightmap(const std::string &path) {
    uint32_t version = 0;
    auto b = Load("HELLMLM1", path, version);
    Lightmap m;
    m.Width = b.Read32();
    m.Height = b.Read32();
    m.SourceFingerprint = b.Read64();
    m.SettingsFingerprint = b.Read64();
    auto rc = b.Count(4);
    if (!m.Width || !m.Height || m.Width > 8192 || m.Height > 8192 || uint64_t(m.Width) * m.Height * 4 != rc)
        throw std::runtime_error("Invalid lightmap dimensions or RGBA count");
    b.ReadFloats(m.Rgba, rc);
    auto bc = b.Count(1);
    m.Bindings.reserve(bc);
    for (uint32_t i = 0; i < bc; ++i) {
        Binding k;
        k.Key = b.ReadString();
        auto c = b.Count(8);
        k.CornerUvs.reserve(c);
        for (uint32_t j = 0; j < c; ++j)
            k.CornerUvs.push_back(b.Read2());
        m.Bindings.push_back(std::move(k));
    }
    if (version >= 2) {
        m.Probe.FaceSize = b.Read32();
        m.Probe.Position = b.Read3();
        m.Probe.BoundsMin = b.Read3();
        m.Probe.BoundsMax = b.Read3();
        const auto levels = b.Count(4);
        uint32_t expectedLevels = m.Probe.FaceSize ? 1u : 0u;
        if (m.Probe.FaceSize > 1024 || (m.Probe.FaceSize & (m.Probe.FaceSize - 1)))
            throw std::runtime_error("Invalid reflection probe face size");
        for (uint32_t size = m.Probe.FaceSize; size > 1; size >>= 1) ++expectedLevels;
        if (levels != expectedLevels) throw std::runtime_error("Reflection probe requires a complete mip chain");
        m.Probe.Mips.resize(levels);
        for (uint32_t level = 0; level < levels; ++level) {
            const uint32_t size = std::max(1u, m.Probe.FaceSize >> level);
            const auto count = b.Count(4);
            if (count != uint64_t(size) * size * 6 * 4)
                throw std::runtime_error("Invalid reflection probe mip count");
            b.ReadFloats(m.Probe.Mips[level], count);
        }
    }
    b.End();
    /// <summary>Reject malformed atlas dimensions, HDR values and original-corner bindings.</summary>
    detail::ValidateLightmap(m);
    return m;
}
} // namespace hellm
