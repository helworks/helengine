#include "VulkanTracing.h"
#include "UvGeometry.h"
#include "Transport.h"
#include "Probe.h"
#include "vulkan/VulkanTransport.h"
#include <unordered_map>

namespace hellm::detail {
namespace {
/// <summary>Write one explicit IEEE32 vector into the std430 four-float boundary layout.</summary>
void PackVector(float *destination, Vec3 source) {
    destination[0] = source.X; destination[1] = source.Y; destination[2] = source.Z; destination[3] = 0;
}
/// <summary>Flatten source geometry and linear textures without reordering primitives or duplicating per-texel data.</summary>
GpuScene PackScene(const Scene &scene, const std::vector<Triangle> &triangles) {
    GpuScene packed;
    std::unordered_map<const Mesh *, uint32_t> indices;
    for (const auto &mesh : scene.Meshes) {
        if (packed.Pixels.size() + mesh.DiffuseTexture.Pixels.size() > UINT32_MAX)
            throw std::runtime_error("GPU material textures exceed 32-bit indexing");
        GpuMaterial material{};
        PackVector(material.Albedo, mesh.Albedo);
        material.Surface[0] = mesh.Metallic; material.Surface[1] = mesh.Roughness;
        material.Texture[0] = mesh.DiffuseTexture.Width;
        material.Texture[1] = mesh.DiffuseTexture.Height;
        material.Texture[2] = uint32_t(packed.Pixels.size());
        indices.emplace(&mesh, uint32_t(packed.Materials.size()));
        packed.Materials.push_back(material);
        for (const auto pixel : mesh.DiffuseTexture.Pixels)
            packed.Pixels.push_back({pixel.X, pixel.Y, pixel.Z, 1});
    }
    packed.Triangles.reserve(triangles.size());
    for (const auto &triangle : triangles) {
        const auto &mesh = *triangle.Material;
        const auto a = mesh.Indices[triangle.Corner], b = mesh.Indices[triangle.Corner + 1], c = mesh.Indices[triangle.Corner + 2];
        GpuTriangle value{};
        PackVector(value.A, triangle.A); PackVector(value.B, triangle.B); PackVector(value.C, triangle.C);
        PackVector(value.Na, mesh.Normals[a]); PackVector(value.Nb, mesh.Normals[b]); PackVector(value.Nc, mesh.Normals[c]);
        value.Uv01[0] = mesh.TexCoords[a].X; value.Uv01[1] = mesh.TexCoords[a].Y;
        value.Uv01[2] = mesh.TexCoords[b].X; value.Uv01[3] = mesh.TexCoords[b].Y;
        value.Uv2[0] = mesh.TexCoords[c].X; value.Uv2[1] = mesh.TexCoords[c].Y;
        value.Metadata[0] = indices.at(&mesh);
        packed.Triangles.push_back(value);
    }
    return packed;
}
/// <summary>Convert exact clipped polygon vertices to source barycentrics while retaining CPU-computed fan weights.</summary>
GpuCoverage PackCoverage(const Triangle &triangle, const Coverage &coverage, uint32_t pixel, uint32_t width) {
    const auto polygon = PixelPolygon(triangle, pixel % width, pixel / width);
    if (polygon.Count < 3 || polygon.Count > 12)
        throw std::runtime_error("GPU coverage requires a valid clipped convex polygon");
    GpuCoverage result{};
    result.Metadata[0] = coverage.TriangleIndex; result.Metadata[1] = polygon.Count;
    result.Measures[0] = coverage.Area; result.Measures[1] = float(Area(polygon));
    if (!(result.Measures[1] > 0)) throw std::runtime_error("GPU coverage lost its positive area");
    for (uint32_t i = 0; i < polygon.Count; ++i) {
        const auto barycentric = Barycentric(triangle, polygon.Points[i]);
        result.Points[i][0] = barycentric.X; result.Points[i][1] = barycentric.Y;
        if (i > 0 && i + 1 < polygon.Count)
            result.Points[i][2] = float(std::abs(Edge(polygon.Points[0], polygon.Points[i], polygon.Points[i + 1])) * .5);
    }
    return result;
}
} // namespace

void TraceVulkan(const Scene &scene, const BakeOptions &options, const std::vector<Triangle> &triangles,
    const std::vector<int32_t> &heads, const std::vector<Coverage> &samples, Lightmap &result) {
    const auto packed = PackScene(scene, triangles);
    VulkanTransport transport(packed);
    if (options.Progress) options.Progress("gpu-device:" + transport.DeviceName(), 1, 1);
    GpuParameters parameters{};
    PackVector(parameters.Sun, Unit(scene.Sun.Direction)); parameters.Sun[3] = options.RayEpsilon;
    PackVector(parameters.Radiance, scene.Sun.Radiance);
    parameters.Radiance[3] = float(SunCapSpan(options.SunAngularDiameterDegrees));
    parameters.Settings[0] = options.SamplesPerTexel; parameters.Settings[1] = options.MaxBounces;
    // Preserve two sun draws for every positive diameter, even if its GPU cap span underflows.
    parameters.Settings[3] = options.SunAngularDiameterDegrees > 0 ? 1u : 0u;
    parameters.Seed[0] = uint32_t(options.Seed); parameters.Seed[1] = uint32_t(options.Seed >> 32);
    const auto cost = uint64_t(options.SamplesPerTexel) * (uint64_t(options.MaxBounces) + 1);
    // A texel may contain many disjoint triangle fragments. Bound actual path work and storage,
    // not merely output texels; keep even the lowest-sample coverage upload below 15 MiB.
    const auto coverageLimit = uint32_t(std::min(uint64_t(65536), std::max(uint64_t(1), uint64_t(1048576) / cost)));
    const auto batchLimit = std::min(options.GpuBatchTexels, coverageLimit);
    std::vector<GpuTexel> texels; std::vector<GpuCoverage> coverages; std::vector<float> output;
    texels.reserve(batchLimit);
    const uint32_t count = uint32_t(heads.size());
    uint32_t pixel = 0;
    if (options.Progress) options.Progress("trace-gpu", 0, count);
    while (pixel < count) {
        texels.clear(); coverages.clear();
        for (; pixel < count && texels.size() < batchLimit;) {
            if (heads[pixel] < 0) { ++pixel; continue; }
            uint32_t fragments = 0;
            for (int32_t index = heads[pixel]; index >= 0; index = samples[index].Next) {
                if (++fragments > coverageLimit)
                    throw std::runtime_error("One texel exceeds the Vulkan coverage work budget; increase atlas resolution, reduce samples/bounces, or select CPU explicitly");
            }
            if (coverages.size() + fragments > coverageLimit) break;
            GpuTexel texel{}; texel.Metadata[0] = uint32_t(coverages.size()); texel.Metadata[2] = pixel;
            for (int32_t index = heads[pixel]; index >= 0; index = samples[index].Next) {
                const auto &coverage = samples[index];
                if (coverages.size() == UINT32_MAX) throw std::runtime_error("GPU batch coverage exceeds addressable indexing");
                coverages.push_back(PackCoverage(triangles[coverage.TriangleIndex], coverage, pixel, options.Width));
                ++texel.Metadata[1];
            }
            texels.push_back(texel);
            ++pixel;
        }
        if (!texels.empty()) {
            parameters.Settings[2] = uint32_t(texels.size());
            transport.Trace(texels, coverages, parameters, output);
            if (output.size() != texels.size() * 4) throw std::runtime_error("GPU returned an incomplete irradiance batch");
            for (size_t i = 0; i < texels.size(); ++i) {
                const auto destination = size_t(texels[i].Metadata[2]) * 4;
                for (uint32_t c = 0; c < 4; ++c) {
                    const auto value = output[i * 4 + c];
                    if (!std::isfinite(value) || value < 0 || (c == 3 && value != 1))
                        throw std::runtime_error("GPU transport produced invalid HDR irradiance");
                    result.Rgba[destination + c] = value;
                }
            }
        }
        if (options.Progress) options.Progress("trace-gpu", pixel, count);
    }
}
void TraceProbeVulkan(const Scene &scene, const BakeOptions &options, const std::vector<Triangle> &triangles,
    std::vector<float> &rgba) {
    const auto packed = PackScene(scene,triangles);
    VulkanTransport transport(packed);
    if (options.Progress) options.Progress("probe-gpu-device:"+transport.DeviceName(),1,1);
    GpuParameters parameters{};
    PackVector(parameters.Sun,Unit(scene.Sun.Direction)); parameters.Sun[3]=options.RayEpsilon;
    PackVector(parameters.Radiance,scene.Sun.Radiance); parameters.Radiance[3]=float(SunCapSpan(options.SunAngularDiameterDegrees));
    parameters.Settings[0]=options.ProbeSamples; parameters.Settings[1]=options.MaxBounces+1;
    parameters.Settings[3]=options.SunAngularDiameterDegrees>0 ? 1u:0u;
    parameters.Seed[0]=uint32_t(options.Seed); parameters.Seed[1]=uint32_t(options.Seed>>32);
    parameters.Seed[2]=1; // Explicit primary-radiance mode; never interpreted as receiver coverage.
    const auto cost=uint64_t(options.ProbeSamples)*(options.MaxBounces+1);
    const auto batch=uint32_t(std::min(uint64_t(options.GpuBatchTexels),std::max(uint64_t(1),uint64_t(1048576)/cost)));
    const uint32_t size=options.ProbeFaceSize,count=size*size*6;
    std::vector<GpuTexel> texels;
    std::vector<GpuCoverage> rays;
    std::vector<float> output;
    for (uint32_t begin=0;begin<count;begin+=batch) {
        const auto end=std::min(count,begin+batch);
        texels.assign(end-begin,GpuTexel{}); rays.assign(end-begin,GpuCoverage{});
        for (uint32_t p=begin;p<end;++p) {
            const auto local=p-begin;
            texels[local].Metadata[0]=local; texels[local].Metadata[1]=1; texels[local].Metadata[2]=p;
            PackVector(rays[local].Points[0],scene.Probe.Position);
            PackVector(rays[local].Points[1],CubeDirection(p/(size*size),2*((p%size)+0.5)/size-1,2*(((p/size)%size)+0.5)/size-1));
        }
        parameters.Settings[2]=end-begin;
        transport.Trace(texels,rays,parameters,output);
        if (output.size()!=size_t(end-begin)*4) throw std::runtime_error("Vulkan reflection capture returned incomplete data");
        for (size_t i=0;i<output.size();++i) {
            if (!std::isfinite(output[i]) || output[i]<0 || (i%4==3 && output[i]!=1))
                throw std::runtime_error("Vulkan reflection capture produced invalid HDR radiance");
            rgba[size_t(begin)*4+i]=output[i];
        }
        if (options.Progress) options.Progress("probe-trace-gpu",end,count);
    }
}
} // namespace hellm::detail
