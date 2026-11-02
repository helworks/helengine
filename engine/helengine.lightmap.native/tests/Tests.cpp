#include "../src/UvGeometry.h"
#include "../src/Transport.h"
#include <algorithm>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <hellm/Lightmap.h>
#include <iostream>
#include <limits>
#include <stdexcept>
namespace {
using namespace hellm;
/// <summary>Run the same physical assertions against the explicitly selected transport implementation.</summary>
BakeBackend SelectedBackend = BakeBackend::Cpu;
/// <summary>Fail a focused physical or asset assertion with its diagnostic context.</summary>
void Require(bool value, const std::string &message) {
    if (!value)
        throw std::runtime_error(message);
}
/// <summary>Check analytic irradiance or throughput within the specified numerical tolerance.</summary>
void Near(double actual, double expected, double tolerance, const std::string &message) {
    if (std::abs(actual - expected) > tolerance)
        throw std::runtime_error(message + " actual=" + std::to_string(actual) +
                                 " expected=" + std::to_string(expected));
}
/// <summary>Create a horizontal test receiver with explicit imported normals and immutable UV0.</summary>
Mesh Plane(const std::string &key, float y, float x0, float x1, float z0, float z1, bool up = true) {
    Mesh m;
    m.Key = key;
    m.Positions = {{x0, y, z0}, {x1, y, z0}, {x1, y, z1}, {x0, y, z1}};
    m.Normals.assign(4, {0, up ? 1.0f : -1.0f, 0});
    m.TexCoords = {{0, 0}, {1, 0}, {1, 1}, {0, 1}};
    m.Indices = {0, 1, 2, 0, 2, 3};
    return m;
}
/// <summary>Create a small deterministic correctness-test configuration.</summary>
BakeOptions Options(uint32_t spp = 8, uint32_t bounces = 0) {
    BakeOptions o;
    o.Backend = SelectedBackend;
    o.Width = o.Height = 64;
    o.SamplesPerTexel = spp;
    o.MaxBounces = bounces;
    o.WorkerCount = 2;
    o.Padding = 2;
    return o;
}
/// <summary>Probe irradiance through original triangle correspondence at a known planar world position.</summary>
Vec3 At(const Scene &scene, const Lightmap &map, const std::string &key, float x, float z) {
    for (size_t mi = 0; mi < scene.Meshes.size(); ++mi) {
        const auto &m = scene.Meshes[mi];
        if (m.Key != key)
            continue;
        const auto &b = map.Bindings[mi];
        for (size_t c = 0; c < m.Indices.size(); c += 3) {
            const auto a = m.Positions[m.Indices[c]], p = m.Positions[m.Indices[c + 1]],
                       q = m.Positions[m.Indices[c + 2]];
            double determinant = (p.X - a.X) * (q.Z - a.Z) - (p.Z - a.Z) * (q.X - a.X),
                   u = ((x - a.X) * (q.Z - a.Z) - (z - a.Z) * (q.X - a.X)) / determinant,
                   v = ((p.X - a.X) * (z - a.Z) - (p.Z - a.Z) * (x - a.X)) / determinant;
            if (u < 0 || v < 0 || u + v > 1)
                continue;
            auto uv0 = b.CornerUvs[c], uv1 = b.CornerUvs[c + 1], uv2 = b.CornerUvs[c + 2];
            double ux = uv0.X * (1 - u - v) + uv1.X * u + uv2.X * v, uy = uv0.Y * (1 - u - v) + uv1.Y * u + uv2.Y * v;
            uint32_t px = std::min(map.Width - 1, uint32_t(ux * map.Width)),
                     py = std::min(map.Height - 1, uint32_t(uy * map.Height));
            auto i = (size_t(py) * map.Width + px) * 4;
            Require(map.Rgba[i + 3] > 0, "Probe texel is uncovered");
            return {map.Rgba[i], map.Rgba[i + 1], map.Rgba[i + 2]};
        }
    }
    throw std::runtime_error("Probe outside mesh");
}
/// <summary>Assert that malformed data is rejected instead of silently repaired.</summary>
void ExpectFailure(const std::function<void()> &operation, const std::string &message) {
    bool failed = false;
    try {
        operation();
    } catch (const std::exception &) {
        failed = true;
    }
    Require(failed, message);
}
/// <summary>Verify analytic directional irradiance, HDR, instance charts, UV0 preservation and receiver albedo
/// separation.</summary>
void DirectCosineAndInstances() {
    Scene s;
    s.Sun.Direction = {0, 0.6f, 0.8f};
    s.Sun.Radiance = {4, 2, 1};
    s.Meshes = {Plane("first", 0, -1, 1, -1, 1), Plane("second", 0, 3, 5, -1, 1)};
    auto original = s.Meshes[0].TexCoords;
    auto map = Bake(s, Options());
    auto p = At(s, map, "first", 0, 0);
    Near(p.X, 2.4, 0.00001, "Direct sun cosine/HDR");
    Near(p.Y, 1.2, 0.00001, "RGB irradiance");
    Require(p.X > 1, "HDR must not clip");
    Require(map.Bindings[0].CornerUvs[0].X != map.Bindings[1].CornerUvs[0].X ||
                map.Bindings[0].CornerUvs[0].Y != map.Bindings[1].CornerUvs[0].Y,
            "Instances need independent UV charts");
    for (size_t i = 0; i < original.size(); ++i)
        Require(s.Meshes[0].TexCoords[i].X == original[i].X && s.Meshes[0].TexCoords[i].Y == original[i].Y,
                "UV0 modified");
    s.Meshes[0].Albedo = {0.1f, 0.2f, 0.3f};
    auto dark = Bake(s, Options());
    Near(At(s, dark, "first", 0, 0).X, p.X, 0.00001, "Receiver albedo must not enter irradiance");
    std::cout << "PASS direct cosine, HDR, instance charts, UV0, albedo separation\n";
}
/// <summary>Verify that opaque back faces block sunlight while a real geometric aperture transmits it.</summary>
void WindowAndBackside() {
    Scene s;
    s.Sun.Radiance = {2, 2, 2};
    s.Meshes = {Plane("floor", 0, -1, 1, -1, 1), Plane("left", 1, -2, -0.3f, -2, 2), Plane("right", 1, 0.3f, 2, -2, 2)};
    auto map = Bake(s, Options(24));
    Near(At(s, map, "floor", 0, 0).X, 2, 0.001, "Sun must pass through aperture");
    Near(At(s, map, "floor", 0.8f, 0).X, 0, 0.001, "Back-facing wall must block sun");
    std::cout << "PASS window aperture and two-sided blockers\n";
}
/// <summary>Verify zero-direct positive bounce transport, material scaling, zero ambient and worker-count
/// reproducibility.</summary>
void DiffuseBouncesAndDeterminism() {
    Scene s;
    s.Sun.Radiance = {3, 3, 3};
    s.Meshes = {Plane("floor", 0, -3, 3, -3, 3), Plane("receiver", 1, -0.4f, 0.4f, -0.4f, 0.4f, false)};
    auto o = Options(96, 0);
    o.SunAngularDiameterDegrees = .53f;
    auto direct = Bake(s, o);
    Near(At(s, direct, "receiver", 0, 0).X, 0, 0.00001, "Downward receiver has no direct sun");
    o.MaxBounces = 1;
    auto indirect = Bake(s, o);
    const auto value = At(s, indirect, "receiver", 0, 0).X;
    Require(value > 0.2f, "Sun diffuse bounce must illuminate receiver");
    o.WorkerCount = 1;
    auto serial = Bake(s, o);
    Require(serial.Rgba == indirect.Rgba, "Transport must be worker-count deterministic");
    o.WorkerCount = 3;
    auto repeat = Bake(s, o);
    Require(repeat.Rgba == indirect.Rgba, "Repeated bake must preserve charts and samples");
    s.Meshes[0].Albedo = {0, 0, 0};
    const auto specularOnly = Bake(s, o);
    const auto baseline = At(s, specularOnly, "receiver", 0, 0).X;
    s.Meshes[0].Albedo = {0.25f, 0.25f, 0.25f};
    auto quarter = Bake(s, o);
    Near(At(s, quarter, "receiver", 0, 0).X, baseline + (value - baseline) * 0.25, 0.00001, "Hit albedo scales bounced irradiance once");
    s.Meshes[0].DiffuseTexture.Width = s.Meshes[0].DiffuseTexture.Height = 1;
    s.Meshes[0].DiffuseTexture.Pixels = {{0.5f, 0.5f, 0.5f}};
    auto textured = Bake(s, o);
    Near(At(s, textured, "receiver", 0, 0).X, baseline + (value - baseline) * 0.125, 0.00001,
         "Linear diffuse texture enters bounce throughput");
    s.Sun.Radiance = {0, 0, 0};
    auto black = Bake(s, o);
    for (size_t i = 0; i < black.Rgba.size(); i += 4)
        Require(black.Rgba[i] == 0 && black.Rgba[i + 1] == 0 && black.Rgba[i + 2] == 0,
                "No artificial ambient is permitted");
    std::cout << "PASS zero-direct positive bounce, albedo/texture scaling, deterministic workers, no ambient\n";
}

/// <summary>Count spatial probes inside the 10-to-90-percent sun visibility transition, expressed as world width.</summary>
double ShadowTransitionWidth(const Scene &scene, const Lightmap &map) {
    uint32_t partial = 0;
    for (int i = -100; i <= 100; ++i) {
        const auto value = At(scene, map, "receiver", float(i * 0.0002), 0).X;
        if (value > 0.1f && value < 0.9f)
            ++partial;
    }
    return partial * 0.0002;
}
/// <summary>Verify realistic solar penumbrae widen with receiver distance and angular diameter, retaining a hard zero-angle control.</summary>
void FiniteSunPenumbra() {
    Scene scene;
    scene.Meshes = {Plane("receiver", 0, -.03f, .03f, -.03f, .03f),
                    Plane("blocker", 1, 0, .03f, -.03f, .03f, false)};
    auto options = Options(512);
    options.Width = options.Height = 128;
    const auto hard = Bake(scene, options);
    options.SunAngularDiameterDegrees = .53f;
    const auto soft = Bake(scene, options);
    const double hardWidth = ShadowTransitionWidth(scene, hard), softWidth = ShadowTransitionWidth(scene, soft);
    Require(softWidth > .003 && softWidth > hardWidth * 2, "Realistic solar diameter must produce partial geometric visibility beyond texel antialiasing");
    Near(At(scene, hard, "receiver", -.003f, 0).X, 1, .001, "Zero-angle light side stays hard");
    Near(At(scene, hard, "receiver", .003f, 0).X, 0, .001, "Zero-angle shadow side stays hard");
    const auto edge = At(scene, soft, "receiver", .002f, 0).X;
    Require(edge > .03f && edge < .8f, "Realistic solar shadow edge must contain partly visible sun");
    options.SunAngularDiameterDegrees = 1.06f;
    const auto larger = Bake(scene, options);
    const double largerWidth = ShadowTransitionWidth(scene, larger);
    Require(largerWidth > softWidth * 1.4, "Larger full angular diameter must widen the penumbra");
    options.SunAngularDiameterDegrees = .53f;
    for (auto &position : scene.Meshes[1].Positions) position.Y = 2;
    const auto distant = Bake(scene, options);
    const double distantWidth = ShadowTransitionWidth(scene, distant);
    Require(distantWidth > softWidth * 1.4, "Larger blocker receiver separation must widen the penumbra");
    options.WorkerCount = 1;
    options.GpuBatchTexels = 17;
    const auto repeated = Bake(scene, options);
    Require(distant.Rgba == repeated.Rgba && distant.SettingsFingerprint == repeated.SettingsFingerprint,
            "Finite sun must remain exactly reproducible across workers and dispatch boundaries");
    std::cout << "PASS finite sun .53-degree penumbra widths hard=" << hardWidth << " soft=" << softWidth
              << " diameter2=" << largerWidth << " distance2=" << distantWidth << ", exact repeatability\n";
}
/// <summary>Verify cap normalization, axis orientation, uniform solid-angle sampling and the zero-angle random contract.</summary>
void FiniteSunSamplingAndEnergy() {
    using namespace hellm::detail;
    Random zero(91), unchanged(91);
    const Vec3 axis = Unit({.2f, 1, .1f});
    const auto same = SunDirection(axis, 0, zero);
    Require(same.X == axis.X && same.Y == axis.Y && same.Z == axis.Z && zero.Next() == unchanged.Next(),
            "Zero angular diameter must preserve direction and consume no random values");
    Random tiny(91), twoDraws(91);
    SunDirection(axis, SunCapSpan(1e-22f), tiny);
    twoDraws.Next(); twoDraws.Next();
    Require(tiny.Next() == twoDraws.Next(), "Every positive angle must consume two sun draws even below GPU cap-span precision");
    const double span = SunCapSpan(20);
    Random random(61);
    double mean = 0;
    for (uint32_t i = 0; i < 100000; ++i) {
        const auto sample = SunDirection(axis, span, random);
        const double cosine = Dot(axis, sample);
        Require(cosine >= 1 - span - 1e-6 && cosine <= 1 + 1e-6, "Sun sample must remain inside the angular cap");
        Near(Dot(sample, sample), 1, 1e-6, "Sun sample must be normalized");
        mean += cosine / 100000;
    }
    Near(mean, 1 - span * .5, .00004, "Solid-angle cap mean cosine");
    Scene scene;
    scene.Sun.Radiance = {4, 2, 1};
    scene.Meshes = {Plane("receiver", 0, -1, 1, -1, 1)};
    auto options = Options(1024);
    const auto legacy = ComputeSettingsFingerprint(options);
    Require(legacy != (SelectedBackend == BakeBackend::Cpu ? 14404318533443029872ull : 13368798500722112768ull),
            "GGX/probe transport version must reject stale Lambertian settings fingerprints");
    auto explicitZero = options; explicitZero.SunAngularDiameterDegrees = 0;
    Require(legacy == ComputeSettingsFingerprint(explicitZero), "Explicit zero retains legacy settings identity");
    for (float angle : {.53f, 20.0f}) {
        options.SunAngularDiameterDegrees = angle;
        const auto map = Bake(scene, options);
        Near(At(scene, map, "receiver", 0, 0).X, 4, .004, "Finite cap retains open normal-incidence integrated energy");
        Require(map.SettingsFingerprint != legacy, "Finite sun must invalidate legacy settings identity");
    }
    options.SunAngularDiameterDegrees = .53f;
    const auto first = ComputeSettingsFingerprint(options);
    options.SunAngularDiameterDegrees = .54f;
    Require(first != ComputeSettingsFingerprint(options), "Angular diameter must enter the settings fingerprint");
    std::cout << "PASS finite sun solid angle, stable energy, zero random contract and settings identity\n";
}

/// <summary>Verify conservative thin-triangle coverage, imported normal orientation and default four-pixel
/// gutters.</summary>
void TinyTriangleAndFourBounces() {
    Scene s;
    Mesh m;
    m.Key = "thin";
    m.Positions = {{0, 0, 0}, {1, 0, 0}, {0.5f, 0, 0.00001f}};
    m.Normals.assign(3, {0, 1, 0});
    m.TexCoords = {{0, 0}, {1, 0}, {0.5f, 1}};
    m.Indices = {0, 1, 2};
    s.Meshes = {m};
    auto o = Options(4, 4);
    o.Padding = 4;
    auto map = Bake(s, o);
    bool covered = false;
    for (size_t i = 0; i < map.Rgba.size(); i += 4)
        if (map.Rgba[i + 3] > 0) {
            covered = true;
            Near(map.Rgba[i], 1, 0.00001, "Thin triangle exact coverage and imported normal opposite winding");
        }
    Require(covered, "Thin triangle must receive valid texels");
    Scene plane;
    plane.Meshes = {Plane("default-padding", 0, -1, 1, -1, 1)};
    auto padded = Bake(plane, o);
    Near(At(plane, padded, "default-padding", 0, 0).X, 1, 0.00001, "Default four-pixel diagonal gutters must fill");
    Require(map.SettingsFingerprint != ComputeSettingsFingerprint(Options(4, 0)),
            "Bounce count must invalidate settings");
    std::cout << "PASS thin triangle conservative coverage, four bounces, normal/winding independence\n";
}
/// <summary>Verify valid world triangles below xatlas's absolute area threshold retain every original corner.</summary>
void SmallWorldTriangleScale() {
    Scene scene;
    auto tiny = Plane("tiny-world-area", 0, 1.0f, 1.00001f, 1.0f, 1.00001f);
    scene.Meshes = {Plane("large-instance", 0, -1, 1, -1, 1), tiny};
    auto options = Options(4, 0);
    options.Padding = 4;
    auto map = Bake(scene, options);
    Require(map.Bindings[1].CornerUvs.size() == tiny.Indices.size(),
            "All tiny-world triangle corners must survive xatlas charting");
    for (size_t corner = 0; corner < tiny.Indices.size(); corner += 3) {
        auto a = map.Bindings[1].CornerUvs[corner], b = map.Bindings[1].CornerUvs[corner + 1],
             c = map.Bindings[1].CornerUvs[corner + 2];
        Require(std::abs(double(b.X - a.X) * (c.Y - a.Y) - double(b.Y - a.Y) * (c.X - a.X)) > 0,
                "Tiny-world triangle chart must not collapse");
    }
    Near(At(scene, map, "tiny-world-area", 1.000005f, 1.000005f).X, 1, 0.00001,
         "Tiny-world valid surface still receives analytic irradiance");
    std::cout << "PASS tiny world triangle scale normalization and original-corner preservation\n";
}
/// <summary>Reject real UV overlap while accepting the long shared edge observed in the initialized scene.</summary>
void SharedUvEdgePrecision() {
    hellm::detail::Triangle a, b;
    a.Uv[0] = {3044.241211f, 1304.085693f};
    a.Uv[1] = {3044.041016f, 438.001282f};
    a.Uv[2] = {2780.800049f, 438.127228f};
    b.Uv[0] = a.Uv[2];
    b.Uv[1] = a.Uv[1];
    b.Uv[2] = {3044.025391f, 422.899139f};
    Near(hellm::detail::Overlap(a, b), 0, 0.00000001,
         "Adjacent large UV triangles must not acquire false overlap from clipped intersection rounding");
    Require(hellm::detail::Overlap(a, a) > 100000, "True positive UV overlap must remain detectable");
    std::cout << "PASS shared UV edge double-precision clipping and positive overlap rejection\n";
}
/// <summary>Verify explicit rejection of degenerate geometry, duplicate instance keys and invalid sample
/// counts.</summary>
void InvalidInputs() {
    Scene s;
    s.Meshes = {Plane("floor", 0, -1, 1, -1, 1)};
    s.Meshes[0].Indices[1] = s.Meshes[0].Indices[0];
    ExpectFailure([&] { Bake(s, Options()); }, "Degenerate triangles must be rejected");
    s.Meshes = {Plane("floor", 0, -1, 1, -1, 1)};
    s.Meshes.push_back(s.Meshes[0]);
    ExpectFailure([&] { Bake(s, Options()); }, "Duplicate stable keys must be rejected");
    auto o = Options();
    o.SamplesPerTexel = 0;
    s.Meshes.pop_back();
    ExpectFailure([&] { Bake(s, o); }, "Zero samples must fail");
    o = Options(); o.Backend = static_cast<BakeBackend>(999);
    ExpectFailure([&] { Bake(s, o); }, "Unknown backend must fail explicitly");
    o = Options(); o.GpuBatchTexels = 0;
    ExpectFailure([&] { Bake(s, o); }, "Empty GPU batch must be rejected");
    for (float angle : {-1.0f, 20.01f, std::numeric_limits<float>::infinity(), std::numeric_limits<float>::quiet_NaN()}) {
        o = Options(); o.SunAngularDiameterDegrees = angle;
        ExpectFailure([&] { Bake(s, o); }, "Invalid sun diameter must be rejected before tracing");
    }
    std::cout << "PASS invalid source/settings rejection\n";
}
/// <summary>Compare textured diffuse transport and every chart against CPU, then prove batching cannot change GPU samples.</summary>
void BackendParity(float sunAngle, float metallic = 0) {
    if (SelectedBackend != BakeBackend::Vulkan) return;
    Scene scene;
    scene.Sun.Direction = {0.2f, 1, 0.1f}; scene.Sun.Radiance = {3, 2, 1};
    scene.Meshes = {Plane("floor", 0, -3, 3, -3, 3), Plane("receiver", 1, -.4f, .4f, -.4f, .4f, false)};
    auto &material = scene.Meshes[0];
    material.Albedo = {.8f, .7f, .6f};
    material.Metallic = metallic; material.Roughness = metallic > 0 ? .35f : .86f;
    material.DiffuseTexture.Width = material.DiffuseTexture.Height = 2;
    material.DiffuseTexture.Pixels = {{1,.2f,.3f},{.1f,1,.3f},{.3f,.2f,1},{.8f,.6f,.4f}};
    material.TexCoords = {{-1,-1},{2,-1},{2,2},{-1,2}};
    auto options = Options(128, 4); options.Backend = BakeBackend::Cpu;
    options.SunAngularDiameterDegrees = sunAngle;
    const auto cpu = Bake(scene, options);
    if (metallic > 0) {
        auto shallowOptions = options; shallowOptions.MaxBounces = 1;
        const auto shallow = Bake(scene, shallowOptions);
        double secondaryContribution = 0;
        for (size_t i = 0; i < cpu.Rgba.size(); ++i)
            if (i % 4 != 3) secondaryContribution += std::abs(double(cpu.Rgba[i]) - shallow.Rgba[i]);
        Require(secondaryContribution > 0.001, "Metal parity fixture must exercise observable secondary BSDF weighting");
    }
    options.Backend = BakeBackend::Vulkan;
    const auto gpu = Bake(scene, options);
    Require(cpu.SourceFingerprint == gpu.SourceFingerprint && cpu.SettingsFingerprint != gpu.SettingsFingerprint,
        "Backend changes settings identity, never the source scene identity");
    double squaredError = 0, squaredReference = 0, maximum = 0;
    for (size_t i = 0; i < cpu.Rgba.size(); ++i) {
        Require(std::isfinite(gpu.Rgba[i]), "GPU parity output must remain finite");
        if (i % 4 == 3) { Require(cpu.Rgba[i] == gpu.Rgba[i], "GPU must preserve covered texels and gutters"); continue; }
        const double difference = double(cpu.Rgba[i]) - gpu.Rgba[i];
        squaredError += difference * difference; squaredReference += double(cpu.Rgba[i]) * cpu.Rgba[i];
        maximum = std::max(maximum, std::abs(difference));
    }
    const double relative = std::sqrt(squaredError / squaredReference);
    Require(relative < .025 && maximum < .5, "GPU/CPU diffuse parity exceeds numeric tolerance");
    for (size_t m = 0; m < cpu.Bindings.size(); ++m) {
        Require(cpu.Bindings[m].Key == gpu.Bindings[m].Key && cpu.Bindings[m].CornerUvs.size() == gpu.Bindings[m].CornerUvs.size(), "GPU preserves every instance/corner binding");
        for (size_t c = 0; c < cpu.Bindings[m].CornerUvs.size(); ++c)
            Require(cpu.Bindings[m].CornerUvs[c].X == gpu.Bindings[m].CornerUvs[c].X && cpu.Bindings[m].CornerUvs[c].Y == gpu.Bindings[m].CornerUvs[c].Y, "Backend cannot change atlas UV charts");
    }
    options.GpuBatchTexels = 17;
    const auto smallBatches = Bake(scene, options);
    Require(gpu.Rgba == smallBatches.Rgba && gpu.SettingsFingerprint == smallBatches.SettingsFingerprint,
        "GPU dispatch boundaries must not change texel sampling or settings identity");
    std::cout << "PASS CPU/GPU sun=" << sunAngle << " textured four-bounce parity nrmse=" << relative << " max=" << maximum << ", exact batch repeatability\n";
}
/// <summary>Verify tiny primitives remain hardware blockers, even when their imported normals face away.</summary>
void TinyHardwareBlocker() {
    Scene scene;
    scene.Meshes = {Plane("receiver",0,0,1e-5f,0,1e-5f),Plane("blocker",1e-4f,-1e-5f,2e-5f,-1e-5f,2e-5f,false)};
    auto options=Options(8); options.RayEpsilon=1e-7f;
    const auto map=Bake(scene,options);
    Near(At(scene,map,"receiver",5e-6f,5e-6f).X,0,1e-6,"Tiny two-sided primitive must occlude sun");
    std::cout << "PASS tiny hardware triangle blocker\n";
}
/// <summary>Reject a valid densely tessellated texel before an unbounded GPU dispatch can stall the driver.</summary>
void DenseCoverageBudget() {
    if (SelectedBackend != BakeBackend::Vulkan) return;
    Scene scene; Mesh mesh; mesh.Key="dense";
    constexpr uint32_t strips=256;
    for (uint32_t i=0;i<=strips;++i) {
        const float x=float(i)/strips;
        mesh.Positions.push_back({x,0,0}); mesh.Positions.push_back({x,0,1});
        mesh.Normals.push_back({0,1,0}); mesh.Normals.push_back({0,1,0});
        mesh.TexCoords.push_back({x,0}); mesh.TexCoords.push_back({x,1});
        if(i<strips) mesh.Indices.insert(mesh.Indices.end(),{i*2,i*2+2,i*2+3,i*2,i*2+3,i*2+1});
    }
    scene.Meshes.push_back(mesh);
    auto options=Options(1); options.Width=options.Height=16; options.Backend=BakeBackend::Cpu;
    const auto valid=Bake(scene,options);
    Require(!valid.Rgba.empty(),"Dense fixture must be valid CPU coverage");
    options.Backend=BakeBackend::Vulkan; options.SamplesPerTexel=65536; options.MaxBounces=16;
    bool rejected=false; std::string rejection;
    try { Bake(scene,options); }
    catch(const std::runtime_error &error) { rejection=error.what(); rejected=rejection.find("coverage work budget")!=std::string::npos; }
    Require(rejected,"Dense coverage must fail explicitly before exceeding GPU work budget; actual=" + rejection);
    std::cout << "PASS dense-coverage GPU work budget rejection\n";
}
/// <summary>Verify source and atlas roundtrips, changed-sun invalidation, corruption rejection and truncation
/// rejection.</summary>
void Assets() {
#ifdef _WIN32
    const std::filesystem::path root = "C:/temp";
#else
    const std::filesystem::path root = "/tmp";
#endif
    const auto scenePath = (root / "hellm-test.scene").string(), mapPath = (root / "hellm-test.lightmap").string();
    Scene s;
    s.Meshes = {Plane("floor", 0, -1, 1, -1, 1)};
    auto options = Options(); options.SunAngularDiameterDegrees = .53f;
    auto map = Bake(s, options);
    // Exact bit preservation covers values that numeric equality alone cannot distinguish.
    map.Rgba[0] = std::numeric_limits<float>::denorm_min();
    map.Rgba[1] = std::nextafter(1.0f, 2.0f);
    map.Rgba[2] = 128.25f;
    map.Rgba[4] = -0.0f;
    try {
        SaveScene(s, scenePath);
        auto loaded = LoadScene(scenePath);
        Require(ComputeSceneFingerprint(loaded) == ComputeSceneFingerprint(s), "Scene roundtrip fingerprint");
        SaveLightmap(map, mapPath);
        auto l = LoadLightmap(mapPath);
        Require(l.Rgba == map.Rgba && l.SourceFingerprint == map.SourceFingerprint &&
                    l.SettingsFingerprint == map.SettingsFingerprint &&
                    l.Bindings[0].CornerUvs.size() == s.Meshes[0].Indices.size(),
                "Lightmap roundtrip");
        Require(std::memcmp(l.Rgba.data(), map.Rgba.data(), map.Rgba.size() * sizeof(float)) == 0,
                "Bulk decode must preserve every HDR float bit, including subnormals and signed zero");
        auto invalid = map;
        invalid.Rgba[0] = std::numeric_limits<float>::quiet_NaN();
        ExpectFailure([&] { SaveLightmap(invalid, mapPath); },
                      "Nonfinite HDR data must remain rejected after bulk decode optimization");
        s.Sun.Radiance.X += 1;
        Require(ComputeSceneFingerprint(s) != map.SourceFingerprint, "Sun mutation invalidates source fingerprint");
        {
            std::fstream f(mapPath, std::ios::binary | std::ios::in | std::ios::out);
            f.seekp(40);
            char invalid = 127;
            f.write(&invalid, 1);
        }
        ExpectFailure([&] { LoadLightmap(mapPath); }, "Checksum must reject corrupted assets");
        std::filesystem::resize_file(scenePath, 29);
        ExpectFailure([&] { LoadScene(scenePath); }, "Truncation must fail");
    } catch (...) {
        std::remove(scenePath.c_str());
        std::remove(mapPath.c_str());
        throw;
    }
    std::remove(scenePath.c_str());
    std::remove(mapPath.c_str());
    std::cout << "PASS asset roundtrip, stale fingerprints, corruption, truncation\n";
}
} // namespace
/// <summary>Run the explicit CLI workflow or focused native correctness suite and surface failures through the process
/// status.</summary>
int main(int argc, char **argv) {
    try {
        if (argc == 3 && std::string(argv[1]) == "--cli-fixture") {
            Scene scene; scene.Meshes = {Plane("receiver", 0, -1, 1, -1, 1)};
            scene.Probe.Enabled = true; scene.Probe.Position = {0,1,0};
            scene.Probe.BoundsMin = {-1,0,-1}; scene.Probe.BoundsMax = {1,2,1};
            SaveScene(scene, argv[2]);
            return 0;
        }
        if (argc == 2 && std::string(argv[1]) == "--vulkan") SelectedBackend = BakeBackend::Vulkan;
        else if (argc != 1) throw std::runtime_error("Expected no arguments or --vulkan");
        DirectCosineAndInstances();
        WindowAndBackside();
        DiffuseBouncesAndDeterminism();
        FiniteSunSamplingAndEnergy();
        FiniteSunPenumbra();
        TinyTriangleAndFourBounces();
        SmallWorldTriangleScale();
        SharedUvEdgePrecision();
        InvalidInputs();
        Assets();
        TinyHardwareBlocker();
        BackendParity(0);
        BackendParity(.53f);
        BackendParity(1e-22f);
        BackendParity(.53f, .65f);
        BackendParity(.53f, 1);
        DenseCoverageBudget();
        std::cout << "All lightmap tests passed\n";
        return 0;
    } catch (const std::exception &e) {
        std::cerr << "FAIL " << e.what() << "\n";
        return 1;
    }
}
