#include <algorithm>
#include <chrono>
#include <cmath>
#include <hellm/Lightmap.h>
#include <iostream>
#include <stdexcept>
namespace {
/// Parse a decimal option without accepting negative values, suffixes or overflow.
uint32_t Number(const std::string &text) {
    size_t end = 0;
    if (text.empty() || text[0] == '-')
        throw std::runtime_error("Invalid positive integer: " + text);
    auto value = std::stoull(text, &end);
    if (end != text.size() || value > UINT32_MAX)
        throw std::runtime_error("Invalid integer option: " + text);
    return uint32_t(value);
}
/// <summary>Parse a complete finite angular diameter in degrees while rejecting suffixes and unsupported ranges.</summary>
float SunAngle(const std::string &text) {
    size_t end = 0;
    const float value = std::stof(text, &end);
    if (end != text.size() || !std::isfinite(value) || value < 0 || value > 20)
        throw std::runtime_error("Sun angular diameter must be finite and in [0,20] degrees: " + text);
    return value;
}
/// Report the material-independent atlas energy and fingerprints without modifying it.
void Inspect(const hellm::Lightmap &map) {
    uint64_t covered = 0;
    double sum = 0;
    float peak = 0;
    for (size_t i = 0; i < map.Rgba.size(); i += 4)
        if (map.Rgba[i + 3] > 0) {
            ++covered;
            sum += (map.Rgba[i] + map.Rgba[i + 1] + map.Rgba[i + 2]) / 3.0;
            peak = std::max({peak, map.Rgba[i], map.Rgba[i + 1], map.Rgba[i + 2]});
        }
    std::cout << "atlas=" << map.Width << "x" << map.Height << " bindings=" << map.Bindings.size()
              << " covered=" << covered << " mean=" << (covered ? sum / covered : 0) << " peak=" << peak
              << " probe_size=" << map.Probe.FaceSize << " probe_mips=" << map.Probe.Mips.size() << " source=" << map.SourceFingerprint << " settings=" << map.SettingsFingerprint << "\n";
}
} // namespace
/// Explicit standalone scene-to-asset workflow; the renderer never invokes a bake.
int main(int argc, char **argv) {
    try {
        if (argc < 3)
            throw std::runtime_error(
                "Usage: helengine-lightmap bake INPUT.scene OUTPUT.lightmap [--size 4096 --samples 128 --bounces 4 "
                "--padding 4 --workers N --seed 1 --backend cpu|vulkan --gpu-batch 4096 --sun-angle 0 --probe-size 128 --probe-samples 128 --probe-filter-samples 128] | inspect ASSET | validate-scene INPUT.scene");
        const std::string command = argv[1];
        if (command == "inspect") {
            if (argc != 3)
                throw std::runtime_error("inspect takes one lightmap path");
            Inspect(hellm::LoadLightmap(argv[2]));
            return 0;
        }
        if (command == "validate-scene") {
            if (argc != 3)
                throw std::runtime_error("validate-scene takes one scene path");
            auto scene = hellm::LoadScene(argv[2]);
            std::cout << "meshes=" << scene.Meshes.size() << " source=" << hellm::ComputeSceneFingerprint(scene)
                      << "\n";
            return 0;
        }
        if (command != "bake" || argc < 4)
            throw std::runtime_error("Expected bake INPUT.scene OUTPUT.lightmap");
        hellm::BakeOptions options;
        for (int i = 4; i < argc; i += 2) {
            if (i + 1 >= argc)
                throw std::runtime_error("Missing option value");
            const std::string option = argv[i];
            if (option == "--backend") {
                const std::string backend = argv[i + 1];
                if (backend == "cpu") options.Backend = hellm::BakeBackend::Cpu;
                else if (backend == "vulkan") options.Backend = hellm::BakeBackend::Vulkan;
                else throw std::runtime_error("Unknown backend: " + backend);
                continue;
            }
            if (option == "--sun-angle") {
                options.SunAngularDiameterDegrees = SunAngle(argv[i + 1]);
                continue;
            }
            auto value = Number(argv[i + 1]);
            if (option == "--size")
                options.Width = options.Height = value;
            else if (option == "--samples")
                options.SamplesPerTexel = value;
            else if (option == "--probe-size")
                options.ProbeFaceSize = value;
            else if (option == "--probe-samples")
                options.ProbeSamples = value;
            else if (option == "--probe-filter-samples")
                options.ProbeFilterSamples = value;
            else if (option == "--bounces")
                options.MaxBounces = value;
            else if (option == "--padding")
                options.Padding = value;
            else if (option == "--gpu-batch")
                options.GpuBatchTexels = value;
            else if (option == "--workers")
                options.WorkerCount = value;
            else if (option == "--seed")
                options.Seed = value;
            else
                throw std::runtime_error("Unknown option: " + option);
        }
        auto last = std::chrono::steady_clock::now() - std::chrono::seconds(2);
        std::string previous;
        options.Progress = [&](const std::string &stage, uint32_t done, uint32_t total) {
            auto now = std::chrono::steady_clock::now();
            if (stage != previous || done == total || now - last >= std::chrono::seconds(1)) {
                std::cout << stage << " " << done << "/" << total << " " << (total ? 100.0 * done / total : 0) << "%\n"
                          << std::flush;
                previous = stage;
                last = now;
            }
        };
        const auto started = std::chrono::steady_clock::now();
        auto scene = hellm::LoadScene(argv[2]);
        std::cout << "backend=" << (options.Backend == hellm::BakeBackend::Vulkan ? "vulkan" : "cpu") << " sun_angle_degrees=" << options.SunAngularDiameterDegrees << "\n" << std::flush;
        auto map = hellm::Bake(scene, options);
        hellm::SaveLightmap(map, argv[3]);
        auto persisted = hellm::LoadLightmap(argv[3]);
        if (persisted.SourceFingerprint != map.SourceFingerprint || persisted.Rgba != map.Rgba || persisted.Probe.Mips != map.Probe.Mips)
            throw std::runtime_error("Saved asset verification failed");
        Inspect(persisted);
        std::cout << "elapsed_seconds=" << std::chrono::duration<double>(std::chrono::steady_clock::now() - started).count() << "\n";
        return 0;
    } catch (const std::exception &error) {
        std::cerr << "lightmap error: " << error.what() << "\n";
        return 1;
    }
}
