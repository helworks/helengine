#include "Probe.h"
#include "Brdf.h"
#ifdef HELLM_ENABLE_VULKAN
#include "VulkanTracing.h"
#endif
#include <atomic>
#include <chrono>
#include <exception>
#include <mutex>
#include <thread>
namespace hellm::detail {
namespace {
/// <summary>Resolve the dominant cube axis to the face and its top-left-origin coordinates.</summary>
void CubeCoordinates(Vec3 direction, uint32_t &face, double &u, double &v) {
    const double x = direction.X, y = direction.Y, z = direction.Z;
    const double ax = std::abs(x), ay = std::abs(y), az = std::abs(z);
    if (ax >= ay && ax >= az) {
        if (x > 0) { face = 0; u = -z / ax; v = -y / ax; }
        else { face = 1; u = z / ax; v = -y / ax; }
    } else if (ay >= az) {
        if (y > 0) { face = 2; u = x / ay; v = z / ay; }
        else { face = 3; u = x / ay; v = -z / ay; }
    } else {
        if (z > 0) { face = 4; u = x / az; v = -y / az; }
        else { face = 5; u = -x / az; v = -y / az; }
    }
}
/// <summary>Read a potentially off-face bilinear tap by projecting it onto the adjacent cube face.</summary>
Vec3 CubeTap(const std::vector<float> &data, uint32_t size, uint32_t face, int x, int y) {
    if (x < 0 || y < 0 || x >= int(size) || y >= int(size)) {
        double u, v;
        CubeCoordinates(CubeDirection(face, 2 * (x + 0.5) / size - 1, 2 * (y + 0.5) / size - 1), face, u, v);
        x = std::clamp(int((u + 1) * 0.5 * size), 0, int(size) - 1);
        y = std::clamp(int((v + 1) * 0.5 * size), 0, int(size) - 1);
    }
    const size_t offset = ((size_t(face) * size + uint32_t(y)) * size + uint32_t(x)) * 4;
    return {data[offset],data[offset+1],data[offset+2]};
}
/// <summary>Execute disjoint offline cube pixels with deterministic streams and propagate worker failures.</summary>
void ParallelPixels(uint32_t count, const BakeOptions &options, const std::string &stage, const std::function<void(uint32_t)> &action) {
    const uint32_t workers = std::min(64u, options.WorkerCount ? options.WorkerCount : std::max(1u,std::thread::hardware_concurrency()));
    std::atomic<uint32_t> next{0}, done{0}, remaining{workers};
    std::atomic<bool> stop{false};
    std::mutex errorMutex;
    std::exception_ptr error;
    std::vector<std::thread> threads;
    const auto worker = [&]() {
        try {
            for (;;) {
                const uint32_t begin = next.fetch_add(32);
                if (begin >= count || stop.load()) break;
                const auto end = std::min(begin + 32, count);
                for (uint32_t p = begin; p < end; ++p) action(p);
                done.fetch_add(end - begin);
            }
        } catch (...) {
            std::lock_guard<std::mutex> lock(errorMutex);
            if (!error) error = std::current_exception();
            stop.store(true);
        }
        --remaining;
    };
    try {
        if (options.Progress) options.Progress(stage, 0, count);
        for (uint32_t i=0;i<workers;++i) threads.emplace_back(worker);
        while (remaining.load()) {
            std::this_thread::sleep_for(std::chrono::milliseconds(200));
            if (options.Progress) options.Progress(stage,done.load(),count);
        }
    } catch (...) {
        stop.store(true);
        for (auto &thread : threads) if (thread.joinable()) thread.join();
        throw;
    }
    for (auto &thread : threads) thread.join();
    if (error) std::rethrow_exception(error);
    if (options.Progress) options.Progress(stage,count,count);
}
}
Vec3 CubeDirection(uint32_t face, double u, double v) {
    switch (face) {
        case 0: return Unit({1,float(-v),float(-u)});
        case 1: return Unit({-1,float(-v),float(u)});
        case 2: return Unit({float(u),1,float(v)});
        case 3: return Unit({float(u),-1,float(-v)});
        case 4: return Unit({float(u),float(-v),1});
        case 5: return Unit({float(-u),float(-v),-1});
        default: throw std::runtime_error("Invalid cubemap face index");
    }
}
Vec3 SampleCube(const std::vector<float> &data, uint32_t size, Vec3 direction) {
    uint32_t face;
    double u,v;
    CubeCoordinates(direction,face,u,v);
    const double x = (u+1)*0.5*size-0.5, y = (v+1)*0.5*size-0.5;
    const int ix = int(std::floor(x)), iy = int(std::floor(y));
    const double fx = x-ix, fy = y-iy;
    return Add(Mul(Add(Mul(CubeTap(data,size,face,ix,iy),1-fx),Mul(CubeTap(data,size,face,ix+1,iy),fx)),1-fy),
               Mul(Add(Mul(CubeTap(data,size,face,ix,iy+1),1-fx),Mul(CubeTap(data,size,face,ix+1,iy+1),fx)),fy));
}
void FilterProbe(ReflectionProbe &probe, const BakeOptions &options) {
    uint32_t levels = 1;
    for (uint32_t size=probe.FaceSize;size>1;size>>=1) ++levels;
    probe.Mips.resize(levels);
    for (uint32_t level=1;level<levels;++level) {
        const auto size = std::max(1u,probe.FaceSize>>level), count = size*size*6;
        const double roughness = double(level)/(levels-1);
        auto &mip = probe.Mips[level];
        mip.assign(size_t(count)*4,1);
        ParallelPixels(count,options,"probe-filter:"+std::to_string(level),[&](uint32_t p) {
            const auto normal = CubeDirection(p/(size*size),2*((p%size)+0.5)/size-1,2*(((p/size)%size)+0.5)/size-1);
            Random random(options.Seed ^ (uint64_t(p)*0xd6e8feb86659fd93ull) ^ (uint64_t(level)<<48));
            Vec3 sum{};
            double weight=0;
            for (uint32_t sample=0;sample<options.ProbeFilterSamples;++sample) {
                const double first=double(sample)/options.ProbeFilterSamples, second=random.Next();
                const auto half=GgxHalfVector(normal,roughness,first,second);
                const auto direction=Sub(Mul(half,2*Dot(normal,half)),normal);
                const double cosine=Dot(normal,direction);
                if (cosine<=0) continue;
                sum=Add(sum,Mul(SampleCube(probe.Mips[0],probe.FaceSize,direction),cosine));
                weight+=cosine;
            }
            if (!(weight>0)) throw std::runtime_error("GGX prefilter has no valid quadrature weight");
            sum=Mul(sum,1/weight);
            mip[size_t(p)*4]=sum.X; mip[size_t(p)*4+1]=sum.Y; mip[size_t(p)*4+2]=sum.Z;
        });
    }
}
void BakeProbe(const Scene &scene, const BakeOptions &options, const std::vector<Triangle> &triangles, Lightmap &result) {
    if (!scene.Probe.Enabled) return;
    auto &probe=result.Probe;
    probe.FaceSize=options.ProbeFaceSize;
    probe.Position=scene.Probe.Position;
    probe.BoundsMin=scene.Probe.BoundsMin;
    probe.BoundsMax=scene.Probe.BoundsMax;
    probe.Mips.resize(1);
    const uint32_t size=probe.FaceSize, count=size*size*6;
    auto &data=probe.Mips[0];
    data.assign(size_t(count)*4,1);
    if (options.Backend==BakeBackend::Vulkan) {
#ifdef HELLM_ENABLE_VULKAN
        TraceProbeVulkan(scene,options,triangles,data);
#else
        throw std::runtime_error("Vulkan reflection capture requested without Vulkan support");
#endif
    } else {
        const Bvh bvh(triangles,options.RayEpsilon);
        const double capSpan=SunCapSpan(options.SunAngularDiameterDegrees);
        ParallelPixels(count,options,"probe-trace",[&](uint32_t p) {
            const auto direction=CubeDirection(p/(size*size),2*((p%size)+0.5)/size-1,2*(((p/size)%size)+0.5)/size-1);
            Random random(options.Seed ^ (uint64_t(p)*0xd6e8feb86659fd93ull));
            Vec3 sum{};
            for (uint32_t sample=0;sample<options.ProbeSamples;++sample)
                sum=Add(sum,RadianceSample(bvh,triangles,probe.Position,direction,scene,options,capSpan,options.MaxBounces+1,random));
            sum=Mul(sum,1.0/options.ProbeSamples);
            if (!Finite(sum) || sum.X<0 || sum.Y<0 || sum.Z<0) throw std::runtime_error("Reflection capture produced invalid HDR radiance");
            data[size_t(p)*4]=sum.X; data[size_t(p)*4+1]=sum.Y; data[size_t(p)*4+2]=sum.Z;
        });
    }
    FilterProbe(probe,options);
}
} // namespace hellm::detail
