#include <hellm/Lightmap.h>
#include "../src/Brdf.h"
#include "../src/Bytes.h"
#include <fstream>
#include "../src/Probe.h"
#include <cstdio>
#include <iostream>
#include <limits>
using namespace hellm;
using namespace hellm::detail;
namespace {
/// <summary>Fail a focused reflection invariant with a readable diagnostic.</summary>
void Require(bool condition,const char *message) {
    if (!condition) throw std::runtime_error(message);
}
/// <summary>Compare radiometric values with an explicit absolute error budget.</summary>
void Near(double actual,double expected,double tolerance,const char *message) {
    if (std::abs(actual-expected)>tolerance) throw std::runtime_error(std::string(message)+" actual="+std::to_string(actual)+" expected="+std::to_string(expected));
}
/// <summary>Require malformed input to preserve explicit failure behavior.</summary>
void Reject(const std::function<void()> &action,const char *message) {
    bool rejected=false;
    try { action(); } catch (const std::exception &) { rejected=true; }
    Require(rejected,message);
}
/// <summary>Construct one fully attributed two-sided quad for radiance and occlusion fixtures.</summary>
Mesh Quad(const std::string &key,Vec3 a,Vec3 b,Vec3 c,Vec3 d) {
    Mesh mesh;
    mesh.Key=key;
    mesh.Positions={a,b,c,d};
    mesh.Normals.assign(4,Unit(Cross(Sub(b,a),Sub(c,a))));
    mesh.TexCoords={{0,0},{1,0},{1,1},{0,1}};
    mesh.Indices={0,1,2,0,2,3};
    return mesh;
}
/// <summary>Create a bright floor and a centered optional probe without any environment illumination.</summary>
Scene Fixture() {
    Scene scene;
    scene.Sun.Direction={0,1,0}; scene.Sun.Radiance={100,30,10};
    scene.Meshes={Quad("floor",{-2,-1,-2},{-2,-1,2},{2,-1,2},{2,-1,-2})};
    scene.Probe.Enabled=true;
    scene.Probe.Position={0,0,0}; scene.Probe.BoundsMin={-2,-1,-2}; scene.Probe.BoundsMax={2,2,2};
    return scene;
}
/// <summary>Use a tiny deterministic fixture while exercising both irradiance and reflection path transport.</summary>
BakeOptions Options(BakeBackend backend) {
    BakeOptions options;
    options.Backend=backend;
    options.Width=options.Height=32; options.Padding=2;
    options.SamplesPerTexel=8; options.MaxBounces=2;
    options.ProbeFaceSize=8; options.ProbeSamples=32; options.ProbeFilterSamples=64;
    options.WorkerCount=2; options.SunAngularDiameterDegrees=.53f;
    return options;
}
/// <summary>Verify D3D face order, face up vectors and seamless bilinear cube lookup.</summary>
void OrientationAndFilter() {
    const Vec3 axes[6]={{1,0,0},{-1,0,0},{0,1,0},{0,-1,0},{0,0,1},{0,0,-1}};
    for (uint32_t face=0;face<6;++face) Near(Dot(CubeDirection(face,0,0),axes[face]),1,1e-6,"D3D face center");
    Require(CubeDirection(0,0,-1).Y>0 && CubeDirection(2,0,-1).Z<0 && CubeDirection(3,0,-1).Z>0,"D3D face top orientation");
    ReflectionProbe probe;
    probe.FaceSize=8; probe.Mips.resize(1); probe.Mips[0].resize(8*8*6*4);
    for (size_t p=0;p<probe.Mips[0].size();p+=4) {
        probe.Mips[0][p]=12; probe.Mips[0][p+1]=3; probe.Mips[0][p+2]=1.25f; probe.Mips[0][p+3]=1;
    }
    auto options=Options(BakeBackend::Cpu);
    FilterProbe(probe,options);
    Require(probe.Mips.size()==4,"Full cube chain reaches one texel");
    for (uint32_t level=0;level<probe.Mips.size();++level) {
        const uint32_t size=8>>level;
        Require(probe.Mips[level].size()==size*size*6*4,"Cube mip dimensions");
        for (size_t p=0;p<probe.Mips[level].size();p+=4) {
            Near(probe.Mips[level][p],12,2e-5,"GGX constant HDR map preserves energy");
            Near(probe.Mips[level][p+2],1.25,2e-6,"GGX constant color");
            Near(probe.Mips[level][p+3],1,0,"Cube alpha");
        }
    }
    auto oneSampleOptions=options; oneSampleOptions.ProbeFilterSamples=1;
    ReflectionProbe oneSample;
    oneSample.FaceSize=2; oneSample.Mips.resize(1); oneSample.Mips[0].assign(2*2*6*4,1);
    for (size_t p=0;p<oneSample.Mips[0].size();p+=4) oneSample.Mips[0][p]=12;
    FilterProbe(oneSample,oneSampleOptions);
    for (size_t p=0;p<oneSample.Mips.back().size();p+=4)
        Near(oneSample.Mips.back()[p],12,0,"One-sample GGX quadrature must have a valid first proposal");
    const uint32_t size=32;
    std::vector<float> directional(size*size*6*4,1);
    for (uint32_t p=0;p<size*size*6;++p) {
        const auto direction=CubeDirection(p/(size*size),2*((p%size)+.5)/size-1,2*(((p/size)%size)+.5)/size-1);
        directional[p*4]=(direction.X+1)*.5f; directional[p*4+1]=(direction.Y+1)*.5f; directional[p*4+2]=(direction.Z+1)*.5f;
    }
    for (Vec3 direction : {Vec3{1,1,1},Vec3{-1,-1,-1},Vec3{.2f,.8f,-.6f},Vec3{1,0,0}}) {
        direction=Unit(direction);
        const auto sampled=SampleCube(directional,size,direction);
        Near(sampled.X,(direction.X+1)*.5,.012,"Seamless cube x mapping");
        Near(sampled.Y,(direction.Y+1)*.5,.012,"Seamless cube y mapping");
        Near(sampled.Z,(direction.Z+1)*.5,.012,"Seamless cube z mapping");
    }
    ReflectionProbe varying;
    varying.FaceSize=size; varying.Mips={directional};
    const double sharp=SampleCube(varying.Mips[0],size,{1,0,0}).X;
    FilterProbe(varying,options);
    const double blurred=SampleCube(varying.Mips.back(),1,{1,0,0}).X;
    Require(blurred<sharp-.08 && blurred>.65,"Increasing GGX mip roughness must broaden directional radiance");
    std::cout<<"PASS D3D cube orientation, seams, HDR constant filter, full mip dimensions\n";
}
/// <summary>Independently integrate the BRDF to verify mixture PDFs, conductor energy and roughness response.</summary>
void MetallicTransport() {
    const Vec3 normal=Unit({.3f,.8f,-.4f}),outgoing=normal;
    const auto black=Brdf({0,0,0},1,.04,normal,outgoing,normal);
    Require(black.X>=0 && black.Y>=0 && black.Z>=0,"Aligned black conductor cannot produce negative Fresnel");
    const auto dielectric=Brdf({0,0,0},0,.5,normal,outgoing,normal);
    Require(dielectric.X>0 && dielectric.X<1,"Black dielectric retains its physical finite F0 specular lobe");
    const auto sharp=Brdf({.9f,.2f,.1f},1,.12,{0,1,0},{0,1,0},{0,1,0});
    const auto rough=Brdf({.9f,.2f,.1f},1,.8,{0,1,0},{0,1,0},{0,1,0});
    Require(sharp.X>rough.X*100,"Roughness must broaden a normalized specular peak");
    const Vec3 oblique=Unit({1,1,0});
    const auto metal=Brdf({1,0,0},1,.04,{0,1,0},{0,1,0},oblique);
    const auto diffuse=Brdf({1,0,0},0,.04,{0,1,0},{0,1,0},oblique);
    Require(metal.X<diffuse.X*.001,"Smooth conductor must not bleed broad Lambertian red");
    for (double metallic : {0.0,.5,1.0}) {
        Random sampled(12),independent(97);
        Vec3 importance{},reference{};
        constexpr uint32_t count=200000;
        for (uint32_t i=0;i<count;++i) {
            Vec3 weight;
            SampleBrdf({.8f,.4f,.1f},metallic,.65,normal,outgoing,sampled,weight);
            importance=Add(importance,Mul(weight,1.0/count));
            const auto direction=CosineDirection(normal,independent);
            reference=Add(reference,Mul(Brdf({.8f,.4f,.1f},metallic,.65,normal,outgoing,direction),Pi/count));
        }
        Near(importance.X,reference.X,.008,"GGX mixture PDF agrees with independent cosine quadrature");
        Near(importance.Y,reference.Y,.008,"GGX green energy");
        Require(importance.X>=0 && importance.X<=1.01 && importance.Y<=1.01,"Material transport conserves reflected energy");
    }
    for (double metallic : {0.0,1.0}) {
        Random random(312);
        const Vec3 view=Unit({.98f,.2f,0});
        Vec3 sum{};
        constexpr uint32_t count=300000;
        for (uint32_t i=0;i<count;++i) {
            Vec3 weight;
            SampleBrdf({1,1,1},metallic,.5,{0,1,0},view,random,weight);
            sum=Add(sum,Mul(weight,1.0/count));
        }
        Near(std::max(0.0,double(sum.X)-1.0),0,.03,("Oblique white-furnace energy metal="+std::to_string(metallic)).c_str());
        Require(sum.X>.2,"Oblique reflected energy must remain nonzero");
        std::cout<<"white_furnace metallic="<<metallic<<" outgoing_cosine="<<view.Y<<" reflected="<<sum.X<<"\n";
    }
    std::cout<<"PASS GGX PDF integration, metallic energy, absent conductor diffuse, roughness response\n";
}
/// <summary>Write a genuine version-one checksummed fixture from canonical payload bytes.</summary>
void WriteLegacy(const Bytes &payload,const char *magic,const std::string &path) {
    uint64_t hash=14695981039346656037ull;
    for (auto value:payload.Data) { hash^=value; hash*=1099511628211ull; }
    Bytes header;
    for (uint32_t i=0;i<8;++i) header.Data.push_back(uint8_t(magic[i]));
    header.U32(1); header.U64(payload.Data.size()); header.U64(hash);
    std::ofstream file(path,std::ios::binary|std::ios::trunc);
    file.write(reinterpret_cast<const char *>(header.Data.data()),std::streamsize(header.Data.size()));
    file.write(reinterpret_cast<const char *>(payload.Data.data()),std::streamsize(payload.Data.size()));
    file.flush();
    Require(bool(file),"Write legacy fixture");
}
/// <summary>Decode actual v1 layouts with explicit legacy defaults while keeping their old fingerprints stale.</summary>
void LegacyFormats(const Scene &scene,const Lightmap &map,const std::string &scenePath,const std::string &mapPath) {
    Bytes payload;
    payload.U32(uint32_t(scene.Meshes.size())); payload.V3(scene.Sun.Direction); payload.V3(scene.Sun.Radiance);
    for (const auto &mesh:scene.Meshes) {
        payload.String(mesh.Key); payload.V3(mesh.Albedo); payload.U32(uint32_t(mesh.Positions.size()));
        for (size_t i=0;i<mesh.Positions.size();++i) { payload.V3(mesh.Positions[i]); payload.V3(mesh.Normals[i]); payload.V2(mesh.TexCoords[i]); }
        payload.U32(uint32_t(mesh.Indices.size())); for (auto index:mesh.Indices) payload.U32(index);
        payload.U32(0); payload.U32(0); payload.U32(0);
    }
    WriteLegacy(payload,"HELLMSC1",scenePath);
    const auto legacyScene=LoadScene(scenePath);
    Require(!legacyScene.Probe.Enabled && legacyScene.Meshes[0].Metallic==0 && legacyScene.Meshes[0].Roughness==.86f,"v1 scene has explicit legacy material and disabled probe");
    payload=Bytes{};
    payload.U32(map.Width); payload.U32(map.Height); payload.U64(17); payload.U64(23);
    payload.U32(uint32_t(map.Rgba.size())); for (float value:map.Rgba) payload.F32(value);
    payload.U32(uint32_t(map.Bindings.size()));
    for (const auto &binding:map.Bindings) { payload.String(binding.Key); payload.U32(uint32_t(binding.CornerUvs.size())); for (auto uv:binding.CornerUvs) payload.V2(uv); }
    WriteLegacy(payload,"HELLMLM1",mapPath);
    const auto legacyMap=LoadLightmap(mapPath);
    Require(legacyMap.Probe.FaceSize==0 && legacyMap.Probe.Mips.empty() && legacyMap.Rgba==map.Rgba,"v1 atlas remains readable without a fabricated probe");
    Require(legacyMap.SourceFingerprint!=ComputeSceneFingerprint(legacyScene) && legacyMap.SettingsFingerprint!=ComputeSettingsFingerprint(Options(BakeBackend::Cpu)),"Legacy identity cannot silently match new source/settings");
}
/// <summary>Exercise capture on the requested backend, v2 roundtrip and invalid box/mip rejection.</summary>
void CaptureAndAssets(BakeBackend backend) {
    auto scene=Fixture(); auto options=Options(backend);
    auto map=Bake(scene,options);
    Require(map.Probe.FaceSize==8 && map.Probe.Mips.size()==4,"Enabled scene produces complete probe");
    const auto &base=map.Probe.Mips[0];
    const auto up=SampleCube(base,8,{0,1,0}),down=SampleCube(base,8,{0,-1,0});
    Near(up.X,0,0,"Direct analytic sun disk is excluded from probe misses");
    Require(down.X>20 && down.Y>5 && down.X>down.Y,"Surface-reflected radiance remains HDR and colored");
    if (backend==BakeBackend::Vulkan) {
        auto cpuOptions=options; cpuOptions.Backend=BakeBackend::Cpu;
        const auto cpu=Bake(scene,cpuOptions);
        double squared=0,reference=0;
        for (size_t p=0;p<base.size();++p) {
            const double difference=base[p]-cpu.Probe.Mips[0][p];
            squared+=difference*difference; reference+=double(cpu.Probe.Mips[0][p])*cpu.Probe.Mips[0][p];
        }
        Require(std::sqrt(squared/reference)<.005,"CPU/Vulkan probe parity");
    }
    if (backend==BakeBackend::Vulkan) {
        for (float metallic : {.6f,1.0f}) {
            auto metalScene=scene;
            metalScene.Sun.Direction=Unit({.7f,1,.3f});
            metalScene.Meshes[0].Metallic=metallic; metalScene.Meshes[0].Roughness=.45f;
            auto cpuOptions=options; cpuOptions.Backend=BakeBackend::Cpu;
            const auto cpu=Bake(metalScene,cpuOptions),gpu=Bake(metalScene,options);
            double squared=0,reference=0;
            for (size_t p=0;p<base.size();++p) {
                const double difference=cpu.Probe.Mips[0][p]-gpu.Probe.Mips[0][p];
                squared+=difference*difference; reference+=double(cpu.Probe.Mips[0][p])*cpu.Probe.Mips[0][p];
            }
            Require(std::sqrt(squared/reference)<.007,"CPU/Vulkan mixed-metal and conductor capture parity");
        }
    }
    const std::string prefix="C:/temp/hellm-reflection-"+std::string(backend==BakeBackend::Cpu?"cpu":"vulkan");
    const auto scenePath=prefix+".scene",mapPath=prefix+".lightmap";
    try {
        scene.Meshes[0].Metallic=.75f; scene.Meshes[0].Roughness=.23f;
        SaveScene(scene,scenePath);
        const auto loaded=LoadScene(scenePath);
        Require(loaded.Probe.Enabled && loaded.Meshes[0].Metallic==.75f && loaded.Meshes[0].Roughness==.23f && ComputeSceneFingerprint(loaded)==ComputeSceneFingerprint(scene),"v2 material/probe scene roundtrip");
        SaveLightmap(map,mapPath);
        const auto persisted=LoadLightmap(mapPath);
        Require(persisted.Probe.Mips==map.Probe.Mips && persisted.Probe.FaceSize==map.Probe.FaceSize && persisted.Probe.BoundsMin.Y==map.Probe.BoundsMin.Y,"v2 HDR probe roundtrip");
        LegacyFormats(scene,map,scenePath,mapPath);
        auto invalid=map; invalid.Probe.Mips.pop_back();
        Reject([&]{SaveLightmap(invalid,mapPath);},"Reject incomplete cube chain");
        invalid=map; invalid.Probe.Mips[0][0]=std::numeric_limits<float>::infinity();
        Reject([&]{SaveLightmap(invalid,mapPath);},"Reject nonfinite probe radiance");
        invalid=map; invalid.Probe.BoundsMax=invalid.Probe.BoundsMin;
        Reject([&]{SaveLightmap(invalid,mapPath);},"Reject invalid probe volume");
        auto invalidScene=scene; invalidScene.Probe.Position=invalidScene.Probe.BoundsMin;
        Reject([&]{ComputeSceneFingerprint(invalidScene);},"Probe position must be strictly interior");
        const auto fingerprint=ComputeSceneFingerprint(scene);
        scene.Meshes[0].Metallic=.5f; Require(ComputeSceneFingerprint(scene)!=fingerprint,"Metallic invalidates stale source");
        scene=loaded; scene.Meshes[0].Roughness=.5f; Require(ComputeSceneFingerprint(scene)!=fingerprint,"Roughness invalidates stale source");
        const auto settings=ComputeSettingsFingerprint(options);
        ++options.ProbeSamples; Require(ComputeSettingsFingerprint(options)!=settings,"Probe samples invalidate settings");
        options=Options(backend); options.ProbeFaceSize=16; Require(ComputeSettingsFingerprint(options)!=settings,"Probe size invalidates settings");
        options=Options(backend); ++options.ProbeFilterSamples; Require(ComputeSettingsFingerprint(options)!=settings,"Probe filter invalidates settings");
        options=Options(backend); options.ProbeFaceSize=7; Reject([&]{ComputeSettingsFingerprint(options);},"Reject non-power-of-two cube");
    } catch (...) {
        std::remove(scenePath.c_str()); std::remove(mapPath.c_str()); throw;
    }
    std::remove(scenePath.c_str()); std::remove(mapPath.c_str());
    scene=Fixture(); options=Options(backend);
    scene.Meshes.push_back(Quad("roof",{-2,2,-2},{2,2,-2},{2,2,2},{-2,2,2}));
    scene.Meshes.push_back(Quad("left",{-2,-1,-2},{-2,2,-2},{-2,2,2},{-2,-1,2}));
    scene.Meshes.push_back(Quad("right",{2,-1,-2},{2,-1,2},{2,2,2},{2,2,-2}));
    scene.Meshes.push_back(Quad("front",{-2,-1,-2},{2,-1,-2},{2,2,-2},{-2,2,-2}));
    scene.Meshes.push_back(Quad("back",{-2,-1,2},{-2,2,2},{2,2,2},{2,-1,2}));
    options.Width=options.Height=64;
    const auto closed=Bake(scene,options);
    for (const auto &mip:closed.Probe.Mips)
        for (size_t p=0;p<mip.size();p+=4) Require(mip[p]==0 && mip[p+1]==0 && mip[p+2]==0,"Closed walls cannot leak sun or invented ambient");
    scene=Fixture(); scene.Probe.Enabled=false;
    const auto disabled=Bake(scene,Options(backend));
    Require(disabled.Probe.FaceSize==0 && disabled.Probe.Mips.empty(),"Disabled probe allocates no capture");
    std::cout<<"PASS selected-backend HDR capture, black misses/closed walls, v2 assets and invalid inputs\n";
}
}
/// <summary>Run focused reflection correctness on CPU or a real Vulkan ray-query device.</summary>
int main(int argc,char **argv) {
    try {
        const auto backend=argc==2 && std::string(argv[1])=="--vulkan"?BakeBackend::Vulkan:BakeBackend::Cpu;
        OrientationAndFilter(); MetallicTransport(); CaptureAndAssets(backend);
        return 0;
    } catch (const std::exception &error) { std::cerr<<"FAIL "<<error.what()<<"\n"; return 1; }
}
