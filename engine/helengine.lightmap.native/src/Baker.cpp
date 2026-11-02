#include "Coverage.h"
#include "Probe.h"
#include "Polygon.h"
#include "Transport.h"
#include "UvGeometry.h"
#include "xatlas.h"
#ifdef HELLM_ENABLE_VULKAN
#include "VulkanTracing.h"
#endif
#include <array>
#include <atomic>
#include <chrono>
#include <condition_variable>
#include <exception>
#include <memory>
#include <mutex>
#include <thread>

namespace hellm {
namespace {
using namespace detail;

/// <summary>Publish stage counts through the optional caller-thread progress callback.</summary>
void Progress(const BakeOptions &o, const std::string &stage, uint32_t done, uint32_t total) {
    if (o.Progress)
        o.Progress(stage, done, total);
}
/// <summary>Fill only xatlas-owned gutters with eight-neighbor expansion matching the packer footprint.</summary>
void Dilate(Lightmap &result, const std::vector<uint32_t> &chartImage, const std::vector<int32_t> &heads,
            const std::vector<Coverage> &samples, const std::vector<Triangle> &triangles, uint32_t padding) {
    const uint32_t count = result.Width * result.Height;
    std::vector<uint32_t> current, next;
    current.reserve(count / 4);
    std::vector<int32_t> owner(count, -1);
    for (uint32_t p = 0; p < count; ++p)
        if (heads[p] >= 0) {
            owner[p] = int32_t(triangles[samples[heads[p]].TriangleIndex].Chart);
            current.push_back(p);
        }
    for (uint32_t ring = 0; ring < padding + 2 && !current.empty(); ++ring) {
        next.clear();
        for (auto p : current) {
            const uint32_t x = p % result.Width, y = p / result.Width;
            const uint32_t neighbors[8] = {x ? p - 1 : p,
                                           x + 1 < result.Width ? p + 1 : p,
                                           y ? p - result.Width : p,
                                           y + 1 < result.Height ? p + result.Width : p,
                                           x && y ? p - result.Width - 1 : p,
                                           x + 1 < result.Width && y ? p - result.Width + 1 : p,
                                           x && y + 1 < result.Height ? p + result.Width - 1 : p,
                                           x + 1 < result.Width && y + 1 < result.Height ? p + result.Width + 1 : p};
            for (auto n : neighbors) {
                if (owner[n] >= 0 || !(chartImage[n] & xatlas::kImageHasChartIndexBit) ||
                    int32_t(chartImage[n] & xatlas::kImageChartIndexMask) != owner[p])
                    continue;
                owner[n] = owner[p];
                for (int c = 0; c < 4; ++c)
                    result.Rgba[size_t(n) * 4 + c] = result.Rgba[size_t(p) * 4 + c];
                next.push_back(n);
            }
        }
        current.swap(next);
    }
    for (uint32_t p = 0; p < count; ++p)
        if ((chartImage[p] & xatlas::kImageHasChartIndexBit) && owner[p] < 0)
            throw std::runtime_error("Unfilled chart/gutter texel; atlas resolution or chart quality is insufficient");
}
} // namespace
Lightmap Bake(const Scene &scene, const BakeOptions &options) {
    using namespace detail;
    ValidateScene(scene);
    ValidateOptions(options);
#ifndef HELLM_ENABLE_VULKAN
    if (options.Backend == BakeBackend::Vulkan)
        throw std::runtime_error("Vulkan backend was requested but this baker was built without HELLM_ENABLE_VULKAN");
#endif
    Lightmap result;
    result.Width = options.Width;
    result.Height = options.Height;
    result.SourceFingerprint = ComputeSceneFingerprint(scene);
    result.SettingsFingerprint = ComputeSettingsFingerprint(options);
    Progress(options, "charts", 0, 1);
    std::unique_ptr<xatlas::Atlas, decltype(&xatlas::Destroy)> atlas(xatlas::Create(), xatlas::Destroy);
    if (!atlas)
        throw std::runtime_error("xatlas allocation failed");
    double worldArea = 0;
    double smallestArea = INFINITY;
    for (const auto &m : scene.Meshes) {
        for (size_t i = 0; i < m.Indices.size(); i += 3) {
            const auto cross = Cross(Sub(m.Positions[m.Indices[i + 1]], m.Positions[m.Indices[i]]),
                                     Sub(m.Positions[m.Indices[i + 2]], m.Positions[m.Indices[i]]));
            const double area = std::sqrt(Dot(cross, cross)) * 0.5;
            worldArea += area;
            smallestArea = std::min(smallestArea, area);
        }
    }
    // xatlas has a fixed FLT_EPSILON area cutoff independent of MeshDecl::epsilon.
    // Change only its coordinate units, uniformly for all meshes, so legitimate tiny
    // world triangles survive. A power-of-two scale avoids extra multiplication rounding.
    const double chartScale = std::max(1.0, std::exp2(std::ceil(std::log2(std::sqrt(0.0001 / smallestArea)))));
    for (const auto &m : scene.Meshes) {
        std::vector<Vec3> chartPositions;
        chartPositions.reserve(m.Positions.size());
        const Vec3 origin = m.Positions.front();
        for (const auto position : m.Positions)
            chartPositions.push_back(Mul(Sub(position, origin), chartScale));
        xatlas::MeshDecl decl;
        decl.vertexPositionData = chartPositions.data();
        decl.vertexCount = uint32_t(m.Positions.size());
        decl.vertexPositionStride = sizeof(Vec3);
        decl.vertexNormalData = m.Normals.data();
        decl.vertexNormalStride = sizeof(Vec3);
        decl.indexData = m.Indices.data();
        decl.indexCount = uint32_t(m.Indices.size());
        decl.indexFormat = xatlas::IndexFormat::UInt32;
        decl.epsilon = float(double(decl.epsilon) * chartScale);
        const auto error = xatlas::AddMesh(atlas.get(), decl, uint32_t(scene.Meshes.size()));
        if (error != xatlas::AddMeshError::Success)
            throw std::runtime_error(std::string("xatlas rejected mesh ") + m.Key + ": " +
                                     xatlas::StringForEnum(error));
    }
    xatlas::ChartOptions charts;
    charts.fixWinding = true;
    xatlas::ComputeCharts(atlas.get(), charts);
    xatlas::PackOptions pack;
    pack.resolution = options.Width;
    pack.padding = options.Padding;
    pack.bilinear = true;
    pack.createImage = true;
    pack.texelsPerUnit = float(std::sqrt(double(options.Width) * options.Height * 0.65 / worldArea) / chartScale);
    for (int attempt = 0; attempt < 48; ++attempt) {
        xatlas::PackCharts(atlas.get(), pack);
        if (atlas->atlasCount == 1 && atlas->width == options.Width && atlas->height == options.Height)
            break;
        pack.texelsPerUnit *= 0.9f;
    }
    if (atlas->atlasCount != 1 || atlas->width != options.Width || atlas->height != options.Height ||
        atlas->meshCount != scene.Meshes.size() || !atlas->image)
        throw std::runtime_error("Scene charts do not fit one requested atlas with padding");
    std::vector<Triangle> triangles;
    for (uint32_t mi = 0; mi < atlas->meshCount; ++mi) {
        const auto &source = scene.Meshes[mi];
        const auto &output = atlas->meshes[mi];
        Binding binding;
        binding.Key = source.Key;
        binding.CornerUvs.resize(source.Indices.size());
        if (output.indexCount != source.Indices.size())
            throw std::runtime_error("xatlas lost original triangles");
        for (uint32_t c = 0; c < output.indexCount; ++c) {
            const auto &vertex = output.vertexArray[output.indexArray[c]];
            if (vertex.xref != source.Indices[c] || vertex.atlasIndex != 0 || vertex.chartIndex < 0)
                throw std::runtime_error(
                    "xatlas corner correspondence rejected mesh=" + source.Key + " corner=" + std::to_string(c) +
                    " expectedVertex=" + std::to_string(source.Indices[c]) + " xref=" + std::to_string(vertex.xref) +
                    " atlas=" + std::to_string(vertex.atlasIndex) + " chart=" + std::to_string(vertex.chartIndex));
            binding.CornerUvs[c] = {vertex.uv[0] / options.Width, vertex.uv[1] / options.Height};
        }
        for (uint32_t c = 0; c < output.indexCount; c += 3) {
            Triangle t;
            t.Material = &source;
            t.Corner = c;
            t.Chart = uint32_t(output.vertexArray[output.indexArray[c]].chartIndex);
            t.A = source.Positions[source.Indices[c]];
            t.B = source.Positions[source.Indices[c + 1]];
            t.C = source.Positions[source.Indices[c + 2]];
            t.Geometric = Unit(Cross(Sub(t.B, t.A), Sub(t.C, t.A)));
            for (int k = 0; k < 3; ++k) {
                const auto &vertex = output.vertexArray[output.indexArray[c + k]];
                if (uint32_t(vertex.chartIndex) != t.Chart)
                    throw std::runtime_error("Triangle spans multiple charts");
                t.Uv[k] = {vertex.uv[0], vertex.uv[1]};
            }
            if (std::abs(Edge(t.Uv[0], t.Uv[1], t.Uv[2])) < 1e-10)
                throw std::runtime_error("Collapsed lightmap triangle; atlas resolution is insufficient: " +
                                         source.Key);
            triangles.push_back(t);
        }
        result.Bindings.push_back(std::move(binding));
    }
    const uint32_t count = options.Width * options.Height;
    std::vector<uint32_t> chartImage(atlas->image, atlas->image + count);
    atlas.reset();
    Progress(options, "charts", 1, 1);
    Progress(options, "raster", 0, uint32_t(triangles.size()));
    std::vector<int32_t> heads(count, -1);
    std::vector<Coverage> samples;
    samples.reserve(count);
    for (uint32_t ti = 0; ti < triangles.size(); ++ti) {
        const auto &t = triangles[ti];
        int minX = int(std::floor(std::min({t.Uv[0].X, t.Uv[1].X, t.Uv[2].X}))),
            maxX = int(std::floor(std::max({t.Uv[0].X, t.Uv[1].X, t.Uv[2].X}))),
            minY = int(std::floor(std::min({t.Uv[0].Y, t.Uv[1].Y, t.Uv[2].Y}))),
            maxY = int(std::floor(std::max({t.Uv[0].Y, t.Uv[1].Y, t.Uv[2].Y})));
        bool covered = false;
        if (minX < 0 || minY < 0 || maxX >= int(options.Width) || maxY >= int(options.Height))
            throw std::runtime_error("Packed UV triangle outside atlas");
        for (int y = minY; y <= maxY; ++y)
            for (int x = minX; x <= maxX; ++x) {
                double area = Area(PixelPolygon(t, uint32_t(x), uint32_t(y)));
                if (area <= 1e-10)
                    continue;
                covered = true;
                uint32_t p = uint32_t(y) * options.Width + uint32_t(x);
                for (int32_t s = heads[p]; s >= 0; s = samples[s].Next) {
                    const auto &other = triangles[samples[s].TriangleIndex];
                    if (t.Chart != other.Chart)
                        throw std::runtime_error("Separate charts share a texel; padding is insufficient");
                    if (Overlap(t, other) > 1e-5)
                        throw std::runtime_error(
                            "Overlapping UV triangles rejected: " + t.Material->Key +
                            " corners=" + std::to_string(t.Corner) + "," + std::to_string(other.Corner) +
                            " chart=" + std::to_string(t.Chart) + " overlap=" + std::to_string(Overlap(t, other)) +
                            " triA=" + std::to_string(t.Uv[0].X) + "," + std::to_string(t.Uv[0].Y) + ";" +
                            std::to_string(t.Uv[1].X) + "," + std::to_string(t.Uv[1].Y) + ";" +
                            std::to_string(t.Uv[2].X) + "," + std::to_string(t.Uv[2].Y) +
                            " triB=" + std::to_string(other.Uv[0].X) + "," + std::to_string(other.Uv[0].Y) + ";" +
                            std::to_string(other.Uv[1].X) + "," + std::to_string(other.Uv[1].Y) + ";" +
                            std::to_string(other.Uv[2].X) + "," + std::to_string(other.Uv[2].Y));
                }
                if (samples.size() >= size_t(INT32_MAX))
                    throw std::runtime_error("Atlas coverage exceeds addressable sample count");
                samples.push_back({ti, heads[p], float(area)});
                heads[p] = int32_t(samples.size() - 1);
            }
        if (!covered)
            throw std::runtime_error("Triangle has no measurable texel coverage: " + t.Material->Key);
        if (ti % 1000 == 0)
            Progress(options, "raster", ti, uint32_t(triangles.size()));
    }
    Progress(options, "raster", uint32_t(triangles.size()), uint32_t(triangles.size()));
    result.Rgba.assign(size_t(count) * 4, 0);
    if (options.Backend == BakeBackend::Vulkan) {
#ifdef HELLM_ENABLE_VULKAN
        TraceVulkan(scene, options, triangles, heads, samples, result);
#endif
    } else {
        Bvh bvh(triangles, options.RayEpsilon);
        const double capSpan = SunCapSpan(options.SunAngularDiameterDegrees);
        const uint32_t workers = std::max(
            1u,
            std::min(options.WorkerCount ? options.WorkerCount : std::max(1u, std::thread::hardware_concurrency()), 64u));
        std::atomic<uint32_t> nextPixel{0}, completed{0}, remaining{workers};
        std::atomic<bool> stop{false};
        std::mutex mutex;
        std::condition_variable wake;
        std::exception_ptr error;
        std::vector<std::thread> threads;
        auto worker = [&]() {
            try {
                for (;;) {
                    uint32_t begin = nextPixel.fetch_add(64);
                    if (begin >= count || stop.load())
                        break;
                    uint32_t end = std::min(count, begin + 64);
                    for (uint32_t p = begin; p < end; ++p) {
                        if (heads[p] < 0)
                            continue;
                        Random random(options.Seed ^ (uint64_t(p) * 0xd6e8feb86659fd93ull));
                        Vec3 sum{};
                        double weight = 0;
                        for (int32_t si = heads[p]; si >= 0; si = samples[si].Next) {
                            const auto &sample = samples[si];
                            const auto &t = triangles[sample.TriangleIndex];
                            auto polygon = PixelPolygon(t, p % options.Width, p / options.Width);
                            Vec3 value{};
                            for (uint32_t s = 0; s < options.SamplesPerTexel; ++s) {
                                auto uv = Barycentric(t, SamplePolygon(polygon, random));
                                value = Add(value, IrradianceSample(bvh, triangles, sample.TriangleIndex, uv.X, uv.Y, scene,
                                                                    options, capSpan, random));
                            }
                            sum = Add(sum, Mul(value, double(sample.Area) / options.SamplesPerTexel));
                            weight += sample.Area;
                        }
                        sum = Mul(sum, 1 / weight);
                        if (!Finite(sum) || sum.X < 0 || sum.Y < 0 || sum.Z < 0)
                            throw std::runtime_error("Transport produced invalid HDR irradiance");
                        const size_t offset = size_t(p) * 4;
                        result.Rgba[offset] = sum.X;
                        result.Rgba[offset + 1] = sum.Y;
                        result.Rgba[offset + 2] = sum.Z;
                        result.Rgba[offset + 3] = 1;
                    }
                    completed.fetch_add(end - begin);
                }
            } catch (...) {
                std::lock_guard<std::mutex> lock(mutex);
                if (!error)
                    error = std::current_exception();
                stop.store(true);
            }
            remaining.fetch_sub(1);
            wake.notify_one();
        };
        Progress(options, "trace", 0, count);
        try {
            for (uint32_t i = 0; i < workers; ++i)
                threads.emplace_back(worker);
            while (remaining.load()) {
                std::unique_lock<std::mutex> lock(mutex);
                wake.wait_for(lock, std::chrono::milliseconds(500));
                lock.unlock();
                Progress(options, "trace", completed.load(), count);
            }
        } catch (...) {
            stop.store(true);
            for (auto &thread : threads)
                if (thread.joinable())
                    thread.join();
            throw;
        }
        for (auto &thread : threads)
            thread.join();
        if (error)
            std::rethrow_exception(error);
        Progress(options, "trace", count, count);
    }
    Progress(options, "dilate", 0, 1);
    Dilate(result, chartImage, heads, samples, triangles, options.Padding);
    Progress(options, "dilate", 1, 1);
    BakeProbe(scene, options, triangles, result);
    ValidateLightmap(result);
    return result;
}
} // namespace hellm
